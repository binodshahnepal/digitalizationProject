using HospitalRecords.Domain.Entities;

namespace HospitalRecords.Application.DTOs;

public record SubmitClinicalFeedbackRequest(
    Guid PatientId,
    string QueryText,
    string AssistantResponseText,
    ClinicalFeedbackType FeedbackType,
    ClinicalFeedbackSeverity Severity,
    string ClinicianNotes
);

public record ClinicalFeedbackDto(
    Guid Id,
    Guid PatientId,
    string ClinicianUsername,
    string QueryText,
    string AssistantResponseText,
    string FeedbackType,
    string Severity,
    string Status,
    string ClinicianNotes,
    string? SupervisorResolutionNotes,
    string? ResolvedBy,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record UpdateFeedbackStatusRequest(
    ClinicalFeedbackStatus Status,
    string? ResolutionNotes
);

public record BenchmarkScenarioResultDto(
    string ScenarioId,
    string ClinicalQuestion,
    bool CitationsValid,
    bool CorrectlyAbstained,
    double PrecisionScore,
    string Details
);

public record ClinicalSafetyBenchmarkReportDto(
    DateTime EvaluatedAt,
    int TotalScenariosTested,
    int SupportedCitationsCount,
    double CitationPrecisionRate,
    double HallucinationRate,
    double GroundedAbstentionRate,
    int SecurityPenetrationTestsPassed,
    int SecurityPenetrationTestsTotal,
    bool OverallGatePassed,
    List<BenchmarkScenarioResultDto> Scenarios
);

public record DisasterRecoveryDrillResultDto(
    string DrillId,
    DateTime ExecutedAt,
    int TotalPatientsVerified,
    int TotalDocumentsVerified,
    int TotalChecksumsMatched,
    long DatabaseSizeEstimateBytes,
    double RecoveryTimeSeconds,
    double TargetRtoSeconds,
    bool RtoTargetMet,
    bool ChecksumIntegrityVerified,
    bool OverallDrillPassed
);
