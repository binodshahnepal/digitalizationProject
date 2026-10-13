using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Infrastructure.Services;

public class TimelineService : ITimelineService
{
    private readonly IApplicationDbContext _context;

    public TimelineService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PatientTimelineDto> GetPatientTimelineAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

        if (patient == null)
        {
            throw new ArgumentException($"Patient with ID '{patientId}' not found.");
        }

        var encounters = await _context.Encounters
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.StartDate)
            .ToListAsync(cancellationToken);

        var documents = await _context.Documents
            .Include(d => d.OcrResults)
            .Include(d => d.Encounter)
            .Where(d => d.PatientId == patientId)
            .OrderBy(d => d.ClinicalDate)
            .ToListAsync(cancellationToken);

        var entities = await _context.ClinicalEntities
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        var clinicalEvents = await _context.ClinicalEvents
            .Include(ce => ce.Encounter)
            .Where(ce => ce.Encounter.PatientId == patientId)
            .OrderBy(ce => ce.EventTime)
            .ToListAsync(cancellationToken);

        // Header summaries
        var primaryDiag = entities
            .Where(e => e.EntityType == ClinicalEntityType.Diagnosis)
            .OrderByDescending(e => e.IsVerified)
            .ThenByDescending(e => e.ConfidenceScore)
            .FirstOrDefault();

        var stage = entities
            .Where(e => e.EntityType == ClinicalEntityType.CancerStage)
            .OrderByDescending(e => e.IsVerified)
            .ThenByDescending(e => e.ConfidenceScore)
            .FirstOrDefault()
            ?? entities
            .Where(e => e.EntityType == ClinicalEntityType.TnmStaging)
            .OrderByDescending(e => e.IsVerified)
            .ThenByDescending(e => e.ConfidenceScore)
            .FirstOrDefault();

        var grade = entities
            .Where(e => e.EntityType == ClinicalEntityType.HistologicGrade)
            .OrderByDescending(e => e.IsVerified)
            .FirstOrDefault();

        var biomarkers = entities
            .Where(e => e.EntityType == ClinicalEntityType.Biomarker)
            .Select(MapEntityToDto)
            .ToList();

        // Synthesize timeline items
        var timelineEvents = new List<TimelineEventDto>();

        // 1. Encounter Events
        for (int i = 0; i < encounters.Count; i++)
        {
            var enc = encounters[i];
            var milestone = (i == 0 && enc.Type == EncounterType.OPD)
                ? MilestoneType.InitialConsult
                : (enc.Type == EncounterType.FollowUp ? MilestoneType.RemissionOrSurveillance : (MilestoneType?)null);

            var mLabel = milestone switch
            {
                MilestoneType.InitialConsult => "Initial Oncology Consult",
                MilestoneType.RemissionOrSurveillance => "Follow-Up Surveillance",
                _ => null
            };

            timelineEvents.Add(new TimelineEventDto(
                Id: enc.Id,
                EventDate: enc.StartDate.ToUniversalTime(),
                EventType: "Encounter",
                Title: $"{enc.Type} Visit ({enc.EncounterNumber})",
                Summary: enc.ReasonForVisit ?? $"Clinical attendance in {enc.Department}",
                Department: enc.Department,
                Clinician: enc.AttendingClinicianName,
                DocumentId: null,
                DocumentCategory: null,
                EncounterId: enc.Id,
                EncounterNumber: enc.EncounterNumber,
                Milestone: milestone,
                MilestoneLabel: mLabel,
                Entities: new List<ClinicalEntityDto>(),
                IsVerified: true
            ));
        }

        // 2. Clinical Events (Consultations, Bedside Notes)
        foreach (var ce in clinicalEvents)
        {
            timelineEvents.Add(new TimelineEventDto(
                Id: ce.Id,
                EventDate: ce.EventTime.ToUniversalTime(),
                EventType: "ClinicalEvent",
                Title: ce.EventType,
                Summary: ce.Description ?? $"Event in {ce.Department}",
                Department: ce.Department,
                Clinician: ce.Author,
                DocumentId: null,
                DocumentCategory: null,
                EncounterId: ce.EncounterId,
                EncounterNumber: ce.Encounter?.EncounterNumber,
                Milestone: null,
                MilestoneLabel: null,
                Entities: new List<ClinicalEntityDto>(),
                IsVerified: true
            ));
        }

        // 3. Document Events (with linked Clinical Entities)
        foreach (var doc in documents)
        {
            var docEntities = entities.Where(e => e.DocumentId == doc.Id).Select(MapEntityToDto).ToList();
            var dtUtc = new DateTimeOffset(doc.ClinicalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

            MilestoneType? milestone = null;
            string? mLabel = null;

            if (doc.Category == DocumentCategory.PathologyLabReport)
            {
                var isBiopsy = docEntities.Any(e => e.Name.Contains("Biopsy", StringComparison.OrdinalIgnoreCase)) 
                               || doc.OcrResults.Any(o => o.ExtractedText.Contains("Biopsy", StringComparison.OrdinalIgnoreCase));
                milestone = isBiopsy ? MilestoneType.DiagnosticBiopsy : MilestoneType.PathologyConfirmation;
                mLabel = isBiopsy ? "Diagnostic Biopsy" : "Pathology Confirmation & Staging";
            }
            else if (doc.Category == DocumentCategory.OperativeNote || docEntities.Any(e => e.EntityType == ClinicalEntityType.SurgicalProcedure))
            {
                milestone = MilestoneType.DefinitiveSurgery;
                mLabel = "Definitive Surgical Intervention";
            }
            else if (docEntities.Any(e => e.EntityType == ClinicalEntityType.ChemotherapyRegimen))
            {
                milestone = MilestoneType.AdjuvantChemotherapy;
                mLabel = "Systemic Chemotherapy Regimen";
            }
            else if (docEntities.Any(e => e.EntityType == ClinicalEntityType.RadiationTherapy))
            {
                milestone = MilestoneType.RadiationTherapy;
                mLabel = "Radiation Oncology Course";
            }
            else if (doc.Category == DocumentCategory.DischargeSummary)
            {
                milestone = MilestoneType.RestagingAssessment;
                mLabel = "Hospital Discharge & Restaging";
            }

            var categoryTitle = doc.Category switch
            {
                DocumentCategory.PathologyLabReport => "Histopathology & IHC Report",
                DocumentCategory.RadiologyReport => "Diagnostic Radiology Scan",
                DocumentCategory.OperativeNote => "Operative Procedure Record",
                DocumentCategory.Prescription => "Chemotherapy & Medical Prescription",
                DocumentCategory.DischargeSummary => "Inpatient Discharge Summary",
                DocumentCategory.ClinicalProgressNote => "Clinical Progress Note",
                _ => "Medical Record Document"
            };

            // Summary text from entities or OCR
            var summary = docEntities.Count > 0
                ? string.Join("; ", docEntities.Take(3).Select(e => $"{e.Name}: {e.Value}"))
                : (doc.OcrResults.FirstOrDefault()?.ExtractedText.Take(120).ToString() ?? "Digitized clinical document");

            timelineEvents.Add(new TimelineEventDto(
                Id: doc.Id,
                EventDate: dtUtc,
                EventType: doc.Category.ToString(),
                Title: categoryTitle,
                Summary: summary,
                Department: "Oncology Services",
                Clinician: null,
                DocumentId: doc.Id,
                DocumentCategory: doc.Category.ToString(),
                EncounterId: doc.EncounterId,
                EncounterNumber: doc.Encounter?.EncounterNumber,
                Milestone: milestone,
                MilestoneLabel: mLabel,
                Entities: docEntities,
                IsVerified: doc.Status == DocumentStatus.Indexed
            ));
        }

        // Sort all events strictly chronologically
        var sortedEvents = timelineEvents.OrderBy(e => e.EventDate).ToList();

        return new PatientTimelineDto(
            PatientId: patient.Id,
            PatientName: patient.FullName,
            Mrn: patient.Mrn,
            PrimaryDiagnosis: primaryDiag != null ? MapEntityToDto(primaryDiag) : null,
            ClinicalStage: stage != null ? MapEntityToDto(stage) : null,
            HistologicGrade: grade != null ? MapEntityToDto(grade) : null,
            Biomarkers: biomarkers,
            Events: sortedEvents,
            TotalEncounters: encounters.Count,
            TotalDocuments: documents.Count,
            TotalEntities: entities.Count
        );
    }

    private static ClinicalEntityDto MapEntityToDto(ClinicalEntity e)
    {
        return new ClinicalEntityDto(
            e.Id,
            e.PatientId,
            e.DocumentId,
            e.EncounterId,
            e.EntityType,
            e.Category,
            e.Name,
            e.Value,
            e.Code,
            e.CodeSystem,
            e.ConfidenceScore,
            e.ExtractedText,
            e.TextOffsetStart,
            e.TextOffsetEnd,
            e.AttributesJson,
            e.ClinicalDate,
            e.IsVerified,
            e.VerifiedBy,
            e.VerifiedAt,
            e.CreatedAt
        );
    }
}
