using HospitalRecords.Domain.Common;
using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Domain.Entities;

public class Patient : BaseEntity
{
    /// <summary>
    /// External Hospital Registration Number / Medical Record Number (unique within issuing authority)
    /// </summary>
    public string Mrn { get; set; } = string.Empty;

    /// <summary>
    /// National Identity / Citizenship Number if available
    /// </summary>
    public string? NationalId { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; } = Gender.Unknown;

    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    // Geographic demographics (Nepal context: District, Municipality, Ward)
    public string? District { get; set; }
    public string? Municipality { get; set; }
    public string? Ward { get; set; }
    public string? AddressLine { get; set; }

    // Emergency Contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }

    /// <summary>
    /// Provenance/Origin of identification (e.g., "National ID", "OPD Card", "Biometric", "Self-Reported")
    /// </summary>
    public string IdentityProvenance { get; set; } = "OPD Desk";

    /// <summary>
    /// Restriction flag for sensitive/VIP/quarantined patient records (FR-PAT-006)
    /// </summary>
    public bool IsRestricted { get; set; } = false;

    /// <summary>
    /// For supervised patient record merge lineage (FR-PAT-004). Null if primary record.
    /// </summary>
    public Guid? MergedIntoPatientId { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(MiddleName) 
        ? $"{FirstName} {LastName}" 
        : $"{FirstName} {MiddleName} {LastName}";
}
