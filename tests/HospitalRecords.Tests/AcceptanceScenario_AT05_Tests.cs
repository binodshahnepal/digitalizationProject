using HospitalRecords.Application.Common.Interfaces;
using HospitalRecords.Application.DTOs;
using HospitalRecords.Domain.Entities;
using HospitalRecords.Domain.Enums;
using HospitalRecords.Infrastructure.Persistence;
using HospitalRecords.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HospitalRecords.Tests;

public class AcceptanceScenario_AT05_Tests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly SearchService _searchService;

    public AcceptanceScenario_AT05_Tests()
    {
        var dbName = "BpkmchAT05_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        _context = new ApplicationDbContext(options);

        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("dr_adhikari");

        var httpClient = new HttpClient();
        var loggerMock = new Mock<ILogger<SearchService>>();

        _searchService = new SearchService(
            _context,
            httpClient,
            _auditServiceMock.Object,
            _currentUserServiceMock.Object,
            loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task Scenario1_TextChunking_And_EmbeddingIndexing_StoresNormalizedVectors()
    {
        // 1. Arrange Patient & Document
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-09101",
            FirstName = "Sunita",
            LastName = "Shrestha",
            DateOfBirth = new DateOnly(1978, 6, 12),
            Gender = Gender.Female,
            NationalId = "27-01-72-11982",
            District = "Chitwan",
            Municipality = "Bharatpur-10"
        };
        _context.Patients.Add(patient);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2026, 3, 15),
            Language = "en",
            Status = DocumentStatus.Indexed,
            CurrentVersionNumber = 1
        };
        _context.Documents.Add(doc);

        var ocrResult = new DocumentOcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            DocumentVersionId = Guid.NewGuid(),
            ExtractedText = @"B.P. KOIRALA MEMORIAL CANCER HOSPITAL
DEPARTMENT OF PATHOLOGY
HISTOPATHOLOGY REPORT

CLINICAL HISTORY:
48-year-old female presenting with right breast lump for 4 months.

GROSS EXAMINATION:
Received modified radical mastectomy specimen measuring 18x14x6 cm with overlying ellipse of skin.
Cut section reveals a firm, greyish-white irregular mass measuring 3.5 x 2.8 cm in upper outer quadrant.

MICROSCOPIC EXAMINATION:
Sections show infiltrating ductal carcinoma (NOS) of breast.
Nottingham Histologic Grade II (Tubule formation 3, Nuclear pleomorphism 2, Mitotic score 1, Total score 6/9).
Surgical margins are free of tumor. 2 out of 14 axillary lymph nodes show metastatic deposits.

