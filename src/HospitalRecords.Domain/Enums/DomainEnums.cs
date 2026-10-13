namespace HospitalRecords.Domain.Enums;

public enum UserRoleType
{
    RegistrationClerk,
    TreatingClinician,
    Nurse,
    MedicalRecordsSupervisor,
    HospitalAdmin,
    ITAdmin,
    ClinicalAiReviewer,
    Auditor
}

public enum Gender
{
    Male,
    Female,
    Other,
    Unknown
}

public enum AuditOutcome
{
    Success,
    Denied,
    Failed
}
