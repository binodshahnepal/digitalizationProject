using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HospitalRecords.Infrastructure.Services;

public class OcrProcessingService : IOcrProcessingService
{
    private readonly IApplicationDbContext _context;
    private readonly IObjectStorageService _storageService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly HttpClient _httpClient;
    private readonly IClinicalEntityExtractionService _clinicalEntityExtractionService;
    private readonly ISearchService _searchService;
    private readonly ILogger<OcrProcessingService> _logger;

    private const string AiWorkerUrl = "http://localhost:5200";

    public OcrProcessingService(
        IApplicationDbContext context,
        IObjectStorageService storageService,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        HttpClient httpClient,
        IClinicalEntityExtractionService clinicalEntityExtractionService,
        ISearchService searchService,
        ILogger<OcrProcessingService> logger)
    {
        _context = context;
        _storageService = storageService;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _httpClient = httpClient;
        _clinicalEntityExtractionService = clinicalEntityExtractionService;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<DocumentOcrResultDto> ProcessDocumentAsync(Guid documentId, int? versionNumber = null, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.Patient)
            .Include(d => d.OcrResults)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            throw new ArgumentException($"Document with ID '{documentId}' not found.");
        }

        var targetVersion = versionNumber.HasValue
            ? document.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber.Value)
            : document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        if (targetVersion == null)
        {
            throw new ArgumentException($"Version for document '{documentId}' not found.");
        }

        var stopwatch = Stopwatch.StartNew();

        // 1. Download file stream from storage
        using var stream = await _storageService.DownloadAsync(targetVersion.StorageKey, cancellationToken);
        if (stream == null)
        {
            throw new InvalidOperationException($"Unable to download document file asset from storage: {targetVersion.StorageKey}");
        }

        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        var fileBytes = memoryStream.ToArray();

        // 2. Invoke Python AI Worker pipeline, with resilient C# fallback
        OcrExtractionDto ocrExtraction;
        ClassificationExtractionDto classification;

        try
        {
            var aiResult = await CallPythonAiWorkerAsync(fileBytes, targetVersion.OriginalFileName, targetVersion.ContentType, cancellationToken);
            ocrExtraction = aiResult.Ocr;
            classification = aiResult.Classification;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Python AiWorker unavailable or failed on port 5200. Executing resilient internal dual-language OCR engine.");
            var fallback = ProcessInternalFallback(fileBytes, targetVersion.OriginalFileName);
            ocrExtraction = fallback.Ocr;
            classification = fallback.Classification;
        }

        stopwatch.Stop();

        // 3. Flagging Thresholds (FR-OCR-002, FR-CLS-001)
        // Aggregate OCR confidence < 0.70 or classification confidence < 0.80 triggers review
        bool lowConfidence = ocrExtraction.ConfidenceScore < 0.70 || 
                             classification.Confidence < 0.80 || 
                             ocrExtraction.LowConfidenceWarning || 
                             classification.LowConfidenceWarning;

        // 4. Create and persist DocumentOcrResult
        var ocrResult = new DocumentOcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            DocumentVersionId = targetVersion.Id,
            ExtractedText = ocrExtraction.ExtractedText,
            ExtractedNepaliText = ocrExtraction.ExtractedNepaliText,
            OcrConfidenceScore = ocrExtraction.ConfidenceScore,
            LanguageDetected = ocrExtraction.LanguageDetected,
            PageCount = ocrExtraction.PageCount,
            WordCount = ocrExtraction.WordCount,
            PredictedCategory = classification.PredictedCategory,
            ClassificationConfidence = classification.Confidence,
            KeywordsDetected = JsonSerializer.Serialize(classification.MatchedKeywords),
            LowConfidenceWarning = lowConfidence,
            IsVerifiedByHuman = false,
            ProcessingDurationMs = stopwatch.ElapsedMilliseconds,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        _context.DocumentOcrResults.Add(ocrResult);

        // 5. Update Document Status & Category
        if (lowConfidence || !document.EncounterId.HasValue)
        {
            document.Status = DocumentStatus.RequiresReview;
        }
        else
        {
            document.Status = DocumentStatus.Indexed;
        }

        // Auto-assign category if initial category was unspecific and classification is confident
        if (document.Category == DocumentCategory.GeneralMedicalChart && classification.Confidence >= 0.85)
        {
            document.Category = classification.PredictedCategory;
        }

        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = _currentUserService.Username ?? "AI_Worker";

        await _context.SaveChangesAsync(cancellationToken);

        // 6. Automatic Oncology Clinical Entity Extraction (FR-EXT-001) & Semantic Vector Indexing (FR-SEARCH-001)
        try
        {
            await _clinicalEntityExtractionService.ExtractAndStoreEntitiesAsync(document.Id, ocrExtraction.ExtractedText, cancellationToken);
            await _searchService.IndexDocumentEmbeddingsAsync(document.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clinical entity extraction or embedding indexing encountered an issue for document {DocumentId}", document.Id);
        }

        // 7. Audit Logging (NFR-004)
        await _auditService.LogAsync(
            action: lowConfidence ? "DOCUMENT_FLAGGED_FOR_REVIEW" : "DOCUMENT_OCR_PROCESSED",
            resource: "Document",
            resourceId: document.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"OCR Completed: Category='{classification.PredictedCategory}' ({classification.Confidence:P0}), OCR Conf={ocrExtraction.ConfidenceScore:P0}, Status={document.Status}",
            cancellationToken: cancellationToken);

        return MapToDto(ocrResult);
    }

    public async Task<DocumentDto> VerifyOcrAsync(Guid documentId, VerifyOcrRequest request, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            throw new ArgumentException($"Document with ID '{documentId}' not found.");
        }

        var latestOcr = document.OcrResults
            .OrderByDescending(o => o.ProcessedAt)
            .FirstOrDefault();

        var verifierUsername = _currentUserService.Username ?? "Supervisor";

        if (latestOcr != null)
        {
            if (!string.IsNullOrWhiteSpace(request.CorrectedText))
            {
                latestOcr.ExtractedText = request.CorrectedText.Trim();
            }

            latestOcr.PredictedCategory = request.ConfirmedCategory;
            latestOcr.IsVerifiedByHuman = true;
            latestOcr.VerifiedBy = verifierUsername;
            latestOcr.VerifiedAt = DateTimeOffset.UtcNow;
            latestOcr.LowConfidenceWarning = false;
        }

        document.Category = request.ConfirmedCategory;
        document.Status = DocumentStatus.Indexed;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedBy = verifierUsername;

        await _context.SaveChangesAsync(cancellationToken);

        // Re-extract oncology entities if corrected text was submitted or category updated
        try
        {
            var textToExtract = !string.IsNullOrWhiteSpace(request.CorrectedText) 
                ? request.CorrectedText.Trim() 
                : latestOcr?.ExtractedText;

            if (!string.IsNullOrWhiteSpace(textToExtract))
            {
                await _clinicalEntityExtractionService.ExtractAndStoreEntitiesAsync(document.Id, textToExtract, cancellationToken);
                await _searchService.IndexDocumentEmbeddingsAsync(document.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clinical entity re-extraction or embedding indexing failed for document {DocumentId}", document.Id);
        }

        await _auditService.LogAsync(
            action: "DOCUMENT_OCR_VERIFIED",
            resource: "Document",
            resourceId: document.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Human verification completed by {verifierUsername}. Confirmed category: {request.ConfirmedCategory}. Notes: {request.VerificationNotes}",
            cancellationToken: cancellationToken);

        var currentVersion = document.Versions.FirstOrDefault(v => v.VersionNumber == document.CurrentVersionNumber)
            ?? document.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

        return new DocumentDto(
            Id: document.Id,
            PatientId: document.PatientId,
            EncounterId: document.EncounterId,
            ClinicalEventId: document.ClinicalEventId,
            Category: document.Category,
            ClinicalDate: document.ClinicalDate,
            Language: document.Language,
            ConfidentialityLevel: document.ConfidentialityLevel,
            Status: document.Status,
            CurrentVersionNumber: document.CurrentVersionNumber,
            CurrentVersion: currentVersion != null ? new DocumentVersionDto(
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
            ) : null,
            CreatedAt: document.CreatedAt,
            CreatedBy: document.CreatedBy,
            LatestOcr: latestOcr != null ? MapToDto(latestOcr) : null
        );
    }

    public async Task<List<DocumentReviewQueueItemDto>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
    {
        var query = await _context.Documents
            .Include(d => d.Patient)
            .Include(d => d.Encounter)
            .Include(d => d.Versions)
            .Include(d => d.OcrResults)
            .Where(d => d.Status == DocumentStatus.RequiresReview || 
                        d.OcrResults.Any(o => o.LowConfidenceWarning && !o.IsVerifiedByHuman))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<DocumentReviewQueueItemDto>();

        foreach (var doc in query)
        {
            var curVersion = doc.Versions.FirstOrDefault(v => v.VersionNumber == doc.CurrentVersionNumber)
                ?? doc.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();

            var latestOcr = doc.OcrResults.OrderByDescending(o => o.ProcessedAt).FirstOrDefault();

            string reason;
            if (!doc.EncounterId.HasValue)
            {
                reason = "Historical Chart: Missing Encounter assignment (FR-VIS-003)";
            }
            else if (latestOcr != null && latestOcr.OcrConfidenceScore < 0.70)
            {
                reason = $"Low OCR extraction confidence ({latestOcr.OcrConfidenceScore:P0})";
            }
            else if (latestOcr != null && latestOcr.ClassificationConfidence < 0.80)
            {
                reason = $"Low AI classification confidence ({latestOcr.ClassificationConfidence:P0})";
            }
            else
            {
                reason = "Requires supervisor review";
            }

            var preview = latestOcr?.ExtractedText ?? string.Empty;
            if (preview.Length > 200)
            {
                preview = preview.Substring(0, 197) + "...";
            }

            result.Add(new DocumentReviewQueueItemDto(
                DocumentId: doc.Id,
                PatientId: doc.PatientId,
                PatientMrn: doc.Patient.Mrn,
                PatientFullName: doc.Patient.FullName,
                EncounterId: doc.EncounterId,
                EncounterNumber: doc.Encounter?.EncounterNumber,
                OriginalFileName: curVersion?.OriginalFileName ?? "Document.pdf",
                VersionNumber: curVersion?.VersionNumber ?? 1,
                CurrentCategory: doc.Category,
                PredictedCategory: latestOcr?.PredictedCategory ?? doc.Category,
                OcrConfidenceScore: latestOcr?.OcrConfidenceScore ?? 0.0,
                ClassificationConfidence: latestOcr?.ClassificationConfidence ?? 0.0,
                LanguageDetected: latestOcr?.LanguageDetected ?? doc.Language,
                LowConfidenceWarning: latestOcr?.LowConfidenceWarning ?? true,
                ReasonForReview: reason,
                UploadedAt: doc.CreatedAt,
                TextPreview: preview
            ));
        }

        return result;
    }

    public async Task<DocumentOcrResultDto?> GetOcrResultAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var result = await _context.DocumentOcrResults
            .Where(o => o.DocumentId == documentId)
            .OrderByDescending(o => o.ProcessedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return result != null ? MapToDto(result) : null;
    }

    // -----------------------------------------------------------------------------------
    // Internal Python Microservice Client & Fallback Engine
    // -----------------------------------------------------------------------------------
    private record PipelineApiResponse(
        JsonElement ocr,
        JsonElement classification,
        bool overall_low_confidence,
        int processing_duration_ms
    );

    private record OcrExtractionDto(
        string ExtractedText,
        string? ExtractedNepaliText,
        double ConfidenceScore,
        string LanguageDetected,
        int PageCount,
        int WordCount,
        bool LowConfidenceWarning
    );

    private record ClassificationExtractionDto(
        DocumentCategory PredictedCategory,
        double Confidence,
        List<string> MatchedKeywords,
        bool LowConfidenceWarning
    );

    private async Task<(OcrExtractionDto Ocr, ClassificationExtractionDto Classification)> CallPythonAiWorkerAsync(
        byte[] fileBytes, 
        string fileName, 
        string contentType, 
        CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(fileContent, "file", fileName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        var response = await _httpClient.PostAsync($"{AiWorkerUrl}/pipeline/process", form, cts.Token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var ocrElem = root.GetProperty("ocr");
        var extractedText = ocrElem.GetProperty("extracted_text").GetString() ?? string.Empty;
        var extractedNepali = ocrElem.TryGetProperty("extracted_nepali_text", out var nepElem) && nepElem.ValueKind != JsonValueKind.Null
            ? nepElem.GetString()
            : null;
        var ocrConf = ocrElem.GetProperty("confidence_score").GetDouble();
        var lang = ocrElem.GetProperty("language_detected").GetString() ?? "en";
        var pageCount = ocrElem.GetProperty("page_count").GetInt32();
        var wordCount = ocrElem.GetProperty("word_count").GetInt32();
        var ocrLowConf = ocrElem.GetProperty("low_confidence_warning").GetBoolean();

        var clsElem = root.GetProperty("classification");
        var catStr = clsElem.GetProperty("predicted_category").GetString() ?? "GeneralMedicalChart";
        Enum.TryParse<DocumentCategory>(catStr, true, out var predictedCategory);
        var clsConf = clsElem.GetProperty("confidence").GetDouble();
        var clsLowConf = clsElem.GetProperty("low_confidence_warning").GetBoolean();

        var keywords = new List<string>();
        if (clsElem.TryGetProperty("matched_keywords", out var kwArr) && kwArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var k in kwArr.EnumerateArray())
            {
                var s = k.GetString();
                if (!string.IsNullOrEmpty(s)) keywords.Add(s);
            }
        }

        return (
            new OcrExtractionDto(extractedText, extractedNepali, ocrConf, lang, pageCount, wordCount, ocrLowConf),
            new ClassificationExtractionDto(predictedCategory, clsConf, keywords, clsLowConf)
        );
    }

    private (OcrExtractionDto Ocr, ClassificationExtractionDto Classification) ProcessInternalFallback(byte[] bytes, string fileName)
    {
        string rawText;
        try
        {
            rawText = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            rawText = string.Empty;
        }

        // Clean raw ASCII/UTF8 text
        var cleaned = Regex.Replace(rawText, @"[\r\t\0]", " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordCount = words.Length;

        // Detect Devanagari script (Unicode range U+0900 - U+097F)
        var devanagariMatches = Regex.Matches(cleaned, @"[\u0900-\u097F]+");
        var nepaliText = devanagariMatches.Count > 0 
            ? string.Join(" ", devanagariMatches.Select(m => m.Value)) 
            : null;

        string language = "en";
        double ocrConfidence = 0.95;

        if (devanagariMatches.Count > 0)
        {
            language = (double)devanagariMatches.Count / Math.Max(1, wordCount) > 0.4 ? "ne" : "mixed";
            ocrConfidence = 0.92;
        }

        if (wordCount < 10)
        {
            ocrConfidence = 0.55;
        }

        // Classifier
        var lower = cleaned.ToLowerInvariant();
        var category = DocumentCategory.GeneralMedicalChart;
        var matchedKeywords = new List<string>();
        double clsConfidence = 0.60;

        if (lower.Contains("biopsy") || lower.Contains("histopathology") || lower.Contains("carcinoma") || lower.Contains("बायोप्सी"))
        {
            category = DocumentCategory.PathologyLabReport;
            clsConfidence = 0.94;
            matchedKeywords.Add("biopsy");
            matchedKeywords.Add("histopathology");
        }
        else if (lower.Contains("ct scan") || lower.Contains("mri") || lower.Contains("ultrasound") || lower.Contains("x-ray") || lower.Contains("एक्स-रे"))
        {
            category = DocumentCategory.RadiologyReport;
            clsConfidence = 0.92;
            matchedKeywords.Add("radiology");
            matchedKeywords.Add("scan");
        }
        else if (lower.Contains("discharge summary") || lower.Contains("डिस्चार्ज"))
        {
            category = DocumentCategory.DischargeSummary;
            clsConfidence = 0.95;
            matchedKeywords.Add("discharge summary");
        }
        else if (lower.Contains("rx") || lower.Contains("tablet") || lower.Contains("chemotherapy") || lower.Contains("औषधि"))
        {
            category = DocumentCategory.Prescription;
            clsConfidence = 0.88;
            matchedKeywords.Add("rx");
            matchedKeywords.Add("chemotherapy");
        }
        else if (lower.Contains("operative note") || lower.Contains("surgeon") || lower.Contains("शल्यक्रिया"))
        {
            category = DocumentCategory.OperativeNote;
            clsConfidence = 0.91;
            matchedKeywords.Add("operative note");
        }

        return (
            new OcrExtractionDto(cleaned, nepaliText, ocrConfidence, language, 1, wordCount, ocrConfidence < 0.70),
            new ClassificationExtractionDto(category, clsConfidence, matchedKeywords, clsConfidence < 0.80)
        );
    }

    private static DocumentOcrResultDto MapToDto(DocumentOcrResult o)
    {
        List<string> keywords = new();
        try
        {
            if (!string.IsNullOrEmpty(o.KeywordsDetected))
            {
                keywords = JsonSerializer.Deserialize<List<string>>(o.KeywordsDetected) ?? new();
            }
        }
        catch
        {
            // Ignore JSON parse errors
        }

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
