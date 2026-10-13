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

public class AcceptanceScenario_AT04_Tests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAuditService> _auditServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly ClinicalEntityExtractionService _entityService;
    private readonly TimelineService _timelineService;

    public AcceptanceScenario_AT04_Tests()
    {
        var dbName = "BpkmchAT04_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        _context = new ApplicationDbContext(options);

        _auditServiceMock = new Mock<IAuditService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _currentUserServiceMock.Setup(s => s.Username).Returns("dr_sharma");

        var httpClient = new HttpClient();
        var loggerEntityMock = new Mock<ILogger<ClinicalEntityExtractionService>>();
        _entityService = new ClinicalEntityExtractionService(
            _context,
            _currentUserServiceMock.Object,
            _auditServiceMock.Object,
            httpClient,
            loggerEntityMock.Object);

        _timelineService = new TimelineService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task Scenario1_PathologyReport_Extracts_Diagnosis_Staging_Grade_And_Biomarkers()
    {
        // 1. Setup Patient
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-08810",
            FirstName = "Sita",
            LastName = "Dahal",
            DateOfBirth = new DateOnly(1974, 5, 12),
            Gender = Gender.Female,
            PhoneNumber = "9845012345",
            District = "Chitwan",
            IdentityProvenance = "OPD Desk",
            CreatedBy = "dr_sharma"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // 2. Realistic BPKMCH Histopathology Report Text
        var pathologyReportText = @"
B.P. KOIRALA MEMORIAL CANCER HOSPITAL
Department of Pathology & Laboratory Medicine
SURGICAL PATHOLOGY & IHC REPORT
Patient: Sita Dahal | Hospital No: BPKMCH-2026-08810 | Age: 52 F
Date: 2026-09-10

SPECIMEN: Right Modified Radical Mastectomy specimen.
HISTOPATHOLOGICAL DIAGNOSIS:
Infiltrating Ductal Carcinoma (NOS) of Right Breast.
Nottingham Histologic Grade: Grade 2 (moderately differentiated).
PATHOLOGICAL STAGE:
pT2 pN1 cM0, Stage IIB.

IMMUNOHISTOCHEMISTRY (IHC) PROFILE:
- Estrogen Receptor (ER): Positive (80%)
- Progesterone Receptor (PR): Positive (60%)
- HER2/neu: Negative (Score 1+)
- Ki-67 proliferation index: 30%
";

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2026, 9, 10),
            Status = DocumentStatus.Indexed,
            CreatedBy = "dr_sharma"
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        // 3. Extract and Store Entities
        var entities = await _entityService.ExtractAndStoreEntitiesAsync(doc.Id, pathologyReportText);

        // 4. Assertions
        Assert.NotEmpty(entities);
        Assert.True(entities.Count >= 7, $"Expected at least 7 entities, got {entities.Count}");

        // Check Diagnosis
        var diag = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.Diagnosis);
        Assert.NotNull(diag);
        Assert.Equal("Infiltrating Ductal Carcinoma", diag.Name);
        Assert.Equal("8500/3", diag.Code);
        Assert.Equal("ICD-O-3", diag.CodeSystem);

        // Check TNM
        var tnm = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.TnmStaging);
        Assert.NotNull(tnm);
        Assert.Contains("pT2", tnm.Value);
        Assert.Contains("pN1", tnm.Value);
        Assert.Contains("cM0", tnm.Value);

        // Check Overall Stage
        var stage = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.CancerStage);
        Assert.NotNull(stage);
        Assert.Equal("Stage IIB", stage.Value);

        // Check Grade
        var grade = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.HistologicGrade);
        Assert.NotNull(grade);
        Assert.Contains("Grade 2", grade.Value);

        // Check Biomarkers
        var er = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.Biomarker && e.Name.Contains("ER"));
        Assert.NotNull(er);
        Assert.Contains("Positive", er.Value);
        Assert.Contains("80%", er.Value);
        Assert.Equal("ER+", er.Code);

        var pr = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.Biomarker && e.Name.Contains("PR"));
        Assert.NotNull(pr);
        Assert.Contains("Positive", pr.Value);
        Assert.Contains("60%", pr.Value);

        var her2 = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.Biomarker && e.Name.Contains("HER2"));
        Assert.NotNull(her2);
        Assert.Contains("Negative", her2.Value);
        Assert.Contains("1+", her2.Value);

        var ki67 = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.Biomarker && e.Name.Contains("Ki-67"));
        Assert.NotNull(ki67);
        Assert.Equal("30%", ki67.Value);
    }

    [Fact]
    public async Task Scenario2_ChemotherapyProtocol_Extracts_Regimen_And_Cycles()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-08811",
            FirstName = "Rita",
            LastName = "Thapa",
            DateOfBirth = new DateOnly(1980, 2, 20),
            Gender = Gender.Female,
            PhoneNumber = "9845099999",
            District = "Nawalparasi",
            IdentityProvenance = "OPD Desk",
            CreatedBy = "dr_sharma"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var chemoText = @"
