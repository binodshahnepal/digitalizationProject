using HospitalRecords.Domain.Common;
using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Domain.Entities;

public class Document : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>
    /// Associated encounter if verified. Nullable for unassigned historical charts (FR-VIS-003).
    /// </summary>
    public Guid? EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public Guid? ClinicalEventId { get; set; }
    public ClinicalEvent? ClinicalEvent { get; set; }

    public DocumentCategory Category { get; set; } = DocumentCategory.GeneralMedicalChart;

    /// <summary>
    /// Actual date of the medical report or event, distinct from system upload time (FR-DOC-003, 5.2)
    /// </summary>
    public DateOnly ClinicalDate { get; set; }

    public string Language { get; set; } = "en"; // "en", "ne", "mixed"
    public ConfidentialityLevel ConfidentialityLevel { get; set; } = ConfidentialityLevel.Normal;
    public DocumentStatus Status { get; set; } = DocumentStatus.Received;

    public int CurrentVersionNumber { get; set; } = 1;

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<DocumentOcrResult> OcrResults { get; set; } = new List<DocumentOcrResult>();
    public ICollection<ClinicalEntity> ClinicalEntities { get; set; } = new List<ClinicalEntity>();
    public ICollection<DocumentEmbedding> Embeddings { get; set; } = new List<DocumentEmbedding>();
}

public class DocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// Cryptographic SHA-256 hash for deduplication and tamper-evidence (FR-DOC-002, FR-DOC-004)
    /// </summary>
    public string Sha256Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Path/key in encrypted object storage (e.g., records-raw/{patientId}/{documentId}/v1.pdf)
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
    public long FileSize { get; set; }
    public int? PageCount { get; set; }

    public string CreationActor { get; set; } = "System";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Mandatory reason why this version was uploaded/amended (FR-DOC-004)
    /// </summary>
    public string ReasonForVersion { get; set; } = "Initial ingestion";
}

public class DocumentOcrResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public Guid DocumentVersionId { get; set; }
    public DocumentVersion DocumentVersion { get; set; } = null!;

    public string ExtractedText { get; set; } = string.Empty;
    public string? ExtractedNepaliText { get; set; }
    public double OcrConfidenceScore { get; set; } = 1.0;
    public string LanguageDetected { get; set; } = "en"; // "en", "ne", "mixed"
    public int PageCount { get; set; } = 1;
    public int WordCount { get; set; }

    public DocumentCategory PredictedCategory { get; set; } = DocumentCategory.GeneralMedicalChart;
    public double ClassificationConfidence { get; set; } = 1.0;
    public string KeywordsDetected { get; set; } = "[]"; // JSON array of matched keywords

    /// <summary>
    /// Flagged true if OCR confidence < 0.70 or classification confidence < 0.80 (FR-OCR-002, FR-CLS-001)
    /// </summary>
    public bool LowConfidenceWarning { get; set; }

    /// <summary>
    /// Human-in-the-loop verification details (FR-OCR-003)
    /// </summary>
    public bool IsVerifiedByHuman { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    public long ProcessingDurationMs { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
