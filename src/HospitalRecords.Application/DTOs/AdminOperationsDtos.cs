namespace HospitalRecords.Application.DTOs;

public record HospitalTelemetryDto(
    long StorageUsedBytes,
    long StorageTotalBytes,
    int TotalPatientsCount,
    int TotalDocumentsCount,
    int TotalEmbeddingsCount,
    int TotalEncountersCount,
    int OcrQueuePending,
    int OcrQueueProcessing,
    int OcrQueueCompleted,
    int OcrQueueFailed,
    double AverageOcrConfidence,
    int TotalAiQueriesServed,
    double AverageAiLatencyMs,
    int PendingClinicalFlagsCount,
    double SystemUptimeHours
);

public record BulkBatchUploadResponseDto(
    Guid BatchJobId,
    string BatchReference,
    string FileName,
    int TotalPagesScanned,
    int TotalDocumentsExtracted,
    int MatchedPatientsCount,
    int UnassignedCount,
    string Status
);

public record BulkBatchItemDto(
    Guid Id,
    int ItemIndex,
    int PageStart,
    int PageEnd,
    string? DetectedBarcodeMrn,
    Guid? MatchedPatientId,
    string? MatchedPatientName,
    Guid? GeneratedDocumentId,
    string Status,
    string? Notes
);

public record BulkBatchJobDetailsDto(
    Guid BatchJobId,
    string BatchReference,
    string FileName,
    int TotalPagesScanned,
    int TotalDocumentsExtracted,
    int MatchedPatientsCount,
    int UnassignedCount,
    string Status,
    string InitiatedBy,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    List<BulkBatchItemDto> Items
);

public record AuditLogExportFilterRequest(
    DateTime? FromDate,
    DateTime? ToDate,
    string? Action,
    string? Role,
    string? ResourceId
);

public record AuditLogExportResultDto(
    string ExportId,
    DateTime GeneratedAt,
    int TotalRecords,
    string Sha256IntegritySeal,
    string CsvContent
);
