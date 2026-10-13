using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Application.DTOs;

public record CreateEncounterRequest(
    Guid PatientId,
    EncounterType Type,
    string Facility,
    string Department,
    DateTimeOffset StartDate,
    string? AttendingClinicianName,
    string? ReasonForVisit
);

public record EncounterDto(
    Guid Id,
    Guid PatientId,
    string EncounterNumber,
    EncounterType Type,
    EncounterStatus Status,
    string Facility,
    string Department,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    string? AttendingClinicianName,
    string? ReasonForVisit,
    int ClinicalEventsCount,
    int DocumentsCount,
    DateTimeOffset CreatedAt
);

public record CreateClinicalEventRequest(
    Guid EncounterId,
    string EventType,
    DateTimeOffset EventTime,
    string Author,
    string Department,
    string? Description
);

public record ClinicalEventDto(
    Guid Id,
    Guid EncounterId,
    string EventType,
    DateTimeOffset EventTime,
    string Author,
    string Department,
    string? Description,
    int DocumentsCount
);
