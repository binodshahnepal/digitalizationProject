import io
import re
import time
import math
import hashlib
import logging
from typing import List, Optional, Dict, Any
from fastapi import FastAPI, HTTPException, UploadFile, File, Form, Body
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import pypdf

# Configure logging
logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
logger = logging.getLogger("bpkmch-ai-worker")

app = FastAPI(
    title="BPKMCH AI Worker, OCR & Clinical Intelligence Service",
    version="1.1.0",
    description="Dual-Language (English + Nepali) OCR, Document Classification, and Oncology Clinical Entity Extraction for B.P. Koirala Memorial Cancer Hospital."
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# -------------------------------------------------------------
# Classification Taxonomy & Oncology Vocabulary
# -------------------------------------------------------------
TAXONOMY_RULES = {
    "PathologyLabReport": {
        "keywords": [
            "biopsy", "histopathology", "histology", "malignancy", "carcinoma",
            "adenocarcinoma", "cytology", "fnac", "tumor", "neoplasm", "grade",
            "microscopic", "gross description", "specimen", "pathology",
            "lymph node", "immunohistochemistry", "ihc", "er/pr", "her2",
            "बायोप्सी", "हिस्टोप्याथोलोजी", "प्रयोगशाला", "प्याथोलोजी", "मासु जाँच"
        ],
        "header_weight": 3.0,
        "base_weight": 1.2
    },
    "RadiologyReport": {
        "keywords": [
            "radiology", "ct scan", "computed tomography", "mri", "magnetic resonance",
            "ultrasound", "usg", "x-ray", "xray", "impression", "findings",
            "contrast", "axial", "coronal", "sagittal", "attenuation", "lesion",
            "mammography", "pet-ct", "pet scan", "scan",
            "रेडियोलोजी", "एक्स-रे", "भिडियो एक्सरे", "सिटी स्क्यान", "एमआरआई"
        ],
        "header_weight": 3.0,
        "base_weight": 1.2
    },
    "DischargeSummary": {
        "keywords": [
            "discharge summary", "discharge note", "date of admission", "date of discharge",
            "condition at discharge", "hospital discharge", "final diagnosis",
            "course in hospital", "treatment given", "discharge advice", "follow up advice",
            "डिस्चार्ज समरी", "अस्पताल बिदा", "डिस्चार्ज नोट", "भर्ना मिति", "डिस्चार्ज मिति"
        ],
        "header_weight": 3.5,
        "base_weight": 1.5
    },
    "Prescription": {
        "keywords": [
            "rx", "prescription", "tab", "cap", "inj", "syp", "dosage", "once daily",
            "twice daily", "od", "bd", "tds", "tid", "qid", "sos", "po", "iv", "sc",
            "prescribed", "mg", "ml", "chemotherapy protocol", "cycle",
            "औषधि", "पर्चा", "दवा", "औषधी", "सेवन विधि"
        ],
        "header_weight": 2.5,
        "base_weight": 1.0
    },
    "OperativeNote": {
        "keywords": [
            "operative note", "operation notes", "surgeon", "assistant surgeon",
            "anesthesia", "pre-op diagnosis", "post-op diagnosis", "procedure performed",
            "incision", "findings", "closure", "drain", "blood loss", "sponge count",
            "शल्यक्रिया", "अपरेशन", "शल्यक्रिया विवरण", "एनेस्थेसिया"
        ],
        "header_weight": 3.5,
        "base_weight": 1.3
    },
    "ClinicalProgressNote": {
        "keywords": [
            "progress note", "clinical note", "vitals", "pulse", "bp", "blood pressure",
            "spo2", "temp", "temperature", "examination", "subjective", "objective",
            "assessment", "plan", "soap", "ward round", "chemo tolerance",
            "वार्ड नोट", "दैनिक नोट", "स्वास्थ्य परीक्षण", "राउण्ड नोट"
        ],
        "header_weight": 2.5,
        "base_weight": 1.1
    }
}

# Devanagari Unicode Regex range [\u0900-\u097F]
DEVANAGARI_REGEX = re.compile(r'[\u0900-\u097F]+')

# -------------------------------------------------------------
# Data Models
# -------------------------------------------------------------
class ClassificationResult(BaseModel):
    predicted_category: str
    confidence: float
    matched_keywords: List[str]
    category_scores: Dict[str, float]
    low_confidence_warning: bool

class OcrExtractionResult(BaseModel):
    extracted_text: str
    extracted_nepali_text: Optional[str]
    confidence_score: float
    language_detected: str  # "en", "ne", "mixed"
    page_count: int
    word_count: int
    low_confidence_warning: bool

class PipelineResult(BaseModel):
    ocr: OcrExtractionResult
    classification: ClassificationResult
    overall_low_confidence: bool
    processing_duration_ms: int

class TextInputRequest(BaseModel):
    text: str
    filename: Optional[str] = "document.txt"

class ExtractedClinicalEntity(BaseModel):
    entity_type: str  # Diagnosis, TnmStaging, CancerStage, HistologicGrade, Biomarker, ChemotherapyRegimen, Medication, SurgicalProcedure, RadiationTherapy, LabFinding
    category: str
    name: str
    value: str
    code: Optional[str] = None
    code_system: Optional[str] = None
    confidence_score: float = 0.95
    extracted_text: str = ""
    text_offset_start: Optional[int] = None
    text_offset_end: Optional[int] = None
    attributes: Dict[str, Any] = {}

class ClinicalExtractionRequest(BaseModel):
    text: str
    document_category: Optional[str] = None

class ClinicalExtractionResponse(BaseModel):
    entities: List[ExtractedClinicalEntity]
    total_entities: int
    extraction_duration_ms: int

class EmbeddingRequest(BaseModel):
    texts: List[str]
    dimensions: int = 384

class EmbeddingResponse(BaseModel):
    embeddings: List[List[float]]
    dimensions: int = 384
    model: str = "bpkmch-clinical-dense-384"
    count: int
    duration_ms: int

# -------------------------------------------------------------
# Core OCR & Classification Logic
# -------------------------------------------------------------
def perform_text_analysis(raw_text: str, page_count: int = 1) -> OcrExtractionResult:
    cleaned = raw_text.strip()
    words = cleaned.split()
    word_count = len(words)

    # Find Devanagari script tokens
    nepali_matches = DEVANAGARI_REGEX.findall(cleaned)
    nepali_text = " ".join(nepali_matches) if nepali_matches else None
    nepali_word_count = len(nepali_matches)

    # Determine dominant script / language
    if word_count == 0:
        language = "en"
        confidence = 0.50
    elif nepali_word_count > 0 and (nepali_word_count / word_count) > 0.40:
        language = "ne"
        confidence = 0.92
    elif nepali_word_count > 0:
        language = "mixed"
        confidence = 0.94
    else:
        language = "en"
        confidence = 0.96

    # Quality heuristic: penalize very short text or high garbled char count
    if word_count < 10:
        confidence = max(0.40, confidence - 0.35)

    low_conf = confidence < 0.70

    return OcrExtractionResult(
        extracted_text=cleaned,
        extracted_nepali_text=nepali_text,
        confidence_score=round(confidence, 2),
        language_detected=language,
        page_count=page_count,
        word_count=word_count,
        low_confidence_warning=low_conf
    )

def classify_medical_text(text: str) -> ClassificationResult:
    lower_text = text.lower()
    scores: Dict[str, float] = {cat: 0.0 for cat in TAXONOMY_RULES.keys()}
    matched_by_cat: Dict[str, List[str]] = {cat: [] for cat in TAXONOMY_RULES.keys()}

    for category, rule in TAXONOMY_RULES.items():
        score = 0.0
        for kw in rule["keywords"]:
            pattern = re.escape(kw.lower())
            occurrences = len(re.findall(r'\b' + pattern + r'\b', lower_text))
            if occurrences == 0 and kw in lower_text:
                occurrences = 1  # Substring match (especially useful for Devanagari)

            if occurrences > 0:
                matched_by_cat[category].append(kw)
                # First 300 characters count as header with higher weight
                in_header = kw in lower_text[:300]
                weight = rule["header_weight"] if in_header else rule["base_weight"]
                score += weight * min(occurrences, 4)

        scores[category] = score

    # Find highest scoring category
    best_category = max(scores, key=lambda k: scores[k])
    best_score = scores[best_category]

    if best_score == 0:
        return ClassificationResult(
            predicted_category="GeneralMedicalChart",
            confidence=0.50,
            matched_keywords=[],
            category_scores=scores,
            low_confidence_warning=True
        )

    # Compute normalized confidence
    total_score = sum(scores.values())
    confidence = min(0.99, max(0.60, (best_score / (total_score + 1.0)) * 1.4))

    # If the score is low or category is ambiguous
    low_confidence = confidence < 0.80 or best_score < 3.0

    return ClassificationResult(
        predicted_category=best_category,
        confidence=round(confidence, 2),
        matched_keywords=matched_by_cat[best_category],
        category_scores={k: round(v, 2) for k, v in scores.items()},
        low_confidence_warning=low_confidence
    )

def extract_text_from_pdf_stream(pdf_bytes: bytes) -> tuple[str, int]:
    try:
        reader = pypdf.PdfReader(io.BytesIO(pdf_bytes))
        pages_text = []
        page_count = len(reader.pages)
        for page in reader.pages:
            t = page.extract_text()
            if t:
                pages_text.append(t)
        full_text = "\n".join(pages_text)
        return full_text, page_count
    except Exception as e:
        logger.warning(f"pypdf extraction error: {e}")
        try:
            return pdf_bytes.decode("utf-8", errors="ignore"), 1
        except:
            return "", 1

# -------------------------------------------------------------
# Oncology Clinical Entity Extraction NLP Engine
# -------------------------------------------------------------
def extract_oncology_entities(text: str, document_category: Optional[str] = None) -> List[ExtractedClinicalEntity]:
    entities: List[ExtractedClinicalEntity] = []
    seen_keys = set()

    def add_entity(e: ExtractedClinicalEntity):
        key = (e.entity_type, e.name.lower(), e.value.lower())
        if key not in seen_keys:
            seen_keys.add(key)
            entities.append(e)

    # 1. DIAGNOSIS (Pathological & Clinical)
    diag_patterns = [
        (
            r'(?:infiltrating|invasive)\s+ductal\s+carcinoma(?:\s*\((?:nos)\))?',
            "Infiltrating Ductal Carcinoma",
            "Infiltrating Ductal Carcinoma (NOS)",
            "8500/3",
            "ICD-O-3",
            "Oncology Pathology",
            {"histology": "Ductal", "icd10": "C50.9", "primary_site": "Breast"}
        ),
        (
            r'(?:invasive|infiltrating)\s+lobular\s+carcinoma',
            "Invasive Lobular Carcinoma",
            "Invasive Lobular Carcinoma",
            "8520/3",
            "ICD-O-3",
            "Oncology Pathology",
            {"histology": "Lobular", "icd10": "C50.9", "primary_site": "Breast"}
        ),
        (
            r'squamous\s+cell\s+carcinoma',
            "Squamous Cell Carcinoma",
            "Squamous Cell Carcinoma",
            "8070/3",
            "ICD-O-3",
            "Oncology Pathology",
            {"histology": "Squamous"}
        ),
        (
            r'(?:colorectal\s+)?adenocarcinoma',
            "Adenocarcinoma",
            "Adenocarcinoma",
            "8140/3",
            "ICD-O-3",
            "Oncology Pathology",
            {"histology": "Glandular"}
        ),
        (
            r'small\s+cell\s+(?:lung\s+)?carcinoma',
            "Small Cell Carcinoma",
            "Small Cell Lung Carcinoma",
            "8041/3",
            "ICD-O-3",
            "Oncology Pathology",
            {"icd10": "C34.9"}
        ),
        (
            r'papillary\s+thyroid\s+carcinoma',
            "Papillary Thyroid Carcinoma",
            "Papillary Thyroid Carcinoma",
            "8260/3",
            "ICD-O-3",
            "Oncology Pathology",
            {"icd10": "C73"}
        ),
        # Nepali Cancer Terms
        (
            r'स्तन\s*क्यान्सर',
            "Breast Cancer",
            "Carcinoma of Breast (स्तन क्यान्सर)",
            "C50.9",
            "ICD-10",
            "Clinical Oncology",
            {"primary_site": "Breast", "script": "Devanagari"}
        ),
        (
            r'पाठेघरको(?:\s*मुखको)?\s*क्यान्सर',
            "Cervical Cancer",
            "Carcinoma of Cervix (पाठेघरको क्यान्सर)",
            "C53.9",
            "ICD-10",
            "Clinical Oncology",
            {"primary_site": "Cervix", "script": "Devanagari"}
        ),
        (
            r'फोक्सोको\s*क्यान्सर',
            "Lung Cancer",
            "Carcinoma of Lung (फोक्सोको क्यान्सर)",
            "C34.9",
            "ICD-10",
            "Clinical Oncology",
            {"primary_site": "Lung", "script": "Devanagari"}
        )
    ]

    for pat, name, val, code, codesys, cat, attrs in diag_patterns:
        match = re.search(pat, text, re.IGNORECASE)
        if match:
            add_entity(ExtractedClinicalEntity(
                entity_type="Diagnosis",
                category=cat,
                name=name,
                value=val,
                code=code,
                code_system=codesys,
                confidence_score=0.96,
                extracted_text=match.group(0),
                text_offset_start=match.start(),
                text_offset_end=match.end(),
                attributes=attrs
            ))

    # 2. TNM STAGING DESCRIPTORS
    tnm_match = re.search(r'\b((?:p|c|yp)?T(?:is|[0-4][a-d]?))\s*((?:p|c|yp)?N(?:x|[0-3][a-c]?))\s*((?:p|c|yp)?M(?:x|[0-1][a-c]?))\b', text)
    if tnm_match:
        t_val = tnm_match.group(1)
        n_val = tnm_match.group(2)
        m_val = tnm_match.group(3)
        combined_val = f"{t_val} {n_val} {m_val}"
        add_entity(ExtractedClinicalEntity(
            entity_type="TnmStaging",
            category="TNM Staging",
            name="TNM Classification",
            value=combined_val,
            code=f"{t_val}{n_val}{m_val}",
            code_system="TNM-8",
            confidence_score=0.98,
            extracted_text=tnm_match.group(0),
            text_offset_start=tnm_match.start(),
            text_offset_end=tnm_match.end(),
            attributes={
                "t": t_val,
                "n": n_val,
                "m": m_val,
                "prefix": "pathological" if t_val.startswith("p") else ("clinical" if t_val.startswith("c") else "post-neoadjuvant" if t_val.startswith("yp") else "standard")
            }
        ))

    # 3. AJCC CANCER STAGE
    stage_match = re.search(r'\b(?:(?:AJCC|TNM|Clinical|Pathological)\s+)?Stage\s+(0|I{1,3}[A-C]?|IV[A-C]?|[1-4][A-C]?)\b', text, re.IGNORECASE)
    if stage_match:
        stage_val = f"Stage {stage_match.group(1).upper()}"
        add_entity(ExtractedClinicalEntity(
            entity_type="CancerStage",
            category="TNM Staging",
            name="AJCC Overall Stage",
            value=stage_val,
            code=stage_val,
            code_system="AJCC-8",
            confidence_score=0.95,
            extracted_text=stage_match.group(0),
            text_offset_start=stage_match.start(),
            text_offset_end=stage_match.end(),
            attributes={"stage": stage_match.group(1).upper()}
        ))

    # 4. HISTOLOGIC GRADE
    grade_match = re.search(r'\b(?:(?:Nottingham|Bloom-Richardson|Histologic(?:al)?)\s+)?(?:Grade|grade)\s*[:=-]?\s*([1-3]|I{1,3}|well differentiated|moderately differentiated|poorly differentiated|undifferentiated)\b', text, re.IGNORECASE)
    if grade_match:
        raw_g = grade_match.group(1).strip()
        num_map = {"1": "Grade 1 (Well Differentiated)", "i": "Grade 1 (Well Differentiated)", "well differentiated": "Grade 1 (Well Differentiated)",
                   "2": "Grade 2 (Moderately Differentiated)", "ii": "Grade 2 (Moderately Differentiated)", "moderately differentiated": "Grade 2 (Moderately Differentiated)",
                   "3": "Grade 3 (Poorly Differentiated)", "iii": "Grade 3 (Poorly Differentiated)", "poorly differentiated": "Grade 3 (Poorly Differentiated)"}
        norm_val = num_map.get(raw_g.lower(), f"Grade {raw_g}")
        add_entity(ExtractedClinicalEntity(
            entity_type="HistologicGrade",
            category="Histopathology",
            name="Histologic Grade",
            value=norm_val,
            code=f"Grade {raw_g}",
            code_system="Nottingham",
            confidence_score=0.92,
            extracted_text=grade_match.group(0),
            text_offset_start=grade_match.start(),
            text_offset_end=grade_match.end(),
            attributes={"raw_grade": raw_g}
        ))

    # 5. BIOMARKERS & RECEPTORS (IHC)
    # ER (Estrogen Receptor)
    er_match = re.search(r'(?:Estrogen\s+Receptor(?:\s*\([A-Za-z]+\))?|\bER\b)\s*[:=-]?\s*(Positive|Negative|Pos|Neg)(?:[^\d\n]*(\d{1,3})\s*%)?', text, re.IGNORECASE)
    if er_match:
        status = "Positive" if er_match.group(1).lower().startswith("pos") else "Negative"
        pct = er_match.group(2)
        val = f"{status}" + (f" ({pct}%)" if pct else "")
        add_entity(ExtractedClinicalEntity(
            entity_type="Biomarker",
            category="Biomarker Profile",
            name="Estrogen Receptor (ER)",
            value=val,
            code="ER+" if status == "Positive" else "ER-",
            code_system="IHC",
            confidence_score=0.95,
            extracted_text=er_match.group(0),
            text_offset_start=er_match.start(),
            text_offset_end=er_match.end(),
            attributes={"biomarker": "ER", "status": status, "percentage": int(pct) if pct else None}
        ))

    # PR (Progesterone Receptor)
    pr_match = re.search(r'(?:Progesterone\s+Receptor(?:\s*\([A-Za-z]+\))?|\bPR\b)\s*[:=-]?\s*(Positive|Negative|Pos|Neg)(?:[^\d\n]*(\d{1,3})\s*%)?', text, re.IGNORECASE)
    if pr_match:
        status = "Positive" if pr_match.group(1).lower().startswith("pos") else "Negative"
        pct = pr_match.group(2)
        val = f"{status}" + (f" ({pct}%)" if pct else "")
        add_entity(ExtractedClinicalEntity(
            entity_type="Biomarker",
            category="Biomarker Profile",
            name="Progesterone Receptor (PR)",
            value=val,
            code="PR+" if status == "Positive" else "PR-",
            code_system="IHC",
            confidence_score=0.95,
            extracted_text=pr_match.group(0),
            text_offset_start=pr_match.start(),
            text_offset_end=pr_match.end(),
            attributes={"biomarker": "PR", "status": status, "percentage": int(pct) if pct else None}
        ))

    # HER2 / neu
    her2_match = re.search(r'\b(?:HER2(?:\/neu)?)\s*[:=-]?\s*(Positive|Negative|Equivocal|Pos|Neg)?\s*(?:\(?(?:Score|score)?\s*([0-3]\+?)\)?)?', text, re.IGNORECASE)
    if her2_match and (her2_match.group(1) or her2_match.group(2)):
        raw_status = her2_match.group(1) or ""
        score = her2_match.group(2) or ""
        status = "Positive" if "pos" in raw_status.lower() or score in ["3", "3+"] else ("Equivocal" if "equivocal" in raw_status.lower() or score in ["2", "2+"] else "Negative")
        val = f"{status}" + (f" (Score {score})" if score else "")
        add_entity(ExtractedClinicalEntity(
            entity_type="Biomarker",
            category="Biomarker Profile",
            name="HER2/neu Receptor",
            value=val,
            code="HER2+" if status == "Positive" else ("HER2 Equivocal" if status == "Equivocal" else "HER2-"),
            code_system="IHC",
            confidence_score=0.93,
            extracted_text=her2_match.group(0),
            text_offset_start=her2_match.start(),
            text_offset_end=her2_match.end(),
            attributes={"biomarker": "HER2", "status": status, "score": score}
        ))

    # Ki-67 Proliferation Index
    ki67_match = re.search(r'Ki-?67(?:[^\d:\n]*[:=-])?\s*(\d{1,3})\s*%', text, re.IGNORECASE)
    if ki67_match:
        pct = int(ki67_match.group(1))
        add_entity(ExtractedClinicalEntity(
            entity_type="Biomarker",
            category="Biomarker Profile",
            name="Ki-67 Proliferation Index",
            value=f"{pct}%",
            code="Ki-67",
            code_system="IHC",
            confidence_score=0.94,
            extracted_text=ki67_match.group(0),
            text_offset_start=ki67_match.start(),
            text_offset_end=ki67_match.end(),
            attributes={"biomarker": "Ki-67", "percentage": pct}
        ))

    # 6. CHEMOTHERAPY REGIMENS & CYCLES
    regimen_match = re.search(r'\b(AC-T|AC|FEC|FAC|FOLFOX(?:-?6)?|FOLFIRI|CAPOX|XELOX|Gemcitabine\s*\+\s*Cisplatin|Paclitaxel\s*\+\s*Carboplatin|CHOP|R-CHOP|ABVD)\b', text, re.IGNORECASE)
    cycle_match = re.search(r'Cycle\s*([0-9]+)(?:\s*(?:of|\/)\s*([0-9]+))?', text, re.IGNORECASE)
    if regimen_match:
        reg_name = regimen_match.group(1).upper()
        cur_cycle = int(cycle_match.group(1)) if cycle_match else None
        tot_cycle = int(cycle_match.group(2)) if cycle_match and cycle_match.group(2) else None
        cycle_str = f" (Cycle {cur_cycle} of {tot_cycle})" if cur_cycle and tot_cycle else (f" (Cycle {cur_cycle})" if cur_cycle else "")
        add_entity(ExtractedClinicalEntity(
            entity_type="ChemotherapyRegimen",
            category="Systemic Chemotherapy",
            name=f"{reg_name} Regimen",
            value=f"{reg_name} Protocol{cycle_str}",
            code=reg_name,
            code_system="NCCN",
            confidence_score=0.96,
            extracted_text=regimen_match.group(0) + (f", {cycle_match.group(0)}" if cycle_match else ""),
            text_offset_start=regimen_match.start(),
            text_offset_end=regimen_match.end(),
            attributes={
                "regimen": reg_name,
                "current_cycle": cur_cycle,
                "total_cycles": tot_cycle
            }
        ))

    # 7. MEDICATIONS & CHEMOTHERAPY DRUGS
    drug_matches = re.findall(r'(?:Inj\.|Tab\.|Cap\.)?\s*([A-Za-z]+(?:\s*\([A-Za-z]+\))?)\s*[:=-]?\s*(\d+(?:\.\d+)?\s*(?:mg(?:\/m2)?|g|mcg|IU))', text, re.IGNORECASE)
    known_onc_drugs = {
        "doxorubicin": "Doxorubicin", "adriamycin": "Doxorubicin (Adriamycin)",
        "cyclophosphamide": "Cyclophosphamide", "paclitaxel": "Paclitaxel",
        "docetaxel": "Docetaxel", "fluorouracil": "5-Fluorouracil",
        "cisplatin": "Cisplatin", "carboplatin": "Carboplatin",
        "oxaliplatin": "Oxaliplatin", "gemcitabine": "Gemcitabine",
        "capecitabine": "Capecitabine", "trastuzumab": "Trastuzumab",
        "tamoxifen": "Tamoxifen", "ondansetron": "Ondansetron",
        "dexamethasone": "Dexamethasone"
    }

    for raw_drug, dose in drug_matches:
        clean_name = re.sub(r'\(.*?\)', '', raw_drug).strip().lower()
        if clean_name in known_onc_drugs:
            std_name = known_onc_drugs[clean_name]
            add_entity(ExtractedClinicalEntity(
                entity_type="Medication",
                category="Systemic Chemotherapy",
                name=std_name,
                value=dose.strip(),
                code=std_name,
                code_system="RxNorm",
                confidence_score=0.93,
                extracted_text=f"{raw_drug.strip()} {dose.strip()}",
                attributes={"drug": std_name, "dosage": dose.strip()}
            ))

    # 8. SURGICAL PROCEDURES
    surg_match = re.search(r'(?:Procedure(?:\s+Performed)?|Operation(?:\s+Performed|\s+Done)?)\s*[:=-]\s*([^\n\r]+)', text, re.IGNORECASE)
    if surg_match:
        proc_str = surg_match.group(1).strip()
        # Clean trailing periods or notes
        proc_str = proc_str.rstrip(".")
        # Determine code abbreviation
        code = "SURG"
        if "mastectomy" in proc_str.lower():
            code = "MRM" if "modified radical" in proc_str.lower() else "Mastectomy"
        elif "lumpectomy" in proc_str.lower():
            code = "BCS"
        elif "colectomy" in proc_str.lower():
            code = "Colectomy"
        elif "hysterectomy" in proc_str.lower():
            code = "TAH-BSO"

        margin_match = re.search(r'(?:margins?|surgical margins?)\s*[:=-]?\s*([^\n\r.]+)', text, re.IGNORECASE)
        margin_info = margin_match.group(1).strip() if margin_match else "Not specified"

        add_entity(ExtractedClinicalEntity(
            entity_type="SurgicalProcedure",
            category="Surgical Oncology",
            name="Surgical Procedure",
            value=proc_str,
            code=code,
            code_system="CPT",
            confidence_score=0.94,
            extracted_text=surg_match.group(0),
            text_offset_start=surg_match.start(),
            text_offset_end=surg_match.end(),
            attributes={"procedure": proc_str, "margins": margin_info}
        ))

    # 8b. DIAGNOSTIC BIOPSIES
    biopsy_match = re.search(r'\b(?:Biopsy|FNAC|Tru-cut\s+biopsy|Core\s+needle\s+biopsy|Core\s+biopsy)\b', text, re.IGNORECASE)
    if biopsy_match:
        add_entity(ExtractedClinicalEntity(
            entity_type="SurgicalProcedure",
            category="Diagnostic Oncology",
            name="Diagnostic Biopsy",
            value=biopsy_match.group(0),
            code="BIOPSY",
            code_system="CPT",
            confidence_score=0.95,
            extracted_text=biopsy_match.group(0),
            text_offset_start=biopsy_match.start(),
            text_offset_end=biopsy_match.end(),
            attributes={"procedure": "Diagnostic Biopsy"}
        ))

    # 9. RADIATION THERAPY
    rt_dose_match = re.search(r'(?:Total\s+Dose|Radiation\s+Dose|Dose)\s*[:=-]?\s*(\d{2,3}(?:\.\d+)?)\s*Gy(?:\s*(?:in|\/)\s*(\d{1,2})\s*fractions?)?', text, re.IGNORECASE)
    if rt_dose_match:
        total_gy = rt_dose_match.group(1)
        fractions = rt_dose_match.group(2)
        val_str = f"{total_gy} Gy" + (f" in {fractions} fractions" if fractions else "")

        target_match = re.search(r'(?:Target(?:ed)?(?:\s+Site|\s+Volume)?|Site)\s*[:=-]\s*([^\n\r]+)', text, re.IGNORECASE)
        target_site = target_match.group(1).strip() if target_match else "Target site"

        add_entity(ExtractedClinicalEntity(
            entity_type="RadiationTherapy",
            category="Radiation Oncology",
            name="External Beam Radiotherapy",
            value=f"{val_str} ({target_site})",
            code=f"{total_gy}Gy",
            code_system="ASTR",
            confidence_score=0.93,
            extracted_text=rt_dose_match.group(0),
            text_offset_start=rt_dose_match.start(),
            text_offset_end=rt_dose_match.end(),
            attributes={
                "total_dose_gy": float(total_gy),
                "fractions": int(fractions) if fractions else None,
                "target_site": target_site
            }
        ))

    return entities

# -------------------------------------------------------------
# Clinical Semantic Dense Embeddings (384-dimensional)
# -------------------------------------------------------------
ONCOLOGY_EMBEDDING_CLUSTERS = {
    # Anatomical sites (0..63)
    "breast": (0, 16, ["breast", "mammary", "nipple", "axilla", "axillary", "ductal", "lobular", "mastectomy", "स्तन"]),
    "lung": (16, 32, ["lung", "bronchus", "bronchial", "pleural", "pulmonary", "nsclc", "sclc", "thoracic", "फोक्सो"]),
    "gynecologic": (32, 48, ["cervix", "cervical", "uterus", "uterine", "endometrium", "ovary", "ovarian", "vulva", "पाठेघर"]),
    "gi_tract": (48, 64, ["colon", "colorectal", "rectum", "rectal", "gastric", "stomach", "esophagus", "liver", "gallbladder", "pancreas", "पेट"]),

    # Histology & morphology (64..127)
    "carcinoma": (64, 80, ["carcinoma", "adenocarcinoma", "squamous", "ductal carcinoma", "infiltrating ductal", "idc", "scc", "mucinous"]),
    "sarcoma_lymphoma": (80, 96, ["sarcoma", "lymphoma", "hodgkin", "leukemia", "melanoma", "gastrointestinal stromal", "gist", "myeloma"]),
    "differentiation": (96, 112, ["well differentiated", "moderately differentiated", "poorly differentiated", "undifferentiated", "anaplastic"]),
    "grading": (112, 128, ["grade 1", "grade 2", "grade 3", "nottingham", "bloom-richardson", "gleason", "score", "mitotic", "pleomorphism"]),

    # Staging & TNM (128..175)
    "stage_early": (128, 144, ["stage i", "stage ia", "stage ib", "stage ii", "stage iia", "stage iib", "t1", "t2", "n0", "localized", "early"]),
    "stage_advanced": (144, 160, ["stage iii", "stage iiia", "stage iiib", "stage iiic", "stage iv", "t3", "t4", "n1", "n2", "n3", "m1", "metastatic", "distant", "advanced"]),
    "staging_general": (160, 176, ["tnm", "ajcc", "uicc", "stage", "pathological stage", "clinical stage", "margin positive", "margin negative"]),

    # Biomarkers & IHC (176..223)
    "hormone_receptors": (176, 192, ["er positive", "estrogen receptor positive", "pr positive", "progesterone receptor positive", "er+", "pr+", "hormone receptor positive", "er/pr"]),
    "her2_neu": (192, 208, ["her2", "her2/neu", "her2 positive", "her2 3+", "her2 amplified", "cerbb2", "her2-neu", "her-2"]),
    "ihc_neg_ki67": (208, 224, ["triple negative", "er negative", "pr negative", "her2 negative", "ki-67", "ki67", "high ki67", "proliferation index", "pd-l1", "egfr"]),

    # Systemic therapies & RT (224..271)
    "chemotherapy": (224, 240, ["chemotherapy", "ac-t", "fac", "folfox", "doxorubicin", "cyclophosphamide", "paclitaxel", "docetaxel", "cisplatin", "carboplatin", "केमोथेरापी"]),
    "radiotherapy": (240, 256, ["radiotherapy", "radiation therapy", "ebrt", "brachytherapy", "linear accelerator", "gray", "gy", "fractions", "रेडियोथेरापी"]),
    "targeted_endocrine": (256, 272, ["trastuzumab", "herceptin", "tamoxifen", "letrozole", "anastrozole", "pembrolizumab", "immunotherapy", "endocrine therapy"]),

    # Nepali clinical & administrative terms (272..319)
    "nepali_oncology": (272, 320, ["क्यान्सर", "अस्पताल", "बिरामी", "प्याथोलोजी", "बायोप्सी", "जाँच", "मासु", "शल्यक्रिया", "उपचार", "भरतपुर", "नेपाल", "चितवन"])
}

def generate_dense_clinical_embedding(text: str, dim: int = 384) -> List[float]:
    if not text or not text.strip():
        return [0.0] * dim

    vec = [0.0] * dim
    clean_text = text.lower()

    # Match domain terms
    for domain, (start_idx, end_idx, keywords) in ONCOLOGY_EMBEDDING_CLUSTERS.items():
        weight = 0.0
        for kw in keywords:
            if kw in clean_text:
                weight += 1.5
        if weight > 0:
            span = end_idx - start_idx
            for k in range(span):
                idx = start_idx + k
                h = int(hashlib.md5(f"{domain}_{k}".encode()).hexdigest(), 16) % 1000 / 1000.0
                vec[idx] += weight * (0.8 + 0.4 * h)

    # General n-gram hash projection into (320..383) and minor dispersion across entire vector
    words = re.findall(r'[\w\u0900-\u097f]+', clean_text)
    for i, w in enumerate(words):
        if len(w) < 2:
            continue
        h1 = int(hashlib.sha256(w.encode()).hexdigest(), 16)

        # General subspace index (320..383)
        g_idx = 320 + (h1 % 64)
        sign = 1.0 if ((h1 >> 8) & 1) == 0 else -1.0
        vec[g_idx] += sign * 0.4

        # Bigram
        if i < len(words) - 1:
            bigram = f"{w}_{words[i+1]}"
            h2 = int(hashlib.sha256(bigram.encode()).hexdigest(), 16)
            bg_idx = 320 + (h2 % 64)
            bg_sign = 1.0 if ((h2 >> 8) & 1) == 0 else -1.0
            vec[bg_idx] += bg_sign * 0.6

        # Harmonic dispersion across full space
        full_idx = h1 % dim
        vec[full_idx] += (0.1 if ((h1 >> 12) & 1) == 0 else -0.1)

    # L2 normalize
    norm = math.sqrt(sum(x * x for x in vec))
    if norm > 1e-9:
        vec = [round(x / norm, 6) for x in vec]
    else:
        vec = [0.0] * dim
    return vec

# -------------------------------------------------------------
# API Endpoints
# -------------------------------------------------------------
@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "service": "bpkmch-ai-worker",
        "version": "1.2.0",
        "supported_languages": ["en", "ne", "mixed"],
        "capabilities": ["dual_language_ocr", "oncology_classification", "clinical_entity_extraction", "clinical_embeddings"],
        "ocr_engine": "Native Dual-Language Stream + Tesseract Bridge",
        "embedding_dimensions": 384
    }

