using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Application.Services;

public interface IPatientService
{
    Task<PatientCreationResult> CreatePatientAsync(CreatePatientRequest request, CancellationToken cancellationToken = default);
    Task<PatientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PatientDto?> GetByMrnAsync(string mrn, CancellationToken cancellationToken = default);
    Task<PaginatedList<PatientDto>> SearchAsync(PatientSearchFilter filter, CancellationToken cancellationToken = default);
    Task<List<DuplicateMatchCandidate>> CheckDuplicatesAsync(CreatePatientRequest request, CancellationToken cancellationToken = default);
    Task<PatientDto?> UpdateAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken = default);
}

public class PatientService : IPatientService
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;

    public PatientService(
        IApplicationDbContext context, 
        IAuditService auditService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<PatientCreationResult> CreatePatientAsync(CreatePatientRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedMrn = request.Mrn.Trim();

        // 1. Enforce strict MRN uniqueness within authority (FR-PAT-001)
        var existingWithMrn = await _context.Patients
            .AnyAsync(p => p.Mrn.ToLower() == normalizedMrn.ToLower(), cancellationToken);

        if (existingWithMrn)
        {
            await _auditService.LogAsync(
                action: "PATIENT_CREATION_FAILED",
                resource: "Patient",
                resourceId: normalizedMrn,
                outcome: AuditOutcome.Failed,
                details: $"MRN '{normalizedMrn}' already exists. Re-registration rejected under FR-PAT-001.",
                cancellationToken: cancellationToken);

            return new PatientCreationResult(
                Success: false,
                Patient: null,
                PotentialDuplicates: [],
                ErrorMessage: $"A patient with MRN '{normalizedMrn}' already exists. Enforce reuse of existing record under AT-01 / FR-PAT-001.");
        }

        // 2. Duplicate Detection (FR-PAT-002) - Don't silently merge; identify potential matches
        var duplicates = await CheckDuplicatesAsync(request, cancellationToken);
        if (duplicates.Count > 0 && !request.ForceCreateIfDuplicate)
        {
            await _auditService.LogAsync(
                action: "PATIENT_CREATION_DUPLICATE_WARNING",
                resource: "Patient",
                resourceId: normalizedMrn,
                outcome: AuditOutcome.Denied,
                details: $"Duplicate check triggered for MRN '{normalizedMrn}'. Found {duplicates.Count} potential match(es).",
                cancellationToken: cancellationToken);

            return new PatientCreationResult(
                Success: false,
                Patient: null,
                PotentialDuplicates: duplicates,
                ErrorMessage: "Potential duplicate patient records found. Review candidates or confirm duplicate override.");
        }

        // 3. Create Persistent Patient Record
        var patient = new Patient
        {
            Mrn = normalizedMrn,
            NationalId = request.NationalId?.Trim(),
            FirstName = request.FirstName.Trim(),
            MiddleName = request.MiddleName?.Trim(),
            LastName = request.LastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            PhoneNumber = request.PhoneNumber?.Trim(),
            Email = request.Email?.Trim(),
            District = request.District?.Trim(),
            Municipality = request.Municipality?.Trim(),
            Ward = request.Ward?.Trim(),
            AddressLine = request.AddressLine?.Trim(),
            EmergencyContactName = request.EmergencyContactName?.Trim(),
            EmergencyContactPhone = request.EmergencyContactPhone?.Trim(),
            EmergencyContactRelation = request.EmergencyContactRelation?.Trim(),
            IdentityProvenance = string.IsNullOrWhiteSpace(request.IdentityProvenance) ? "OPD Desk" : request.IdentityProvenance.Trim(),
            CreatedBy = _currentUserService.Username ?? "System",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Patients.Add(patient);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PATIENT_CREATED",
            resource: "Patient",
            resourceId: patient.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Created patient MRN '{patient.Mrn}' with internal ID '{patient.Id}'. Provenance: {patient.IdentityProvenance}",
            cancellationToken: cancellationToken);

        return new PatientCreationResult(
            Success: true,
            Patient: MapToDto(patient),
            PotentialDuplicates: duplicates,
            ErrorMessage: null);
    }

    public async Task<List<DuplicateMatchCandidate>> CheckDuplicatesAsync(CreatePatientRequest request, CancellationToken cancellationToken = default)
    {
        var candidates = new List<DuplicateMatchCandidate>();
        var normFirstName = request.FirstName.Trim().ToLower();
        var normLastName = request.LastName.Trim().ToLower();
        var normPhone = request.PhoneNumber?.Trim();
        var normNid = request.NationalId?.Trim().ToLower();

        // Check National ID match
        if (!string.IsNullOrWhiteSpace(normNid))
        {
            var nidMatches = await _context.Patients
                .Where(p => p.NationalId != null && p.NationalId.ToLower() == normNid)
                .ToListAsync(cancellationToken);

            foreach (var match in nidMatches)
            {
                candidates.Add(new DuplicateMatchCandidate(
                    ExistingPatientId: match.Id,
                    Mrn: match.Mrn,
                    FullName: match.FullName,
                    DateOfBirth: match.DateOfBirth,
                    PhoneNumber: match.PhoneNumber,
                    MatchScore: 1.0,
                    MatchReason: "Exact National Identity / Citizenship match"
                ));
            }
        }

        // Check Name + DOB match
        var nameDobMatches = await _context.Patients
            .Where(p => p.FirstName.ToLower() == normFirstName &&
                        p.LastName.ToLower() == normLastName &&
                        p.DateOfBirth == request.DateOfBirth)
            .ToListAsync(cancellationToken);

        foreach (var match in nameDobMatches)
        {
            if (candidates.All(c => c.ExistingPatientId != match.Id))
            {
                candidates.Add(new DuplicateMatchCandidate(
                    ExistingPatientId: match.Id,
                    Mrn: match.Mrn,
                    FullName: match.FullName,
                    DateOfBirth: match.DateOfBirth,
                    PhoneNumber: match.PhoneNumber,
                    MatchScore: 0.90,
                    MatchReason: "Exact First Name, Last Name, and Date of Birth match"
                ));
            }
        }

        // Check Name + Phone match (if phone provided)
        if (!string.IsNullOrWhiteSpace(normPhone))
        {
            var namePhoneMatches = await _context.Patients
                .Where(p => p.FirstName.ToLower() == normFirstName &&
                            p.LastName.ToLower() == normLastName &&
                            p.PhoneNumber == normPhone)
                .ToListAsync(cancellationToken);

            foreach (var match in namePhoneMatches)
            {
                if (candidates.All(c => c.ExistingPatientId != match.Id))
                {
                    candidates.Add(new DuplicateMatchCandidate(
                        ExistingPatientId: match.Id,
                        Mrn: match.Mrn,
                        FullName: match.FullName,
                        DateOfBirth: match.DateOfBirth,
                        PhoneNumber: match.PhoneNumber,
                        MatchScore: 0.85,
                        MatchReason: "Exact Full Name and Phone Number match"
                    ));
                }
            }
        }

        return candidates;
    }

    public async Task<PatientDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (patient == null) return null;

        await _auditService.LogAsync(
            action: "PATIENT_VIEWED",
            resource: "Patient",
            resourceId: patient.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Viewed patient MRN: {patient.Mrn}",
            cancellationToken: cancellationToken);

        return MapToDto(patient);
    }

    public async Task<PatientDto?> GetByMrnAsync(string mrn, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Mrn.ToLower() == mrn.Trim().ToLower(), cancellationToken);

        if (patient == null) return null;

        await _auditService.LogAsync(
            action: "PATIENT_VIEWED_BY_MRN",
            resource: "Patient",
            resourceId: patient.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Looked up patient by MRN: {patient.Mrn}",
            cancellationToken: cancellationToken);

        return MapToDto(patient);
    }

    public async Task<PaginatedList<PatientDto>> SearchAsync(PatientSearchFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query.Trim().ToLower();
            query = query.Where(p =>
                p.Mrn.ToLower().Contains(q) ||
                p.FirstName.ToLower().Contains(q) ||
                p.LastName.ToLower().Contains(q) ||
                (p.PhoneNumber != null && p.PhoneNumber.Contains(q)) ||
                (p.NationalId != null && p.NationalId.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Mrn))
        {
            var m = filter.Mrn.Trim().ToLower();
            query = query.Where(p => p.Mrn.ToLower().Contains(m));
        }

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            var n = filter.Name.Trim().ToLower();
            query = query.Where(p => p.FirstName.ToLower().Contains(n) || p.LastName.ToLower().Contains(n));
        }

        if (filter.DateOfBirth.HasValue)
        {
            query = query.Where(p => p.DateOfBirth == filter.DateOfBirth.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.PhoneNumber))
        {
            query = query.Where(p => p.PhoneNumber != null && p.PhoneNumber.Contains(filter.PhoneNumber.Trim()));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PATIENT_SEARCHED",
            resource: "Patient",
            resourceId: null,
            outcome: AuditOutcome.Success,
            details: $"Search executed. Query: '{filter.Query}', Count: {total}",
            cancellationToken: cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        var totalPages = (int)Math.Ceiling((double)total / pageSize);

        return new PaginatedList<PatientDto>(dtos, total, pageNumber, totalPages);
    }

    public async Task<PatientDto?> UpdateAsync(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (patient == null) return null;

        patient.NationalId = request.NationalId?.Trim();
        patient.FirstName = request.FirstName.Trim();
        patient.MiddleName = request.MiddleName?.Trim();
        patient.LastName = request.LastName.Trim();
        patient.DateOfBirth = request.DateOfBirth;
        patient.Gender = request.Gender;
        patient.PhoneNumber = request.PhoneNumber?.Trim();
        patient.Email = request.Email?.Trim();
        patient.District = request.District?.Trim();
        patient.Municipality = request.Municipality?.Trim();
        patient.Ward = request.Ward?.Trim();
        patient.AddressLine = request.AddressLine?.Trim();
        patient.EmergencyContactName = request.EmergencyContactName?.Trim();
        patient.EmergencyContactPhone = request.EmergencyContactPhone?.Trim();
        patient.EmergencyContactRelation = request.EmergencyContactRelation?.Trim();
        patient.UpdatedAt = DateTimeOffset.UtcNow;
        patient.UpdatedBy = _currentUserService.Username ?? "System";

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PATIENT_UPDATED",
            resource: "Patient",
            resourceId: patient.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Updated patient demographics. Reason: {request.UpdateReason}",
            cancellationToken: cancellationToken);

        return MapToDto(patient);
    }

    private static PatientDto MapToDto(Patient p)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - p.DateOfBirth.Year;
        if (p.DateOfBirth > today.AddYears(-age)) age--;

        return new PatientDto(
            Id: p.Id,
            Mrn: p.Mrn,
            NationalId: p.NationalId,
            FirstName: p.FirstName,
            MiddleName: p.MiddleName,
            LastName: p.LastName,
            FullName: p.FullName,
            DateOfBirth: p.DateOfBirth,
            Age: age,
            Gender: p.Gender,
            PhoneNumber: p.PhoneNumber,
            Email: p.Email,
            District: p.District,
            Municipality: p.Municipality,
            Ward: p.Ward,
            AddressLine: p.AddressLine,
            EmergencyContactName: p.EmergencyContactName,
            EmergencyContactPhone: p.EmergencyContactPhone,
            EmergencyContactRelation: p.EmergencyContactRelation,
            IdentityProvenance: p.IdentityProvenance,
            IsRestricted: p.IsRestricted,
            MergedIntoPatientId: p.MergedIntoPatientId,
            CreatedAt: p.CreatedAt,
            CreatedBy: p.CreatedBy
        );
    }
}
