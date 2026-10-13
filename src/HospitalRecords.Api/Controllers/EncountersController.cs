using HospitalRecords.Application.DTOs;
using HospitalRecords.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Authorize]
public class EncountersController : ControllerBase
{
    private readonly IEncounterService _encounterService;

    public EncountersController(IEncounterService encounterService)
    {
        _encounterService = encounterService;
    }

    /// <summary>
    /// Create a new encounter for a patient (FR-VIS-001, FR-VIS-002)
    /// </summary>
    [HttpPost("api/v1/patients/{patientId:guid}/encounters")]
    [Authorize(Roles = "RegistrationClerk,MedicalRecordsSupervisor,HospitalAdmin,TreatingClinician,Nurse")]
    [ProducesResponseType(typeof(EncounterDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEncounter([FromRoute] Guid patientId, [FromBody] CreateEncounterRequest request, CancellationToken cancellationToken)
    {
        if (patientId != request.PatientId)
        {
            return BadRequest(new { message = "Patient ID in URL route does not match request body." });
        }

        var created = await _encounterService.CreateEncounterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Retrieve all encounters for a patient chronologically (FR-PAT-005)
    /// </summary>
    [HttpGet("api/v1/patients/{patientId:guid}/encounters")]
    [ProducesResponseType(typeof(List<EncounterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatientId([FromRoute] Guid patientId, CancellationToken cancellationToken)
    {
        var encounters = await _encounterService.GetByPatientIdAsync(patientId, cancellationToken);
        return Ok(encounters);
    }

    /// <summary>
    /// Retrieve an encounter by ID
    /// </summary>
    [HttpGet("api/v1/encounters/{id:guid}")]
    [ProducesResponseType(typeof(EncounterDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var encounter = await _encounterService.GetByIdAsync(id, cancellationToken);
        if (encounter == null)
        {
            return NotFound(new { message = $"Encounter with ID '{id}' was not found." });
        }

        return Ok(encounter);
    }

    /// <summary>
    /// Add a dated clinical event to an encounter (FR-VIS-002)
    /// </summary>
    [HttpPost("api/v1/encounters/{id:guid}/events")]
    [Authorize(Roles = "TreatingClinician,Nurse,MedicalRecordsSupervisor,HospitalAdmin")]
    [ProducesResponseType(typeof(ClinicalEventDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateClinicalEvent([FromRoute] Guid id, [FromBody] CreateClinicalEventRequest request, CancellationToken cancellationToken)
    {
        if (id != request.EncounterId)
        {
            return BadRequest(new { message = "Encounter ID in URL route does not match request body." });
        }

        var created = await _encounterService.CreateClinicalEventAsync(request, cancellationToken);
        return Ok(created);
    }

    /// <summary>
    /// List all clinical events for an encounter
    /// </summary>
    [HttpGet("api/v1/encounters/{id:guid}/events")]
    [ProducesResponseType(typeof(List<ClinicalEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClinicalEvents([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var events = await _encounterService.GetClinicalEventsAsync(id, cancellationToken);
        return Ok(events);
    }
}
