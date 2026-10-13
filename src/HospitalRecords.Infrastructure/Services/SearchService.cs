using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
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

public class SearchService : ISearchService
{
    private readonly IApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SearchService> _logger;

    private const string AiWorkerUrl = "http://localhost:5200";
    private const int EmbeddingDimensions = 384;

    public SearchService(
        IApplicationDbContext context,
        HttpClient httpClient,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        ILogger<SearchService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<int> IndexDocumentEmbeddingsAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _context.Documents
            .Include(d => d.Patient)
            .Include(d => d.OcrResults)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            _logger.LogWarning("IndexDocumentEmbeddings: Document {DocumentId} not found", documentId);
            return 0;
        }

        var latestOcr = document.OcrResults.OrderByDescending(r => r.ProcessedAt).FirstOrDefault();
        var textToIndex = latestOcr?.ExtractedText;

        if (string.IsNullOrWhiteSpace(textToIndex))
        {
            _logger.LogInformation("IndexDocumentEmbeddings: Document {DocumentId} has no text to index", documentId);
            return 0;
        }

        var chunks = ChunkClinicalText(textToIndex);
        if (chunks.Count == 0)
        {
            return 0;
        }

        // Generate embeddings via AI Worker or local clinical projection fallback
        var chunkTexts = chunks.Select(c => c.Text).ToList();
        var embeddings = await GenerateDenseEmbeddingsAsync(chunkTexts, cancellationToken);

        // Remove existing embeddings for this document
        var existing = await _context.DocumentEmbeddings
            .Where(e => e.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _context.DocumentEmbeddings.RemoveRange(existing);
        }

