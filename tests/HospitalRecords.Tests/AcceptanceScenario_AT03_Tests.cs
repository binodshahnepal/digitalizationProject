using System.Text;
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
using Xunit;

namespace HospitalRecords.Tests;

/// <summary>
/// Acceptance Scenario AT-03: Dual-Language OCR & AI Document Classification Pipeline (Sprint 3)
/// Verifies:
/// 1. Dual-Language text extraction (English + Nepali Devanagari)
/// 2. Automatic oncology document taxonomy classification
/// 3. Flagging threshold: confidence < 0.70 triggers RequiresReview status and supervisor queue
/// 4. Human-in-the-loop verification workflow transitioning document to Indexed
/// </summary>
public class AcceptanceScenario_AT03_Tests
{
    private readonly ApplicationDbContext _context;
    private readonly IFileSecurityValidator _fileValidator;
    private readonly IObjectStorageService _storageService;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly OcrProcessingService _ocrService;
    private readonly DocumentService _documentService;

    public AcceptanceScenario_AT03_Tests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _fileValidator = new FileSecurityValidator();

        var configMock = new Mock<IConfiguration>();
        var tempStoragePath = Path.Combine(Path.GetTempPath(), "BpkmchOcrTests_" + Guid.NewGuid());
        configMock.Setup(c => c["Storage:BasePath"]).Returns(tempStoragePath);
        var loggerStorageMock = new Mock<ILogger<FileSystemObjectStorageService>>();
        _storageService = new FileSystemObjectStorageService(configMock.Object, loggerStorageMock.Object);

        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("supervisor_sunita");

        var httpClient = new HttpClient();
        var loggerOcrMock = new Mock<ILogger<OcrProcessingService>>();
        var loggerEntityMock = new Mock<ILogger<ClinicalEntityExtractionService>>();
        var clinicalEntityService = new ClinicalEntityExtractionService(
            _context,
            _currentUserServiceMock.Object,
            _auditServiceMock.Object,
            httpClient,
            loggerEntityMock.Object);

        var searchServiceMock = new Mock<ISearchService>();

        _ocrService = new OcrProcessingService(
            _context,
            _storageService,
            _auditServiceMock.Object,
            _currentUserServiceMock.Object,
            httpClient,
            clinicalEntityService,
            searchServiceMock.Object,
            loggerOcrMock.Object);

