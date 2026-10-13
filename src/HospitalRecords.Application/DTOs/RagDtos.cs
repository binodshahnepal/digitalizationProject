namespace HospitalRecords.Application.DTOs;

public record ClinicalRagQueryRequest(
    Guid PatientId,
    string Query,
    string Language = "en",
    double MinRelevance = 0.20,
    bool StrictGroundedOnly = true
);

public record ClinicalCitationDto(
    Guid DocumentId,
    string DocumentTitle,
    string? SectionHeader,
    string CitationLabel,
    string Snippet,
    double RelevanceScore
);

public record ClinicalRagResponseDto(
    string Query,
    string AnswerMarkdown,
    List<ClinicalCitationDto> Citations,
    double GroundedConfidence,
    bool RefusalTriggered,
    long DurationMs,
    string ModelName
);

public record SuggestedQuestionDto(
    string Category,
    string QuestionText,
    string Icon
);
