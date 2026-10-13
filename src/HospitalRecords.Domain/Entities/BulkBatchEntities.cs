namespace HospitalRecords.Domain.Entities;

public enum BulkBatchStatus
{
    Uploaded = 1,
    Processing = 2,
    Completed = 3,
    CompletedWithWarnings = 4,
    Failed = 5
}

public enum BulkBatchItemStatus
{
    Pending = 1,
    Matched = 2,
    Unassigned = 3,
    DuplicateDetected = 4,
    Failed = 5
}

public class BulkBatchJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BatchReference { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int TotalPagesScanned { get; set; }
    public int TotalDocumentsExtracted { get; set; }
    public int MatchedPatientsCount { get; set; }
    public int UnassignedDocumentsCount { get; set; }
    public BulkBatchStatus Status { get; set; } = BulkBatchStatus.Uploaded;
    public string? ErrorSummary { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ICollection<BulkBatchItem> Items { get; set; } = new List<BulkBatchItem>();
}

public class BulkBatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BulkBatchJobId { get; set; }
    public int ItemIndex { get; set; }
    public int PageStart { get; set; }
    public int PageEnd { get; set; }
    public string? DetectedBarcodeMrn { get; set; }
    public Guid? MatchedPatientId { get; set; }
    public Guid? GeneratedDocumentId { get; set; }
    public BulkBatchItemStatus Status { get; set; } = BulkBatchItemStatus.Pending;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public BulkBatchJob BulkBatchJob { get; set; } = null!;
    public Patient? MatchedPatient { get; set; }
    public Document? GeneratedDocument { get; set; }
}