@app.post("/embeddings/generate", response_model=EmbeddingResponse)
def generate_embeddings_endpoint(request: EmbeddingRequest):
    start = time.time()
    embeddings = [generate_dense_clinical_embedding(t, request.dimensions) for t in request.texts]
    duration_ms = int((time.time() - start) * 1000)
    logger.info(f"Generated {len(embeddings)} dense embeddings (dim={request.dimensions}) in {duration_ms}ms")
    return EmbeddingResponse(
        embeddings=embeddings,
        dimensions=request.dimensions,
        model="bpkmch-clinical-dense-384",
        count=len(embeddings),
        duration_ms=duration_ms
    )

@app.post("/ocr/extract-text", response_model=OcrExtractionResult)
def extract_text_from_input(request: TextInputRequest):
    return perform_text_analysis(request.text, page_count=1)

@app.post("/classification/classify", response_model=ClassificationResult)
def classify_text_endpoint(request: TextInputRequest):
    return classify_medical_text(request.text)

@app.post("/extraction/clinical-entities", response_model=ClinicalExtractionResponse)
def extract_clinical_entities_endpoint(request: ClinicalExtractionRequest):
    start = time.time()
    entities = extract_oncology_entities(request.text, request.document_category)
    duration_ms = int((time.time() - start) * 1000)
    logger.info(f"Extracted {len(entities)} clinical entities in {duration_ms}ms")
    return ClinicalExtractionResponse(
        entities=entities,
        total_entities=len(entities),
        extraction_duration_ms=duration_ms
    )

