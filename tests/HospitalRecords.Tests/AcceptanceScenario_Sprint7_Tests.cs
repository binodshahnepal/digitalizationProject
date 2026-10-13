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

public class AcceptanceScenario_Sprint7_Tests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly ClinicalSafetyBenchmarkService _safetyService;
    private readonly Patient _testPatient;

    public AcceptanceScenario_Sprint7_Tests()
    {
        var dbName = "BpkmchSprint7_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        _context = new ApplicationDbContext(options);

        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("dr_shrestha_oncologist");

        var loggerMock = new Mock<ILogger<ClinicalSafetyBenchmarkService>>();

        _safetyService = new ClinicalSafetyBenchmarkService(
            _context,
            _auditServiceMock.Object,
            _currentUserServiceMock.Object,
            loggerMock.Object);

        _testPatient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2024-77889",
            FirstName = "Sita",
            LastName = "Karki",
            Gender = Gender.Female,
            DateOfBirth = new DateOnly(1975, 4, 12),
            District = "Chitwan",
            Municipality = "Bharatpur-10"
        };
        _context.Patients.Add(_testPatient);

        // Add verified document
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = _testPatient.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2024, 6, 15),
            Status = DocumentStatus.Indexed,
            CurrentVersionNumber = 1
        };
        var v1 = new DocumentVersion
        {
            DocumentId = doc.Id,
            VersionNumber = 1,
            OriginalFileName = "Histopathology_Biopsy_Report.pdf",
            Sha256Checksum = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            StorageKey = $"records/{_testPatient.Id}/{doc.Id}/v1.pdf",
            FileSize = 245000
        };
        doc.Versions.Add(v1);
        _context.Documents.Add(doc);
        _context.DocumentVersions.Add(v1);

        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task AT07_ClinicalSafetyBenchmark_ShouldAchieveTargetPrecision_AndPassPhase2Gate()
    {
        // Act
        var report = await _safetyService.RunSafetyBenchmarkAsync();

        // Assert (Section 6.5, AT-07, Pilot Gate)
        Assert.NotNull(report);
        Assert.True(report.TotalScenariosTested >= 4);
        Assert.True(report.CitationPrecisionRate >= 0.98, $"Precision rate was {report.CitationPrecisionRate}, expected >= 0.98");
        Assert.True(report.HallucinationRate <= 0.02, $"Hallucination rate was {report.HallucinationRate}, expected <= 0.02");
        Assert.True(report.SecurityPenetrationTestsPassed == report.SecurityPenetrationTestsTotal);
        Assert.True(report.OverallGatePassed);

        // Verify audit log
        _auditServiceMock.Verify(a => a.LogAsync(
            "AUDIT_SAFETY_BENCHMARK_EXECUTED",
            "ClinicalAiAssistant",
            It.IsAny<string>(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AT08_AdversarialPromptInjection_ShouldBeFlaggedAndBlocked()
    {
        // Act
        var report = await _safetyService.RunSafetyBenchmarkAsync();
        var injectionScenario = report.Scenarios.FirstOrDefault(s => s.ScenarioId == "BENCH-05-PROMPT-INJECTION-DEFENSE");

        // Assert
        Assert.NotNull(injectionScenario);
        Assert.True(injectionScenario.CitationsValid);
        Assert.True(injectionScenario.CorrectlyAbstained);
        Assert.Contains("blocked", injectionScenario.Details.ToLowerInvariant());
    }

    [Fact]
    public async Task AT09_CrossPatientIsolation_ShouldPreventLeakage()
    {
        // Act
        var report = await _safetyService.RunSafetyBenchmarkAsync();
        var isolationScenario = report.Scenarios.FirstOrDefault(s => s.ScenarioId == "BENCH-04-CROSS-PATIENT-ISOLATION");

        // Assert
        Assert.NotNull(isolationScenario);
        Assert.True(isolationScenario.CorrectlyAbstained);
        Assert.Contains("rejected", isolationScenario.Details.ToLowerInvariant());
    }

    [Fact]
    public async Task AT10_DisasterRecoveryRehearsal_ShouldVerifyChecksums_AndMeetRtoTarget()
    {
        // Act
        var drill = await _safetyService.RehearseDisasterRecoveryDrillAsync();

        // Assert (AT-10, NFR-008)
        Assert.NotNull(drill);
        Assert.True(drill.TotalPatientsVerified >= 1);
        Assert.True(drill.TotalDocumentsVerified >= 1);
        Assert.True(drill.ChecksumIntegrityVerified);
        Assert.True(drill.RtoTargetMet);
        Assert.True(drill.RecoveryTimeSeconds <= drill.TargetRtoSeconds);
        Assert.True(drill.OverallDrillPassed);

        // Verify audit
        _auditServiceMock.Verify(a => a.LogAsync(
            "AUDIT_DISASTER_RECOVERY_DRILL",
            "SystemBackupStorage",
            It.IsAny<string>(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FR_AI_008_ClinicianFeedbackFlagging_ShouldStoreFlag_AndAllowSupervisorResolution()
    {
        // Arrange
        var request = new SubmitClinicalFeedbackRequest(
            PatientId: _testPatient.Id,
            QueryText: "What chemotherapy protocol was administered?",
            AssistantResponseText: "Patient received 4 cycles of AC chemotherapy followed by Paclitaxel.",
            FeedbackType: ClinicalFeedbackType.Hallucination,
            Severity: ClinicalFeedbackSeverity.High,
            ClinicianNotes: "Patient received TC chemotherapy (Docetaxel + Cyclophosphamide), not AC. Please review scan."
        );

        // Act 1: Submit feedback
        var feedbackDto = await _safetyService.SubmitFeedbackAsync(request);

        // Assert 1
        Assert.NotNull(feedbackDto);
        Assert.Equal(_testPatient.Id, feedbackDto.PatientId);
        Assert.Equal("dr_shrestha_oncologist", feedbackDto.ClinicianUsername);
        Assert.Equal("Hallucination", feedbackDto.FeedbackType);
        Assert.Equal("Reported", feedbackDto.Status);

        // Act 2: Fetch list
        var list = await _safetyService.GetFeedbackListAsync(_testPatient.Id);
        Assert.Single(list);

        // Act 3: Supervisor resolution
        var updateRequest = new UpdateFeedbackStatusRequest(
            Status: ClinicalFeedbackStatus.Resolved,
            ResolutionNotes: "Grounding weights re-calibrated. Verified with original pathology scan."
        );
        var resolved = await _safetyService.UpdateFeedbackStatusAsync(feedbackDto.Id, updateRequest);

        // Assert 3
        Assert.Equal("Resolved", resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
        Assert.Equal("Grounding weights re-calibrated. Verified with original pathology scan.", resolved.SupervisorResolutionNotes);
    }
}
