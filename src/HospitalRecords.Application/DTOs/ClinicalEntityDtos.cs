using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Application.DTOs;

public record ClinicalEntityDto(
    Guid Id,
    Guid PatientId,
    Guid DocumentId,
    Guid? EncounterId,
    ClinicalEntityType EntityType,
    string Category,
    string Name,
    string Value,
    string? Code,
    string? CodeSystem,
    double ConfidenceScore,
    string ExtractedText,
    int? TextOffsetStart,
    int? TextOffsetEnd,
    string AttributesJson,
    DateOnly? ClinicalDate,
    bool IsVerified,
    string? VerifiedBy,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset CreatedAt
);

public record VerifyClinicalEntityRequest(
    string? ConfirmedName = null,
    string? ConfirmedValue = null,
    string? ConfirmedCode = null,
    string? VerificationNotes = null
);

public record CreateClinicalEntityRequest(
    Guid PatientId,
    Guid DocumentId,
    Guid? EncounterId,
    ClinicalEntityType EntityType,
    string Category,
    string Name,
    string Value,
    string? Code = null,
    string? CodeSystem = null,
    double ConfidenceScore = 1.0,
    string? ExtractedText = null,
    string AttributesJson = "{}",
    DateOnly? ClinicalDate = null
);

public record UpdateClinicalEntityRequest(
    ClinicalEntityType? EntityType = null,
    string? Category = null,
    string? Name = null,
    string? Value = null,
    string? Code = null,
    string? CodeSystem = null,
    string? AttributesJson = null,
    DateOnly? ClinicalDate = null
);

public record TimelineEventDto(
    Guid Id,
    DateTimeOffset EventDate,
    string EventType,
    string Title,
    string Summary,
    string Department,
    string? Clinician,
    Guid? DocumentId,
    string? DocumentCategory,
    Guid? EncounterId,
    string? EncounterNumber,
    MilestoneType? Milestone,
    string? MilestoneLabel,
    List<ClinicalEntityDto> Entities,
    bool IsVerified
);

public record PatientTimelineDto(
    Guid PatientId,
    string PatientName,
    string Mrn,
    ClinicalEntityDto? PrimaryDiagnosis,
    ClinicalEntityDto? ClinicalStage,
    ClinicalEntityDto? HistologicGrade,
    List<ClinicalEntityDto> Biomarkers,
    List<TimelineEventDto> Events,
    int TotalEncounters,
    int TotalDocuments,
    int TotalEntities
);