B.P. KOIRALA MEMORIAL CANCER HOSPITAL
MEDICAL ONCOLOGY DAY CARE UNIT
CHEMOTHERAPY PROTOCOL SHEET

Patient: Rita Thapa | Hospital No: BPKMCH-2026-08811
Diagnosis: Infiltrating Ductal Carcinoma, Stage IIB
Regimen: AC-T Regimen (Adjuvant Chemotherapy)
Current Cycle: Cycle 2 of 4
Rx:
1. Inj. Doxorubicin 60 mg/m2 IV
2. Inj. Cyclophosphamide 600 mg/m2 IV
";

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Category = DocumentCategory.Prescription,
            ClinicalDate = new DateOnly(2026, 9, 25),
            Status = DocumentStatus.Indexed,
            CreatedBy = "dr_sharma"
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var entities = await _entityService.ExtractAndStoreEntitiesAsync(doc.Id, chemoText);

        Assert.NotEmpty(entities);
        var regimen = entities.FirstOrDefault(e => e.EntityType == ClinicalEntityType.ChemotherapyRegimen);
        Assert.NotNull(regimen);
        Assert.Contains("AC-T", regimen.Name);
        Assert.Contains("Cycle 2", regimen.Value);
    }

    [Fact]
    public async Task Scenario3_LongitudinalTimeline_Synthesizes_Chronological_Patient_Journey()
    {
        // 1. Create Patient
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-08812",
            FirstName = "Hari",
            LastName = "Khadka",
            DateOfBirth = new DateOnly(1968, 8, 14),
            Gender = Gender.Male,
            PhoneNumber = "9845112233",
            District = "Makwanpur",
            IdentityProvenance = "OPD Desk",
            CreatedBy = "dr_sharma"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        // 2. Encounter 1: Initial OPD Consult (2026-08-01)
        var enc1 = new Encounter
        {
            Id = Guid.NewGuid(),
            EncounterNumber = "ENC-2026-00091",
            PatientId = patient.Id,
            Facility = "BPKMCH Main Campus",
            Department = "Surgical Oncology OPD",
            Type = EncounterType.OPD,
            StartDate = new DateTimeOffset(2026, 8, 1, 9, 30, 0, TimeSpan.Zero),
            AttendingClinicianName = "Dr. K. P. Sharma",
            ReasonForVisit = "Lump in right breast for 3 months",
            CreatedBy = "dr_sharma"
        };
        _context.Encounters.Add(enc1);
        await _context.SaveChangesAsync();

        // 3. Document 1: Biopsy & Pathology Report (2026-08-05)
        var doc1 = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            EncounterId = enc1.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2026, 8, 5),
            Status = DocumentStatus.Indexed,
            CreatedBy = "dr_sharma"
        };
        _context.Documents.Add(doc1);
        await _context.SaveChangesAsync();

        var pathologyText = @"
HISTOPATHOLOGY REPORT
Biopsy from lump: Infiltrating Ductal Carcinoma, pT2 pN1 cM0, Stage IIB. Grade 2.
ER: Positive (80%), PR: Positive (60%), HER2: Negative (Score 1+), Ki-67: 30%.
";
        await _entityService.ExtractAndStoreEntitiesAsync(doc1.Id, pathologyText);

        // 4. Encounter 2: Inpatient Admission for Surgery (2026-08-20)
        var enc2 = new Encounter
        {
            Id = Guid.NewGuid(),
            EncounterNumber = "ENC-2026-00092",
            PatientId = patient.Id,
            Facility = "BPKMCH Surgical Wing",
            Department = "Surgical Inpatient Ward",
            Type = EncounterType.Inpatient,
            StartDate = new DateTimeOffset(2026, 8, 20, 8, 0, 0, TimeSpan.Zero),
            AttendingClinicianName = "Dr. S. K. Shrestha",
            ReasonForVisit = "Elective Right MRM",
            CreatedBy = "dr_sharma"
        };
        _context.Encounters.Add(enc2);
        await _context.SaveChangesAsync();

        // 5. Document 2: Operative Note (2026-08-20)
        var doc2 = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            EncounterId = enc2.Id,
            Category = DocumentCategory.OperativeNote,
            ClinicalDate = new DateOnly(2026, 8, 20),
            Status = DocumentStatus.Indexed,
            CreatedBy = "dr_sharma"
        };
        _context.Documents.Add(doc2);
        await _context.SaveChangesAsync();

        var operativeText = @"
