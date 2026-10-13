using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Domain.Entities;

public enum ClinicalFeedbackType
{
    Hallucination = 1,
    MissingCitation = 2,
    IncorrectStageOrDiagnosis = 3,
    MedicationDiscrepancy = 4,
    IncompleteContext = 5,
    Other = 6
}

public enum ClinicalFeedbackSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ClinicalFeedbackStatus
{
    Reported = 1,
    UnderReview = 2,
    Resolved = 3,
    Dismissed = 4
}

public class ClinicalAiFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientId { get; set; }
    public Guid? UserId { get; set; }
    public string ClinicianUsername { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string AssistantResponseText { get; set; } = string.Empty;
    public ClinicalFeedbackType FeedbackType { get; set; }
    public ClinicalFeedbackSeverity Severity { get; set; }
    public ClinicalFeedbackStatus Status { get; set; } = ClinicalFeedbackStatus.Reported;
    public string ClinicianNotes { get; set; } = string.Empty;
    public string? SupervisorResolutionNotes { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    // Navigation
    public Patient Patient { get; set; } = null!;
}
