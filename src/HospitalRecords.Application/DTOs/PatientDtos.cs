using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Application.DTOs;

public record CreatePatientRequest(
    string Mrn,
    string? NationalId,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    string? Email,
    string? District,
    string? Municipality,
    string? Ward,
    string? AddressLine,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    string IdentityProvenance,
    bool ForceCreateIfDuplicate = false
);

public record UpdatePatientRequest(
    string? NationalId,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    string? Email,
    string? District,
    string? Municipality,
    string? Ward,
    string? AddressLine,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    string UpdateReason
);

public record PatientDto(
    Guid Id,
    string Mrn,
    string? NationalId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    Gender Gender,
    string? PhoneNumber,
    string? Email,
    string? District,
    string? Municipality,
    string? Ward,
    string? AddressLine,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelation,
    string IdentityProvenance,
    bool IsRestricted,
    Guid? MergedIntoPatientId,
    DateTimeOffset CreatedAt,
    string CreatedBy
);

public record DuplicateMatchCandidate(
    Guid ExistingPatientId,
    string Mrn,
    string FullName,
    DateOnly DateOfBirth,
    string? PhoneNumber,
    double MatchScore,
    string MatchReason
);

public record PatientCreationResult(
    bool Success,
    PatientDto? Patient,
    List<DuplicateMatchCandidate> PotentialDuplicates,
    string? ErrorMessage
);

public record PatientSearchFilter(
    string? Query,
    string? Mrn,
    string? Name,
    DateOnly? DateOfBirth,
    string? PhoneNumber,
    int PageNumber = 1,
    int PageSize = 20
);

public record PaginatedList<T>(
    List<T> Items,
    int TotalCount,
    int PageNumber,
    int TotalPages
);
