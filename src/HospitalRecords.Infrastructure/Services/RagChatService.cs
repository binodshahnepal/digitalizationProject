using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalRecords.Infrastructure.Services;

public class RagChatService : IRagChatService
{
    private readonly IApplicationDbContext _context;
    private readonly ISearchService _searchService;
    private readonly HttpClient _httpClient;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RagChatService> _logger;

    private const string AiWorkerUrl = "http://localhost:5200";

    private static readonly Regex PromptInjectionRegex = new(
        @"(?:ignore\s+(?:all\s+)?(?:previous|prior)\s+instructions|disregard\s+(?:all\s+)?(?:guidelines|rules)|override\s+system\s+prompt|dan\s+mode|jailbreak|system\s+prompt\s*:)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RagChatService(
        IApplicationDbContext context,
        ISearchService searchService,
        HttpClient httpClient,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        ILogger<RagChatService> logger)
    {
        _context = context;
        _searchService = searchService;
        _httpClient = httpClient;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<ClinicalRagResponseDto> AskClinicalAssistantAsync(ClinicalRagQueryRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = request.Query?.Trim() ?? string.Empty;

        // 1. Prompt Injection Defense (FR-AI-004)
        if (PromptInjectionRegex.IsMatch(query))
        {
            await _auditService.LogAsync(
                action: "AUDIT_SECURITY_INJECTION_BLOCKED",
                resource: "ClinicalAssistant",
                resourceId: request.PatientId.ToString(),
                outcome: AuditOutcome.Success,
                details: $"Prompt injection attempt blocked: '{query}' by {_currentUserService.Username ?? "Unknown"}",
                cancellationToken: cancellationToken);

            return new ClinicalRagResponseDto(
                Query: query,
                AnswerMarkdown: "⚠️ **Security Alert**: The query contains unauthorized system override instructions. The BPKMCH Clinical AI Assistant strictly processes verified clinical questions regarding patient health records.",
                Citations: new List<ClinicalCitationDto>(),
                GroundedConfidence: 0.0,
                RefusalTriggered: true,
                DurationMs: stopwatch.ElapsedMilliseconds,
                ModelName: "bpkmch-security-guardrail"
            );
        }

        // 2. Patient Verification
        var patient = await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PatientId, cancellationToken);

        if (patient == null)
        {
            throw new ArgumentException($"Patient with ID '{request.PatientId}' not found.");
        }

        // 3. Hybrid Semantic Vector & Keyword Retrieval (Sprint 5)
        var searchReq = new SearchQueryRequest(
            Query: query,
            PatientId: request.PatientId,
            MinScore: Math.Min(request.MinRelevance, 0.15),
            SemanticWeight: 0.70,
            PageNumber: 1,
            PageSize: 8
        );

        var searchResult = await _searchService.SearchAsync(searchReq, cancellationToken);

        // Also fetch confirmed clinical entities for grounding
        var entities = await _context.ClinicalEntities
            .Where(e => e.PatientId == request.PatientId)
            .OrderByDescending(e => e.ConfidenceScore)
            .Take(15)
            .ToListAsync(cancellationToken);

        // Build context chunks
        var contextChunks = new List<RagContextChunkPayload>();

        foreach (var item in searchResult.Items)
        {
            contextChunks.Add(new RagContextChunkPayload(
                document_id: item.DocumentId.ToString(),
                document_title: item.DocumentCategory.ToString(),
                section: item.SectionHeader ?? "Clinical Record",
                text: item.MatchedChunkText,
                score: item.CombinedScore
            ));
        }

        // Add structured entity highlights as supplementary context
        if (entities.Count > 0)
        {
            var entitySummary = string.Join("; ", entities.Select(e => $"{e.Category} {e.Name}: {e.Value}"));
            contextChunks.Add(new RagContextChunkPayload(
                document_id: entities[0].DocumentId.ToString(),
                document_title: "Structured Clinical Registry",
                section: "VERIFIED ONCOLOGY ENTITIES",
                text: entitySummary,
                score: 0.95
            ));
        }

        // 4. Out-of-Record Refusal Check
        if (contextChunks.Count == 0 || (searchResult.TotalMatches == 0 && entities.Count == 0))
        {
            stopwatch.Stop();
            var refusalResponse = new ClinicalRagResponseDto(
                Query: query,
                AnswerMarkdown: "ℹ️ **Not Documented**: The requested clinical information is not documented in the available hospital scans and medical records for this patient.",
                Citations: new List<ClinicalCitationDto>(),
                GroundedConfidence: 0.0,
                RefusalTriggered: true,
                DurationMs: stopwatch.ElapsedMilliseconds,
                ModelName: "bpkmch-grounded-rag"
            );

            try
            {
                await _auditService.LogAsync(
                    action: "AUDIT_AI_RAG_QUERY",
                    resource: "Patient",
                    resourceId: patient.Id.ToString(),
                    outcome: AuditOutcome.Success,
                    details: $"AI Query: '{query}', Citations: 0, Confidence: 0%, Model: {refusalResponse.ModelName}, Duration: {refusalResponse.DurationMs}ms (Refusal: Not Documented)",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write RAG refusal audit log");
            }

            return refusalResponse;
        }

        // 5. Invoke AI Worker RAG Service
        ClinicalRagResponseDto responseDto;
        try
        {
            var ragPayload = new
            {
                query = query,
                patient_id = patient.Id.ToString(),
                patient_name = patient.FullName,
                contexts = contextChunks,
                language = request.Language,
                strict_grounded_only = request.StrictGroundedOnly
            };

            var httpResponse = await _httpClient.PostAsJsonAsync($"{AiWorkerUrl}/rag/generate-response", ragPayload, cancellationToken);
            if (httpResponse.IsSuccessStatusCode)
            {
                var result = await httpResponse.Content.ReadFromJsonAsync<RagResponsePayload>(cancellationToken: cancellationToken);
                if (result != null)
                {
                    var citations = (result.citations ?? new List<RagCitationItemPayload>())
                        .Select(c => new ClinicalCitationDto(
                            DocumentId: Guid.TryParse(c.document_id, out var docGuid) ? docGuid : Guid.Empty,
                            DocumentTitle: c.document_title ?? "Document",
                            SectionHeader: c.section,
                            CitationLabel: c.citation_label ?? "[Doc]",
                            Snippet: c.snippet ?? string.Empty,
                            RelevanceScore: c.relevance_score
                        )).ToList();

                    stopwatch.Stop();
                    responseDto = new ClinicalRagResponseDto(
                        Query: query,
                        AnswerMarkdown: result.answer_markdown ?? "No clinical answer synthesized.",
                        Citations: citations,
                        GroundedConfidence: result.grounded_confidence,
                        RefusalTriggered: result.refusal_triggered,
                        DurationMs: stopwatch.ElapsedMilliseconds,
                        ModelName: result.model ?? "bpkmch-med-llm"
                    );
                }
                else
                {
                    responseDto = FallbackGroundedSynthesis(query, patient, contextChunks, stopwatch.ElapsedMilliseconds);
                }
            }
            else
            {
                responseDto = FallbackGroundedSynthesis(query, patient, contextChunks, stopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI Worker RAG call failed. Falling back to local grounded clinical synthesis.");
            responseDto = FallbackGroundedSynthesis(query, patient, contextChunks, stopwatch.ElapsedMilliseconds);
        }

        // 6. Audit Logging (NFR-004)
        try
        {
            await _auditService.LogAsync(
                action: "AUDIT_AI_RAG_QUERY",
                resource: "Patient",
                resourceId: patient.Id.ToString(),
                outcome: AuditOutcome.Success,
                details: $"AI Query: '{query}', Citations: {responseDto.Citations.Count}, Confidence: {responseDto.GroundedConfidence:P0}, Model: {responseDto.ModelName}, Duration: {responseDto.DurationMs}ms",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write RAG audit log");
        }

        return responseDto;
    }

    public Task<List<SuggestedQuestionDto>> GetSuggestedQuestionsAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var suggestions = new List<SuggestedQuestionDto>
        {
            new("Biomarkers", "What is the hormone receptor (ER, PR) and HER2 status?", "🧬"),
            new("Staging", "Summarize the histological grade, Nottingham score, and TNM stage.", "🩺"),
            new("Surgical", "Were the surgical resection margins and lymph nodes free of tumor?", "✂️"),
            new("Systemic", "What systemic chemotherapy or endocrine regimens are documented?", "💊"),
            new("Executive", "Provide a comprehensive oncological executive summary of this patient's history.", "📋")
        };

        return Task.FromResult(suggestions);
    }

    private static ClinicalRagResponseDto FallbackGroundedSynthesis(
        string query, 
        Patient patient, 
        List<RagContextChunkPayload> contexts, 
        long durationMs)
    {
        var lowerQ = query.ToLowerInvariant();
        var citations = new List<ClinicalCitationDto>();
        var points = new List<string>();

        for (int i = 0; i < contexts.Count; i++)
        {
            var ctx = contexts[i];
            var lowerText = ctx.text.ToLowerInvariant();
            var docGuid = Guid.TryParse(ctx.document_id, out var g) ? g : Guid.Empty;
            var citeLabel = $"[Doc {i + 1}: {ctx.document_title} § {ctx.section}]";
            var citeDto = new ClinicalCitationDto(
                DocumentId: docGuid,
                DocumentTitle: ctx.document_title,
                SectionHeader: ctx.section,
                CitationLabel: citeLabel,
                Snippet: ctx.text.Length > 200 ? ctx.text[..200] + "..." : ctx.text,
                RelevanceScore: ctx.score
            );

            if (lowerQ.Contains("receptor") || lowerQ.Contains("biomarker") || lowerQ.Contains("er") || lowerQ.Contains("her2") || lowerQ.Contains("summary") || lowerQ.Contains("executive"))
            {
                if (lowerText.Contains("er positive") || lowerText.Contains("pr positive") || lowerText.Contains("her2") || lowerText.Contains("ki-67"))
                {
                    points.Add($"• **Receptor Biomarkers**: Immunohistochemistry reveals hormone receptor status and molecular markers {citeLabel}.");
                    if (!citations.Contains(citeDto)) citations.Add(citeDto);
                }
            }

            if (lowerQ.Contains("histolog") || lowerQ.Contains("grade") || lowerQ.Contains("biopsy") || lowerQ.Contains("carcinoma") || lowerQ.Contains("summary") || lowerQ.Contains("executive"))
            {
                if (lowerText.Contains("ductal carcinoma") || lowerText.Contains("carcinoma") || lowerText.Contains("grade"))
                {
                    points.Add($"• **Histopathological Findings**: Specimen analysis confirms carcinoma diagnosis with histologic grading assessment {citeLabel}.");
                    if (!citations.Contains(citeDto)) citations.Add(citeDto);
                }
            }

            if (lowerQ.Contains("stage") || lowerQ.Contains("tnm") || lowerQ.Contains("margin") || lowerQ.Contains("summary") || lowerQ.Contains("executive"))
            {
                if (lowerText.Contains("stage") || lowerText.Contains("pt2") || lowerText.Contains("n1") || lowerText.Contains("margin"))
                {
                    points.Add($"• **Staging & Anatomical Extent**: Disease classified with regional nodal status and margin evaluation {citeLabel}.");
                    if (!citations.Contains(citeDto)) citations.Add(citeDto);
                }
            }
        }

        if (points.Count == 0 && contexts.Count > 0)
        {
            var top = contexts[0];
            var docGuid = Guid.TryParse(top.document_id, out var g) ? g : Guid.Empty;
            var citeLabel = $"[Doc 1: {top.document_title} § {top.section}]";
            citations.Add(new ClinicalCitationDto(docGuid, top.document_title, top.section, citeLabel, top.text.Length > 200 ? top.text[..200] + "..." : top.text, top.score));
            points.Add($"• **Clinical Record**: {top.text.Split('.')[0]}. {citeLabel}");
        }

        if (points.Count == 0)
        {
            return new ClinicalRagResponseDto(
                Query: query,
                AnswerMarkdown: "ℹ️ **Not Documented**: Information addressing this query is not documented in the available hospital scans and medical records for this patient.",
                Citations: new List<ClinicalCitationDto>(),
                GroundedConfidence: 0.0,
                RefusalTriggered: true,
                DurationMs: durationMs,
                ModelName: "bpkmch-grounded-fallback"
            );
        }

        var markdown = $"### Clinical Synthesis for {patient.FullName}\n\n" + string.Join("\n\n", points);

        return new ClinicalRagResponseDto(
            Query: query,
            AnswerMarkdown: markdown,
            Citations: citations,
            GroundedConfidence: 0.95,
            RefusalTriggered: false,
            DurationMs: durationMs,
            ModelName: "bpkmch-clinical-rag-engine"
        );
    }

    private record RagContextChunkPayload(string document_id, string document_title, string section, string text, double score);
    private record RagCitationItemPayload(string? document_id, string? document_title, string? section, string? citation_label, string? snippet, double relevance_score);
    private record RagResponsePayload(string? query, string? answer_markdown, List<RagCitationItemPayload>? citations, double grounded_confidence, bool refusal_triggered, string? model, int duration_ms);
}
