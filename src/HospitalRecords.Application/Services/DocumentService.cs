using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalRecords.Application.Services;

public interface IDocumentService
{
    Task<DocumentUploadResult> UploadDocumentsAsync(Guid patientId, UploadDocumentsRequest request, CancellationToken cancellationToken = default);
    Task<DocumentDto?> AmendDocumentAsync(Guid documentId, AmendDocumentRequest request, CancellationToken cancellationToken = default);
    Task<List<DocumentDto>> GetDocumentsByEncounterAsync(Guid encounterId, CancellationToken cancellationToken = default);
    Task<List<DocumentDto>> GetDocumentsByPatientAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(Stream? Stream, string ContentType, string FileName)?> DownloadFileAsync(Guid documentId, int? versionNumber = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, string? deletionReason = null, CancellationToken cancellationToken = default);
}

public class DocumentService : IDocumentService
{
    private readonly IApplicationDbContext _context;
    private readonly IObjectStorageService _storageService;
    private readonly IFileSecurityValidator _fileValidator;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IOcrProcessingService _ocrService;

    public DocumentService(
        IApplicationDbContext context,
        IObjectStorageService storageService,
        IFileSecurityValidator fileValidator,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IOcrProcessingService ocrService)
    {
        _context = context;
        _storageService = storageService;
        _fileValidator = fileValidator;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _ocrService = ocrService;
    }

    public async Task<DocumentUploadResult> UploadDocumentsAsync(Guid patientId, UploadDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        var patient = await _context.Patients
            .FirstOrDefaultAsync(p => p.Id == patientId, cancellationToken);

        if (patient == null)
        {
            throw new ArgumentException($"Patient with ID '{patientId}' not found.");
        }

        // Verify encounter if specified
        Encounter? encounter = null;
        if (request.EncounterId.HasValue)
        {
            encounter = await _context.Encounters
                .FirstOrDefaultAsync(e => e.Id == request.EncounterId.Value && e.PatientId == patientId, cancellationToken);

            if (encounter == null)
            {
                throw new ArgumentException($"Encounter with ID '{request.EncounterId}' does not belong to patient {patient.Mrn}.");
            }
        }

        var createdDocuments = new List<DocumentDto>();
        var errors = new List<string>();

        foreach (var fileDto in request.Files)
        {
            // 1. File Security & Magic Byte Validation (FR-DOC-002)
            var validation = _fileValidator.Validate(fileDto.ContentStream, fileDto.FileName, fileDto.FileSize);
            if (!validation.IsValid)
            {
                var errorMsg = $"File '{fileDto.FileName}' failed security validation: {validation.ErrorMessage}";
                errors.Add(errorMsg);

                await _auditService.LogAsync(
                    action: "DOCUMENT_UPLOAD_REJECTED",
                    resource: "Document",
                    resourceId: fileDto.FileName,
                    outcome: AuditOutcome.Denied,
                    details: errorMsg,
                    cancellationToken: cancellationToken);

                continue;
            }

            // 2. In-Encounter Duplicate Check via SHA-256 Checksum (FR-DOC-002)
            if (request.EncounterId.HasValue)
            {
                var duplicateExists = await _context.DocumentVersions
                    .Include(dv => dv.Document)
                    .AnyAsync(dv => dv.Document.EncounterId == request.EncounterId.Value &&
                                    dv.Sha256Checksum == validation.Sha256Checksum, cancellationToken);

                if (duplicateExists)
                {
                    var dupMsg = $"Duplicate scan detected: File '{fileDto.FileName}' matches SHA-256 hash of an existing document in this encounter.";
                    errors.Add(dupMsg);

                    await _auditService.LogAsync(
                        action: "DOCUMENT_UPLOAD_DUPLICATE_REJECTED",
                        resource: "Document",
                        resourceId: validation.Sha256Checksum,
                        outcome: AuditOutcome.Denied,
                        details: dupMsg,
                        cancellationToken: cancellationToken);

                    continue;
                }
            }

            // 3. Create Document Entity with initial version (FR-DOC-003, FR-DOC-004)
            var documentId = Guid.NewGuid();
            var sanitizedFileName = Path.GetFileName(fileDto.FileName);
            var storageKey = $"records-raw/{patientId}/{documentId}/v1_{sanitizedFileName}";

            // Upload to encrypted immutable storage
            await _storageService.UploadAsync(
                fileDto.ContentStream, 
                storageKey, 
                validation.DetectedContentType!, 
                cancellationToken);

            var initialStatus = request.EncounterId.HasValue 
                ? DocumentStatus.OcrQueued 
                : DocumentStatus.RequiresReview; // Unassigned historical charts require supervisor review (FR-VIS-003)

            var document = new Document
            {
                Id = documentId,
                PatientId = patientId,
                EncounterId = request.EncounterId,
                ClinicalEventId = request.ClinicalEventId,
                Category = request.Category,
                ClinicalDate = request.ClinicalDate,
                Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim(),
                ConfidentialityLevel = request.ConfidentialityLevel,
                Status = initialStatus,
                CurrentVersionNumber = 1,
                CreatedBy = _currentUserService.Username ?? "System",
                CreatedAt = DateTimeOffset.UtcNow
            };

            var version = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                VersionNumber = 1,
                Sha256Checksum = validation.Sha256Checksum!,
                StorageKey = storageKey,
                OriginalFileName = sanitizedFileName,
                ContentType = validation.DetectedContentType!,
                FileSize = fileDto.FileSize,
                CreationActor = _currentUserService.Username ?? "System",
                CreatedAt = DateTimeOffset.UtcNow,
                ReasonForVersion = "Initial upload"
            };

            document.Versions.Add(version);
            _context.Documents.Add(document);

            await _context.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(
                action: "DOCUMENT_UPLOADED",
                resource: "Document",
                resourceId: document.Id.ToString(),
                outcome: AuditOutcome.Success,
                details: $"Uploaded '{sanitizedFileName}' (SHA256: {version.Sha256Checksum}) for patient {patient.Mrn}. Status: {document.Status}",
                cancellationToken: cancellationToken);

            // Automatically queue and run OCR & classification pipeline (Sprint 3)
            DocumentOcrResultDto? ocrDto = null;
            try
            {
                ocrDto = await _ocrService.ProcessDocumentAsync(document.Id, 1, cancellationToken);
            }
            catch
            {
                // OCR pipeline is non-blocking; errors are captured in review queue
            }

            createdDocuments.Add(MapToDto(document, version, ocrDto));
        }

