using HospitalRecords.Application.DTOs;

namespace HospitalRecords.Application.Common.Interfaces;

public interface IOcrProcessingService
{
    /// <summary>
    /// Processes OCR and auto-classification for a specific document version.
    /// Updates Document status to Indexed (if confident) or RequiresReview (if low confidence).
    /// </summary>
    Task<DocumentOcrResultDto> ProcessDocumentAsync(Guid documentId, int? versionNumber = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Human-in-the-loop verification workflow: supervisor/clinician reviews and confirms category & OCR text.
    /// Transitions document status to Indexed.
    /// </summary>
    Task<DocumentDto> VerifyOcrAsync(Guid documentId, VerifyOcrRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all documents flagged with RequiresReview or pending verification in the supervisor queue.
    /// </summary>
    Task<List<DocumentReviewQueueItemDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest OCR and classification result for a document.
    /// </summary>
    Task<DocumentOcrResultDto?> GetOcrResultAsync(Guid documentId, CancellationToken cancellationToken = default);
}
