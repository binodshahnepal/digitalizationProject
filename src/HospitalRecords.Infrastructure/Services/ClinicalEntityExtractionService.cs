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

public class ClinicalEntityExtractionService : IClinicalEntityExtractionService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClinicalEntityExtractionService> _logger;

    private const string AiWorkerUrl = "http://localhost:5200";

    public ClinicalEntityExtractionService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        HttpClient httpClient,
        ILogger<ClinicalEntityExtractionService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ClinicalEntityDto>> ExtractAndStoreEntitiesAsync(Guid documentId, string text, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Patient)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            throw new ArgumentException($"Document with ID '{documentId}' not found.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<ClinicalEntityDto>();
        }

        // Try AI Worker first
        List<ExtractedRawEntity> rawEntities;
        try
        {
            rawEntities = await ExtractViaAiWorkerAsync(text, document.Category.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call AI Worker for clinical extraction. Falling back to native C# NLP engine.");
            rawEntities = ExtractViaNativeEngine(text);
        }

        if (rawEntities.Count == 0)
        {
            // If AI worker returned empty, run native engine as supplementary
            rawEntities = ExtractViaNativeEngine(text);
        }

        // Remove existing unverified entities for this document to avoid duplicates on re-extraction
        var existingEntities = await _context.ClinicalEntities
            .Where(e => e.DocumentId == documentId && !e.IsVerified)
            .ToListAsync(cancellationToken);

        if (existingEntities.Count > 0)
        {
            _context.ClinicalEntities.RemoveRange(existingEntities);
        }

        var newEntities = new List<ClinicalEntity>();
        var now = DateTimeOffset.UtcNow;

        foreach (var raw in rawEntities)
        {
            if (!Enum.TryParse<ClinicalEntityType>(raw.EntityType, true, out var entityType))
            {
                entityType = ClinicalEntityType.Diagnosis;
            }

            var entity = new ClinicalEntity
            {
                Id = Guid.NewGuid(),
                PatientId = document.PatientId,
                DocumentId = document.Id,
                EncounterId = document.EncounterId,
                EntityType = entityType,
                Category = raw.Category,
                Name = raw.Name,
                Value = raw.Value,
                Code = raw.Code,
                CodeSystem = raw.CodeSystem,
                ConfidenceScore = raw.ConfidenceScore,
                ExtractedText = raw.ExtractedText,
                TextOffsetStart = raw.TextOffsetStart,
                TextOffsetEnd = raw.TextOffsetEnd,
                AttributesJson = raw.AttributesJson,
                ClinicalDate = document.ClinicalDate,
                IsVerified = false,
                CreatedAt = now,
                CreatedBy = _currentUserService.Username ?? "SystemAI"
            };

            newEntities.Add(entity);
            _context.ClinicalEntities.Add(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLINICAL_ENTITIES_EXTRACTED",
            resource: "Document",
            resourceId: documentId.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Extracted {newEntities.Count} oncology entities for patient MRN {document.Patient.Mrn}",
            cancellationToken: cancellationToken
        );

        return newEntities.Select(MapToDto).ToList();
    }

    public async Task<List<ClinicalEntityDto>> GetEntitiesByDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.ClinicalEntities
            .Where(e => e.DocumentId == documentId)
            .OrderBy(e => e.EntityType)
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<List<ClinicalEntityDto>> GetEntitiesByPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.ClinicalEntities
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.ClinicalDate)
            .ThenBy(e => e.EntityType)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<ClinicalEntityDto> VerifyEntityAsync(Guid entityId, VerifyClinicalEntityRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ClinicalEntities
            .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Clinical entity with ID '{entityId}' not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.ConfirmedName))
            entity.Name = request.ConfirmedName.Trim();

        if (!string.IsNullOrWhiteSpace(request.ConfirmedValue))
            entity.Value = request.ConfirmedValue.Trim();

        if (!string.IsNullOrWhiteSpace(request.ConfirmedCode))
            entity.Code = request.ConfirmedCode.Trim();

        entity.IsVerified = true;
        entity.VerifiedBy = _currentUserService.Username ?? "Clinician";
        entity.VerifiedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = entity.VerifiedBy;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLINICAL_ENTITY_VERIFIED",
            resource: "ClinicalEntity",
            resourceId: entityId.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Verified entity '{entity.Name}' ({entity.Value}) by {entity.VerifiedBy}. Notes: {request.VerificationNotes ?? "None"}",
            cancellationToken: cancellationToken
        );

        return MapToDto(entity);
    }

    public async Task<ClinicalEntityDto> CreateManualEntityAsync(CreateClinicalEntityRequest request, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new ClinicalEntity
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            DocumentId = request.DocumentId,
            EncounterId = request.EncounterId,
            EntityType = request.EntityType,
            Category = request.Category,
            Name = request.Name,
            Value = request.Value,
            Code = request.Code,
            CodeSystem = request.CodeSystem,
            ConfidenceScore = request.ConfidenceScore,
            ExtractedText = request.ExtractedText ?? request.Value,
            AttributesJson = request.AttributesJson,
            ClinicalDate = request.ClinicalDate,
            IsVerified = true,
            VerifiedBy = _currentUserService.Username ?? "Clinician",
            VerifiedAt = now,
            CreatedAt = now,
            CreatedBy = _currentUserService.Username ?? "Clinician"
        };

        _context.ClinicalEntities.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLINICAL_ENTITY_CREATED",
            resource: "ClinicalEntity",
            resourceId: entity.Id.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Manual creation of {entity.EntityType} '{entity.Name}' by {entity.CreatedBy}",
            cancellationToken: cancellationToken
        );

        return MapToDto(entity);
    }

    public async Task<ClinicalEntityDto> UpdateEntityAsync(Guid entityId, UpdateClinicalEntityRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ClinicalEntities
            .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);

        if (entity == null)
        {
            throw new ArgumentException($"Clinical entity with ID '{entityId}' not found.");
        }

        if (request.EntityType.HasValue) entity.EntityType = request.EntityType.Value;
        if (!string.IsNullOrWhiteSpace(request.Category)) entity.Category = request.Category.Trim();
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.Value)) entity.Value = request.Value.Trim();
        if (request.Code != null) entity.Code = request.Code.Trim();
        if (request.CodeSystem != null) entity.CodeSystem = request.CodeSystem.Trim();
        if (!string.IsNullOrWhiteSpace(request.AttributesJson)) entity.AttributesJson = request.AttributesJson;
        if (request.ClinicalDate.HasValue) entity.ClinicalDate = request.ClinicalDate.Value;

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = _currentUserService.Username ?? "Clinician";

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteEntityAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ClinicalEntities
            .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);

        if (entity == null) return false;

        _context.ClinicalEntities.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "CLINICAL_ENTITY_DELETED",
            resource: "ClinicalEntity",
            resourceId: entityId.ToString(),
            outcome: AuditOutcome.Success,
            details: $"Deleted entity '{entity.Name}'",
            cancellationToken: cancellationToken
        );

        return true;
    }

    private async Task<List<ExtractedRawEntity>> ExtractViaAiWorkerAsync(string text, string category, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { text = text, document_category = category });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{AiWorkerUrl}/extraction/clinical-entities", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"AI Worker returned status {response.StatusCode}");
        }

        var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        var results = new List<ExtractedRawEntity>();
        if (root.TryGetProperty("entities", out var entitiesArray))
        {
            foreach (var item in entitiesArray.EnumerateArray())
            {
                var attrsJson = item.TryGetProperty("attributes", out var attrs) ? attrs.GetRawText() : "{}";
                results.Add(new ExtractedRawEntity(
                    EntityType: item.GetProperty("entity_type").GetString() ?? "Diagnosis",
                    Category: item.GetProperty("category").GetString() ?? "Oncology",
                    Name: item.GetProperty("name").GetString() ?? "",
                    Value: item.GetProperty("value").GetString() ?? "",
                    Code: item.TryGetProperty("code", out var c) && c.ValueKind != JsonValueKind.Null ? c.GetString() : null,
                    CodeSystem: item.TryGetProperty("code_system", out var cs) && cs.ValueKind != JsonValueKind.Null ? cs.GetString() : null,
                    ConfidenceScore: item.TryGetProperty("confidence_score", out var conf) ? conf.GetDouble() : 0.95,
                    ExtractedText: item.TryGetProperty("extracted_text", out var et) ? et.GetString() ?? "" : "",
                    TextOffsetStart: item.TryGetProperty("text_offset_start", out var tos) && tos.ValueKind == JsonValueKind.Number ? tos.GetInt32() : null,
                    TextOffsetEnd: item.TryGetProperty("text_offset_end", out var toe) && toe.ValueKind == JsonValueKind.Number ? toe.GetInt32() : null,
                    AttributesJson: attrsJson
                ));
            }
        }

        return results;
    }

    private static List<ExtractedRawEntity> ExtractViaNativeEngine(string text)
    {
        var list = new List<ExtractedRawEntity>();

        // 1. Diagnosis
        if (Regex.IsMatch(text, @"(?:infiltrating|invasive)\s+ductal\s+carcinoma", RegexOptions.IgnoreCase))
        {
            list.Add(new ExtractedRawEntity(
                "Diagnosis", "Oncology Pathology", "Infiltrating Ductal Carcinoma",
                "Infiltrating Ductal Carcinoma (NOS)", "8500/3", "ICD-O-3", 0.95,
                "Infiltrating Ductal Carcinoma", null, null,
                "{\"histology\":\"Ductal\",\"icd10\":\"C50.9\",\"primary_site\":\"Breast\"}"
            ));
        }
        else if (Regex.IsMatch(text, @"(?:infiltrating|invasive)\s+lobular\s+carcinoma", RegexOptions.IgnoreCase))
        {
            list.Add(new ExtractedRawEntity(
                "Diagnosis", "Oncology Pathology", "Invasive Lobular Carcinoma",
                "Invasive Lobular Carcinoma", "8520/3", "ICD-O-3", 0.95,
                "Invasive Lobular Carcinoma", null, null,
                "{\"histology\":\"Lobular\",\"icd10\":\"C50.9\",\"primary_site\":\"Breast\"}"
            ));
        }
        else if (Regex.IsMatch(text, @"squamous\s+cell\s+carcinoma", RegexOptions.IgnoreCase))
        {
            list.Add(new ExtractedRawEntity(
                "Diagnosis", "Oncology Pathology", "Squamous Cell Carcinoma",
                "Squamous Cell Carcinoma", "8070/3", "ICD-O-3", 0.95,
                "Squamous Cell Carcinoma", null, null,
                "{\"histology\":\"Squamous\"}"
            ));
        }

        // 2. TNM Staging
        var tnmMatch = Regex.Match(text, @"\b((?:p|c|yp)?T(?:is|[0-4][a-d]?))\s*((?:p|c|yp)?N(?:x|[0-3][a-c]?))\s*((?:p|c|yp)?M(?:x|[0-1][a-c]?))\b");
        if (tnmMatch.Success)
        {
            var t = tnmMatch.Groups[1].Value;
            var n = tnmMatch.Groups[2].Value;
            var m = tnmMatch.Groups[3].Value;
            list.Add(new ExtractedRawEntity(
                "TnmStaging", "TNM Staging", "TNM Classification",
                $"{t} {n} {m}", $"{t}{n}{m}", "TNM-8", 0.98,
                tnmMatch.Value, tnmMatch.Index, tnmMatch.Index + tnmMatch.Length,
                $"{{\"t\":\"{t}\",\"n\":\"{n}\",\"m\":\"{m}\"}}"
            ));
        }

        // 3. AJCC Stage
        var stageMatch = Regex.Match(text, @"\b(?:(?:AJCC|TNM|Clinical|Pathological)\s+)?Stage\s+(0|I{1,3}[A-C]?|IV[A-C]?|[1-4][A-C]?)\b", RegexOptions.IgnoreCase);
        if (stageMatch.Success)
        {
            var stageVal = $"Stage {stageMatch.Groups[1].Value.ToUpper()}";
            list.Add(new ExtractedRawEntity(
                "CancerStage", "TNM Staging", "AJCC Overall Stage",
                stageVal, stageVal, "AJCC-8", 0.95,
                stageMatch.Value, stageMatch.Index, stageMatch.Index + stageMatch.Length,
                $"{{\"stage\":\"{stageMatch.Groups[1].Value.ToUpper()}\"}}"
            ));
        }

        // 4. Histologic Grade
        var gradeMatch = Regex.Match(text, @"\b(?:(?:Nottingham|Bloom-Richardson|Histologic(?:al)?)\s+)?(?:Grade|grade)\s*[:=-]?\s*([1-3]|I{1,3}|well differentiated|moderately differentiated|poorly differentiated)\b", RegexOptions.IgnoreCase);
        if (gradeMatch.Success)
        {
            var rawG = gradeMatch.Groups[1].Value;
            var formatted = rawG.Contains("2") || rawG.Equals("ii", StringComparison.OrdinalIgnoreCase) || rawG.Contains("moderately") 
                ? "Grade 2 (Moderately Differentiated)" 
                : (rawG.Contains("1") || rawG.Equals("i", StringComparison.OrdinalIgnoreCase) ? "Grade 1 (Well Differentiated)" : "Grade 3 (Poorly Differentiated)");
            list.Add(new ExtractedRawEntity(
                "HistologicGrade", "Histopathology", "Histologic Grade",
                formatted, $"Grade {rawG}", "Nottingham", 0.92,
                gradeMatch.Value, gradeMatch.Index, gradeMatch.Index + gradeMatch.Length,
                $"{{\"raw_grade\":\"{rawG}\"}}"
            ));
        }

        // 5. Biomarkers (ER, PR, HER2, Ki-67)
        var erMatch = Regex.Match(text, @"(?:Estrogen\s+Receptor(?:\s*\([A-Za-z]+\))?|\bER\b)\s*[:=-]?\s*(Positive|Negative|Pos|Neg)(?:[^\d\n]*(\d{1,3})\s*%)?", RegexOptions.IgnoreCase);
        if (erMatch.Success)
        {
            var status = erMatch.Groups[1].Value.StartsWith("pos", StringComparison.OrdinalIgnoreCase) ? "Positive" : "Negative";
            var pct = erMatch.Groups[2].Success ? erMatch.Groups[2].Value : null;
            var val = status + (pct != null ? $" ({pct}%)" : "");
            list.Add(new ExtractedRawEntity(
                "Biomarker", "Biomarker Profile", "Estrogen Receptor (ER)",
                val, status == "Positive" ? "ER+" : "ER-", "IHC", 0.95,
                erMatch.Value, erMatch.Index, erMatch.Index + erMatch.Length,
                $"{{\"biomarker\":\"ER\",\"status\":\"{status}\",\"percentage\":{(pct ?? "null")}}}"
            ));
        }

        var prMatch = Regex.Match(text, @"(?:Progesterone\s+Receptor(?:\s*\([A-Za-z]+\))?|\bPR\b)\s*[:=-]?\s*(Positive|Negative|Pos|Neg)(?:[^\d\n]*(\d{1,3})\s*%)?", RegexOptions.IgnoreCase);
        if (prMatch.Success)
        {
            var status = prMatch.Groups[1].Value.StartsWith("pos", StringComparison.OrdinalIgnoreCase) ? "Positive" : "Negative";
            var pct = prMatch.Groups[2].Success ? prMatch.Groups[2].Value : null;
            var val = status + (pct != null ? $" ({pct}%)" : "");
            list.Add(new ExtractedRawEntity(
                "Biomarker", "Biomarker Profile", "Progesterone Receptor (PR)",
                val, status == "Positive" ? "PR+" : "PR-", "IHC", 0.95,
                prMatch.Value, prMatch.Index, prMatch.Index + prMatch.Length,
                $"{{\"biomarker\":\"PR\",\"status\":\"{status}\",\"percentage\":{(pct ?? "null")}}}"
            ));
        }

        var her2Match = Regex.Match(text, @"\b(?:HER2(?:\/neu)?)\s*[:=-]?\s*(Positive|Negative|Equivocal|Pos|Neg)?\s*(?:\(?(?:Score|score)?\s*([0-3]\+?)\)?)?", RegexOptions.IgnoreCase);
        if (her2Match.Success && (!string.IsNullOrEmpty(her2Match.Groups[1].Value) || !string.IsNullOrEmpty(her2Match.Groups[2].Value)))
        {
            var rawSt = her2Match.Groups[1].Value;
            var sc = her2Match.Groups[2].Value;
            var st = rawSt.Contains("pos", StringComparison.OrdinalIgnoreCase) || sc.Contains("3") ? "Positive" : (rawSt.Contains("equivocal", StringComparison.OrdinalIgnoreCase) || sc.Contains("2") ? "Equivocal" : "Negative");
            var val = st + (!string.IsNullOrEmpty(sc) ? $" (Score {sc})" : "");
            list.Add(new ExtractedRawEntity(
                "Biomarker", "Biomarker Profile", "HER2/neu Receptor",
                val, st == "Positive" ? "HER2+" : (st == "Equivocal" ? "HER2 Equivocal" : "HER2-"), "IHC", 0.93,
                her2Match.Value, her2Match.Index, her2Match.Index + her2Match.Length,
                $"{{\"biomarker\":\"HER2\",\"status\":\"{st}\",\"score\":\"{sc}\"}}"
            ));
        }

        var ki67Match = Regex.Match(text, @"Ki-?67(?:[^\d:\n]*[:=-])?\s*(\d{1,3})\s*%", RegexOptions.IgnoreCase);
        if (ki67Match.Success)
        {
            var pct = ki67Match.Groups[1].Value;
            list.Add(new ExtractedRawEntity(
                "Biomarker", "Biomarker Profile", "Ki-67 Proliferation Index",
                $"{pct}%", "Ki-67", "IHC", 0.94,
                ki67Match.Value, ki67Match.Index, ki67Match.Index + ki67Match.Length,
                $"{{\"biomarker\":\"Ki-67\",\"percentage\":{pct}}}"
            ));
        }

        // 6. Chemotherapy Regimen
        var regMatch = Regex.Match(text, @"\b(AC-T|AC|FEC|FAC|FOLFOX(?:-?6)?|FOLFIRI|CAPOX|XELOX|Gemcitabine\s*\+\s*Cisplatin|Paclitaxel\s*\+\s*Carboplatin|CHOP|R-CHOP|ABVD)\b", RegexOptions.IgnoreCase);
        if (regMatch.Success)
        {
            var reg = regMatch.Groups[1].Value.ToUpper();
            var cycleMatch = Regex.Match(text, @"Cycle\s*([0-9]+)(?:\s*(?:of|\/)\s*([0-9]+))?", RegexOptions.IgnoreCase);
            var cycleStr = cycleMatch.Success ? $" (Cycle {cycleMatch.Groups[1].Value}" + (cycleMatch.Groups[2].Success ? $" of {cycleMatch.Groups[2].Value})" : ")") : "";
            list.Add(new ExtractedRawEntity(
                "ChemotherapyRegimen", "Systemic Chemotherapy", $"{reg} Regimen",
                $"{reg} Protocol{cycleStr}", reg, "NCCN", 0.96,
                regMatch.Value, regMatch.Index, regMatch.Index + regMatch.Length,
                $"{{\"regimen\":\"{reg}\"}}"
            ));
        }

        // 7. Surgical Procedures & Biopsies
        var surgMatch = Regex.Match(text, @"(?:Procedure(?:\s+Performed)?|Operation(?:\s+Performed|\s+Done)?)\s*[:=-]\s*([^\n\r]+)", RegexOptions.IgnoreCase);
        if (surgMatch.Success)
        {
            var procStr = surgMatch.Groups[1].Value.Trim().TrimEnd('.');
            list.Add(new ExtractedRawEntity(
                "SurgicalProcedure", "Surgical Oncology", "Surgical Procedure",
                procStr, "SURG", "CPT", 0.94,
                surgMatch.Value, surgMatch.Index, surgMatch.Index + surgMatch.Length,
                $"{{\"procedure\":\"{procStr}\"}}"
            ));
        }

        var biopsyMatch = Regex.Match(text, @"\b(?:Biopsy|FNAC|Tru-cut\s+biopsy|Core\s+biopsy)\b", RegexOptions.IgnoreCase);
        if (biopsyMatch.Success)
        {
            list.Add(new ExtractedRawEntity(
                "SurgicalProcedure", "Diagnostic Oncology", "Diagnostic Biopsy",
                biopsyMatch.Value, "BIOPSY", "CPT", 0.95,
                biopsyMatch.Value, biopsyMatch.Index, biopsyMatch.Index + biopsyMatch.Length,
                "{\"procedure\":\"Diagnostic Biopsy\"}"
            ));
        }

        return list;
    }

    private static ClinicalEntityDto MapToDto(ClinicalEntity entity)
    {
        return new ClinicalEntityDto(
            entity.Id,
            entity.PatientId,
            entity.DocumentId,
            entity.EncounterId,
            entity.EntityType,
            entity.Category,
            entity.Name,
            entity.Value,
            entity.Code,
            entity.CodeSystem,
            entity.ConfidenceScore,
            entity.ExtractedText,
            entity.TextOffsetStart,
            entity.TextOffsetEnd,
            entity.AttributesJson,
            entity.ClinicalDate,
            entity.IsVerified,
            entity.VerifiedBy,
            entity.VerifiedAt,
            entity.CreatedAt
        );
    }

    private record ExtractedRawEntity(
        string EntityType,
        string Category,
        string Name,
        string Value,
        string? Code,
        string? CodeSystem,
        double ConfidenceScore,
        string ExtractedText,
        int? TextOffsetStart,
        int? TextOffsetEnd,
        string AttributesJson
    );
}