FINAL DIAGNOSIS:
Invasive Ductal Carcinoma, Right Breast, Grade II.
pT2 N1a M0 (AJCC Stage IIB).
Immunohistochemistry: ER positive (85%), PR positive (70%), HER2 score 1+ (negative), Ki-67 index 22%.",
            OcrConfidenceScore = 0.96,
            LanguageDetected = "en",
            PageCount = 1,
            WordCount = 145,
            LowConfidenceWarning = false,
            ProcessedAt = DateTimeOffset.UtcNow
        };
        _context.DocumentOcrResults.Add(ocrResult);
        await _context.SaveChangesAsync();

        // 2. Act: Index Document Embeddings
        int chunksIndexed = await _searchService.IndexDocumentEmbeddingsAsync(doc.Id);

        // 3. Assert
        Assert.True(chunksIndexed > 0, "At least one chunk should be generated and indexed");

        var embeddings = await _context.DocumentEmbeddings
            .Where(e => e.DocumentId == doc.Id)
            .OrderBy(e => e.ChunkIndex)
            .ToListAsync();

        Assert.Equal(chunksIndexed, embeddings.Count);

        foreach (var emb in embeddings)
        {
            Assert.Equal(patient.Id, emb.PatientId);
            Assert.Equal(384, emb.Dimensions);
            Assert.Equal(384, emb.Embedding.Length);
            Assert.False(string.IsNullOrWhiteSpace(emb.ChunkText));

            // Verify L2 norm is ~1.0
            double norm = Math.Sqrt(emb.Embedding.Sum(x => x * x));
            Assert.InRange(norm, 0.99, 1.01);
        }
    }

    [Fact]
    public async Task Scenario2_SemanticSearch_RetrievesConceptualMatches_WithoutExactKeywords()
    {
        // 1. Patient A: Breast cancer
        var patientA = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-BREAST-01",
            FirstName = "Asha",
            LastName = "Gurung",
            DateOfBirth = new DateOnly(1980, 4, 10),
            Gender = Gender.Female,
            NationalId = "27-01-72-88761",
            District = "Kaski",
            Municipality = "Pokhara-8"
        };
        _context.Patients.Add(patientA);

        var docA = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patientA.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2026, 2, 10),
            Language = "en",
            Status = DocumentStatus.Indexed
        };
        _context.Documents.Add(docA);

        _context.DocumentOcrResults.Add(new DocumentOcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = docA.Id,
            DocumentVersionId = Guid.NewGuid(),
            ExtractedText = "Infiltrating ductal carcinoma of right breast, Nottingham grade 2, ER positive, PR positive, HER2 3+ positive.",
            ProcessedAt = DateTimeOffset.UtcNow
        });

        // 2. Patient B: Cervical cancer
        var patientB = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-CERVIX-02",
            FirstName = "Mina",
            LastName = "Tamang",
            DateOfBirth = new DateOnly(1975, 8, 20),
            Gender = Gender.Female,
            NationalId = "27-01-72-99881",
            District = "Makwanpur",
            Municipality = "Hetauda-4"
        };
        _context.Patients.Add(patientB);

        var docB = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patientB.Id,
            Category = DocumentCategory.DischargeSummary,
            ClinicalDate = new DateOnly(2026, 2, 15),
            Language = "en",
            Status = DocumentStatus.Indexed
        };
        _context.Documents.Add(docB);

        _context.DocumentOcrResults.Add(new DocumentOcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = docB.Id,
            DocumentVersionId = Guid.NewGuid(),
            ExtractedText = "Patient admitted for squamous cell carcinoma of cervix stage IIIB, receiving concurrent chemoradiotherapy.",
            ProcessedAt = DateTimeOffset.UtcNow
        });

        // 3. Patient C: Leg fracture
        var patientC = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-TRAUMA-03",
            FirstName = "Ramesh",
            LastName = "Thapa",
            DateOfBirth = new DateOnly(1990, 1, 15),
            Gender = Gender.Male,
            NationalId = "27-01-72-55443",
            District = "Chitwan",
            Municipality = "Bharatpur-1"
        };
        _context.Patients.Add(patientC);

        var docC = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patientC.Id,
            Category = DocumentCategory.RadiologyReport,
            ClinicalDate = new DateOnly(2026, 2, 20),
            Language = "en",
            Status = DocumentStatus.Indexed
        };
        _context.Documents.Add(docC);

        _context.DocumentOcrResults.Add(new DocumentOcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = docC.Id,
            DocumentVersionId = Guid.NewGuid(),
            ExtractedText = "Closed transverse fracture of left tibial shaft following road traffic accident. Cast applied.",
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync();

        // Index all documents
        await _searchService.IndexDocumentEmbeddingsAsync(docA.Id);
        await _searchService.IndexDocumentEmbeddingsAsync(docB.Id);
        await _searchService.IndexDocumentEmbeddingsAsync(docC.Id);

        // Act: Search with conceptual query
        var searchReq = new SearchQueryRequest(
            Query: "mammary neoplasm hormone receptor positive",
            SemanticWeight: 0.80,
            MinScore: 0.15
        );

        var searchResult = await _searchService.SearchAsync(searchReq);

        // Assert
        Assert.NotEmpty(searchResult.Items);
        var topResult = searchResult.Items[0];
        Assert.Equal(docA.Id, topResult.DocumentId);
        Assert.Equal(patientA.Id, topResult.PatientId);
        Assert.True(topResult.SemanticScore > 0.40, $"Expected strong semantic score for breast pathology, got {topResult.SemanticScore}");
        Assert.True(topResult.CombinedScore > topResult.KeywordScore, "Semantic score boosted the conceptual match");

        // Verify fracture document is not the top result
        Assert.DoesNotContain(searchResult.Items.Take(1), item => item.DocumentId == docC.Id);
    }

    [Fact]
    public async Task Scenario3_HybridCombination_Weights_Semantic_And_Keyword_Accurately()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-HYBRID-01",
            FirstName = "Kamala",
            LastName = "Karki",
            DateOfBirth = new DateOnly(1982, 11, 5),
            Gender = Gender.Female,
            NationalId = "27-01-72-44123",
            District = "Tanahun",
            Municipality = "Damauli-2"
        };
        _context.Patients.Add(patient);

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2026, 1, 10),
            Status = DocumentStatus.Indexed
        };
        _context.Documents.Add(doc);

        _context.DocumentOcrResults.Add(new DocumentOcrResult
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            DocumentVersionId = Guid.NewGuid(),
            ExtractedText = "DIAGNOSIS: Infiltrating ductal carcinoma of breast, Nottingham Grade II, ER positive.",
            ProcessedAt = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync();
        await _searchService.IndexDocumentEmbeddingsAsync(doc.Id);

        // 1. Search with pure semantic weight (1.0)
        var semResult = await _searchService.SearchAsync(new SearchQueryRequest(
            Query: "breast ductal carcinoma",
            SemanticWeight: 1.0,
            MinScore: 0.10
        ));

        // 2. Search with pure keyword weight (0.0)
        var kwResult = await _searchService.SearchAsync(new SearchQueryRequest(
            Query: "breast ductal carcinoma",
            SemanticWeight: 0.0,
            MinScore: 0.10
        ));

        // 3. Search with balanced hybrid (0.6)
        var hybridResult = await _searchService.SearchAsync(new SearchQueryRequest(
            Query: "breast ductal carcinoma",
            SemanticWeight: 0.6,
            MinScore: 0.10
        ));

        Assert.NotEmpty(semResult.Items);
        Assert.NotEmpty(kwResult.Items);
        Assert.NotEmpty(hybridResult.Items);

        var item = hybridResult.Items[0];
        double expectedCombined = (0.6 * item.SemanticScore) + (0.4 * item.KeywordScore);
        Assert.Equal(expectedCombined, item.CombinedScore, precision: 2);

        // Highlight snippet check
        Assert.Contains("<mark>", item.HighlightSnippet);
    }

    [Fact]
    public async Task Scenario4_PatientScopedSearch_IsolatesResultsToTargetPatient()
    {
        // Patient 1
        var p1 = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-P1",
            FirstName = "P1",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1985, 1, 1),
            Gender = Gender.Female,
            NationalId = "27-01-72-00001",
            District = "Chitwan",
            Municipality = "Bharatpur"
        };
        var d1 = new Document { Id = Guid.NewGuid(), PatientId = p1.Id, Category = DocumentCategory.PathologyLabReport, ClinicalDate = new DateOnly(2026, 1, 1), Status = DocumentStatus.Indexed };
        var o1 = new DocumentOcrResult { Id = Guid.NewGuid(), DocumentId = d1.Id, DocumentVersionId = Guid.NewGuid(), ExtractedText = "Biopsy showing breast carcinoma.", ProcessedAt = DateTimeOffset.UtcNow };

        // Patient 2
        var p2 = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-P2",
            FirstName = "P2",
            LastName = "Patient",
            DateOfBirth = new DateOnly(1986, 2, 2),
            Gender = Gender.Female,
            NationalId = "27-01-72-00002",
            District = "Chitwan",
            Municipality = "Bharatpur"
        };
        var d2 = new Document { Id = Guid.NewGuid(), PatientId = p2.Id, Category = DocumentCategory.PathologyLabReport, ClinicalDate = new DateOnly(2026, 1, 2), Status = DocumentStatus.Indexed };
        var o2 = new DocumentOcrResult { Id = Guid.NewGuid(), DocumentId = d2.Id, DocumentVersionId = Guid.NewGuid(), ExtractedText = "Biopsy showing breast carcinoma.", ProcessedAt = DateTimeOffset.UtcNow };

        _context.Patients.AddRange(p1, p2);
        _context.Documents.AddRange(d1, d2);
        _context.DocumentOcrResults.AddRange(o1, o2);
        await _context.SaveChangesAsync();

        await _searchService.IndexDocumentEmbeddingsAsync(d1.Id);
        await _searchService.IndexDocumentEmbeddingsAsync(d2.Id);

        // Act: Search scoped strictly to Patient 1
        var result = await _searchService.SearchAsync(new SearchQueryRequest(
            Query: "breast carcinoma",
            PatientId: p1.Id
        ));

        // Assert
        Assert.All(result.Items, item => Assert.Equal(p1.Id, item.PatientId));
        Assert.DoesNotContain(result.Items, item => item.PatientId == p2.Id);
    }

    [Fact]
    public async Task Scenario5_AuditLog_RecordedForSearchOperations()
    {
        var result = await _searchService.SearchAsync(new SearchQueryRequest(Query: "oncology follow up"));

        _auditServiceMock.Verify(a => a.LogAsync(
            "AUDIT_SEARCH_PERFORMED",
            "Search",
            It.IsAny<string>(),
            AuditOutcome.Success,
            It.Is<string>(d => d.Contains("oncology follow up")),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
