import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DocumentService } from '../../services/document.service';
import { AuthService } from '../../services/auth.service';
import { DocumentReviewQueueItemDto, DocumentOcrResultDto, DocumentCategory } from '../../models/document.models';

@Component({
  selector: 'app-review-queue',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="queue-container">
      <div class="queue-header">
        <div>
          <h2>Supervisor Document Review Queue (FR-OCR-003, FR-VIS-003)</h2>
          <p class="subtitle">Human-in-the-loop verification for documents flagged with low OCR confidence, ambiguous categorization, or historical intake.</p>
        </div>
        <div class="header-actions">
          <button (click)="loadQueue()" class="btn-refresh" [disabled]="loading()">
            🔄 Refresh Queue
          </button>
        </div>
      </div>

      <!-- Queue Summary Metrics -->
      <div class="metrics-bar">
        <div class="metric-card">
          <span class="metric-val">{{ queueItems().length }}</span>
          <span class="metric-label">Pending Verification</span>
        </div>
        <div class="metric-card warning">
          <span class="metric-val">{{ lowOcrCount() }}</span>
          <span class="metric-label">Low OCR Confidence (&lt;70%)</span>
        </div>
        <div class="metric-card info">
          <span class="metric-val">{{ unassignedCount() }}</span>
          <span class="metric-label">Unassigned Historical Scans</span>
        </div>
      </div>

      <!-- Loading State -->
      <div *ngIf="loading()" class="loading-state">
        Fetching flagged clinical records from queue...
      </div>

      <!-- Empty State -->
      <div *ngIf="!loading() && queueItems().length === 0" class="empty-state">
        <div class="empty-icon">✅</div>
        <h3>Review Queue Empty</h3>
        <p>All scanned hospital records have met AI confidence thresholds and are successfully indexed.</p>
        <a routerLink="/patients" class="btn-primary">Return to Patient Directory</a>
      </div>

      <!-- Queue Table -->
      <div *ngIf="!loading() && queueItems().length > 0" class="table-card">
        <table class="queue-table">
          <thead>
            <tr>
              <th>Patient</th>
              <th>Document Asset</th>
              <th>Category (Current → Predicted)</th>
              <th>AI Confidence</th>
              <th>Reason Flagged</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let item of queueItems()">
              <td>
                <strong>{{ item.patientFullName }}</strong>
                <div class="meta-sub">
                  <span class="mrn-badge">{{ item.patientMrn }}</span>
                  <span *ngIf="item.encounterNumber">• {{ item.encounterNumber }}</span>
                </div>
              </td>
              <td>
                <div class="file-name">📄 {{ item.originalFileName }}</div>
                <div class="meta-sub">v{{ item.versionNumber }} • {{ item.uploadedAt | date:'short' }}</div>
              </td>
              <td>
                <div class="category-flow">
                  <span class="cat-pill current">{{ item.currentCategory }}</span>
                  <span class="arrow">→</span>
                  <span class="cat-pill predicted">{{ item.predictedCategory }}</span>
                </div>
              </td>
              <td>
                <div class="confidence-badges">
                  <span class="conf-pill" [class.low]="item.ocrConfidenceScore < 0.70">
                    OCR: {{ (item.ocrConfidenceScore * 100).toFixed(0) }}%
                  </span>
                  <span class="conf-pill" [class.low]="item.classificationConfidence < 0.80">
                    Class: {{ (item.classificationConfidence * 100).toFixed(0) }}%
                  </span>
                  <span class="lang-tag">{{ item.languageDetected }}</span>
                </div>
              </td>
              <td>
                <span class="reason-tag">⚠️ {{ item.reasonForReview }}</span>
              </td>
              <td>
                <button (click)="openVerificationModal(item)" class="btn-verify">
                  🔍 Review &amp; Verify
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Side-by-Side Human Verification Modal -->
      <div *ngIf="selectedItem()" class="modal-overlay">
        <div class="modal-box modal-xl">
          <div class="modal-header">
            <h4>Human Verification &amp; Indexing Workbench (FR-OCR-003)</h4>
            <button (click)="closeModal()" class="btn-close">✕</button>
          </div>

          <div class="modal-body workbench-grid">
            <!-- Left Panel: Original Document & Metadata -->
            <div class="panel-left">
              <div class="panel-title">📄 Document Metadata &amp; Raw Asset</div>
              
              <div class="info-group">
                <label>Patient:</label>
                <div><strong>{{ selectedItem()?.patientFullName }}</strong> ({{ selectedItem()?.patientMrn }})</div>
              </div>

              <div class="info-group">
                <label>File Name:</label>
                <div>{{ selectedItem()?.originalFileName }} (v{{ selectedItem()?.versionNumber }})</div>
              </div>

              <div class="info-group">
                <label>Flag Reason:</label>
                <div class="flag-note">⚠️ {{ selectedItem()?.reasonForReview }}</div>
              </div>

              <div class="asset-actions">
                <a [href]="getDownloadUrl(selectedItem()!.documentId, selectedItem()!.versionNumber)" target="_blank" class="btn-download">
                  ⬇️ View / Download Original Document
                </a>
              </div>

              <!-- Matched Keywords -->
              <div *ngIf="activeOcr()?.keywordsDetected?.length" class="keywords-box">
                <label>Matched Clinical Keywords:</label>
                <div class="keyword-tags">
                  <span *ngFor="let kw of activeOcr()!.keywordsDetected" class="kw-tag">
                    {{ kw }}
                  </span>
                </div>
              </div>

              <div class="confidence-summary">
                <div class="conf-gauge">
                  <span>OCR Confidence:</span>
                  <strong>{{ (selectedItem()!.ocrConfidenceScore * 100).toFixed(0) }}%</strong>
                </div>
                <div class="conf-gauge">
                  <span>Classification Confidence:</span>
                  <strong>{{ (selectedItem()!.classificationConfidence * 100).toFixed(0) }}%</strong>
                </div>
              </div>
            </div>

            <!-- Right Panel: Dual-Language OCR & Category Adjustment -->
            <div class="panel-right">
              <div class="panel-title">🤖 AI Predictions &amp; Supervisor Correction</div>

              <div class="form-group">
                <label><strong>Confirm Document Category *</strong></label>
                <select [(ngModel)]="verificationCategory" class="form-select">
                  <option value="DischargeSummary">Discharge Summary</option>
                  <option value="PathologyLabReport">Pathology / Biopsy Report</option>
                  <option value="RadiologyReport">Radiology / CT / MRI Scan Report</option>
                  <option value="Prescription">Prescription / Medication Chart</option>
                  <option value="OperativeNote">Surgical / Operative Note</option>
                  <option value="ClinicalProgressNote">Clinical Progress Note</option>
                  <option value="ExternalReferral">External Referral</option>
                  <option value="GeneralMedicalChart">General Medical Chart</option>
                </select>
                <small class="hint">AI Recommendation: <strong>{{ selectedItem()?.predictedCategory }}</strong></small>
              </div>

              <div class="ocr-text-section">
                <div class="text-tabs">
                  <button type="button" [class.active]="activeTextTab === 'full'" (click)="activeTextTab = 'full'">
                    Extracted Text (Dual-Language)
                  </button>
                  <button type="button" [class.active]="activeTextTab === 'nepali'" (click)="activeTextTab = 'nepali'">
                    Nepali Script (देवनागरी)
                  </button>
                </div>

                <div *ngIf="activeTextTab === 'full'">
                  <label class="text-label">Extracted Clinical Content (Editable):</label>
                  <textarea [(ngModel)]="editableOcrText" rows="10" class="ocr-textarea" placeholder="OCR extracted clinical text..."></textarea>
                </div>

                <div *ngIf="activeTextTab === 'nepali'">
                  <label class="text-label">Extracted Nepali Devanagari Segments:</label>
                  <div class="nepali-box">
                    {{ activeOcr()?.extractedNepaliText || 'कुनै नेपाली देवनागरी पाठ भेटिएन (No specific Devanagari text isolated)' }}
                  </div>
                </div>
              </div>

              <div class="form-group notes-group">
                <label>Verification Notes / Rationale (Mandatory for Audit):</label>
                <input type="text" [(ngModel)]="verificationNotes" placeholder="e.g. Verified against biopsy pathology report signed by Dr. Koirala" class="form-input" />
              </div>
            </div>
          </div>

          <div class="modal-footer">
            <button (click)="submitVerification()" [disabled]="submittingVerification()" class="btn-primary">
              {{ submittingVerification() ? 'Verifying...' : '✅ Confirm Category &amp; Index Record' }}
            </button>
            <button (click)="closeModal()" class="btn-cancel">
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .queue-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 2rem 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .queue-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 1.5rem;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 1rem;
    }
    .queue-header h2 {
      margin: 0 0 0.5rem 0;
      color: #0f172a;
      font-size: 1.75rem;
    }
    .subtitle {
      color: #64748b;
      margin: 0;
      font-size: 0.9rem;
    }
    .btn-refresh {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      padding: 0.5rem 1rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      font-size: 0.85rem;
    }
    .metrics-bar {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
      margin-bottom: 1.5rem;
    }
    .metric-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1rem 1.25rem;
      display: flex;
      flex-direction: column;
    }
    .metric-card.warning {
      border-left: 4px solid #f59e0b;
    }
    .metric-card.info {
      border-left: 4px solid #0284c7;
    }
    .metric-val {
      font-size: 1.75rem;
      font-weight: 700;
      color: #0f172a;
    }
    .metric-label {
      font-size: 0.8rem;
      color: #64748b;
      font-weight: 500;
    }
    .table-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      overflow: hidden;
      box-shadow: 0 1px 3px rgba(0,0,0,0.05);
    }
    .queue-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.875rem;
    }
    .queue-table th {
      background: #f8fafc;
      padding: 0.75rem 1rem;
      text-align: left;
      border-bottom: 1px solid #e2e8f0;
      color: #475569;
      font-weight: 600;
    }
    .queue-table td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      color: #1e293b;
    }
    .meta-sub {
      font-size: 0.75rem;
      color: #64748b;
      margin-top: 0.25rem;
    }
    .mrn-badge {
      background: #e0f2fe;
      color: #0369a1;
      font-family: monospace;
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .file-name {
      font-weight: 600;
      color: #0f172a;
    }
    .category-flow {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .cat-pill {
      font-size: 0.75rem;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .cat-pill.current {
      background: #f1f5f9;
      color: #475569;
    }
    .cat-pill.predicted {
      background: #e0f2fe;
      color: #0369a1;
      border: 1px solid #bae6fd;
    }
    .arrow {
      color: #94a3b8;
    }
    .confidence-badges {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }
    .conf-pill {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      background: #dcfce7;
      color: #15803d;
    }
    .conf-pill.low {
      background: #fef3c7;
      color: #b45309;
    }
    .lang-tag {
      font-size: 0.7rem;
      background: #f1f5f9;
      color: #475569;
      padding: 0.15rem 0.35rem;
      border-radius: 3px;
      text-transform: uppercase;
    }
    .reason-tag {
      font-size: 0.75rem;
      background: #fef2f2;
      color: #b91c1c;
      padding: 0.25rem 0.5rem;
      border-radius: 4px;
      display: inline-block;
      font-weight: 500;
    }
    .btn-verify {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.45rem 0.85rem;
      border-radius: 4px;
      font-weight: 600;
      cursor: pointer;
      font-size: 0.8rem;
    }
    .btn-verify:hover {
      background: #0369a1;
    }
    .empty-state {
      padding: 4rem 2rem;
      text-align: center;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
    }
    .empty-icon {
      font-size: 3rem;
      margin-bottom: 1rem;
    }
    .loading-state {
      padding: 3rem;
      text-align: center;
      color: #64748b;
    }
    /* Modal styles */
    .modal-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(15, 23, 42, 0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }
    .modal-box.modal-xl {
      width: 95%;
      max-width: 1050px;
      background: #ffffff;
      border-radius: 8px;
      box-shadow: 0 20px 25px -5px rgba(0,0,0,0.1);
      display: flex;
      flex-direction: column;
      max-height: 90vh;
    }
    .modal-header {
      padding: 1rem 1.5rem;
      border-bottom: 1px solid #e2e8f0;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .modal-header h4 {
      margin: 0;
      color: #0f172a;
    }
    .btn-close {
      background: none;
      border: none;
      font-size: 1.25rem;
      cursor: pointer;
      color: #64748b;
    }
    .workbench-grid {
      display: grid;
      grid-template-columns: 1fr 1.3fr;
      gap: 1.5rem;
      padding: 1.5rem;
      overflow-y: auto;
    }
    .panel-title {
      font-weight: 700;
      color: #0f172a;
      font-size: 0.95rem;
      margin-bottom: 1rem;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 0.5rem;
    }
    .info-group {
      margin-bottom: 0.75rem;
      font-size: 0.85rem;
    }
    .info-group label {
      color: #64748b;
      font-size: 0.75rem;
      font-weight: 600;
      display: block;
      margin-bottom: 0.15rem;
    }
    .flag-note {
      color: #b91c1c;
      font-weight: 600;
      background: #fef2f2;
      padding: 0.4rem 0.6rem;
      border-radius: 4px;
    }
    .asset-actions {
      margin: 1rem 0;
    }
    .btn-download {
      display: inline-block;
      background: #f1f5f9;
      color: #0284c7;
      text-decoration: none;
      padding: 0.45rem 0.85rem;
      border-radius: 4px;
      font-weight: 600;
      font-size: 0.8rem;
      border: 1px solid #cbd5e1;
    }
    .keywords-box {
      margin-top: 1rem;
    }
    .keywords-box label {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: 600;
      display: block;
      margin-bottom: 0.35rem;
    }
    .keyword-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }
    .kw-tag {
      background: #e0f2fe;
      color: #0369a1;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 500;
    }
    .confidence-summary {
      margin-top: 1.25rem;
      background: #f8fafc;
      padding: 0.75rem;
      border-radius: 6px;
      border: 1px solid #e2e8f0;
    }
    .conf-gauge {
      display: flex;
      justify-content: space-between;
      font-size: 0.8rem;
      padding: 0.2rem 0;
    }
    .form-group {
      margin-bottom: 1rem;
    }
    .form-group label {
      font-size: 0.8rem;
      color: #334155;
      margin-bottom: 0.25rem;
      display: block;
    }
    .form-select, .form-input {
      width: 100%;
      padding: 0.5rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.875rem;
    }
    .hint {
      color: #0284c7;
      font-size: 0.75rem;
      margin-top: 0.25rem;
      display: block;
    }
    .text-tabs {
      display: flex;
      gap: 0.5rem;
      margin-bottom: 0.5rem;
    }
    .text-tabs button {
      padding: 0.35rem 0.75rem;
      font-size: 0.75rem;
      font-weight: 600;
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      border-radius: 4px;
      cursor: pointer;
    }
    .text-tabs button.active {
      background: #0284c7;
      color: #ffffff;
      border-color: #0284c7;
    }
    .ocr-textarea {
      width: 100%;
      font-family: monospace;
      font-size: 0.8rem;
      padding: 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      resize: vertical;
      line-height: 1.4;
      box-sizing: border-box;
    }
    .nepali-box {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      padding: 0.75rem;
      font-size: 0.9rem;
      line-height: 1.6;
      min-height: 150px;
    }
    .modal-footer {
      padding: 1rem 1.5rem;
      border-top: 1px solid #e2e8f0;
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
    }
    .btn-primary {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.6rem 1.25rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      text-decoration: none;
      display: inline-block;
    }
    .btn-primary:hover {
      background: #0369a1;
    }
    .btn-cancel {
      background: none;
      border: 1px solid #cbd5e1;
      padding: 0.6rem 1.25rem;
      border-radius: 6px;
      cursor: pointer;
    }
  `]
})
export class ReviewQueueComponent implements OnInit {
  queueItems = signal<DocumentReviewQueueItemDto[]>([]);
  loading = signal(false);

  selectedItem = signal<DocumentReviewQueueItemDto | null>(null);
  activeOcr = signal<DocumentOcrResultDto | null>(null);
  activeTextTab: 'full' | 'nepali' = 'full';

  verificationCategory: DocumentCategory = 'GeneralMedicalChart';
  editableOcrText = '';
  verificationNotes = '';
  submittingVerification = signal(false);

  constructor(
    private documentService: DocumentService,
    public authService: AuthService
  ) {}

  ngOnInit(): void {
    this.loadQueue();
  }

  loadQueue(): void {
    this.loading.set(true);
    this.documentService.getReviewQueue().subscribe({
      next: items => {
        this.queueItems.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  lowOcrCount(): number {
    return this.queueItems().filter(i => i.ocrConfidenceScore < 0.70).length;
  }

  unassignedCount(): number {
    return this.queueItems().filter(i => !i.encounterId).length;
  }

  openVerificationModal(item: DocumentReviewQueueItemDto): void {
    this.selectedItem.set(item);
    this.verificationCategory = item.predictedCategory || item.currentCategory;
    this.editableOcrText = item.textPreview || '';
    this.verificationNotes = '';
    this.activeTextTab = 'full';

    this.documentService.getOcrResult(item.documentId).subscribe({
      next: ocr => {
        this.activeOcr.set(ocr);
        this.editableOcrText = ocr.extractedText;
      },
      error: () => {
        this.activeOcr.set(null);
      }
    });
  }

  closeModal(): void {
    this.selectedItem.set(null);
    this.activeOcr.set(null);
  }

  submitVerification(): void {
    const item = this.selectedItem();
    if (!item) return;

    this.submittingVerification.set(true);
    this.documentService.verifyOcr(item.documentId, {
      confirmedCategory: this.verificationCategory,
      correctedText: this.editableOcrText,
      verificationNotes: this.verificationNotes || 'Verified by supervisor'
    }).subscribe({
      next: () => {
        this.submittingVerification.set(false);
        this.closeModal();
        this.loadQueue();
      },
      error: () => {
        this.submittingVerification.set(false);
      }
    });
  }

  getDownloadUrl(documentId: string, versionNumber: number): string {
    return this.documentService.getDownloadUrl(documentId, versionNumber);
  }
}