OPERATIVE NOTE
Procedure Performed: Right Modified Radical Mastectomy with Axillary Dissection
Surgeon: Dr. S. K. Shrestha
Surgical Margins: Free of tumor (> 5mm clearance).
";
        await _entityService.ExtractAndStoreEntitiesAsync(doc2.Id, operativeText);

        // 6. Document 3: Adjuvant Chemotherapy Cycle 1 (2026-09-10)
        var doc3 = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Category = DocumentCategory.Prescription,
            ClinicalDate = new DateOnly(2026, 9, 10),
            Status = DocumentStatus.Indexed,
            CreatedBy = "dr_sharma"
        };
        _context.Documents.Add(doc3);
        await _context.SaveChangesAsync();

        var chemoText = "Chemotherapy Prescription: AC-T Regimen, Cycle 1 of 4.";
        await _entityService.ExtractAndStoreEntitiesAsync(doc3.Id, chemoText);

        // 7. Request Synthesized Longitudinal Timeline
        var timeline = await _timelineService.GetPatientTimelineAsync(patient.Id);

        // 8. Assertions
        Assert.NotNull(timeline);
        Assert.Equal("Hari Khadka", timeline.PatientName);
        Assert.Equal("BPKMCH-2026-08812", timeline.Mrn);

        // Summary Header Cards
        Assert.NotNull(timeline.PrimaryDiagnosis);
        Assert.Equal("Infiltrating Ductal Carcinoma", timeline.PrimaryDiagnosis.Name);
        Assert.NotNull(timeline.ClinicalStage);
        Assert.Equal("Stage IIB", timeline.ClinicalStage.Value);
        Assert.NotEmpty(timeline.Biomarkers);

        // Chronological order assertion
        Assert.True(timeline.Events.Count >= 5, $"Expected at least 5 events, got {timeline.Events.Count}");
        for (int i = 0; i < timeline.Events.Count - 1; i++)
        {
            Assert.True(timeline.Events[i].EventDate <= timeline.Events[i + 1].EventDate,
                $"Events must be strictly chronological: {timeline.Events[i].EventDate} should precede {timeline.Events[i + 1].EventDate}");
        }

        // Milestone Presence Assertions
        var milestones = timeline.Events.Where(e => e.Milestone.HasValue).Select(e => e.Milestone!.Value).ToList();
        Assert.Contains(MilestoneType.InitialConsult, milestones);
        Assert.Contains(MilestoneType.DiagnosticBiopsy, milestones);
        Assert.Contains(MilestoneType.DefinitiveSurgery, milestones);
        Assert.Contains(MilestoneType.AdjuvantChemotherapy, milestones);
    }

    [Fact]
    public async Task Scenario4_Clinician_Can_Verify_And_Amend_Extracted_Entity()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            Mrn = "BPKMCH-2026-08813",
            FirstName = "Gita",
            LastName = "Gurung",
            DateOfBirth = new DateOnly(1985, 11, 23),
            Gender = Gender.Female,
            IdentityProvenance = "OPD Desk",
            CreatedBy = "dr_sharma"
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Category = DocumentCategory.PathologyLabReport,
            ClinicalDate = new DateOnly(2026, 9, 1),
            Status = DocumentStatus.Indexed,
            CreatedBy = "dr_sharma"
        };
        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();

        var entities = await _entityService.ExtractAndStoreEntitiesAsync(doc.Id, "Pathology: Stage IIB, ER: Positive");
        var erEntity = entities.First(e => e.EntityType == ClinicalEntityType.Biomarker);

        Assert.False(erEntity.IsVerified);

        // Clinician verifies entity
        var verifyReq = new VerifyClinicalEntityRequest(
            ConfirmedName: "Estrogen Receptor (ER Alpha)",
            ConfirmedValue: "Strong Positive (90%)",
            ConfirmedCode: "ER+",
            VerificationNotes: "Reviewed on high power field by Dr. Sharma"
        );

        var verified = await _entityService.VerifyEntityAsync(erEntity.Id, verifyReq);

        Assert.True(verified.IsVerified);
        Assert.Equal("dr_sharma", verified.VerifiedBy);
        Assert.NotNull(verified.VerifiedAt);
        Assert.Equal("Strong Positive (90%)", verified.Value);
        Assert.Equal("Estrogen Receptor (ER Alpha)", verified.Name);

        // Verify audit log call
        _auditServiceMock.Verify(a => a.LogAsync(
            "CLINICAL_ENTITY_VERIFIED",
            "ClinicalEntity",
            erEntity.Id.ToString(),
            AuditOutcome.Success,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
