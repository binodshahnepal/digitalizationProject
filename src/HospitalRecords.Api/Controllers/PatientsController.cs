using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Route("api/v1/patients")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;
    private readonly ITimelineService _timelineService;

    public PatientsController(
        IPatientService patientService,
        ITimelineService timelineService)
    {
        _patientService = patientService;
        _timelineService = timelineService;
    }

    /// <summary>
    /// Register a new patient with MRN uniqueness enforcement and duplicate checks (FR-PAT-001, FR-PAT-002)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "RegistrationClerk,MedicalRecordsSupervisor,HospitalAdmin")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PatientCreationResult), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePatientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Mrn) || string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { message = "MRN, First Name, and Last Name are required." });
        }

        var result = await _patientService.CreatePatientAsync(request, cancellationToken);

        if (!result.Success)
        {
            return Conflict(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Patient!.Id }, result.Patient);
    }

    /// <summary>
    /// Check for potential duplicate patient records prior to registration (FR-PAT-002)
    /// </summary>
    [HttpPost("check-duplicates")]
    [ProducesResponseType(typeof(List<DuplicateMatchCandidate>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckDuplicates([FromBody] CreatePatientRequest request, CancellationToken cancellationToken)
    {
        var duplicates = await _patientService.CheckDuplicatesAsync(request, cancellationToken);
        return Ok(duplicates);
    }

    /// <summary>
    /// Search patients by MRN, name, DOB, or phone number with pagination (FR-PAT-002)
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PaginatedList<PatientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PatientSearchFilter filter, CancellationToken cancellationToken)
    {
        var result = await _patientService.SearchAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve patient by persistent internal identifier (FR-PAT-001)
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var patient = await _patientService.GetByIdAsync(id, cancellationToken);
        if (patient == null)
        {
            return NotFound(new { message = $"Patient with ID '{id}' was not found." });
        }

        return Ok(patient);
    }

    /// <summary>
    /// Retrieve patient by Hospital Registration / Medical Record Number (MRN)
    /// </summary>
    [HttpGet("by-mrn/{mrn}")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByMrn([FromRoute] string mrn, CancellationToken cancellationToken)
    {
        var patient = await _patientService.GetByMrnAsync(mrn, cancellationToken);
        if (patient == null)
        {
            return NotFound(new { message = $"Patient with MRN '{mrn}' was not found." });
        }

        return Ok(patient);
    }

    /// <summary>
    /// Update patient demographics with full provenance and audit logging (FR-PAT-003)
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "RegistrationClerk,MedicalRecordsSupervisor,HospitalAdmin")]
    [ProducesResponseType(typeof(PatientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdatePatientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UpdateReason))
        {
            return BadRequest(new { message = "Update reason is mandatory for demographic audit provenance." });
        }

        var updated = await _patientService.UpdateAsync(id, request, cancellationToken);
        if (updated == null)
        {
            return NotFound(new { message = $"Patient with ID '{id}' was not found." });
        }

        return Ok(updated);
    }

    /// <summary>
    /// Retrieve longitudinal clinical trajectory and chronological patient timeline (FR-VIS-001, FR-VIS-002)
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType(typeof(PatientTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var timeline = await _timelineService.GetPatientTimelineAsync(id, cancellationToken);
            return Ok(timeline);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
