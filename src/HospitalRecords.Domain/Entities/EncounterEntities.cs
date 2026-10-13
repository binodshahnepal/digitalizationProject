using HospitalRecords.Domain.Common;
using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Domain.Entities;

public class Encounter : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>
    /// Unique encounter identifier (e.g. ENC-2026-00102) within hospital authority (FR-VIS-001)
    /// </summary>
    public string EncounterNumber { get; set; } = string.Empty;

    public string Facility { get; set; } = "BPKMCH Main Campus";
    public string Department { get; set; } = "General Oncology";

    public EncounterType Type { get; set; } = EncounterType.OPD;
    public EncounterStatus Status { get; set; } = EncounterStatus.Active;

    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndDate { get; set; }

    public string? AttendingClinicianName { get; set; }
    public string? ReasonForVisit { get; set; }

    public ICollection<ClinicalEvent> ClinicalEvents { get; set; } = new List<ClinicalEvent>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

public class ClinicalEvent : BaseEntity
{
    public Guid EncounterId { get; set; }
    public Encounter Encounter { get; set; } = null!;

    public string EventType { get; set; } = "Consultation";
    public DateTimeOffset EventTime { get; set; } = DateTimeOffset.UtcNow;
    public string Author { get; set; } = "Clinician";
    public string Department { get; set; } = "Oncology";
    public string? Description { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
