using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Executes hybrid clinical semantic vector and keyword search across hospital records (FR-SEARCH-001)
    /// </summary>
    [HttpPost("api/v1/search/hybrid")]
    [ProducesResponseType(typeof(HybridSearchResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> HybridSearch([FromBody] SearchQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await _searchService.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Search scoped to a specific patient's medical records (FR-SEARCH-002)
    /// </summary>
    [HttpGet("api/v1/patients/{patientId:guid}/search")]
    [ProducesResponseType(typeof(HybridSearchResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> PatientScopedSearch(
        [FromRoute] Guid patientId,
        [FromQuery] string q,
        [FromQuery] DocumentCategory? category = null,
        [FromQuery] double minScore = 0.20,
        [FromQuery] double semanticWeight = 0.60,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 15,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchQueryRequest(
            Query: q,
            PatientId: patientId,
            Category: category,
            MinScore: minScore,
            SemanticWeight: semanticWeight,
            PageNumber: pageNumber,
            PageSize: pageSize
        );

        var result = await _searchService.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Generate and index dense semantic embeddings for a specific document
    /// </summary>
    [HttpPost("api/v1/search/documents/{documentId:guid}/index")]
    [Authorize(Roles = "MedicalRecordsSupervisor,ScanningTechnician,HospitalAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> IndexDocument([FromRoute] Guid documentId, CancellationToken cancellationToken)
    {
        int chunksIndexed = await _searchService.IndexDocumentEmbeddingsAsync(documentId, cancellationToken);
        return Ok(new { documentId, chunksIndexed, status = "Indexed" });
    }

    /// <summary>
    /// Reindex dense embeddings across all hospital documents
    /// </summary>
    [HttpPost("api/v1/search/reindex-all")]
    [Authorize(Roles = "HospitalAdmin,MedicalRecordsSupervisor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReindexAll(CancellationToken cancellationToken)
    {
        int totalChunks = await _searchService.ReindexAllDocumentsAsync(cancellationToken);
        return Ok(new { totalChunksIndexed = totalChunks, status = "Completed" });
    }
}
