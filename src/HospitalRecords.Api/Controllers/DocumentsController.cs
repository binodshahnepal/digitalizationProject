using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Application.Services;
using HospitalRecords.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalRecords.Api.Controllers;

[ApiController]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IOcrProcessingService _ocrService;

    public DocumentsController(
        IDocumentService documentService,
        IOcrProcessingService ocrService)
    {
        _documentService = documentService;
        _ocrService = ocrService;
    }

    /// <summary>
    /// Upload one or more documents/scans for a patient or encounter (FR-DOC-001, FR-DOC-002, FR-DOC-003)
    /// </summary>
    [HttpPost("api/v1/patients/{patientId:guid}/documents/upload")]
    [Authorize(Roles = "RegistrationClerk,MedicalRecordsSupervisor,HospitalAdmin,TreatingClinician,Nurse")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB total batch limit
    [ProducesResponseType(typeof(DocumentUploadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadDocuments(
        [FromRoute] Guid patientId,
        [FromForm] Guid? encounterId,
        [FromForm] Guid? clinicalEventId,
        [FromForm] DocumentCategory category,
        [FromForm] DateOnly clinicalDate,
        [FromForm] string? language,
        [FromForm] ConfidentialityLevel confidentialityLevel,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { message = "At least one file must be provided for upload." });
        }

        var fileDtos = new List<UploadDocumentFileDto>();
        foreach (var file in files)
        {
            fileDtos.Add(new UploadDocumentFileDto(
                ContentStream: file.OpenReadStream(),
                FileName: file.FileName,
                FileSize: file.Length
            ));
        }

        var request = new UploadDocumentsRequest(
            EncounterId: encounterId,
            ClinicalEventId: clinicalEventId,
            Category: category,
            ClinicalDate: clinicalDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : clinicalDate,
            Language: language ?? "en",
            ConfidentialityLevel: confidentialityLevel,
            Files: fileDtos
        );

        var result = await _documentService.UploadDocumentsAsync(patientId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Upload a corrected/rescan version of an existing document without mutating the original (FR-DOC-004)
    /// </summary>
    [HttpPost("api/v1/documents/{id:guid}/amend")]
    [Authorize(Roles = "MedicalRecordsSupervisor,HospitalAdmin,RegistrationClerk")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AmendDocument(
        [FromRoute] Guid id,
        [FromForm] IFormFile file,
        [FromForm] string reasonForAmendment,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "A valid replacement document file is required." });
        }

        if (string.IsNullOrWhiteSpace(reasonForAmendment))
        {
            return BadRequest(new { message = "Reason for amendment is mandatory under FR-DOC-004." });
        }

        var request = new AmendDocumentRequest(
            ContentStream: file.OpenReadStream(),
            FileName: file.FileName,
            FileSize: file.Length,
            ReasonForAmendment: reasonForAmendment
        );

        var updated = await _documentService.AmendDocumentAsync(id, request, cancellationToken);
        if (updated == null)
        {
            return NotFound(new { message = $"Document with ID '{id}' was not found." });
        }

        return Ok(updated);
    }

    /// <summary>
    /// Retrieve all documents for an encounter
    /// </summary>
    [HttpGet("api/v1/encounters/{encounterId:guid}/documents")]
    [ProducesResponseType(typeof(List<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEncounter([FromRoute] Guid encounterId, CancellationToken cancellationToken)
    {
        var docs = await _documentService.GetDocumentsByEncounterAsync(encounterId, cancellationToken);
        return Ok(docs);
    }

    /// <summary>
    /// Retrieve all documents for a patient chronologically (FR-PAT-005)
    /// </summary>
    [HttpGet("api/v1/patients/{patientId:guid}/documents")]
    [ProducesResponseType(typeof(List<DocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatient([FromRoute] Guid patientId, CancellationToken cancellationToken)
    {
        var docs = await _documentService.GetDocumentsByPatientAsync(patientId, cancellationToken);
        return Ok(docs);
    }

    /// <summary>
    /// Get document metadata and version history by ID
    /// </summary>
    [HttpGet("api/v1/documents/{id:guid}")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var doc = await _documentService.GetByIdAsync(id, cancellationToken);
        if (doc == null)
        {
            return NotFound(new { message = $"Document with ID '{id}' was not found." });
        }

        return Ok(doc);
    }

    /// <summary>
    /// Stream or download binary document asset with access control audit (FR-DOC-006, NFR-004)
    /// </summary>
    [HttpGet("api/v1/documents/{id:guid}/download")]
    [HttpGet("api/v1/documents/{id:guid}/view")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(
        [FromRoute] Guid id, 
        [FromQuery] int? versionNumber, 
        [FromQuery] bool inline = false,
        CancellationToken cancellationToken = default)
    {
        var fileResult = await _documentService.DownloadFileAsync(id, versionNumber, cancellationToken);
        if (fileResult == null || fileResult.Value.Stream == null)
        {
            return NotFound(new { message = "Document file asset not found." });
        }

        bool isInline = inline || Request.Path.Value?.Contains("/view", StringComparison.OrdinalIgnoreCase) == true;
        if (isInline)
        {
            Response.Headers.Append("Content-Disposition", $"inline; filename=\"{fileResult.Value.FileName}\"");
            return File(fileResult.Value.Stream, fileResult.Value.ContentType);
        }

        return File(fileResult.Value.Stream, fileResult.Value.ContentType, fileResult.Value.FileName);
    }

    /// <summary>
    /// Delete a medical record document and its associated versions and OCR results (FR-DOC-006, NFR-004)
    /// </summary>
    [HttpDelete("api/v1/documents/{id:guid}")]
    [Authorize(Roles = "RegistrationClerk,MedicalRecordsSupervisor,HospitalAdmin,ITAdmin,TreatingClinician,Nurse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(
        [FromRoute] Guid id,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var success = await _documentService.DeleteDocumentAsync(id, reason, cancellationToken);
        if (!success)
        {
            return NotFound(new { message = $"Document with ID '{id}' was not found or already deleted." });
        }

        return Ok(new { message = "Document and associated versions successfully deleted.", documentId = id });
    }

    /// <summary>
    /// Retrieve list of all documents flagged for human-in-the-loop review (FR-OCR-003, FR-VIS-003)
    /// </summary>
    [HttpGet("api/v1/documents/review-queue")]
    [Authorize(Roles = "MedicalRecordsSupervisor,HospitalAdmin,TreatingClinician,RegistrationClerk")]
    [ProducesResponseType(typeof(List<DocumentReviewQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviewQueue(CancellationToken cancellationToken)
    {
        var items = await _ocrService.GetReviewQueueAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Retrieve latest OCR and classification result for a document
    /// </summary>
    [HttpGet("api/v1/documents/{id:guid}/ocr")]
    [ProducesResponseType(typeof(DocumentOcrResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOcrResult([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var ocr = await _ocrService.GetOcrResultAsync(id, cancellationToken);
        if (ocr == null)
        {
            return NotFound(new { message = $"No OCR extraction recorded for document '{id}'." });
        }
        return Ok(ocr);
    }

    /// <summary>
    /// Human-in-the-loop verification and confirmation of document classification and OCR (FR-OCR-003)
    /// </summary>
    [HttpPost("api/v1/documents/{id:guid}/verify-ocr")]
    [Authorize(Roles = "MedicalRecordsSupervisor,HospitalAdmin,TreatingClinician")]
    [ProducesResponseType(typeof(DocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyOcr(
        [FromRoute] Guid id,
        [FromBody] VerifyOcrRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var verifiedDoc = await _ocrService.VerifyOcrAsync(id, request, cancellationToken);
            return Ok(verifiedDoc);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Manually trigger or reprocess OCR and classification pipeline
    /// </summary>
    [HttpPost("api/v1/documents/{id:guid}/reprocess-ocr")]
    [Authorize(Roles = "MedicalRecordsSupervisor,HospitalAdmin,TreatingClinician,RegistrationClerk")]
    [ProducesResponseType(typeof(DocumentOcrResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReprocessOcr(
        [FromRoute] Guid id,
        [FromQuery] int? versionNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ocrService.ProcessDocumentAsync(id, versionNumber, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