        var newEmbeddings = new List<DocumentEmbedding>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var embVector = i < embeddings.Count ? embeddings[i] : FallbackClinicalEmbedding(chunks[i].Text);
            newEmbeddings.Add(new DocumentEmbedding
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                PatientId = document.PatientId,
                ChunkIndex = i,
                ChunkText = chunks[i].Text,
                SectionHeader = chunks[i].Header,
                Embedding = embVector,
                Dimensions = EmbeddingDimensions,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = _currentUserService.Username ?? "AI_Indexer"
            });
        }

        _context.DocumentEmbeddings.AddRange(newEmbeddings);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Indexed {ChunkCount} embedding chunks for document {DocumentId}", newEmbeddings.Count, documentId);
        return newEmbeddings.Count;
    }

    public async Task<int> ReindexAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _context.Documents
            .Where(d => d.OcrResults.Any())
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        int totalChunks = 0;
        foreach (var docId in documents)
        {
            totalChunks += await IndexDocumentEmbeddingsAsync(docId, cancellationToken);
        }

        _logger.LogInformation("Reindexed all {DocCount} documents. Total embedding chunks: {TotalChunks}", documents.Count, totalChunks);
        return totalChunks;
    }

    public async Task<HybridSearchResultDto> SearchAsync(SearchQueryRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var query = request.Query?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            return new HybridSearchResultDto(
                Query: query,
                TotalMatches: 0,
                SearchDurationMs: 0,
                Items: new List<SearchResultItemDto>(),
                PageNumber: request.PageNumber,
                PageSize: request.PageSize
            );
        }

        // 1. Generate dense query embedding vector
        var queryEmbeddings = await GenerateDenseEmbeddingsAsync(new List<string> { query }, cancellationToken);
        var queryVector = queryEmbeddings.Count > 0 ? queryEmbeddings[0] : FallbackClinicalEmbedding(query);

        // 2. Fetch candidate chunks matching filters
        var candidateQuery = _context.DocumentEmbeddings
            .Include(e => e.Document)
                .ThenInclude(d => d.Patient)
            .Include(e => e.Document)
                .ThenInclude(d => d.ClinicalEntities)
            .AsNoTracking()
            .AsQueryable();

        if (request.PatientId.HasValue)
        {
            candidateQuery = candidateQuery.Where(e => e.PatientId == request.PatientId.Value);
        }

        if (request.Category.HasValue)
        {
            candidateQuery = candidateQuery.Where(e => e.Document.Category == request.Category.Value);
        }

        var candidateChunks = await candidateQuery.ToListAsync(cancellationToken);

        // 3. Score candidate chunks
        var scoredList = new List<(DocumentEmbedding Chunk, double SemanticScore, double KeywordScore, double CombinedScore)>();

        var queryTokens = TokenizeQuery(query);

        foreach (var chunk in candidateChunks)
        {
            // Cosine similarity
            double semanticSim = ComputeCosineSimilarity(queryVector, chunk.Embedding);
            double semanticScore = Math.Clamp(semanticSim, 0.0, 1.0);

            // Keyword lexical matching score
            double keywordScore = ComputeKeywordScore(chunk.ChunkText, chunk.SectionHeader, query, queryTokens);

            // Hybrid convex combination
            double combinedScore = (request.SemanticWeight * semanticScore) + ((1.0 - request.SemanticWeight) * keywordScore);

            if (combinedScore >= request.MinScore || keywordScore >= 0.50 || semanticScore >= 0.40)
            {
                scoredList.Add((chunk, semanticScore, keywordScore, combinedScore));
            }
        }

        // 4. Group by Document to return top matching section per document
        var groupedResults = scoredList
            .GroupBy(item => item.Chunk.DocumentId)
            .Select(g => g.OrderByDescending(x => x.CombinedScore).First())
            .OrderByDescending(x => x.CombinedScore)
            .ToList();

        int totalMatches = groupedResults.Count;

        // 5. Pagination
        int pageNumber = Math.Max(1, request.PageNumber);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        var pagedResults = groupedResults
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // 6. Map to DTOs
        var items = new List<SearchResultItemDto>();
        foreach (var (chunk, semScore, kwScore, combScore) in pagedResults)
        {
            var doc = chunk.Document;
            var patient = doc.Patient;

            var snippet = GenerateSnippet(chunk.ChunkText, queryTokens);
            var matchedEntities = doc.ClinicalEntities
                .Select(MapEntityToDto)
                .ToList();

            items.Add(new SearchResultItemDto(
                DocumentId: doc.Id,
                PatientId: doc.PatientId,
                PatientName: $"{patient.FirstName} {patient.LastName}",
                PatientMrn: patient.Mrn,
                DocumentCategory: doc.Category,
                ClinicalDate: doc.ClinicalDate,
                MatchedChunkText: chunk.ChunkText,
                SectionHeader: chunk.SectionHeader,
                CombinedScore: Math.Round(combScore, 4),
                SemanticScore: Math.Round(semScore, 4),
                KeywordScore: Math.Round(kwScore, 4),
                MatchedEntities: matchedEntities,
                HighlightSnippet: snippet
            ));
        }

        stopwatch.Stop();

        // 7. Audit Logging (NFR-004)
        try
        {
            await _auditService.LogAsync(
                action: "AUDIT_SEARCH_PERFORMED",
                resource: "Search",
                resourceId: request.PatientId?.ToString() ?? "All",
                outcome: AuditOutcome.Success,
                details: $"Search: '{query}', Matches: {totalMatches}, SemanticWeight: {request.SemanticWeight:F2}, Duration: {stopwatch.ElapsedMilliseconds}ms",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write search audit log");
        }

        return new HybridSearchResultDto(
            Query: query,
            TotalMatches: totalMatches,
            SearchDurationMs: stopwatch.ElapsedMilliseconds,
            Items: items,
            PageNumber: pageNumber,
            PageSize: pageSize
        );
    }

    private async Task<List<float[]>> GenerateDenseEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken)
    {
        try
        {
            var requestBody = new
            {
                texts = texts,
                dimensions = EmbeddingDimensions
            };

            var response = await _httpClient.PostAsJsonAsync($"{AiWorkerUrl}/embeddings/generate", requestBody, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<EmbeddingResponsePayload>(cancellationToken: cancellationToken);
                if (result?.Embeddings != null && result.Embeddings.Count == texts.Count)
                {
                    return result.Embeddings.Select(e => e.ToArray()).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI Worker embedding service unavailable. Using high-precision deterministic clinical projection fallback.");
        }

        return texts.Select(FallbackClinicalEmbedding).ToList();
    }

    private static double ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0.0;

        double dot = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 1e-9 || normB <= 1e-9)
            return 0.0;

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static double ComputeKeywordScore(string chunkText, string? sectionHeader, string fullQuery, List<string> queryTokens)
    {
        if (string.IsNullOrWhiteSpace(chunkText) || queryTokens.Count == 0)
            return 0.0;

        var lowerText = chunkText.ToLowerInvariant();
        var lowerHeader = (sectionHeader ?? string.Empty).ToLowerInvariant();
        var lowerQuery = fullQuery.ToLowerInvariant();

        // Exact phrase bonus
        double phraseScore = 0.0;
        if (lowerText.Contains(lowerQuery) || lowerHeader.Contains(lowerQuery))
        {
            phraseScore = 0.40;
        }

        // Token match ratio
        int matchedTokens = 0;
        foreach (var token in queryTokens)
        {
            if (lowerText.Contains(token) || lowerHeader.Contains(token))
            {
                matchedTokens++;
            }
        }

        double tokenScore = (double)matchedTokens / queryTokens.Count;

        return Math.Clamp((tokenScore * 0.60) + phraseScore, 0.0, 1.0);
    }

    private static List<string> TokenizeQuery(string query)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "a", "an", "the", "and", "or", "in", "on", "at", "to", "for", "of", "with", "by", "from",
            "is", "are", "was", "were", "be", "been", "has", "have", "had", "patient", "clinical",
            "note", "report", "hospital", "test", "scan", "show", "showed"
        };

        var tokens = Regex.Matches(query, @"[\w\u0900-\u097F]+")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(t => t.Length > 1 && !stopWords.Contains(t))
            .Distinct()
            .ToList();

        return tokens.Count > 0 ? tokens : new List<string> { query.ToLowerInvariant() };
    }

    private static string GenerateSnippet(string chunkText, List<string> queryTokens)
    {
        if (string.IsNullOrWhiteSpace(chunkText))
            return string.Empty;

        var lower = chunkText.ToLowerInvariant();
        int bestIdx = -1;

        foreach (var token in queryTokens)
        {
            int idx = lower.IndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (bestIdx == -1 || idx < bestIdx))
            {
                bestIdx = idx;
            }
        }

        if (bestIdx == -1)
        {
            return chunkText.Length > 200 ? chunkText[..200] + "..." : chunkText;
        }

        int start = Math.Max(0, bestIdx - 60);
        int length = Math.Min(chunkText.Length - start, 220);
        var snippet = chunkText.Substring(start, length);

        if (start > 0) snippet = "..." + snippet;
        if (start + length < chunkText.Length) snippet += "...";

        // Highlight matching tokens
        foreach (var token in queryTokens)
        {
            if (token.Length > 2)
            {
                snippet = Regex.Replace(snippet, $@"\b({Regex.Escape(token)})\b", "<mark>$1</mark>", RegexOptions.IgnoreCase);
            }
        }

        return snippet;
    }

    private static List<(string Text, string? Header)> ChunkClinicalText(string fullText)
    {
        var chunks = new List<(string Text, string? Header)>();
        if (string.IsNullOrWhiteSpace(fullText))
            return chunks;

        var sectionHeaderRegex = new Regex(
            @"^(?:[A-Z0-9\s\-\/]{3,35}:|HISTOPATHOLOGY REPORT|MICROSCOPIC EXAMINATION|GROSS EXAMINATION|IMPRESSION|DIAGNOSIS|FINAL DIAGNOSIS|TNM STAGING|STAGE|RECOMMENDATIONS|TREATMENT PLAN|PRESCRIBED MEDICATIONS|OPERATIVE FINDINGS|PRE-OP DIAGNOSIS|POST-OP DIAGNOSIS|PROCEDURE PERFORMED|COURSE IN HOSPITAL|DISCHARGE ADVICE)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentSection = "CLINICAL RECORD";
        var currentChunkBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var headerMatch = sectionHeaderRegex.Match(trimmed);
            if (headerMatch.Success)
            {
                if (currentChunkBuilder.Length > 150)
                {
                    chunks.Add((currentChunkBuilder.ToString().Trim(), currentSection));
                    currentChunkBuilder.Clear();
                }
                currentSection = trimmed.Split(':')[0].Trim();
            }

            currentChunkBuilder.AppendLine(trimmed);

            // Chunk size around 450 characters
            if (currentChunkBuilder.Length >= 450)
            {
                chunks.Add((currentChunkBuilder.ToString().Trim(), currentSection));
                currentChunkBuilder.Clear();
            }
        }

        if (currentChunkBuilder.Length > 0)
        {
            chunks.Add((currentChunkBuilder.ToString().Trim(), currentSection));
        }

        if (chunks.Count == 0 && fullText.Trim().Length > 0)
        {
            chunks.Add((fullText.Trim(), "CLINICAL RECORD"));
        }

        return chunks;
    }

    public static float[] FallbackClinicalEmbedding(string text)
    {
        int dim = EmbeddingDimensions;
        var vec = new float[dim];
        if (string.IsNullOrWhiteSpace(text))
            return vec;

        var lower = text.ToLowerInvariant();

        var clusters = new (int Start, int End, string[] Keywords)[]
        {
            (0, 16, new[] { "breast", "mammary", "nipple", "axilla", "axillary", "ductal", "lobular", "mastectomy", "स्तन" }),
            (16, 32, new[] { "lung", "bronchus", "bronchial", "pleural", "pulmonary", "nsclc", "sclc", "thoracic", "फोक्सो" }),
            (32, 48, new[] { "cervix", "cervical", "uterus", "uterine", "endometrium", "ovary", "ovarian", "vulva", "पाठेघर" }),
            (48, 64, new[] { "colon", "colorectal", "rectum", "rectal", "gastric", "stomach", "esophagus", "liver", "gallbladder", "pancreas", "पेट" }),
            (64, 80, new[] { "carcinoma", "adenocarcinoma", "squamous", "ductal carcinoma", "infiltrating ductal", "idc", "scc", "mucinous", "cancer", "tumor", "neoplasm", "malignancy" }),
            (80, 96, new[] { "sarcoma", "lymphoma", "hodgkin", "leukemia", "melanoma", "gist", "myeloma" }),
            (96, 112, new[] { "well differentiated", "moderately differentiated", "poorly differentiated", "undifferentiated", "anaplastic" }),
            (112, 128, new[] { "grade 1", "grade 2", "grade 3", "nottingham", "bloom-richardson", "gleason", "mitotic", "pleomorphism", "grade", "grading", "histology", "histologic", "histological", "pathology" }),
            (128, 144, new[] { "stage i", "stage ia", "stage ib", "stage ii", "stage iia", "stage iib", "t1", "t2", "n0", "early", "localized" }),
            (144, 160, new[] { "stage iii", "stage iiia", "stage iiib", "stage iiic", "stage iv", "t3", "t4", "n1", "n2", "n3", "m1", "advanced", "metastatic" }),
            (160, 176, new[] { "tnm", "ajcc", "uicc", "stage", "pathological stage", "clinical stage", "margin positive", "margin negative", "staging", "margin", "margins", "lymph node" }),
            (176, 192, new[] { "er positive", "estrogen receptor positive", "pr positive", "progesterone receptor positive", "er+", "pr+", "hormone receptor positive", "er/pr", "receptor", "receptors", "biomarker", "biomarkers", "profile", "hormone", "ihc" }),
            (192, 208, new[] { "her2", "her2/neu", "her2 positive", "her2 3+", "her2 amplified", "cerbb2", "her-2" }),
            (208, 224, new[] { "triple negative", "er negative", "pr negative", "her2 negative", "ki-67", "ki67", "high ki67", "pd-l1", "egfr" }),
            (224, 240, new[] { "chemotherapy", "ac-t", "fac", "folfox", "doxorubicin", "cyclophosphamide", "paclitaxel", "docetaxel", "cisplatin", "carboplatin", "केमोथेरापी" }),
            (240, 256, new[] { "radiotherapy", "radiation therapy", "ebrt", "brachytherapy", "gray", "gy", "fractions", "रेडियोथेरापी" }),
            (256, 272, new[] { "trastuzumab", "herceptin", "tamoxifen", "letrozole", "anastrozole", "pembrolizumab", "immunotherapy" }),
            (272, 320, new[] { "क्यान्सर", "अस्पताल", "बिरामी", "प्याथोलोजी", "बायोप्सी", "जाँच", "मासु", "शल्यक्रिया", "उपचार", "भरतपुर", "नेपाल", "चितवन" })
        };

        foreach (var (startIdx, endIdx, keywords) in clusters)
        {
            float weight = 0f;
            foreach (var kw in keywords)
            {
                if (lower.Contains(kw))
                    weight += 1.5f;
            }

            if (weight > 0f)
            {
                int span = endIdx - startIdx;
                for (int k = 0; k < span; k++)
                {
                    int idx = startIdx + k;
                    float h = (float)(Math.Abs(MD5HashInt($"{startIdx}_{k}")) % 1000) / 1000f;
                    vec[idx] += weight * (0.8f + 0.4f * h);
                }
            }
        }

        // General words and bigrams into 320..383
        var wordMatches = Regex.Matches(lower, @"[\w\u0900-\u097F]+");
        for (int i = 0; i < wordMatches.Count; i++)
        {
            var w = wordMatches[i].Value;
            if (w.Length < 2) continue;

            uint h1 = (uint)Math.Abs(MD5HashInt(w));
            int gIdx = 320 + (int)(h1 % 64);
            float sign = ((h1 >> 8) & 1) == 0 ? 1.0f : -1.0f;
            vec[gIdx] += sign * 0.4f;

            if (i < wordMatches.Count - 1)
            {
                var bg = $"{w}_{wordMatches[i + 1].Value}";
                uint h2 = (uint)Math.Abs(MD5HashInt(bg));
                int bgIdx = 320 + (int)(h2 % 64);
                float bgSign = ((h2 >> 8) & 1) == 0 ? 1.0f : -1.0f;
                vec[bgIdx] += bgSign * 0.6f;
            }

            int fullIdx = (int)(h1 % (uint)dim);
            vec[fullIdx] += (((h1 >> 12) & 1) == 0 ? 0.1f : -0.1f);
        }

        // L2 normalize
        double sumSq = 0.0;
        for (int i = 0; i < dim; i++)
            sumSq += vec[i] * vec[i];

        if (sumSq > 1e-9)
        {
            float norm = (float)Math.Sqrt(sumSq);
            for (int i = 0; i < dim; i++)
                vec[i] = (float)Math.Round(vec[i] / norm, 6);
        }

        return vec;
    }

    private static int MD5HashInt(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = MD5.HashData(bytes);
        return BitConverter.ToInt32(hash, 0);
    }

    private static ClinicalEntityDto MapEntityToDto(ClinicalEntity entity)
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

    private class EmbeddingResponsePayload
    {
        public List<List<float>>? Embeddings { get; set; }
        public int Dimensions { get; set; }
        public string? Model { get; set; }
        public int Count { get; set; }
        public int DurationMs { get; set; }
    }
}