        _documentService = new DocumentService(
            _context,
            _storageService,
            _fileValidator,
            _auditServiceMock.Object,
            _currentUserServiceMock.Object,
            _ocrService);
    }

    private static byte[] CreateValidPdfWithText(string textContent)
    {
        // Valid PDF 1.4 header + stream containing text content
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        sb.Append("1 0 obj << /Title (BPKMCH Report) >> endobj\n");
        sb.Append("2 0 obj << /Length " + Encoding.UTF8.GetByteCount(textContent) + " >>\nstream\n");
        sb.Append(textContent);
        sb.Append("\nendstream\nendobj\n");
        sb.Append("trailer << /Root 1 0 R >>\n%%EOF");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<(Patient patient, Encounter encounter)> SeedPatientAndEncounterAsync()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPK-2026-08192",
            FirstName = "Sita",
            LastName = "Adhikari",
            DateOfBirth = new DateOnly(1982, 4, 15),
            Gender = Gender.Female,
            IdentityProvenance = "OPD Registration Intake",
            CreatedBy = "clerk_ramesh"
        };
        _context.Patients.Add(patient);

        var encounter = new Encounter
        {
            Id = Guid.NewGuid(),
            EncounterNumber = "ENC-2026-00045",
            PatientId = patient.Id,
            Type = EncounterType.OPD,
            Facility = "BPKMCH Bharatpur",
            Department = "Medical Oncology",
            StartDate = DateTimeOffset.UtcNow,
            CreatedBy = "clerk_ramesh"
        };
        _context.Encounters.Add(encounter);

        await _context.SaveChangesAsync();
        return (patient, encounter);
    }

    [Fact]
    public async Task AT03_1_DualLanguageOcr_And_PathologyClassification_Succeeds()
    {
        // GIVEN: A patient with an active clinical encounter
        var (patient, encounter) = await SeedPatientAndEncounterAsync();

        // AND: A scanned report containing dual-language content (Nepali Devanagari headers + English biopsy text)
        var reportContent =
            "बी.पी. कोइराला मेमोरियल क्यान्सर अस्पताल, भरतपुर, नेपाल।\n" +
            "DEPARTMENT OF PATHOLOGY & LABORATORY SERVICES\n" +
            "HISTOPATHOLOGY EXAMINATION REPORT\n" +
            "Patient: Sita Devi Adhikari | MRN: BPK-2026-08192\n" +
            "Specimen: Right Breast Core Needle Biopsy\n" +
            "Microscopic Examination: Sections show infiltrating ductal carcinoma (Grade II).\n" +
            "Immunohistochemistry: ER Positive, PR Positive, HER2 Negative.\n" +
            "Conclusion: Invasive Ductal Carcinoma. मासु जाँच विवरण सम्पन्न भयो।";

        var pdfBytes = CreateValidPdfWithText(reportContent);

        var uploadRequest = new UploadDocumentsRequest(
            EncounterId: encounter.Id,
            ClinicalEventId: null,
            Category: DocumentCategory.GeneralMedicalChart, // Uploaded as generic chart, expect AI auto-classification
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "mixed",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: new List<UploadDocumentFileDto>
            {
                new(new MemoryStream(pdfBytes), "Biopsy_Report_Sita.pdf", pdfBytes.Length)
            }
        );

        // WHEN: Ingestion pipeline executes
        var uploadResult = await _documentService.UploadDocumentsAsync(patient.Id, uploadRequest);

        // THEN: Upload succeeds and document is created
        Assert.True(uploadResult.Success);
        Assert.Single(uploadResult.CreatedDocuments);

        var doc = uploadResult.CreatedDocuments[0];
        Assert.NotNull(doc.LatestOcr);

        // AND: Dual-language OCR extracted English and Nepali script
        Assert.Contains("biopsy", doc.LatestOcr.ExtractedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("infiltrating ductal carcinoma", doc.LatestOcr.ExtractedText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("बी.पी. कोइराला मेमोरियल क्यान्सर अस्पताल", doc.LatestOcr.ExtractedText);

        // AND: Document is automatically classified as PathologyLabReport
        Assert.Equal(DocumentCategory.PathologyLabReport, doc.LatestOcr.PredictedCategory);
        Assert.True(doc.LatestOcr.ClassificationConfidence >= 0.80, "Classification confidence should meet or exceed 0.80");
        Assert.True(doc.LatestOcr.OcrConfidenceScore >= 0.70, "OCR confidence should meet or exceed 0.70");

        // AND: High-confidence document status is automatically set to Indexed
        Assert.Equal(DocumentStatus.Indexed, doc.Status);
        Assert.False(doc.LatestOcr.LowConfidenceWarning);
    }

    [Fact]
    public async Task AT03_2_LowConfidenceDocument_FlagsForSupervisorReview()
    {
        // GIVEN: A patient and encounter
        var (patient, encounter) = await SeedPatientAndEncounterAsync();

        // AND: A poor quality / degraded scan with minimal text (under word threshold)
        var degradedScanContent = "Scanned scan 123";
        var pdfBytes = CreateValidPdfWithText(degradedScanContent);

        var uploadRequest = new UploadDocumentsRequest(
            EncounterId: encounter.Id,
            ClinicalEventId: null,
            Category: DocumentCategory.GeneralMedicalChart,
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: new List<UploadDocumentFileDto>
            {
                new(new MemoryStream(pdfBytes), "Degraded_Scan_Page.pdf", pdfBytes.Length)
            }
        );

        // WHEN: Uploading degraded document
        var uploadResult = await _documentService.UploadDocumentsAsync(patient.Id, uploadRequest);
        var doc = uploadResult.CreatedDocuments[0];

        // THEN: Document status is set to RequiresReview (FR-OCR-002)
        Assert.Equal(DocumentStatus.RequiresReview, doc.Status);
        Assert.NotNull(doc.LatestOcr);
        Assert.True(doc.LatestOcr.LowConfidenceWarning, "Degraded document should trigger low confidence warning");

        // AND: The document is visible in the Supervisor Review Queue (FR-OCR-003)
        var reviewQueue = await _ocrService.GetReviewQueueAsync();
        Assert.Contains(reviewQueue, q => q.DocumentId == doc.Id);

        var queueItem = reviewQueue.First(q => q.DocumentId == doc.Id);
        Assert.Equal(patient.Mrn, queueItem.PatientMrn);
        Assert.Contains("confidence", queueItem.ReasonForReview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AT03_3_HumanInTheLoopVerification_TransitionsToIndexed()
    {
        // GIVEN: A document currently in RequiresReview status
        var (patient, encounter) = await SeedPatientAndEncounterAsync();
        var degradedScan = CreateValidPdfWithText("CT abdomen contrast scan unclear page");

        var uploadResult = await _documentService.UploadDocumentsAsync(patient.Id, new UploadDocumentsRequest(
            EncounterId: encounter.Id,
            ClinicalEventId: null,
            Category: DocumentCategory.GeneralMedicalChart,
            ClinicalDate: new DateOnly(2026, 10, 12),
            Language: "en",
            ConfidentialityLevel: ConfidentialityLevel.Normal,
            Files: new List<UploadDocumentFileDto>
            {
                new(new MemoryStream(degradedScan), "CT_Scan_Unclear.pdf", degradedScan.Length)
            }
        ));

        var docId = uploadResult.CreatedDocuments[0].Id;

        // WHEN: Supervisor reviews side-by-side, confirms category as RadiologyReport, and edits text
        var verifyRequest = new VerifyOcrRequest(
            ConfirmedCategory: DocumentCategory.RadiologyReport,
            CorrectedText: "CT Abdomen and Pelvis with IV Contrast: No distant hepatic or retroperitoneal metastasis.",
            VerificationNotes: "Verified CT report against physical film folder by Supervisor Sunita"
        );

        var verifiedDoc = await _ocrService.VerifyOcrAsync(docId, verifyRequest);

        // THEN: Document category is updated and status transitions to Indexed (FR-OCR-003)
        Assert.Equal(DocumentCategory.RadiologyReport, verifiedDoc.Category);
        Assert.Equal(DocumentStatus.Indexed, verifiedDoc.Status);

        // AND: Latest OCR shows human verification details
        var ocr = await _ocrService.GetOcrResultAsync(docId);
        Assert.NotNull(ocr);
        Assert.True(ocr.IsVerifiedByHuman);
        Assert.Equal("supervisor_sunita", ocr.VerifiedBy);
        Assert.NotNull(ocr.VerifiedAt);
        Assert.False(ocr.LowConfidenceWarning);
        Assert.Contains("CT Abdomen and Pelvis", ocr.ExtractedText);

        // AND: Audit log recorded the human verification event (NFR-004)
        _auditServiceMock.Verify(a => a.LogAsync(
            "DOCUMENT_OCR_VERIFIED",
            "Document",
            docId.ToString(),
            AuditOutcome.Success,
            It.Is<string>(d => d.Contains("RadiologyReport")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
