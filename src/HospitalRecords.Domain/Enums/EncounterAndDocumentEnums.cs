namespace HospitalRecords.Domain.Enums;

public enum EncounterType
{
    OPD,
    Emergency,
    Inpatient,
    FollowUp
}

public enum EncounterStatus
{
    Active,
    Discharged,
    Completed,
    Cancelled
}

public enum DocumentCategory
{
    DischargeSummary,
    PathologyLabReport,
    RadiologyReport,
    Prescription,
    OperativeNote,
    ClinicalProgressNote,
    ExternalReferral,
    GeneralMedicalChart
}

public enum DocumentStatus
{
    Received,
    Scanned,
    OcrQueued,
    Indexed,
    RequiresReview,
    Failed,
    Archived
}

public enum ConfidentialityLevel
{
    Normal,
    Restricted,
    HighlyConfidential
}
