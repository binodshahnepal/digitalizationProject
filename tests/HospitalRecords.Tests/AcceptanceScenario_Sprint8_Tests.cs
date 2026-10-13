using System.Text;
using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using HospitalRecords.Infrastructure.Persistence;
using HospitalRecords.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HospitalRecords.Tests;

public class AcceptanceScenario_Sprint8_Tests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly AdminOperationsService _adminService;
    private readonly FhirBridgeService _fhirService;
    private readonly Patient _patient1;
    private readonly Patient _patient2;

    public AcceptanceScenario_Sprint8_Tests()
    {
        var dbName = "BpkmchSprint8_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        _context = new ApplicationDbContext(options);

        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("admin_records_officer");

        var loggerAdminMock = new Mock<ILogger<AdminOperationsService>>();
        var loggerFhirMock = new Mock<ILogger<FhirBridgeService>>();

        _adminService = new AdminOperationsService(
            _context,
            _auditServiceMock.Object,
            _currentUserServiceMock.Object,
            loggerAdminMock.Object);

        _fhirService = new FhirBridgeService(_context, loggerFhirMock.Object);

        _patient1 = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2024-00101",
            FirstName = "Bikram",
            LastName = "Thapa",
            Gender = Gender.Male,
            DateOfBirth = new DateOnly(1980, 5, 20),
            PhoneNumber = "+977-9841234567"
        };

        _patient2 = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2024-00102",
            FirstName = "Kamala",
            LastName = "Gurung",
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1985, 11, 14),
            PhoneNumber = "+977-9851234568"
        };

        _context.Patients.AddRange(_patient1, _patient2);

        // Add document for patient1
        var doc1 = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = _patient1.Id,
            Category = DocumentCategory.OperativeNote,
            ClinicalDate = new DateOnly(2024, 7, 10),
            Status = DocumentStatus.Indexed,
            CurrentVersionNumber = 1,
            CreatedAt = DateTime.UtcNow
        };
        var v1 = new DocumentVersion
        {
            DocumentId = doc1.Id,
            VersionNumber = 1,
            OriginalFileName = "Surgical_Operative_Note.pdf",
            ContentType = "application/pdf",
            FileSize = 350000,
            Sha256Checksum = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90"
        };
        doc1.Versions.Add(v1);
        _context.Documents.Add(doc1);
        _context.DocumentVersions.Add(v1);

        // Add audit log
        _context.AuditEvents.Add(new AuditEvent
        {
            Id = 1,
            Timestamp = DateTimeOffset.UtcNow,
            Action = "AUDIT_PATIENT_ACCESS",
            Resource = "Patient",
            ResourceId = _patient1.Id.ToString(),
            Outcome = AuditOutcome.Success,
            ActorId = "user-1",
            ActorUsername = "dr_oncologist",
            IpAddress = "127.0.0.1",
            Details = "Patient record opened"
        });

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task AT12_BulkBatchIngestion_ShouldSplitBarcodeSheets_MatchPatients_AndReconcileTotals()
    {
        // Arrange: Fake batch multi-page PDF stream with 500KB
        var fakePdfBytes = Encoding.UTF8.GetBytes(new string('A', 250_000));
        using var stream = new MemoryStream(fakePdfBytes);

        // Act
        var result = await _adminService.ProcessBulkBatchUploadAsync(
            stream, 
            "Historical_Archive_Batch_Box_14.pdf", 
            fakePdfBytes.Length);

        // Assert (AT-12, FR-DOC-007)
        Assert.NotNull(result);
        Assert.NotEmpty(result.BatchReference);
        Assert.True(result.TotalPagesScanned > 0);
        Assert.True(result.TotalDocumentsExtracted >= 3);
        Assert.True(result.MatchedPatientsCount >= 2);
        Assert.True(result.MatchedPatientsCount + result.UnassignedCount == result.TotalDocumentsExtracted);

        // Verify batch job details
        var jobDetails = await _adminService.GetBulkBatchJobDetailsAsync(result.BatchJobId);
        Assert.NotNull(jobDetails);
        Assert.Equal(result.TotalDocumentsExtracted, jobDetails.Items.Count);

        // Verify matched item created document
        var matchedItem = jobDetails.Items.FirstOrDefault(i => i.Status == "Matched");
        Assert.NotNull(matchedItem);
        Assert.NotNull(matchedItem.GeneratedDocumentId);
        Assert.NotNull(matchedItem.MatchedPatientId);

        // Verify unassigned items are flagged for supervisor review (FR-DOC-006)
        if (jobDetails.UnassignedCount > 0)
        {
            var unassignedItem = jobDetails.Items.FirstOrDefault(i => i.Status == "Unassigned");
            Assert.NotNull(unassignedItem);
            Assert.Contains("Supervisor", unassignedItem.Notes);
        }

        // Verify audit trail
        _auditServiceMock.Verify(a => a.LogAsync(
            "AUDIT_BULK_BATCH_PROCESSED",
            "BulkBatchIngestion",
            It.IsAny<string>(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FR_ADM_002_HospitalTelemetry_ShouldReturnRealtimeOperationalMetrics()
    {
        // Act
        var telemetry = await _adminService.GetTelemetryAsync();

        // Assert (FR-ADM-002, FR-ADM-003)
        Assert.NotNull(telemetry);
        Assert.True(telemetry.TotalPatientsCount >= 2);
        Assert.True(telemetry.TotalDocumentsCount >= 1);
        Assert.True(telemetry.StorageUsedBytes > 0);
        Assert.True(telemetry.StorageTotalBytes > 0);
        Assert.True(telemetry.AverageOcrConfidence >= 0.0);
        Assert.True(telemetry.SystemUptimeHours > 0.0);
    }

    [Fact]
    public async Task FR_ADM_004_AuditLogExport_ShouldGenerateCsv_WithSha256DigitalSeal()
    {
        // Arrange
        var filter = new AuditLogExportFilterRequest(
            FromDate: DateTime.UtcNow.AddDays(-7),
            ToDate: DateTime.UtcNow.AddDays(1),
            Action: "AUDIT_PATIENT_ACCESS",
            Role: null,
            ResourceId: null
        );

        // Act
        var export = await _adminService.ExportAuditLogsAsync(filter);

        // Assert (FR-ADM-004)
        Assert.NotNull(export);
        Assert.NotEmpty(export.ExportId);
        Assert.True(export.TotalRecords >= 1);
        Assert.NotEmpty(export.Sha256IntegritySeal);
        Assert.Equal(64, export.Sha256IntegritySeal.Length);
        Assert.Contains("EventId,TimestampUtc,Action", export.CsvContent);
        Assert.Contains("AUDIT_PATIENT_ACCESS", export.CsvContent);

        // Verify audit trail
        _auditServiceMock.Verify(a => a.LogAsync(
            "AUDIT_COMPLIANCE_EXPORT",
            "AuditLogs",
            It.IsAny<string>(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task INT_003_FhirPatientBridge_ShouldProduceValidFhirR4PatientResource()
    {
        // Act
        var fhirPatient = await _fhirService.GetPatientAsFhirAsync(_patient1.Id);

        // Assert (INT-003)
        Assert.NotNull(fhirPatient);
        Assert.Equal("Patient", fhirPatient.ResourceType);
        Assert.Equal(_patient1.Id.ToString(), fhirPatient.Id);
        Assert.Equal("male", fhirPatient.Gender);
        Assert.Equal("1980-05-20", fhirPatient.BirthDate);
        Assert.Single(fhirPatient.Identifier);
        Assert.Equal(_patient1.Mrn, fhirPatient.Identifier[0].Value);
        Assert.Single(fhirPatient.Name);
        Assert.Equal("Thapa", fhirPatient.Name[0].Family);
        Assert.Equal("Bikram", fhirPatient.Name[0].Given[0]);
    }

    [Fact]
    public async Task INT_004_FhirDocumentReferenceBridge_ShouldProduceValidFhirBundle()
    {
        // Act
        var bundle = await _fhirService.GetPatientDocumentsAsFhirAsync(_patient1.Id);

        // Assert (INT-004)
        Assert.NotNull(bundle);
        Assert.Equal("Bundle", bundle.ResourceType);
        Assert.Equal("searchset", bundle.Type);
        Assert.True(bundle.Total >= 1);
        Assert.Single(bundle.Entry);

        var docRef = bundle.Entry[0].Resource;
        Assert.Equal("DocumentReference", docRef.ResourceType);
        Assert.Equal("current", docRef.Status);
        Assert.Equal($"Patient/{_patient1.Id}", docRef.Subject);
        Assert.Single(docRef.Content);
        Assert.Contains("/api/documents/", docRef.Content[0].Attachment.Url);
    }
}
