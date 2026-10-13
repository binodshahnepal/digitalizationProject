using HospitalRecords.Application.DTOs;

namespace HospitalRecords.Application.Common.Interfaces;

public interface IAdminOperationsService
{
    Task<HospitalTelemetryDto> GetTelemetryAsync(CancellationToken cancellationToken = default);
    Task<BulkBatchUploadResponseDto> ProcessBulkBatchUploadAsync(Stream fileStream, string fileName, long fileSize, CancellationToken cancellationToken = default);
    Task<List<BulkBatchJobDetailsDto>> GetBulkBatchJobsAsync(CancellationToken cancellationToken = default);
    Task<BulkBatchJobDetailsDto> GetBulkBatchJobDetailsAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<AuditLogExportResultDto> ExportAuditLogsAsync(AuditLogExportFilterRequest filter, CancellationToken cancellationToken = default);
}

public interface IFhirBridgeService
{
    Task<FhirPatientResource> GetPatientAsFhirAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<FhirBundleResponse<FhirDocumentReferenceResource>> GetPatientDocumentsAsFhirAsync(Guid patientId, CancellationToken cancellationToken = default);
}
