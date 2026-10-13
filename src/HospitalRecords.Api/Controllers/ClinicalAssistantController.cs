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
    private readonly IClinicalSafetyBenchmarkService _safetyBenchmarkService;

    public ClinicalAssistantController(
        IRagChatService ragChatService,
        IClinicalSafetyBenchmarkService safetyBenchmarkService)
    {
        _ragChatService = ragChatService;
        _safetyBenchmarkService = safetyBenchmarkService;
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

    /// <summary>
    /// Clinician flags an AI assistant response for supervisory review (FR-AI-008, Section 6.5)
    /// </summary>
    [HttpPost("api/v1/patients/{patientId:guid}/ai-assistant/feedback")]
    [ProducesResponseType(typeof(ClinicalFeedbackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitFeedback(
        [FromRoute] Guid patientId,
        [FromBody] SubmitClinicalFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _safetyBenchmarkService.SubmitFeedbackAsync(request with { PatientId = patientId }, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve clinical AI feedbacks (optionally filtered by patient)
    /// </summary>
    [HttpGet("api/v1/ai-assistant/feedback")]
    [ProducesResponseType(typeof(List<ClinicalFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeedbacks(
        [FromQuery] Guid? patientId,
        CancellationToken cancellationToken)
    {
        var results = await _safetyBenchmarkService.GetFeedbackListAsync(patientId, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Clinical supervisor resolves or updates flagged feedback status
    /// </summary>
    [HttpPut("api/v1/ai-assistant/feedback/{feedbackId:guid}/status")]
    [Authorize(Roles = "MedicalRecordsSupervisor,Administrator")]
    [ProducesResponseType(typeof(ClinicalFeedbackDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFeedbackStatus(
        [FromRoute] Guid feedbackId,
        [FromBody] UpdateFeedbackStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _safetyBenchmarkService.UpdateFeedbackStatusAsync(feedbackId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Run automated Clinical Safety Benchmark and adversarial test suite (Phase 2 Pilot Gate, Section 6.5)
    /// </summary>
    [HttpGet("api/v1/ai-assistant/safety-benchmark")]
    [Authorize(Roles = "MedicalRecordsSupervisor,Administrator")]
    [ProducesResponseType(typeof(ClinicalSafetyBenchmarkReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunSafetyBenchmark(CancellationToken cancellationToken)
    {
        var report = await _safetyBenchmarkService.RunSafetyBenchmarkAsync(cancellationToken);
        return Ok(report);
    }
}

public record AskAssistantRequest(
    string Query,
    string? Language = "en",
    double? MinRelevance = 0.20,
    bool? StrictGroundedOnly = true
);
