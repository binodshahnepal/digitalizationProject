import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  HospitalTelemetry,
  BulkBatchUploadResponse,
  BulkBatchJobDetails,
  AuditLogExportFilter,
  AuditLogExportResult,
  ClinicalSafetyBenchmarkReport,
  DisasterRecoveryDrillResult,
  ClinicalFeedbackRequest,
  ClinicalFeedbackDto
} from '../models/admin-operations.models';
import { getApiBaseUrl } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class AdminOperationsService {
  private readonly baseUrl = getApiBaseUrl();

  constructor(private http: HttpClient) {}

  getTelemetry(): Observable<HospitalTelemetry> {
    return this.http.get<HospitalTelemetry>(`${this.baseUrl}/admin/telemetry`);
  }

  uploadBulkBatch(formData: FormData): Observable<BulkBatchUploadResponse> {
    return this.http.post<BulkBatchUploadResponse>(`${this.baseUrl}/admin/bulk-batch/upload`, formData);
  }

  getBulkBatchJobs(): Observable<BulkBatchJobDetails[]> {
    return this.http.get<BulkBatchJobDetails[]>(`${this.baseUrl}/admin/bulk-batch/jobs`);
  }

  getBulkBatchJobDetails(jobId: string): Observable<BulkBatchJobDetails> {
    return this.http.get<BulkBatchJobDetails>(`${this.baseUrl}/admin/bulk-batch/jobs/${jobId}`);
  }

  exportAuditLogs(filter: AuditLogExportFilter): Observable<AuditLogExportResult> {
    return this.http.post<AuditLogExportResult>(`${this.baseUrl}/admin/audit-logs/export`, filter);
  }

  runSafetyBenchmark(): Observable<ClinicalSafetyBenchmarkReport> {
    return this.http.get<ClinicalSafetyBenchmarkReport>(`${this.baseUrl}/ai-assistant/safety-benchmark`);
  }

  rehearseDisasterRecovery(): Observable<DisasterRecoveryDrillResult> {
    return this.http.post<DisasterRecoveryDrillResult>(`${this.baseUrl}/admin/backup/rehearse-restore`, {});
  }

  submitFeedback(patientId: string, req: ClinicalFeedbackRequest): Observable<ClinicalFeedbackDto> {
    return this.http.post<ClinicalFeedbackDto>(`${this.baseUrl}/patients/${patientId}/ai-assistant/feedback`, req);
  }

  getFeedbacks(patientId?: string): Observable<ClinicalFeedbackDto[]> {
    const url = patientId 
      ? `${this.baseUrl}/ai-assistant/feedback?patientId=${patientId}` 
      : `${this.baseUrl}/ai-assistant/feedback`;
    return this.http.get<ClinicalFeedbackDto[]>(url);
  }
}
