using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalRecords.Infrastructure.Services;

public class FhirBridgeService : IFhirBridgeService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<FhirBridgeService> _logger;

    public FhirBridgeService(IApplicationDbContext context, ILogger<FhirBridgeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FhirPatientResource> GetPatientAsFhirAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

        if (patient == null)
            throw new ArgumentException($"Patient '{patientId}' not found.");

        var givenNames = new List<string> { patient.FirstName };
        if (!string.IsNullOrWhiteSpace(patient.MiddleName))
            givenNames.Add(patient.MiddleName);

        var fhirPatient = new FhirPatientResource
        {
            Id = patient.Id.ToString(),
            Active = true,
            Gender = patient.Gender.ToString().ToLowerInvariant(),
            BirthDate = patient.DateOfBirth.ToString("yyyy-MM-dd"),
            Identifier = new List<FhirIdentifier>
            {
                new() { System = "https://bpkmch.org.np/identifiers/mrn", Value = patient.Mrn }
            },
            Name = new List<FhirHumanName>
            {
                new()
                {
                    Use = "official",
                    Family = patient.LastName,
                    Given = givenNames
                }
            },
            Telecom = new List<FhirContactPoint>()
        };

        if (!string.IsNullOrWhiteSpace(patient.PhoneNumber))
        {
            fhirPatient.Telecom.Add(new FhirContactPoint
            {
                System = "phone",
                Value = patient.PhoneNumber
            });
        }

        return fhirPatient;
    }

    public async Task<FhirBundleResponse<FhirDocumentReferenceResource>> GetPatientDocumentsAsFhirAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

        if (patient == null)
            throw new ArgumentException($"Patient '{patientId}' not found.");

        var docs = await _context.Documents
            .Include(d => d.Versions)
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var entries = new List<FhirBundleEntry<FhirDocumentReferenceResource>>();

        foreach (var doc in docs)
        {
            var latestVersion = doc.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            var fileName = latestVersion?.OriginalFileName ?? $"Document_{doc.Id}.pdf";
            var docRef = new FhirDocumentReferenceResource
            {
                Id = doc.Id.ToString(),
                Status = "current",
                DocStatus = "final",
                Date = doc.CreatedAt.ToString("O"),
                Subject = $"Patient/{patient.Id}",
                Description = $"{doc.Category} - {fileName}",
                Content = new List<FhirDocumentContent>
                {
                    new()
                    {
                        Attachment = new FhirAttachment
                        {
                            ContentType = latestVersion?.ContentType ?? "application/pdf",
                            Url = $"http://localhost:5000/api/documents/{doc.Id}/download",
                            Title = fileName
                        }
                    }
                }
            };

            entries.Add(new FhirBundleEntry<FhirDocumentReferenceResource>
            {
                FullUrl = $"http://localhost:5000/fhir/DocumentReference/{doc.Id}",
                Resource = docRef
            });
        }

        return new FhirBundleResponse<FhirDocumentReferenceResource>
        {
            Total = entries.Count,
            Entry = entries
        };
    }
}
