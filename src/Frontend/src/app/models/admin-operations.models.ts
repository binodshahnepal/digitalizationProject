export interface ClinicalFeedbackRequest {
  patientId: string;
  queryText: string;
  assistantResponseText: string;
  feedbackType: string;
  severity: string;
  clinicianNotes: string;
}

export interface ClinicalFeedbackDto {
  id: string;
  patientId: string;
  clinicianUsername: string;
  queryText: string;
  assistantResponseText: string;
  feedbackType: string;
  severity: string;
  status: string;
  clinicianNotes: string;
  supervisorResolutionNotes?: string;
  resolvedBy?: string;
  createdAt: string;
  resolvedAt?: string;
}

export interface BenchmarkScenarioResult {
  scenarioId: string;
  clinicalQuestion: string;
  citationsValid: boolean;
  correctlyAbstained: boolean;
  precisionScore: number;
  details: string;
}

export interface ClinicalSafetyBenchmarkReport {
  evaluatedAt: string;
  totalScenariosTested: number;
  supportedCitationsCount: number;
  citationPrecisionRate: number;
  hallucinationRate: number;
  groundedAbstentionRate: number;
  securityPenetrationTestsPassed: number;
  securityPenetrationTestsTotal: number;
  overallGatePassed: boolean;
  scenarios: BenchmarkScenarioResult[];
}

export interface DisasterRecoveryDrillResult {
  drillId: string;
  executedAt: string;
  totalPatientsVerified: number;
  totalDocumentsVerified: number;
  totalChecksumsMatched: number;
  databaseSizeEstimateBytes: number;
  recoveryTimeSeconds: number;
  targetRtoSeconds: number;
  rtoTargetMet: boolean;
  checksumIntegrityVerified: boolean;
  overallDrillPassed: boolean;
}

export interface HospitalTelemetry {
  storageUsedBytes: number;
  storageTotalBytes: number;
  totalPatientsCount: number;
  totalDocumentsCount: number;
  totalEmbeddingsCount: number;
  totalEncountersCount: number;
  ocrQueuePending: number;
  ocrQueueProcessing: number;
  ocrQueueCompleted: number;
  ocrQueueFailed: number;
  averageOcrConfidence: number;
  totalAiQueriesServed: number;
  averageAiLatencyMs: number;
  pendingClinicalFlagsCount: number;
  systemUptimeHours: number;
}

export interface BulkBatchUploadResponse {
  batchJobId: string;
  batchReference: string;
  fileName: string;
  totalPagesScanned: number;
  totalDocumentsExtracted: number;
  matchedPatientsCount: number;
  unassignedCount: number;
  status: string;
}

export interface BulkBatchItem {
  id: string;
  itemIndex: number;
  pageStart: number;
  pageEnd: number;
  detectedBarcodeMrn?: string;
  matchedPatientId?: string;
  matchedPatientName?: string;
  generatedDocumentId?: string;
  status: string;
  notes?: string;
}

export interface BulkBatchJobDetails {
  batchJobId: string;
  batchReference: string;
  fileName: string;
  totalPagesScanned: number;
  totalDocumentsExtracted: number;
  matchedPatientsCount: number;
  unassignedCount: number;
  status: string;
  initiatedBy: string;
  createdAt: string;
  completedAt?: string;
  items: BulkBatchItem[];
}

export interface AuditLogExportFilter {
  fromDate?: string;
  toDate?: string;
  action?: string;
  role?: string;
  resourceId?: string;
}

export interface AuditLogExportResult {
  exportId: string;
  generatedAt: string;
  totalRecords: number;
  sha256IntegritySeal: string;
  csvContent: string;
}
