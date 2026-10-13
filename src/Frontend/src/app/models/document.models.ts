export type DocumentCategory = 
  | 'DischargeSummary' 
  | 'PathologyLabReport' 
  | 'RadiologyReport' 
  | 'Prescription' 
  | 'OperativeNote' 
  | 'ClinicalProgressNote' 
  | 'ExternalReferral' 
  | 'GeneralMedicalChart';

export type DocumentStatus = 
  | 'Received' 
  | 'Scanned' 
  | 'OcrQueued' 
  | 'Indexed' 
  | 'RequiresReview' 
  | 'Failed' 
  | 'Archived';

export type ConfidentialityLevel = 'Normal' | 'Restricted' | 'HighlyConfidential';

export interface DocumentVersionDto {
  id: string;
  documentId: string;
  versionNumber: number;
  sha256Checksum: string;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  pageCount?: number;
  reasonForVersion: string;
  createdAt: string;
  creationActor: string;
}

export interface DocumentOcrResultDto {
  id: string;
  documentId: string;
  documentVersionId: string;
  extractedText: string;
  extractedNepaliText?: string | null;
  ocrConfidenceScore: number;
  languageDetected: string;
  pageCount: number;
  wordCount: number;
  predictedCategory: DocumentCategory;
  classificationConfidence: number;
  keywordsDetected: string[];
  lowConfidenceWarning: boolean;
  isVerifiedByHuman: boolean;
  verifiedBy?: string | null;
  verifiedAt?: string | null;
  processingDurationMs: number;
  processedAt: string;
}

export interface VerifyOcrRequest {
  confirmedCategory: DocumentCategory;
  correctedText?: string | null;
  verificationNotes?: string | null;
}

export interface DocumentReviewQueueItemDto {
  documentId: string;
  patientId: string;
  patientMrn: string;
  patientFullName: string;
  encounterId?: string | null;
  encounterNumber?: string | null;
  originalFileName: string;
  versionNumber: number;
  currentCategory: DocumentCategory;
  predictedCategory: DocumentCategory;
  ocrConfidenceScore: number;
  classificationConfidence: number;
  languageDetected: string;
  lowConfidenceWarning: boolean;
  reasonForReview: string;
  uploadedAt: string;
  textPreview: string;
}

export interface DocumentDto {
  id: string;
  patientId: string;
  encounterId?: string;
  clinicalEventId?: string;
  category: DocumentCategory;
  clinicalDate: string;
  language: string;
  confidentialityLevel: ConfidentialityLevel;
  status: DocumentStatus;
  currentVersionNumber: number;
  currentVersion?: DocumentVersionDto;
  createdAt: string;
  createdBy: string;
  latestOcr?: DocumentOcrResultDto | null;
}

export interface DocumentUploadResult {
  success: boolean;
  createdDocuments: DocumentDto[];
  errors: string[];
}