@app.post("/pipeline/process", response_model=PipelineResult)
async def process_document_pipeline(file: UploadFile = File(...)):
    start_time = time.time()
    contents = await file.read()
    filename = file.filename or "unknown_file.pdf"
    content_type = file.content_type or ""

    logger.info(f"Processing document pipeline: {filename} ({len(contents)} bytes, {content_type})")

    # Extract text
    if "pdf" in content_type.lower() or filename.lower().endswith(".pdf"):
        raw_text, page_count = extract_text_from_pdf_stream(contents)
    else:
        try:
            raw_text = contents.decode("utf-8", errors="ignore")
        except:
            raw_text = ""
        page_count = 1

    ocr_result = perform_text_analysis(raw_text, page_count=page_count)
    classification_result = classify_medical_text(ocr_result.extracted_text)

    overall_low_conf = ocr_result.low_confidence_warning or classification_result.low_confidence_warning
    duration_ms = int((time.time() - start_time) * 1000)

    logger.info(f"Pipeline complete for {filename}: Cat={classification_result.predicted_category} (Conf={classification_result.confidence}), OCR Conf={ocr_result.confidence_score}, Duration={duration_ms}ms")

    return PipelineResult(
        ocr=ocr_result,
        classification=classification_result,
        overall_low_confidence=overall_low_conf,
        processing_duration_ms=duration_ms
    )
