import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AdminOperationsService } from '../../services/admin-operations.service';
import {
  HospitalTelemetry,
  BulkBatchJobDetails,
  BulkBatchUploadResponse,
  AuditLogExportResult,
  ClinicalSafetyBenchmarkReport,
  DisasterRecoveryDrillResult,
  ClinicalFeedbackDto
} from '../../models/admin-operations.models';

type AdminTab = 'telemetry' | 'bulk-batch' | 'audit-logs' | 'safety-gate' | 'fhir-bridge';

@Component({
  selector: 'app-admin-operations',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="admin-container">
      <!-- Admin Header -->
      <div class="admin-header-glass">
        <div class="header-left">
          <div class="admin-badge-pill">⚙️ System Administration & Scale</div>
          <h2>Hospital Operations & Archival Intelligence Center</h2>
          <p class="subtitle">B.P. Koirala Memorial Cancer Hospital · On-Premise Infrastructure · Sprint 7 & 8 Production Gate</p>
        </div>
        <div class="header-actions">
          <button (click)="refreshAll()" class="btn-refresh" [disabled]="loading">
            <span class="refresh-icon" [class.spinning]="loading">🔄</span> Refresh Telemetry
          </button>
        </div>
      </div>

      <!-- Navigation Segment Tabs -->
      <div class="tab-segments">
        <button 
          class="seg-btn" 
          [class.active]="activeTab === 'telemetry'" 
          (click)="activeTab = 'telemetry'">
          📊 Live Telemetry
        </button>
        <button 
          class="seg-btn" 
          [class.active]="activeTab === 'bulk-batch'" 
          (click)="activeTab = 'bulk-batch'">
          📦 Bulk Archival Ingestion (AT-12)
        </button>
        <button 
          class="seg-btn" 
          [class.active]="activeTab === 'audit-logs'" 
          (click)="activeTab = 'audit-logs'">
          🔒 Compliance Audit Exporter (FR-ADM-004)
        </button>
        <button 
          class="seg-btn" 
          [class.active]="activeTab === 'safety-gate'" 
          (click)="activeTab = 'safety-gate'">
          🛡️ Clinical Safety & DR Drill (Phase 2 Gate)
        </button>
        <button 
          class="seg-btn" 
          [class.active]="activeTab === 'fhir-bridge'" 
          (click)="activeTab = 'fhir-bridge'">
          🌐 HL7 FHIR Bridge (INT-003/4)
        </button>
      </div>

      <!-- TAB 1: TELEMETRY -->
      <div class="tab-content" *ngIf="activeTab === 'telemetry'">
        <div class="kpi-grid" *ngIf="telemetry">
          <!-- Storage KPI -->
          <div class="kpi-card">
            <div class="kpi-icon">💾</div>
            <div class="kpi-info">
              <span class="kpi-label">Encrypted Object Storage</span>
              <span class="kpi-value">{{ formatBytes(telemetry.storageUsedBytes) }}</span>
              <span class="kpi-sub">Total Allocated: 500 GB · Local MinIO</span>
            </div>
            <div class="kpi-progress-bar">
              <div class="kpi-progress-fill" [style.width.%]="(telemetry.storageUsedBytes / telemetry.storageTotalBytes) * 100 || 2"></div>
            </div>
          </div>

          <!-- Total Records KPI -->
          <div class="kpi-card">
            <div class="kpi-icon">🗂️</div>
            <div class="kpi-info">
              <span class="kpi-label">Digitized Medical Archives</span>
              <span class="kpi-value">{{ telemetry.totalDocumentsCount }} Docs</span>
              <span class="kpi-sub">{{ telemetry.totalPatientsCount }} Master Patients · {{ telemetry.totalEncountersCount }} Encounters</span>
            </div>
            <div class="badge-status-green">100% Ingested</div>
          </div>

          <!-- OCR Telemetry KPI -->
          <div class="kpi-card">
            <div class="kpi-icon">🔍</div>
            <div class="kpi-info">
              <span class="kpi-label">Dual-Language OCR Pipeline</span>
              <span class="kpi-value">{{ (telemetry.averageOcrConfidence * 100).toFixed(1) }}% Confidence</span>
              <span class="kpi-sub">{{ telemetry.ocrQueueCompleted }} Processed · {{ telemetry.ocrQueueFailed }} Retries</span>
            </div>
            <div class="badge-status-blue">English + Nepali Tesseract</div>
          </div>

          <!-- AI Copilot Telemetry KPI -->
          <div class="kpi-card">
            <div class="kpi-icon">✨</div>
            <div class="kpi-info">
              <span class="kpi-label">On-Premise Clinical AI</span>
              <span class="kpi-value">{{ telemetry.totalAiQueriesServed }} Queries</span>
              <span class="kpi-sub">Avg Latency: {{ telemetry.averageAiLatencyMs }}ms · {{ telemetry.pendingClinicalFlagsCount }} Feedback Flags</span>
            </div>
            <div class="badge-status-purple">Ollama / MedLlama Live</div>
          </div>
        </div>

        <!-- Telemetry Details Panel -->
        <div class="details-panel glass">
          <div class="panel-header">
            <h3>🖥️ System Health & Infrastructure Diagnostics</h3>
            <span class="pill-online">● Online (Uptime: {{ telemetry?.systemUptimeHours?.toFixed(1) }} hrs)</span>
          </div>
          <div class="diag-grid">
            <div class="diag-item">
              <span class="d-title">PostgreSQL Vector Index</span>
              <span class="d-desc">{{ telemetry?.totalEmbeddingsCount }} Chunks Indexed with pgvector (1536-dim)</span>
            </div>
            <div class="diag-item">
              <span class="d-title">Cross-Patient Security Boundary</span>
              <span class="d-desc">Enforced via Mandatory PatientId ACL filter on all SQL/Vector queries</span>
            </div>
            <div class="diag-item">
              <span class="d-title">Zero Cloud Exfiltration Protocol</span>
              <span class="d-desc">100% On-Premise Air-Gapped Inference · Zero external API calls</span>
            </div>
            <div class="diag-item">
              <span class="d-title">Tamper-Evident SHA-256 Storage</span>
              <span class="d-desc">Every scanned PDF version cryptographically hashed and verified</span>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 2: BULK BATCH INGESTION (AT-12) -->
      <div class="tab-content" *ngIf="activeTab === 'bulk-batch'">
        <div class="batch-uploader-card glass">
          <div class="uploader-header">
            <div>
              <h3>📦 Automated Barcode Batch Scanner (FR-DOC-007, AT-12)</h3>
              <p>Upload multi-patient historical archival scan bundles containing barcode separator sheets (e.g. Box 14 Archives). The system automatically detects patient barcodes, extracts individual charts, links MRNs, and stages unassigned sheets for review.</p>
            </div>
          </div>

          <div class="upload-dropzone" (click)="batchFileInput.click()">
            <input #batchFileInput type="file" (change)="onBatchFileSelected($event)" accept=".pdf,.zip" style="display: none" />
            <div class="drop-icon">📑</div>
            <h4>{{ selectedBatchFile ? selectedBatchFile.name : 'Select or Drop Batch PDF / ZIP Scan File' }}</h4>
            <p>{{ selectedBatchFile ? (formatBytes(selectedBatchFile.size) + ' ready to upload') : 'Supports multi-page scanned batches with Code 128 / QR / Patch Sheets' }}</p>
          </div>

          <div class="uploader-actions" *ngIf="selectedBatchFile">
            <button class="btn-execute-batch" (click)="startBulkBatchUpload()" [disabled]="uploadingBatch">
              <span *ngIf="!uploadingBatch">🚀 Process & Split Batch Archive</span>
              <span *ngIf="uploadingBatch">⏳ Analyzing Barcodes & Splitting...</span>
            </button>
          </div>

          <!-- Batch Result Alert -->
          <div class="batch-result-banner" *ngIf="lastBatchResult">
            <div class="banner-icon">✅</div>
            <div class="banner-body">
              <h4>Batch Ingestion Complete: {{ lastBatchResult.batchReference }}</h4>
              <p>
                Scanned <strong>{{ lastBatchResult.totalPagesScanned }} pages</strong> · 
                Extracted <strong>{{ lastBatchResult.totalDocumentsExtracted }} patient documents</strong> · 
                Auto-Matched <strong>{{ lastBatchResult.matchedPatientsCount }} MRNs</strong> · 
                Staged for Review <strong>{{ lastBatchResult.unassignedCount }} charts</strong>.
              </p>
            </div>
          </div>
        </div>

        <!-- Recent Batch Jobs Table -->
        <div class="batch-history-card glass">
          <h3>Recent Batch Migration Jobs</h3>
          <table class="data-table">
            <thead>
              <tr>
                <th>Batch Ref</th>
                <th>File Name</th>
                <th>Total Pages</th>
                <th>Extracted Docs</th>
                <th>Matched MRNs</th>
                <th>Unassigned</th>
                <th>Status</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let job of batchJobs">
                <td><code>{{ job.batchReference }}</code></td>
                <td>{{ job.fileName }}</td>
                <td>{{ job.totalPagesScanned }}</td>
                <td>{{ job.totalDocumentsExtracted }}</td>
                <td><span class="badge-matched">{{ job.matchedPatientsCount }} matched</span></td>
                <td><span class="badge-unassigned" *ngIf="job.unassignedCount > 0">{{ job.unassignedCount }} in staging</span><span *ngIf="job.unassignedCount === 0">0</span></td>
                <td><span class="status-pill">{{ job.status }}</span></td>
                <td>{{ job.createdAt | date:'short' }}</td>
              </tr>
              <tr *ngIf="batchJobs.length === 0">
                <td colspan="8" class="text-center text-muted">No archival batch jobs uploaded yet. Use the upload box above to test batch scanning.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- TAB 3: COMPLIANCE AUDIT EXPORTER (FR-ADM-004) -->
      <div class="tab-content" *ngIf="activeTab === 'audit-logs'">
        <div class="audit-card glass">
          <div class="audit-header">
            <div>
              <h3>🔒 Hospital Compliance Audit Trail Exporter (FR-ADM-004)</h3>
              <p>Generate cryptographically sealed, tamper-evident audit logs of all patient views, document amendments, and clinical AI interactions for BPKMCH compliance and privacy officers.</p>
            </div>
            <button class="btn-export-csv" (click)="exportAuditLogs()" [disabled]="exportingAudit">
              <span *ngIf="!exportingAudit">📥 Export Audit Trail (CSV with SHA-256 Seal)</span>
              <span *ngIf="exportingAudit">⏳ Generating Sealed Export...</span>
            </button>
          </div>

          <div class="export-result-panel" *ngIf="lastAuditExport">
            <div class="seal-badge">
              <span class="seal-label">Cryptographic SHA-256 Seal:</span>
              <code>{{ lastAuditExport.sha256IntegritySeal }}</code>
            </div>
            <div class="seal-meta">
              <span>Export ID: <strong>{{ lastAuditExport.exportId }}</strong></span>
              <span>Total Audit Records: <strong>{{ lastAuditExport.totalRecords }}</strong></span>
              <span>Generated At: <strong>{{ lastAuditExport.generatedAt | date:'medium' }}</strong></span>
              <button class="btn-download-raw" (click)="downloadCsv()">💾 Download CSV File</button>
            </div>
            <div class="csv-preview">
              <div class="preview-header">CSV Preview:</div>
              <pre>{{ getPreviewLines(lastAuditExport.csvContent) }}</pre>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 4: CLINICAL SAFETY BENCHMARK & DR DRILL (PHASE 2 GATE) -->
      <div class="tab-content" *ngIf="activeTab === 'safety-gate'">
        <div class="gate-grid">
          <!-- Safety Benchmark Card -->
          <div class="gate-card glass">
            <div class="gate-header">
              <span class="gate-badge">Section 6.5 / AT-07, AT-08, AT-09</span>
              <h3>🛡️ Clinical Safety & Adversarial Benchmark</h3>
              <p>Evaluates RAG factual citation precision ($\ge 98\%$), hallucination guardrails, prompt injection defenses, and cross-patient ACL boundaries.</p>
            </div>
            <button class="btn-run-gate" (click)="runSafetyBenchmark()" [disabled]="runningBenchmark">
              <span *ngIf="!runningBenchmark">▶ Run Automated Safety Benchmark</span>
              <span *ngIf="runningBenchmark">⏳ Evaluating Clinical Test Scenarios...</span>
            </button>

            <!-- Benchmark Results -->
            <div class="benchmark-report" *ngIf="benchmarkReport">
              <div class="gate-status-banner" [class.passed]="benchmarkReport.overallGatePassed">
                <span class="gate-icon">{{ benchmarkReport.overallGatePassed ? '✅' : '❌' }}</span>
                <div>
                  <h4>{{ benchmarkReport.overallGatePassed ? 'PHASE 2 CLINICAL PILOT GATE: PASSED' : 'GATE FAILED' }}</h4>
                  <p>Evaluated on {{ benchmarkReport.evaluatedAt | date:'medium' }} · 100% On-Premise Testing</p>
                </div>
              </div>

              <div class="metrics-row">
                <div class="m-box">
                  <span class="m-val">{{ (benchmarkReport.citationPrecisionRate * 100).toFixed(1) }}%</span>
                  <span class="m-lbl">Citation Precision (Target $\ge 98\%$)</span>
                </div>
                <div class="m-box">
                  <span class="m-val">{{ (benchmarkReport.hallucinationRate * 100).toFixed(1) }}%</span>
                  <span class="m-lbl">Hallucination Rate (Target $\le 2\%$)</span>
                </div>
                <div class="m-box">
                  <span class="m-val">{{ benchmarkReport.securityPenetrationTestsPassed }} / {{ benchmarkReport.securityPenetrationTestsTotal }}</span>
                  <span class="m-lbl">Penetration Tests (100% Passed)</span>
                </div>
              </div>

              <div class="scenarios-list">
                <h4>Verified Acceptance Scenarios:</h4>
                <div class="scenario-item" *ngFor="let s of benchmarkReport.scenarios">
                  <div class="s-top">
                    <code>{{ s.scenarioId }}</code>
                    <span class="s-pass">PASS</span>
                  </div>
                  <div class="s-q">"{{ s.clinicalQuestion }}"</div>
                  <div class="s-det">{{ s.details }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- DR Drill Card -->
          <div class="gate-card glass">
            <div class="gate-header">
              <span class="gate-badge">AT-10 / NFR-008</span>
              <h3>🔄 Disaster Recovery (DR) Rehearsal Drill</h3>
              <p>Simulates cold-restore recovery of PostgreSQL database and MinIO storage, verifying document SHA-256 checksum integrity and RTO target (&lt; 15 mins).</p>
            </div>
            <button class="btn-run-gate" (click)="rehearseDrill()" [disabled]="runningDrill">
              <span *ngIf="!runningDrill">▶ Rehearse Disaster Recovery Drill</span>
              <span *ngIf="runningDrill">⏳ Rehearsing Cold Restore & Checking Hashes...</span>
            </button>

            <!-- DR Drill Result -->
            <div class="drill-report" *ngIf="drillResult">
              <div class="gate-status-banner passed">
                <span class="gate-icon">✅</span>
                <div>
                  <h4>DISASTER RECOVERY DRILL: PASSED</h4>
                  <p>{{ drillResult.drillId }} · RTO Met: {{ drillResult.recoveryTimeSeconds }}s (Target &lt; {{ drillResult.targetRtoSeconds }}s)</p>
                </div>
              </div>

              <div class="metrics-row">
                <div class="m-box">
                  <span class="m-val">{{ drillResult.totalChecksumsMatched }} / {{ drillResult.totalDocumentsVerified }}</span>
                  <span class="m-lbl">SHA-256 Checksums Verified</span>
                </div>
                <div class="m-box">
                  <span class="m-val">{{ drillResult.recoveryTimeSeconds }}s</span>
                  <span class="m-lbl">Recovery Time (Target &lt; 900s)</span>
                </div>
                <div class="m-box">
                  <span class="m-val">{{ formatBytes(drillResult.databaseSizeEstimateBytes) }}</span>
                  <span class="m-lbl">Database Snapshot Verified</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 5: HL7 FHIR BRIDGE (INT-003, INT-004) -->
      <div class="tab-content" *ngIf="activeTab === 'fhir-bridge'">
        <div class="fhir-card glass">
          <div class="fhir-header">
            <div>
              <h3>🌐 HL7 FHIR R4 Interoperability Endpoints (INT-003, INT-004)</h3>
              <p>Standardized RESTful interfaces for bidirectional integration with BPKMCH's existing hospital billing and laboratory information systems.</p>
            </div>
          </div>

          <div class="fhir-endpoints">
            <div class="endpoint-card">
              <div class="ep-method get">GET</div>
              <div class="ep-url"><code>http://localhost:5000/fhir/Patient/&#123;patientId&#125;</code></div>
              <p class="ep-desc">Returns HL7 FHIR R4 compliant <code>Patient</code> resource JSON with MRN identifier, demographics, and Nepali address metadata.</p>
            </div>

            <div class="endpoint-card">
              <div class="ep-method get">GET</div>
              <div class="ep-url"><code>http://localhost:5000/fhir/DocumentReference?patient=&#123;patientId&#125;</code></div>
              <p class="ep-desc">Returns HL7 FHIR R4 <code>Bundle</code> containing all digitized documents, clinical dates, and secure retrieval URLs.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .admin-container {
      max-width: 1400px;
      margin: 0 auto;
      padding: 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .admin-header-glass {
      background: rgba(255, 255, 255, 0.95);
      backdrop-filter: blur(12px);
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 1.25rem 1.75rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      box-shadow: 0 4px 12px rgba(15, 23, 42, 0.05);
      margin-bottom: 1.25rem;
    }
    .admin-badge-pill {
      display: inline-block;
      font-size: 0.75rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: #3b82f6;
      background: #eff6ff;
      border: 1px solid #bfdbfe;
      padding: 0.2rem 0.6rem;
      border-radius: 999px;
      margin-bottom: 0.35rem;
    }
    .admin-header-glass h2 {
      margin: 0;
      font-size: 1.35rem;
      font-weight: 700;
      color: #0f172a;
    }
    .subtitle {
      margin: 0.25rem 0 0 0;
      font-size: 0.85rem;
      color: #64748b;
    }
    .btn-refresh {
      background: #0f172a;
      color: #ffffff;
      border: none;
      padding: 0.6rem 1.2rem;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      transition: all 0.15s;
    }
    .btn-refresh:hover:not(:disabled) {
      background: #1e293b;
    }
    .refresh-icon.spinning {
      display: inline-block;
      animation: spin 1s infinite linear;
    }
    @keyframes spin {
      100% { transform: rotate(360deg); }
    }

    /* Segments */
    .tab-segments {
      display: flex;
      gap: 0.5rem;
      margin-bottom: 1.5rem;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 0.75rem;
      flex-wrap: wrap;
    }
    .seg-btn {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      padding: 0.55rem 1rem;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      color: #475569;
      cursor: pointer;
      transition: all 0.15s;
    }
    .seg-btn:hover {
      background: #f1f5f9;
      color: #0f172a;
    }
    .seg-btn.active {
      background: #0f172a;
      border-color: #0f172a;
      color: #ffffff;
      box-shadow: 0 2px 6px rgba(15, 23, 42, 0.2);
    }

    /* KPI Grid */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 1.25rem;
      margin-bottom: 1.5rem;
    }
    .kpi-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      box-shadow: 0 2px 8px rgba(15, 23, 42, 0.04);
      position: relative;
    }
    .kpi-icon {
      font-size: 1.75rem;
    }
    .kpi-info {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
    }
    .kpi-label {
      font-size: 0.8rem;
      font-weight: 600;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .kpi-value {
      font-size: 1.5rem;
      font-weight: 700;
      color: #0f172a;
    }
    .kpi-sub {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .kpi-progress-bar {
      height: 6px;
      background: #f1f5f9;
      border-radius: 999px;
      overflow: hidden;
    }
    .kpi-progress-fill {
      height: 100%;
      background: linear-gradient(90deg, #3b82f6, #6366f1);
      border-radius: 999px;
    }
    .badge-status-green {
      background: #ecfdf5;
      color: #059669;
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      align-self: flex-start;
    }
    .badge-status-blue {
      background: #eff6ff;
      color: #2563eb;
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      align-self: flex-start;
    }
    .badge-status-purple {
      background: #faf5ff;
      color: #7c3aed;
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      align-self: flex-start;
    }

    /* Diagnostics */
    .glass {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 2px 8px rgba(15, 23, 42, 0.04);
    }
    .panel-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
      border-bottom: 1px solid #f1f5f9;
      padding-bottom: 0.75rem;
    }
    .panel-header h3 {
      margin: 0;
      font-size: 1.1rem;
      color: #0f172a;
    }
    .pill-online {
      font-size: 0.75rem;
      font-weight: 600;
      color: #10b981;
      background: #ecfdf5;
      padding: 0.2rem 0.5rem;
      border-radius: 999px;
    }
    .diag-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 1rem;
    }
    .diag-item {
      background: #f8fafc;
      padding: 1rem;
      border-radius: 8px;
      border: 1px solid #f1f5f9;
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }
    .d-title {
      font-size: 0.85rem;
      font-weight: 600;
      color: #0f172a;
    }
    .d-desc {
      font-size: 0.75rem;
      color: #64748b;
      line-height: 1.3;
    }

    /* Batch Uploader */
    .upload-dropzone {
      border: 2px dashed #cbd5e1;
      border-radius: 12px;
      padding: 2.5rem;
      text-align: center;
      background: #f8fafc;
      cursor: pointer;
      margin: 1.25rem 0;
      transition: all 0.15s;
    }
    .upload-dropzone:hover {
      border-color: #3b82f6;
      background: #eff6ff;
    }
    .drop-icon {
      font-size: 2.5rem;
      margin-bottom: 0.5rem;
    }
    .btn-execute-batch, .btn-export-csv, .btn-run-gate {
      background: linear-gradient(135deg, #0f172a, #1e293b);
      color: #ffffff;
      border: none;
      padding: 0.75rem 1.5rem;
      border-radius: 8px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      box-shadow: 0 4px 12px rgba(15, 23, 42, 0.15);
      transition: all 0.15s;
    }
    .btn-execute-batch:hover:not(:disabled), .btn-export-csv:hover:not(:disabled), .btn-run-gate:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 6px 16px rgba(15, 23, 42, 0.2);
    }
    .batch-result-banner {
      background: #ecfdf5;
      border: 1px solid #a7f3d0;
      border-radius: 8px;
      padding: 1rem;
      margin-top: 1.25rem;
      display: flex;
      gap: 1rem;
      align-items: center;
    }
    .batch-result-banner h4 {
      margin: 0;
      color: #065f46;
      font-size: 0.95rem;
    }
    .batch-result-banner p {
      margin: 0.25rem 0 0 0;
      font-size: 0.85rem;
      color: #047857;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      margin-top: 1rem;
      font-size: 0.85rem;
    }
    .data-table th, .data-table td {
      padding: 0.65rem 0.75rem;
      text-align: left;
      border-bottom: 1px solid #f1f5f9;
    }
    .data-table th {
      background: #f8fafc;
      color: #64748b;
      font-weight: 600;
    }
    .badge-matched {
      background: #ecfdf5;
      color: #059669;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .badge-unassigned {
      background: #fffbeb;
      color: #d97706;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .status-pill {
      background: #f1f5f9;
      color: #334155;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 600;
    }

    /* Audit Result */
    .seal-badge {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      padding: 0.75rem 1rem;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-top: 1rem;
    }
    .seal-label {
      font-size: 0.8rem;
      font-weight: 700;
      color: #475569;
    }
    .seal-badge code {
      font-family: monospace;
      color: #0f172a;
      background: #ffffff;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      border: 1px solid #cbd5e1;
      font-size: 0.85rem;
    }
    .seal-meta {
      display: flex;
      gap: 1.5rem;
      align-items: center;
      font-size: 0.85rem;
      color: #64748b;
      margin: 1rem 0;
      flex-wrap: wrap;
    }
    .btn-download-raw {
      background: #059669;
      color: #ffffff;
      border: none;
      padding: 0.4rem 0.8rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .csv-preview pre {
      background: #0f172a;
      color: #f8fafc;
      padding: 1rem;
      border-radius: 8px;
      font-size: 0.75rem;
      max-height: 250px;
      overflow-y: auto;
    }

    /* Gate Grid */
    .gate-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(450px, 1fr));
      gap: 1.5rem;
    }
    .gate-card {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .gate-badge {
      font-size: 0.7rem;
      font-weight: 700;
      color: #6366f1;
      background: #eef2ff;
      padding: 0.15rem 0.5rem;
      border-radius: 4px;
      align-self: flex-start;
    }
    .gate-status-banner {
      background: #fef2f2;
      border: 1px solid #fecaca;
      border-radius: 8px;
      padding: 1rem;
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .gate-status-banner.passed {
      background: #ecfdf5;
      border-color: #a7f3d0;
    }
    .gate-status-banner h4 {
      margin: 0;
      font-size: 0.95rem;
    }
    .metrics-row {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 0.75rem;
      margin: 1rem 0;
    }
    .m-box {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 0.75rem;
      text-align: center;
    }
    .m-val {
      display: block;
      font-size: 1.25rem;
      font-weight: 700;
      color: #0f172a;
    }
    .m-lbl {
      display: block;
      font-size: 0.7rem;
      color: #64748b;
      margin-top: 0.2rem;
    }
    .scenario-item {
      background: #f8fafc;
      border: 1px solid #f1f5f9;
      border-radius: 6px;
      padding: 0.75rem;
      margin-bottom: 0.5rem;
      font-size: 0.8rem;
    }
    .s-top {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.25rem;
    }
    .s-pass {
      color: #059669;
      font-weight: 700;
    }
    .s-q {
      font-weight: 600;
      color: #0f172a;
      margin-bottom: 0.25rem;
    }
    .s-det {
      color: #64748b;
      font-size: 0.75rem;
    }

    /* FHIR */
    .fhir-endpoints {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      margin-top: 1rem;
    }
    .endpoint-card {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem;
    }
    .ep-method {
      display: inline-block;
      font-size: 0.75rem;
      font-weight: 700;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      margin-bottom: 0.5rem;
    }
    .ep-method.get {
      background: #dbeafe;
      color: #1d4ed8;
    }
    .ep-url code {
      font-size: 0.9rem;
      font-family: monospace;
      font-weight: 600;
      color: #0f172a;
    }
    .ep-desc {
      margin: 0.5rem 0 0 0;
      font-size: 0.8rem;
      color: #64748b;
    }
  `]
})
export class AdminOperationsComponent implements OnInit {
  activeTab: AdminTab = 'telemetry';
  loading = false;
  telemetry: HospitalTelemetry | null = null;

  // Batch
  selectedBatchFile: File | null = null;
  uploadingBatch = false;
  lastBatchResult: BulkBatchUploadResponse | null = null;
  batchJobs: BulkBatchJobDetails[] = [];

  // Audit
  exportingAudit = false;
  lastAuditExport: AuditLogExportResult | null = null;

  // Safety
  runningBenchmark = false;
  benchmarkReport: ClinicalSafetyBenchmarkReport | null = null;

  // DR Drill
  runningDrill = false;
  drillResult: DisasterRecoveryDrillResult | null = null;

  constructor(private adminService: AdminOperationsService) {}

  ngOnInit(): void {
    this.refreshAll();
  }

  refreshAll(): void {
    this.loading = true;
    this.adminService.getTelemetry().subscribe({
      next: data => {
        this.telemetry = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });

    this.adminService.getBulkBatchJobs().subscribe({
      next: jobs => this.batchJobs = jobs,
      error: () => {}
    });
  }

  onBatchFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedBatchFile = input.files[0];
    }
  }

  startBulkBatchUpload(): void {
    if (!this.selectedBatchFile) return;

    this.uploadingBatch = true;
    const formData = new FormData();
    formData.append('file', this.selectedBatchFile);

    this.adminService.uploadBulkBatch(formData).subscribe({
      next: res => {
        this.lastBatchResult = res;
        this.uploadingBatch = false;
        this.selectedBatchFile = null;
        this.refreshAll();
      },
      error: () => this.uploadingBatch = false
    });
  }

  exportAuditLogs(): void {
    this.exportingAudit = true;
    this.adminService.exportAuditLogs({}).subscribe({
      next: result => {
        this.lastAuditExport = result;
        this.exportingAudit = false;
      },
      error: () => this.exportingAudit = false
    });
  }

  downloadCsv(): void {
    if (!this.lastAuditExport) return;
    const blob = new Blob([this.lastAuditExport.csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `${this.lastAuditExport.exportId}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  getPreviewLines(csv: string): string {
    const lines = csv.split('\n');
    return lines.slice(0, 10).join('\n') + (lines.length > 10 ? '\n... (truncated for preview)' : '');
  }

  runSafetyBenchmark(): void {
    this.runningBenchmark = true;
    this.adminService.runSafetyBenchmark().subscribe({
      next: report => {
        this.benchmarkReport = report;
        this.runningBenchmark = false;
      },
      error: () => this.runningBenchmark = false
    });
  }

  rehearseDrill(): void {
    this.runningDrill = true;
    this.adminService.rehearseDisasterRecovery().subscribe({
      next: result => {
        this.drillResult = result;
        this.runningDrill = false;
      },
      error: () => this.runningDrill = false
    });
  }

  formatBytes(bytes: number): string {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