        return new DocumentUploadResult(
            Success: createdDocuments.Count > 0,
            CreatedDocuments: createdDocuments,
            Errors: errors
        );
    }

    public async Task<DocumentDto?> AmendDocumentAsync(Guid documentId, AmendDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null) return null;

        var validation = _fileValidator.Validate(request.ContentStream, request.FileName, request.FileSize);
        if (!validation.IsValid)
        {
            throw new ArgumentException($"Amendment file validation failed: {validation.ErrorMessage}");
        }

        var nextVersionNumber = document.CurrentVersionNumber + 1;
        var sanitizedFileName = Path.GetFileName(request.FileName);
        var storageKey = $"records-raw/{document.PatientId}/{document.Id}/v{nextVersionNumber}_{sanitizedFileName}";

        await _storageService.UploadAsync(
            request.ContentStream, 
            storageKey, 
            validation.DetectedContentType!, 
            cancellationToken);

        var newVersion = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = nextVersionNumber,
            Sha256Checksum = validation.Sha256Checksum!,
            StorageKey = storageKey,
            OriginalFileName = sanitizedFileName,
            ContentType = validation.DetectedContentType!,
            FileSize = request.FileSize,
            CreationActor = _currentUserService.Username ?? "System",
            CreatedAt = DateTimeOffset.UtcNow,
            ReasonForVersion = string.IsNullOrWhiteSpace(request.ReasonForAmendment) ? "Amended document version" : request.ReasonForAmendment.Trim()
        };

        document.CurrentVersionNumber = nextVersionNumber;
        document.Status = DocumentStatus.OcrQueued; // Queued for re-processing
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = _currentUserService.Username ?? "System";

        _context.DocumentVersions.Add(newVersion);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "DOCUMENT_AMENDED",
            resource: "Document",
            resourceId: document.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Created version {nextVersionNumber} for document {document.Id}. Reason: {newVersion.ReasonForVersion}",
            cancellationToken: cancellationToken);

        DocumentOcrResultDto? ocrDto = null;
        try
        {
            ocrDto = await _ocrService.ProcessDocumentAsync(document.Id, nextVersionNumber, cancellationToken);
        }
        catch
        {
            // Non-blocking
        }

        return MapToDto(document, newVersion, ocrDto);
    }

    public async Task<List<DocumentDto>> GetDocumentsByEncounterAsync(Guid encounterId, CancellationToken cancellationToken = default)
    {
        var documents = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .Where(d => d.EncounterId == encounterId)
            .OrderByDescending(d => d.ClinicalDate)
            .ToListAsync(cancellationToken);

        return documents.Select(d =>
        {
            var currentVersion = d.Versions.FirstOrDefault(v => v.VersionNumber == d.CurrentVersionNumber)
                ?? d.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            var latestOcr = d.OcrResults.OrderByDescending(o => o.ProcessedAt).FirstOrDefault();
            return MapToDto(d, currentVersion, MapOcrDto(latestOcr));
        }).ToList();
    }

    public async Task<List<DocumentDto>> GetDocumentsByPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var documents = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .Where(d => d.PatientId == patientId)
            .OrderByDescending(d => d.ClinicalDate)
            .ToListAsync(cancellationToken);

        return documents.Select(d =>
        {
            var currentVersion = d.Versions.FirstOrDefault(v => v.VersionNumber == d.CurrentVersionNumber)
                ?? d.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            var latestOcr = d.OcrResults.OrderByDescending(o => o.ProcessedAt).FirstOrDefault();
            return MapToDto(d, currentVersion, MapOcrDto(latestOcr));
        }).ToList();
    }

    public async Task<DocumentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document == null) return null;

        var currentVersion = document.Versions.FirstOrDefault(v => v.VersionNumber == document.CurrentVersionNumber)
            ?? document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        var latestOcr = document.OcrResults.OrderByDescending(o => o.ProcessedAt).FirstOrDefault();

        return MapToDto(document, currentVersion, MapOcrDto(latestOcr));
    }

    public async Task<(Stream? Stream, string ContentType, string FileName)?> DownloadFileAsync(Guid documentId, int? versionNumber = null, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null) return null;

        var targetVersionNumber = versionNumber ?? document.CurrentVersionNumber;
        var version = document.Versions.FirstOrDefault(v => v.VersionNumber == targetVersionNumber);

        if (version == null) return null;

        var stream = await _storageService.DownloadAsync(version.StorageKey, cancellationToken);
        if (stream == null) return null;

        await _auditService.LogAsync(
            action: "DOCUMENT_DOWNLOADED",
            resource: "Document",
            resourceId: document.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Downloaded version {targetVersionNumber} ({version.OriginalFileName})",
            cancellationToken: cancellationToken);

        return (stream, version.ContentType, version.OriginalFileName);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, string? deletionReason = null, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .Include(d => d.ClinicalEntities)
            .Include(d => d.Embeddings)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null) return false;

        var fileName = document.Versions.FirstOrDefault()?.OriginalFileName ?? "Scanned Record";

        // 1. Delete physical files from encrypted storage vault
        foreach (var version in document.Versions)
        {
            if (!string.IsNullOrWhiteSpace(version.StorageKey))
            {
                await _storageService.DeleteAsync(version.StorageKey, cancellationToken);
            }
        }

        // 2. Remove document and dependent records from database (cascade deletes versions/ocr/entities/embeddings)
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);

        // 3. Log audit event for compliance
        await _auditService.LogAsync(
            action: "DOCUMENT_DELETED",
            resource: "Document",
            resourceId: documentId.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Permanently deleted document '{fileName}' (Patient: {document.PatientId}, Reason: {deletionReason ?? "User initiated"})",
            cancellationToken: cancellationToken);

        return true;
    }

    private static DocumentDto MapToDto(Document d, DocumentVersion? currentVersion, DocumentOcrResultDto? latestOcr = null)
    {
        DocumentVersionDto? versionDto = null;
        if (currentVersion != null)
        {
            versionDto = new DocumentVersionDto(
                Id: currentVersion.Id,
                DocumentId: currentVersion.DocumentId,
                VersionNumber: currentVersion.VersionNumber,
                Sha256Checksum: currentVersion.Sha256Checksum,
                OriginalFileName: currentVersion.OriginalFileName,
                ContentType: currentVersion.ContentType,
                FileSize: currentVersion.FileSize,
                PageCount: currentVersion.PageCount,
                ReasonForVersion: currentVersion.ReasonForVersion,
                CreatedAt: currentVersion.CreatedAt,
                CreationActor: currentVersion.CreationActor
            );
        }

        return new DocumentDto(
            Id: d.Id,
            PatientId: d.PatientId,
            EncounterId: d.EncounterId,
            ClinicalEventId: d.ClinicalEventId,
            Category: d.Category,
            ClinicalDate: d.ClinicalDate,
            Language: d.Language,
            ConfidentialityLevel: d.ConfidentialityLevel,
            Status: d.Status,
            CurrentVersionNumber: d.CurrentVersionNumber,
            CurrentVersion: versionDto,
            CreatedAt: d.CreatedAt,
            CreatedBy: d.CreatedBy,
            LatestOcr: latestOcr
        );
    }

    private static DocumentOcrResultDto? MapOcrDto(DocumentOcrResult? o)
    {
        if (o == null) return null;

        List<string> keywords = new();
        try
        {
            if (!string.IsNullOrEmpty(o.KeywordsDetected))
            {
                keywords = System.Text.Json.JsonSerializer.Deserialize<List<string>>(o.KeywordsDetected) ?? new();
            }
        }
        catch { }

        return new DocumentOcrResultDto(
            Id: o.Id,
            DocumentId: o.DocumentId,
            DocumentVersionId: o.DocumentVersionId,
            ExtractedText: o.ExtractedText,
            ExtractedNepaliText: o.ExtractedNepaliText,
            OcrConfidenceScore: o.OcrConfidenceScore,
            LanguageDetected: o.LanguageDetected,
            PageCount: o.PageCount,
            WordCount: o.WordCount,
            PredictedCategory: o.PredictedCategory,
            ClassificationConfidence: o.ClassificationConfidence,
            KeywordsDetected: keywords,
            LowConfidenceWarning: o.LowConfidenceWarning,
            IsVerifiedByHuman: o.IsVerifiedByHuman,
            VerifiedBy: o.VerifiedBy,
            VerifiedAt: o.VerifiedAt,
            ProcessingDurationMs: o.ProcessingDurationMs,
            ProcessedAt: o.ProcessedAt
        );
    }
}
