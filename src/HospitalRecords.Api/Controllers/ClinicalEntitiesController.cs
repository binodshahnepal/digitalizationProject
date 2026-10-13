using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Authorize]
public class ClinicalEntitiesController : ControllerBase
{
    private readonly IClinicalEntityExtractionService _entityService;

    public ClinicalEntitiesController(IClinicalEntityExtractionService entityService)
    {
        _entityService = entityService;
    }

    /// <summary>
    /// Retrieve all structured clinical entities extracted from a specific document (FR-EXT-001)
    /// </summary>
    [HttpGet("api/v1/documents/{documentId:guid}/entities")]
    [ProducesResponseType(typeof(List<ClinicalEntityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByDocument([FromRoute] Guid documentId, CancellationToken cancellationToken)
    {
        var entities = await _entityService.GetEntitiesByDocumentAsync(documentId, cancellationToken);
        return Ok(entities);
    }

    /// <summary>
    /// Retrieve all structured oncology entities across the entire record of a patient (FR-EXT-001)
    /// </summary>
    [HttpGet("api/v1/patients/{patientId:guid}/entities")]
    [ProducesResponseType(typeof(List<ClinicalEntityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient([FromRoute] Guid patientId, CancellationToken cancellationToken)
    {
        var entities = await _entityService.GetEntitiesByPatientAsync(patientId, cancellationToken);
        return Ok(entities);
    }

    /// <summary>
    /// Validate and confirm an extracted clinical entity by an oncology clinician (FR-EXT-002)
    /// </summary>
    [HttpPost("api/v1/entities/{id:guid}/verify")]
    [Authorize(Roles = "TreatingClinician,MedicalRecordsSupervisor,ClinicalAiReviewer,HospitalAdmin")]
    [ProducesResponseType(typeof(ClinicalEntityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyEntity(
        [FromRoute] Guid id, 
        [FromBody] VerifyClinicalEntityRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var verified = await _entityService.VerifyEntityAsync(id, request, cancellationToken);
            return Ok(verified);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Manually register a new clinical entity (e.g. physician clinical impression or uncaptured staging)
    /// </summary>
    [HttpPost("api/v1/entities")]
    [Authorize(Roles = "TreatingClinician,MedicalRecordsSupervisor,ClinicalAiReviewer,HospitalAdmin")]
    [ProducesResponseType(typeof(ClinicalEntityDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateManualEntity(
        [FromBody] CreateClinicalEntityRequest request, 
        CancellationToken cancellationToken)
    {
        var entity = await _entityService.CreateManualEntityAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByDocument), new { documentId = entity.DocumentId }, entity);
    }

    /// <summary>
    /// Modify or correct an extracted clinical entity
    /// </summary>
    [HttpPut("api/v1/entities/{id:guid}")]
    [Authorize(Roles = "TreatingClinician,MedicalRecordsSupervisor,ClinicalAiReviewer,HospitalAdmin")]
    [ProducesResponseType(typeof(ClinicalEntityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEntity(
        [FromRoute] Guid id, 
        [FromBody] UpdateClinicalEntityRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _entityService.UpdateEntityAsync(id, request, cancellationToken);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete an erroneous or false-positive clinical entity
    /// </summary>
    [HttpDelete("api/v1/entities/{id:guid}")]
    [Authorize(Roles = "TreatingClinician,MedicalRecordsSupervisor,ClinicalAiReviewer,HospitalAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEntity([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var success = await _entityService.DeleteEntityAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = $"Clinical entity with ID '{id}' was not found." });
        }

        return NoContent();
    }
}
