using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Application.Services;

public interface IEncounterService
{
    Task<EncounterDto> CreateEncounterAsync(CreateEncounterRequest request, CancellationToken cancellationToken = default);
    Task<List<EncounterDto>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<EncounterDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClinicalEventDto> CreateClinicalEventAsync(CreateClinicalEventRequest request, CancellationToken cancellationToken = default);
    Task<List<ClinicalEventDto>> GetClinicalEventsAsync(Guid encounterId, CancellationToken cancellationToken = default);
}

public class EncounterService : IEncounterService
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public EncounterService(
        IApplicationDbContext context,
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<EncounterDto> CreateEncounterAsync(CreateEncounterRequest request, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == request.PatientId, cancellationToken);

        if (patient == null)
        {
            throw new ArgumentException($"Patient with ID '{request.PatientId}' not found.");
        }

        // Generate unique Encounter Number (e.g., ENC-2026-00001)
        var currentYear = DateTime.UtcNow.Year;
        var existingCountThisYear = await _context.Encounters
            .CountAsync(e => e.EncounterNumber.StartsWith($"ENC-{currentYear}"), cancellationToken);
        var encounterNumber = $"ENC-{currentYear}-{(existingCountThisYear + 1):D5}";

        var encounter = new Encounter
        {
            PatientId = request.PatientId,
            EncounterNumber = encounterNumber,
            Facility = string.IsNullOrWhiteSpace(request.Facility) ? "BPKMCH Main Campus" : request.Facility.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? "General Oncology" : request.Department.Trim(),
            Type = request.Type,
            Status = EncounterStatus.Active,
            StartDate = request.StartDate.ToUniversalTime(),
            AttendingClinicianName = request.AttendingClinicianName?.Trim(),
            ReasonForVisit = request.ReasonForVisit?.Trim(),
            CreatedBy = _currentUserService.Username ?? "System",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "ENCOUNTER_CREATED",
            resource: "Encounter",
            resourceId: encounter.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Created encounter {encounter.EncounterNumber} ({encounter.Type}) for patient {patient.Mrn}",
            cancellationToken: cancellationToken);

        return MapToDto(encounter, 0, 0);
    }

    public async Task<List<EncounterDto>> GetByPatientIdAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var encounters = await _context.Encounters
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.StartDate)
            .Select(e => new
            {
                Encounter = e,
                ClinicalEventsCount = e.ClinicalEvents.Count,
                DocumentsCount = e.Documents.Count
            })
            .ToListAsync(cancellationToken);

        return encounters.Select(x => MapToDto(x.Encounter, x.ClinicalEventsCount, x.DocumentsCount)).ToList();
    }

    public async Task<EncounterDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var encounter = await _context.Encounters
            .Include(e => e.ClinicalEvents)
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (encounter == null) return null;

        return MapToDto(encounter, encounter.ClinicalEvents.Count, encounter.Documents.Count);
    }

    public async Task<ClinicalEventDto> CreateClinicalEventAsync(CreateClinicalEventRequest request, CancellationToken cancellationToken = default)
    {
        var encounter = await _context.Encounters
            .FirstOrDefaultAsync(e => e.Id == request.EncounterId, cancellationToken);

        if (encounter == null)
        {
            throw new ArgumentException($"Encounter with ID '{request.EncounterId}' not found.");
        }

        var clinicalEvent = new ClinicalEvent
        {
            EncounterId = request.EncounterId,
            EventType = request.EventType.Trim(),
            EventTime = request.EventTime,
            Author = string.IsNullOrWhiteSpace(request.Author) ? (_currentUserService.Username ?? "Clinician") : request.Author.Trim(),
            Department = string.IsNullOrWhiteSpace(request.Department) ? encounter.Department : request.Department.Trim(),
            Description = request.Description?.Trim(),
            CreatedBy = _currentUserService.Username ?? "System",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.ClinicalEvents.Add(clinicalEvent);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLINICAL_EVENT_CREATED",
            resource: "ClinicalEvent",
            resourceId: clinicalEvent.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Created event '{clinicalEvent.EventType}' in encounter {encounter.EncounterNumber}",
            cancellationToken: cancellationToken);

        return new ClinicalEventDto(
            Id: clinicalEvent.Id,
            EncounterId: clinicalEvent.EncounterId,
            EventType: clinicalEvent.EventType,
            EventTime: clinicalEvent.EventTime,
            Author: clinicalEvent.Author,
            Department: clinicalEvent.Department,
            Description: clinicalEvent.Description,
            DocumentsCount: 0
        );
    }

    public async Task<List<ClinicalEventDto>> GetClinicalEventsAsync(Guid encounterId, CancellationToken cancellationToken = default)
    {
        var events = await _context.ClinicalEvents
            .Where(ce => ce.EncounterId == encounterId)
            .OrderByDescending(ce => ce.EventTime)
            .Select(ce => new ClinicalEventDto(
                ce.Id,
                ce.EncounterId,
                ce.EventType,
                ce.EventTime,
                ce.Author,
                ce.Department,
                ce.Description,
                ce.Documents.Count
            ))
            .ToListAsync(cancellationToken);

        return events;
    }

    private static EncounterDto MapToDto(Encounter e, int eventsCount, int docsCount)
    {
        return new EncounterDto(
            Id: e.Id,
            PatientId: e.PatientId,
            EncounterNumber: e.EncounterNumber,
            Type: e.Type,
            Status: e.Status,
            Facility: e.Facility,
            Department: e.Department,
            StartDate: e.StartDate,
            EndDate: e.EndDate,
            AttendingClinicianName: e.AttendingClinicianName,
            ReasonForVisit: e.ReasonForVisit,
            ClinicalEventsCount: eventsCount,
            DocumentsCount: docsCount,
            CreatedAt: e.CreatedAt
        );
    }
}
