using HospitalRecords.Domain.Common;
using HospitalRecords.Domain.Enums;

namespace HospitalRecords.Domain.Entities;

public class ClinicalEntity : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public Guid? EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public ClinicalEntityType EntityType { get; set; } = ClinicalEntityType.Diagnosis;

    /// <summary>
    /// Higher-level classification (e.g. "Oncology Pathology", "Systemic Chemotherapy", "Surgical Oncology", "Biomarker Profile")
    /// </summary>
    public string Category { get; set; } = "Oncology";

    /// <summary>
    /// Primary entity name (e.g., "Infiltrating Ductal Carcinoma", "T2N1M0", "ER Status", "AC-T Regimen")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Extracted value or clinical grade (e.g., "Positive (80%)", "Stage IIB", "Cycle 2 of 4", "Grade 2")
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Standardized medical classification code (e.g., ICD-O "8500/3", ICD-10 "C50.9", "T2N1M0")
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Standard medical terminology coding system (e.g., "ICD-O-3", "ICD-10", "AJCC-8", "RxNorm", "SNOMED-CT")
    /// </summary>
    public string? CodeSystem { get; set; }

    /// <summary>
    /// Model extraction confidence score (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; } = 1.0;

    /// <summary>
    /// Raw text passage from which this entity was extracted
    /// </summary>
    public string ExtractedText { get; set; } = string.Empty;

    /// <summary>
    /// Start character index in extracted document OCR text
    /// </summary>
    public int? TextOffsetStart { get; set; }

    /// <summary>
    /// End character index in extracted document OCR text
    /// </summary>
    public int? TextOffsetEnd { get; set; }

    /// <summary>
    /// Structured JSON dictionary storing specialized oncology fields (e.g., T/N/M components, receptor %, dosage mg/m2)
    /// </summary>
    public string AttributesJson { get; set; } = "{}";

    /// <summary>
    /// Clinical observation date or report date
    /// </summary>
    public DateOnly? ClinicalDate { get; set; }

    /// <summary>
    /// Clinician validation tracking (FR-EXT-002)
    /// </summary>
    public bool IsVerified { get; set; } = false;
    public string? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}
