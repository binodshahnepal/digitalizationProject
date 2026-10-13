using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Authorize]
public class ClinicalAssistantController : ControllerBase
{
    private readonly IRagChatService _ragChatService;

    public ClinicalAssistantController(IRagChatService ragChatService)
    {
        _ragChatService = ragChatService;
    }

    /// <summary>
    /// Ask clinical questions grounded strictly on patient's scanned records with mandatory citations (FR-AI-001, FR-AI-002)
    /// </summary>
    [HttpPost("api/v1/patients/{patientId:guid}/ai-assistant/ask")]
    [ProducesResponseType(typeof(ClinicalRagResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AskClinicalAssistant(
        [FromRoute] Guid patientId,
        [FromBody] AskAssistantRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { message = "Query cannot be empty." });
        }

        var ragRequest = new ClinicalRagQueryRequest(
            PatientId: patientId,
            Query: request.Query,
            Language: request.Language ?? "en",
            MinRelevance: request.MinRelevance ?? 0.20,
            StrictGroundedOnly: request.StrictGroundedOnly ?? true
        );

        var response = await _ragChatService.AskClinicalAssistantAsync(ragRequest, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Retrieve tailored clinical prompt suggestions for a patient (FR-AI-003)
    /// </summary>
    [HttpGet("api/v1/patients/{patientId:guid}/ai-assistant/suggested-questions")]
    [ProducesResponseType(typeof(List<SuggestedQuestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuggestedQuestions(
        [FromRoute] Guid patientId,
        CancellationToken cancellationToken)
    {
        var questions = await _ragChatService.GetSuggestedQuestionsAsync(patientId, cancellationToken);
        return Ok(questions);
    }
}

public record AskAssistantRequest(
    string Query,
    string? Language = "en",
    double? MinRelevance = 0.20,
    bool? StrictGroundedOnly = true
);
