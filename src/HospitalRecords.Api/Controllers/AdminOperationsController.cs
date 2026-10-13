using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public class AdminOperationsController : ControllerBase
{
    private readonly IAdminOperationsService _adminService;
    private readonly IClinicalSafetyBenchmarkService _safetyBenchmarkService;

    public AdminOperationsController(
        IAdminOperationsService adminService,
        IClinicalSafetyBenchmarkService safetyBenchmarkService)
    {
        _adminService = adminService;
        _safetyBenchmarkService = safetyBenchmarkService;
    }

    /// <summary>
    /// Live hospital system telemetry and operational queue status (FR-ADM-002, FR-ADM-003)
    /// </summary>
    [HttpGet("telemetry")]
    [ProducesResponseType(typeof(HospitalTelemetryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTelemetry(CancellationToken cancellationToken)
    {
        var telemetry = await _adminService.GetTelemetryAsync(cancellationToken);
        return Ok(telemetry);
    }

    /// <summary>
    /// Bulk historical chart migration: automated batch upload and barcode splitting (FR-DOC-007, AT-12)
    /// </summary>
    [HttpPost("bulk-batch/upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(BulkBatchUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadBulkBatch(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Batch file is required." });
        }

        await using var stream = file.OpenReadStream();
        var result = await _adminService.ProcessBulkBatchUploadAsync(
            stream, 
            file.FileName, 
            file.Length, 
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// List all archival bulk migration batch jobs
    /// </summary>
    [HttpGet("bulk-batch/jobs")]
    [ProducesResponseType(typeof(List<BulkBatchJobDetailsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBulkBatchJobs(CancellationToken cancellationToken)
    {
        var jobs = await _adminService.GetBulkBatchJobsAsync(cancellationToken);
        return Ok(jobs);
    }

    /// <summary>
    /// Retrieve details and extracted items of a specific batch job
    /// </summary>
    [HttpGet("bulk-batch/jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(BulkBatchJobDetailsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBulkBatchJobDetails(
        [FromRoute] Guid jobId,
        CancellationToken cancellationToken)
    {
        var details = await _adminService.GetBulkBatchJobDetailsAsync(jobId, cancellationToken);
        return Ok(details);
    }

    /// <summary>
    /// Export compliance audit logs with SHA-256 digital integrity seal (FR-ADM-004)
    /// </summary>
    [HttpPost("audit-logs/export")]
    [ProducesResponseType(typeof(AuditLogExportResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLogs(
        [FromBody] AuditLogExportFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var result = await _adminService.ExportAuditLogsAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Automated disaster recovery cold-restore rehearsal drill (AT-10, NFR-008)
    /// </summary>
    [HttpPost("backup/rehearse-restore")]
    [Authorize(Roles = "Administrator,MedicalRecordsSupervisor")]
    [ProducesResponseType(typeof(DisasterRecoveryDrillResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RehearseDisasterRecovery(CancellationToken cancellationToken)
    {
        var result = await _safetyBenchmarkService.RehearseDisasterRecoveryDrillAsync(cancellationToken);
        return Ok(result);
    }
}
