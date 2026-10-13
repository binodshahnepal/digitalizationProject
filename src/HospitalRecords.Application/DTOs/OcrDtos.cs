using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Application.DTOs;

public record DocumentOcrResultDto(
    Guid Id,
    Guid DocumentId,
    Guid DocumentVersionId,
    string ExtractedText,
    string? ExtractedNepaliText,
    double OcrConfidenceScore,
    string LanguageDetected,
    int PageCount,
    int WordCount,
    DocumentCategory PredictedCategory,
    double ClassificationConfidence,
    List<string> KeywordsDetected,
    bool LowConfidenceWarning,
    bool IsVerifiedByHuman,
    string? VerifiedBy,
    DateTimeOffset? VerifiedAt,
    long ProcessingDurationMs,
    DateTimeOffset ProcessedAt
);

public record VerifyOcrRequest(
    DocumentCategory ConfirmedCategory,
    string? CorrectedText,
    string? VerificationNotes
);

public record DocumentReviewQueueItemDto(
    Guid DocumentId,
    Guid PatientId,
    string PatientMrn,
    string PatientFullName,
    Guid? EncounterId,
    string? EncounterNumber,
    string OriginalFileName,
    int VersionNumber,
    DocumentCategory CurrentCategory,
    DocumentCategory PredictedCategory,
    double OcrConfidenceScore,
    double ClassificationConfidence,
    string LanguageDetected,
    bool LowConfidenceWarning,
    string ReasonForReview,
    DateTimeOffset UploadedAt,
    string TextPreview
);
