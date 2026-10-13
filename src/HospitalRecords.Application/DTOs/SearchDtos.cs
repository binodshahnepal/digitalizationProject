using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Application.DTOs;

public record SearchQueryRequest(
    string Query,
    Guid? PatientId = null,
    DocumentCategory? Category = null,
    double MinScore = 0.20,
    double SemanticWeight = 0.60,
    int PageNumber = 1,
    int PageSize = 15
);

public record SearchResultItemDto(
    Guid DocumentId,
    Guid PatientId,
    string PatientName,
    string PatientMrn,
    DocumentCategory DocumentCategory,
    DateOnly ClinicalDate,
    string MatchedChunkText,
    string? SectionHeader,
    double CombinedScore,
    double SemanticScore,
    double KeywordScore,
    List<ClinicalEntityDto> MatchedEntities,
    string HighlightSnippet
);

public record HybridSearchResultDto(
    string Query,
    int TotalMatches,
    long SearchDurationMs,
    List<SearchResultItemDto> Items,
    int PageNumber,
    int PageSize
);
