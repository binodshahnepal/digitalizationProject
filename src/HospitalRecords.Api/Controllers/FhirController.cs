using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Route("fhir")]
[Authorize]
public class FhirController : ControllerBase
{
    private readonly IFhirBridgeService _fhirService;

    public FhirController(IFhirBridgeService fhirService)
    {
        _fhirService = fhirService;
    }

    /// <summary>
    /// Retrieve HL7 FHIR R4 Patient representation (INT-003)
    /// </summary>
    [HttpGet("Patient/{patientId:guid}")]
    [Produces("application/fhir+json", "application/json")]
    [ProducesResponseType(typeof(FhirPatientResource), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientFhir(
        [FromRoute] Guid patientId,
        CancellationToken cancellationToken)
    {
        var resource = await _fhirService.GetPatientAsFhirAsync(patientId, cancellationToken);
        return Ok(resource);
    }

    /// <summary>
    /// Retrieve HL7 FHIR R4 DocumentReference bundle for patient documents (INT-004)
    /// </summary>
    [HttpGet("DocumentReference")]
    [Produces("application/fhir+json", "application/json")]
    [ProducesResponseType(typeof(FhirBundleResponse<FhirDocumentReferenceResource>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDocumentReferences(
        [FromQuery] Guid patient,
        CancellationToken cancellationToken)
    {
        if (patient == Guid.Empty)
        {
            return BadRequest(new { message = "Patient parameter is required." });
        }

        var bundle = await _fhirService.GetPatientDocumentsAsFhirAsync(patient, cancellationToken);
        return Ok(bundle);
    }
}
