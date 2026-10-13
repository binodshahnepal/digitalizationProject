using System.Text;
using FluentAssertions;
using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Application.Services;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using HospitalRecords.Infrastructure.Persistence;
using HospitalRecords.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace HospitalRecords.Tests;

public class AcceptanceScenario_AT02_Tests
{
    private readonly ApplicationDbContext _context;
    private readonly IFileSecurityValidator _fileValidator;
    private readonly IObjectStorageService _storageService;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IOcrProcessingService> _ocrServiceMock;
    private readonly DocumentService _documentService;

    public AcceptanceScenario_AT02_Tests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _fileValidator = new FileSecurityValidator();

        var configMock = new Mock<IConfiguration>();
        var tempStoragePath = Path.Combine(Path.GetTempPath(), "BpkmchTests_" + Guid.NewGuid());
        configMock.Setup(c => c["Storage:BasePath"]).Returns(tempStoragePath);
        var loggerMock = new Mock<ILogger<FileSystemObjectStorageService>>();
        _storageService = new FileSystemObjectStorageService(configMock.Object, loggerMock.Object);

        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("clerk_ramesh");
        _ocrServiceMock = new Mock<IOcrProcessingService>();

        _documentService = new DocumentService(
            _context,
            _storageService,
            _fileValidator,
            _auditServiceMock.Object,
            _currentUserServiceMock.Object,
            _ocrServiceMock.Object);
    }

    private static byte[] CreateValidPdfBytes(string customContent)
    {
        // Valid PDF magic header %PDF-1.4 followed by unique content
        var header = "%PDF-1.4\n%âãÏÓ\n1 0 obj\n<< /Title (" + customContent + ") >>\nendobj\ntrailer\n<< >>\n%%EOF";
        return Encoding.UTF8.GetBytes(header);
    }

    [Fact]
    public async Task AT02_GivenOneEncounter_When12FilesUploaded_ThenEachHasUniqueDocumentIdAndCorrectEncounterLink()
    {
        // Acceptance Scenario AT-02:
        // "Given one encounter, when 12 files are uploaded, then each has unique document ID, 
        //  correct encounter link and retrievable version."

        // Arrange: Seed patient and encounter
        var patient = new Patient
        {
            Mrn = "BPKMCH-2026-0500",
            FirstName = "Kiran",
            LastName = "Shrestha",
            DateOfBirth = new DateOnly(1980, 4, 10),
            IdentityProvenance = "OPD Desk"
        };
        _context.Patients.Add(patient);

        var encounter = new Encounter
        {
            PatientId = patient.Id,
            EncounterNumber = "ENC-2026-00042",
            Facility = "BPKMCH Main Campus",
            Department = "Radiation Oncology",
            Type = EncounterType.OPD,
            Status = EncounterStatus.Active
        };
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync();

        // Create 12 distinct multi-page simulated scans
        var uploadFiles = new List<UploadDocumentFileDto>();
        for (int i = 1; i <= 12; i++)
        {
            var pdfBytes = CreateValidPdfBytes($"Document Page Specimen {i} for Patient {patient.Mrn}");
            uploadFiles.Add(new UploadDocumentFileDto(
                ContentStream: new MemoryStream(pdfBytes),
                FileName: $"Chart_Page_{i:D2}.pdf",
                FileSize: pdfBytes.Length
            ));
        }

        var uploadRequest = new UploadDocumentsRequest(
            EncounterId: encounter.Id,
            ClinicalEventId: null,
            Category: DocumentCategory.GeneralMedicalChart,
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: uploadFiles
        );

        // Act: Upload all 12 documents for this encounter
        var result = await _documentService.UploadDocumentsAsync(patient.Id, uploadRequest);

        // Assert
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.CreatedDocuments.Should().HaveCount(12);

        // 1. Verify every document has a unique Document ID
        var distinctIds = result.CreatedDocuments.Select(d => d.Id).Distinct().ToList();
        distinctIds.Should().HaveCount(12);

        // 2. Verify all documents are linked to the correct encounter
        result.CreatedDocuments.Should().OnlyContain(d => d.EncounterId == encounter.Id);
        result.CreatedDocuments.Should().OnlyContain(d => d.PatientId == patient.Id);

        // 3. Verify retrievable version for every document
        foreach (var docDto in result.CreatedDocuments)
        {
            docDto.CurrentVersionNumber.Should().Be(1);
            docDto.CurrentVersion.Should().NotBeNull();
            docDto.Status.Should().Be(DocumentStatus.OcrQueued);

            // Test download of the physical asset from storage
            var download = await _documentService.DownloadFileAsync(docDto.Id);
            download.Should().NotBeNull();
            download!.Value.Stream.Should().NotBeNull();
            download.Value.ContentType.Should().Be("application/pdf");

            using var reader = new StreamReader(download.Value.Stream);
            var content = await reader.ReadToEndAsync();
            content.Should().StartWith("%PDF-");
        }

        // Verify database records
        var dbDocsCount = await _context.Documents.CountAsync(d => d.EncounterId == encounter.Id);
        dbDocsCount.Should().Be(12);
    }

    [Fact]
    public async Task FileSecurity_ExecutableDisguisedAsPdf_ShouldBeRejectedByMagicBytes()
    {
        // Requirement FR-DOC-002: Pre-storage antivirus & true file-type validation
        // Arrange
        var patient = new Patient { Mrn = "BPKMCH-2026-0501", FirstName = "Test", LastName = "User" };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // Simulate an .exe file disguised as .pdf (starts with MZ header 0x4D 0x5A)
        var maliciousBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
        var files = new List<UploadDocumentFileDto>
        {
            new(new MemoryStream(maliciousBytes), "LabReport.pdf", maliciousBytes.Length)
        };

        var request = new UploadDocumentsRequest(
            EncounterId: null,
            ClinicalEventId: null,
            Category: DocumentCategory.PathologyLabReport,
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: files
        );

        // Act
        var result = await _documentService.UploadDocumentsAsync(patient.Id, request);

        // Assert
        result.Success.Should().BeFalse();
        result.CreatedDocuments.Should().BeEmpty();
        result.Errors.Should().ContainSingle(e => e.Contains("Executable binaries (.exe/.dll) are prohibited"));

        _auditServiceMock.Verify(a => a.LogAsync(
            "DOCUMENT_UPLOAD_REJECTED",
            "Document",
            "LabReport.pdf",
            AuditOutcome.Denied,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateDetection_SameFileUploadedTwiceToSameEncounter_ShouldBeBlockedBySha256()
    {
        // Requirement FR-DOC-002: In-encounter duplicate detection via SHA-256
        // Arrange
        var patient = new Patient { Mrn = "BPKMCH-2026-0502", FirstName = "Hari", LastName = "Dev" };
        var encounter = new Encounter { PatientId = patient.Id, EncounterNumber = "ENC-2026-00088" };
        _context.Patients.Add(patient);
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync();

        var pdfBytes = CreateValidPdfBytes("Single Chest X-Ray Scan");

        // Act 1: First upload succeeds
        var firstUpload = await _documentService.UploadDocumentsAsync(patient.Id, new UploadDocumentsRequest(
            EncounterId: encounter.Id,
            ClinicalEventId: null,
            Category: DocumentCategory.RadiologyReport,
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: [new(new MemoryStream(pdfBytes), "Scan1.pdf", pdfBytes.Length)]
        ));

        firstUpload.Success.Should().BeTrue();

        // Act 2: Second upload with the exact same binary content to the same encounter
        var secondUpload = await _documentService.UploadDocumentsAsync(patient.Id, new UploadDocumentsRequest(
            EncounterId: encounter.Id,
            ClinicalEventId: null,
            Category: DocumentCategory.RadiologyReport,
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: [new(new MemoryStream(pdfBytes), "Duplicate_Scan.pdf", pdfBytes.Length)]
        ));

        // Assert: Second upload flagged as duplicate
        secondUpload.Success.Should().BeFalse();
        secondUpload.Errors.Should().ContainSingle(e => e.Contains("Duplicate scan detected"));
    }

    [Fact]
    public async Task AmendDocument_ShouldCreateNewVersionAndPreserveOriginal()
    {
        // Requirement FR-DOC-004:
        // "Preserve immutable originals; corrections create linked new versions with reason and author."

        // Arrange: Upload initial document
        var patient = new Patient { Mrn = "BPKMCH-2026-0503", FirstName = "Gopal", LastName = "Gurung" };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var originalPdf = CreateValidPdfBytes("Initial Ingestion Page");
        var initialResult = await _documentService.UploadDocumentsAsync(patient.Id, new UploadDocumentsRequest(
            EncounterId: null,
            ClinicalEventId: null,
            Category: DocumentCategory.DischargeSummary,
            ClinicalDate: new DateOnly(2026, 10, 10),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: [new(new MemoryStream(originalPdf), "DischargeSummary_v1.pdf", originalPdf.Length)]
        ));

        var docId = initialResult.CreatedDocuments.First().Id;

        // Act: Supervisor uploads corrected scan
        var correctedPdf = CreateValidPdfBytes("Corrected Ingestion Page with Doctor Signature");
        var amendRequest = new AmendDocumentRequest(
            ContentStream: new MemoryStream(correctedPdf),
            FileName: "DischargeSummary_v2_Signed.pdf",
            FileSize: correctedPdf.Length,
            ReasonForAmendment: "Supervisor re-scanned page to include physical clinician signature"
        );

        var amendedDoc = await _documentService.AmendDocumentAsync(docId, amendRequest);

        // Assert
        amendedDoc.Should().NotBeNull();
        amendedDoc!.CurrentVersionNumber.Should().Be(2);

        // Verify both versions exist in database
        var versions = await _context.DocumentVersions
            .Where(v => v.DocumentId == docId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();

        versions.Should().HaveCount(2);
        versions[0].VersionNumber.Should().Be(1);
        versions[0].OriginalFileName.Should().Be("DischargeSummary_v1.pdf");
        versions[1].VersionNumber.Should().Be(2);
        versions[1].OriginalFileName.Should().Be("DischargeSummary_v2_Signed.pdf");
        versions[1].ReasonForVersion.Should().Contain("clinician signature");

        // Verify both physical versions remain retrievable independently
        var v1Download = await _documentService.DownloadFileAsync(docId, versionNumber: 1);
        var v2Download = await _documentService.DownloadFileAsync(docId, versionNumber: 2);

        v1Download.Should().NotBeNull();
        v2Download.Should().NotBeNull();
        v1Download!.Value.FileName.Should().Be("DischargeSummary_v1.pdf");
        v2Download!.Value.FileName.Should().Be("DischargeSummary_v2_Signed.pdf");
    }
}
