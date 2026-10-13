import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { PatientService } from '../../services/patient.service';
import { EncounterService } from '../../services/encounter.service';
import { DocumentService } from '../../services/document.service';
import { ScannerService } from '../../services/scanner.service';
import { AuthService } from '../../services/auth.service';
import { PatientDto } from '../../models/patient.models';
import { EncounterDto, CreateEncounterRequest, EncounterType } from '../../models/encounter.models';
import { DocumentDto, DocumentCategory, ConfidentialityLevel } from '../../models/document.models';
import { ScannerDeviceDto } from '../../models/scanner.models';
import { ClinicalEntityService } from '../../services/clinical-entity.service';
import { 
  ClinicalEntityDto, 
  PatientTimelineDto, 
  TimelineEventDto, 
  VerifyClinicalEntityRequest 
} from '../../models/clinical-entity.models';

@Component({
  selector: 'app-patient-details',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-container" *ngIf="patient()">
      <div class="page-header">
        <a routerLink="/patients" class="back-link">← Back to Patient Directory</a>
        <div class="header-main">
          <div class="patient-title">
            <h2>{{ patient()?.fullName }}</h2>
            <span class="mrn-tag">MRN: {{ patient()?.mrn }}</span>
            <span class="provenance-tag">Origin: {{ patient()?.identityProvenance }}</span>
            <span *ngIf="patient()?.isRestricted" class="restricted-tag">Restricted Access</span>
          </div>
          <div class="patient-meta">
            <span>Internal ID: <code>{{ patient()?.id }}</code></span>
          </div>
        </div>
      </div>

      <!-- Quick Demographics Bar -->
      <div class="demographics-grid">
        <div class="demo-card">
          <span class="demo-label">Age & Gender</span>
          <span class="demo-val">{{ patient()?.age }} yrs • {{ patient()?.gender }}</span>
          <span class="demo-sub">DOB: {{ patient()?.dateOfBirth }}</span>
        </div>
        <div class="demo-card">
          <span class="demo-label">National Identity</span>
          <span class="demo-val">{{ patient()?.nationalId || 'Not Recorded' }}</span>
          <span class="demo-sub">Citizenship / NID</span>
        </div>
        <div class="demo-card">
          <span class="demo-label">Contact Phone</span>
          <span class="demo-val">{{ patient()?.phoneNumber || 'No phone' }}</span>
          <span class="demo-sub">{{ patient()?.email || 'No email' }}</span>
        </div>
        <div class="demo-card">
          <span class="demo-label">Location (Nepal)</span>
          <span class="demo-val">{{ patient()?.district || 'Chitwan' }}, Ward {{ patient()?.ward || '—' }}</span>
          <span class="demo-sub">{{ patient()?.municipality || 'Bharatpur' }}</span>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="workspace-tabs">
        <button 
          (click)="activeTab.set('timeline')" 
          [class.active]="activeTab() === 'timeline'" 
          class="tab-button tab-highlight">
          📈 Oncology Trajectory &amp; Timeline
        </button>
        <button 
          (click)="activeTab.set('entities')" 
          [class.active]="activeTab() === 'entities'" 
          class="tab-button">
          🧬 Extracted Oncology Entities ({{ entities().length }})
        </button>
        <button 
          (click)="activeTab.set('documents')" 
          [class.active]="activeTab() === 'documents'" 
          class="tab-button">
          📄 Scans &amp; Documents ({{ documents().length }})
        </button>
        <button 
          (click)="activeTab.set('encounters')" 
          [class.active]="activeTab() === 'encounters'" 
          class="tab-button">
          📁 Visits &amp; Encounters ({{ encounters().length }})
        </button>
        <button 
          (click)="activeTab.set('demographics')" 
          [class.active]="activeTab() === 'demographics'" 
          class="tab-button">
          👤 Demographics &amp; Identity
        </button>
      </div>

      <!-- TAB: Oncology Journey & Longitudinal Timeline (FR-VIS-001, FR-VIS-002) -->
      <div *ngIf="activeTab() === 'timeline'" class="tab-content">
        <!-- Cancer Profile Summary Banner -->
        <div class="cancer-summary-banner">
          <div class="cancer-header-title">
            <span class="trajectory-tag">🏥 BPKMCH Oncology Disease Trajectory</span>
            <h3>Longitudinal Cancer Care Journey &amp; Milestones</h3>
          </div>
          
          <div class="cancer-metrics-grid">
            <!-- Primary Diagnosis -->
            <div class="metric-box diag-box">
              <span class="metric-label">Primary Histological Diagnosis</span>
              <div class="metric-main">
                <span class="metric-val">{{ timeline()?.primaryDiagnosis?.name || 'No Diagnosis Extracted' }}</span>
                <span *ngIf="timeline()?.primaryDiagnosis?.code" class="code-pill">
                  ICD-O: {{ timeline()?.primaryDiagnosis?.code }}
                </span>
              </div>
              <span class="metric-sub">{{ timeline()?.primaryDiagnosis?.value || 'Pending pathology record' }}</span>
            </div>

            <!-- Stage & TNM -->
            <div class="metric-box stage-box">
              <span class="metric-label">AJCC Stage &amp; TNM Classification</span>
              <div class="metric-main">
                <span class="metric-val badge-stage">{{ timeline()?.clinicalStage?.value || 'Stage Unspecified' }}</span>
              </div>
              <span class="metric-sub" *ngIf="timeline()?.histologicGrade">
                Grade: {{ timeline()?.histologicGrade?.value }}
              </span>
            </div>

            <!-- Receptor Biomarkers -->
            <div class="metric-box bio-box">
              <span class="metric-label">IHC Receptor Panel &amp; Biomarkers</span>
              <div class="biomarker-pills-row" *ngIf="timeline()?.biomarkers?.length; else noBio">
                <span *ngFor="let b of timeline()!.biomarkers" class="bio-pill" [class.bio-pos]="b.value.includes('Positive') || (b.code && b.code.includes('+'))" [class.bio-neg]="b.value.includes('Negative') || (b.code && b.code.includes('-'))">
                  <strong>{{ b.name }}:</strong> {{ b.value }}
                </span>
              </div>
              <ng-template #noBio>
                <span class="text-muted">No IHC biomarkers recorded</span>
              </ng-template>
            </div>
          </div>
        </div>

        <!-- Timeline Filters & Controls -->
        <div class="timeline-controls-bar">
          <div class="filter-pills">
            <button (click)="timelineFilter.set('all')" [class.active]="timelineFilter() === 'all'" class="filter-pill">All Events ({{ timeline()?.events?.length || 0 }})</button>
            <button (click)="timelineFilter.set('milestones')" [class.active]="timelineFilter() === 'milestones'" class="filter-pill">⭐ Key Milestones Only</button>
            <button (click)="timelineFilter.set('pathology')" [class.active]="timelineFilter() === 'pathology'" class="filter-pill">🔬 Pathology &amp; Biopsy</button>
            <button (click)="timelineFilter.set('surgery')" [class.active]="timelineFilter() === 'surgery'" class="filter-pill">⚕️ Surgical Operations</button>
            <button (click)="timelineFilter.set('chemo')" [class.active]="timelineFilter() === 'chemo'" class="filter-pill">💊 Chemotherapy</button>
          </div>
          <button (click)="refreshTimeline()" class="btn-refresh-timeline" [disabled]="loadingTimeline()">
            {{ loadingTimeline() ? '⏳ Synthesizing...' : '🔄 Refresh Trajectory' }}
          </button>
        </div>

        <!-- Vertical Interactive Timeline Track -->
        <div class="vertical-timeline-track" *ngIf="filteredTimelineEvents().length; else noEvents">
          <div *ngFor="let ev of filteredTimelineEvents(); let idx = index" class="timeline-event-item" [class.milestone-highlight]="ev.milestone">
            <div class="timeline-marker">
              <div class="timeline-dot" [ngClass]="getEventDotClass(ev)">
                <span class="dot-icon">{{ getEventIcon(ev) }}</span>
              </div>
              <div class="timeline-line"></div>
            </div>

            <div class="timeline-card">
              <div class="timeline-card-header">
                <div class="event-title-area">
                  <span *ngIf="ev.milestoneLabel" class="milestone-badge">
                    ⭐ {{ ev.milestoneLabel }}
                  </span>
                  <h4>{{ ev.title }}</h4>
                  <span class="event-dept">{{ ev.department }} {{ ev.clinician ? '• ' + ev.clinician : '' }}</span>
                </div>
                <div class="event-date-badge">
                  <span class="ev-date">{{ ev.eventDate | date:'mediumDate' }}</span>
                  <span class="ev-time">{{ ev.eventDate | date:'shortTime' }}</span>
                </div>
              </div>

              <div class="timeline-card-body">
                <p class="event-summary">{{ ev.summary }}</p>

                <!-- Extracted Entities in this Event -->
                <div *ngIf="ev.entities && ev.entities.length" class="event-entities-container">
                  <span class="entities-label">🧬 Extracted Clinical Entities:</span>
                  <div class="entity-chips-list">
                    <div *ngFor="let ent of ev.entities" class="ent-chip" [class.ent-verified]="ent.isVerified" (click)="openVerifyEntityModal(ent)">
                      <span class="chip-type">[{{ ent.entityType }}]</span>
                      <strong class="chip-name">{{ ent.name }}:</strong>
                      <span class="chip-val">{{ ent.value }}</span>
                      <span *ngIf="ent.code" class="chip-code">{{ ent.code }}</span>
                      <span *ngIf="ent.isVerified" class="chip-check" title="Verified by clinician">✓</span>
                    </div>
                  </div>
                </div>

                <div class="timeline-card-footer">
                  <span *ngIf="ev.encounterNumber" class="enc-pill">Visit Ref: {{ ev.encounterNumber }}</span>
                  <span *ngIf="ev.isVerified" class="status-verified-pill">✓ Clinician Verified</span>
                  <button *ngIf="ev.documentId" (click)="openDocFromTimeline(ev.documentId)" class="btn-inspect-scan">
                    👁️ Inspect Linked Scan
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <ng-template #noEvents>
          <div class="empty-timeline-state">
            <div class="empty-icon">📅</div>
            <h4>No Timeline Events Found</h4>
            <p>Upload pathology lab reports, operative notes, or register hospital encounters to synthesize the oncology trajectory.</p>
          </div>
        </ng-template>
      </div>

      <!-- TAB: Extracted Clinical Entities (FR-EXT-001, FR-EXT-002) -->
      <div *ngIf="activeTab() === 'entities'" class="tab-content">
        <div class="tab-action-bar">
          <div>
            <h3>Structured Oncology Entities (AI Extracted &amp; Clinician Verified)</h3>
            <p class="subtitle">ICD-O, TNM-8, AJCC-8, Nottingham Histologic Grade, IHC Receptors, and Chemotherapy Protocols</p>
          </div>
          <button (click)="refreshEntities()" class="btn-primary">
            🔄 Refresh Entities
          </button>
        </div>

        <div *ngIf="entities().length === 0" class="empty-state">
          No structured entities found for this patient yet. Scan or upload clinical documents to run AI extraction.
        </div>

        <table *ngIf="entities().length > 0" class="data-table">
          <thead>
            <tr>
              <th>Type</th>
              <th>Entity Name</th>
              <th>Extracted Value / Findings</th>
              <th>Standard Code</th>
              <th>Confidence</th>
              <th>Verification Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let ent of entities()">
              <td>
                <span class="entity-type-badge" [ngClass]="getEntityTypeClass(ent.entityType)">
                  {{ ent.entityType }}
                </span>
              </td>
              <td><strong>{{ ent.name }}</strong></td>
              <td>
                <span class="entity-value-text">{{ ent.value }}</span>
                <div *ngIf="ent.attributesJson && ent.attributesJson !== '{}'" class="ent-attr-preview">
                  <code>{{ ent.attributesJson }}</code>
                </div>
              </td>
              <td>
                <span *ngIf="ent.code" class="code-tag">
                  {{ ent.code }} <small *ngIf="ent.codeSystem">({{ ent.codeSystem }})</small>
                </span>
                <span *ngIf="!ent.code" class="text-muted">—</span>
              </td>
              <td>
                <div class="conf-bar-wrapper">
                  <div class="conf-bar-fill" [style.width.%]="(ent.confidenceScore * 100).toFixed(0)"></div>
                </div>
                <small>{{ (ent.confidenceScore * 100).toFixed(0) }}%</small>
              </td>
              <td>
                <span *ngIf="ent.isVerified" class="badge-verified" title="Verified by {{ ent.verifiedBy }}">
                  ✓ Verified ({{ ent.verifiedBy }})
                </span>
                <span *ngIf="!ent.isVerified" class="badge-unverified">
                  ⚡ AI Extracted
                </span>
              </td>
              <td>
                <button (click)="openVerifyEntityModal(ent)" class="btn-verify-entity" [class.btn-edit-ent]="ent.isVerified">
                  {{ ent.isVerified ? '✏️ Edit' : '✓ Verify' }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Clinician Entity Verification Modal (FR-EXT-002) -->
      <div *ngIf="selectedVerifyEntity()" class="modal-overlay">
        <div class="modal-box modal-md">
          <div class="modal-header">
            <h4>Clinician Verification: {{ selectedVerifyEntity()?.name }}</h4>
            <button (click)="selectedVerifyEntity.set(null)" class="btn-close">✕</button>
          </div>
          <div class="modal-body">
            <p class="verification-lead">
              Validate or modify the extracted oncology finding to confirm it in the official electronic medical record.
            </p>

            <div class="form-group">
              <label>Extracted Passage (Evidence):</label>
              <div class="ocr-snippet-box">
                "{{ selectedVerifyEntity()?.extractedText || selectedVerifyEntity()?.value }}"
              </div>
            </div>

            <div class="form-group">
              <label>Entity Name:</label>
              <input type="text" [(ngModel)]="verifyReq.confirmedName" class="form-control" />
            </div>

            <div class="form-group">
              <label>Confirmed Value / Clinical Finding:</label>
              <input type="text" [(ngModel)]="verifyReq.confirmedValue" class="form-control" />
            </div>

            <div class="form-group">
              <label>Standardized Medical Code (ICD-O, TNM, IHC, etc.):</label>
              <input type="text" [(ngModel)]="verifyReq.confirmedCode" class="form-control" />
            </div>

            <div class="form-group">
              <label>Clinician Notes / Tumor Board Review:</label>
              <textarea [(ngModel)]="verifyReq.verificationNotes" rows="2" class="form-control" placeholder="E.g., Confirmed with pathologist Dr. Shrestha"></textarea>
            </div>
          </div>
          <div class="modal-footer">
            <button (click)="selectedVerifyEntity.set(null)" class="btn-cancel">Cancel</button>
            <button (click)="submitVerifyEntity()" class="btn-primary" [disabled]="submittingVerifyEntity()">
              {{ submittingVerifyEntity() ? 'Saving...' : '✓ Confirm & Certify Entity' }}
            </button>
          </div>
        </div>
      </div>

      <!-- TAB 1: Demographics -->
      <div *ngIf="activeTab() === 'demographics'" class="tab-content">
        <div class="section-card">
          <h3>Emergency Contact Information</h3>
          <div class="details-row">
            <div>
              <span class="row-label">Contact Name:</span>
              <strong>{{ patient()?.emergencyContactName || 'None' }}</strong>
            </div>
            <div>
              <span class="row-label">Relationship:</span>
              <strong>{{ patient()?.emergencyContactRelation || 'None' }}</strong>
            </div>
            <div>
              <span class="row-label">Contact Phone:</span>
              <strong>{{ patient()?.emergencyContactPhone || 'None' }}</strong>
            </div>
          </div>
        </div>

        <div class="section-card">
          <h3>Record Provenance & Audit Metadata (FR-PAT-003, NFR-004)</h3>
          <div class="details-row">
            <div>
              <span class="row-label">Created At:</span>
              <span>{{ patient()?.createdAt | date:'medium' }}</span>
            </div>
            <div>
              <span class="row-label">Registered By:</span>
              <span>{{ patient()?.createdBy }}</span>
            </div>
            <div>
              <span class="row-label">Internal UUID:</span>
              <span class="mono">{{ patient()?.id }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 2: Encounters -->
      <div *ngIf="activeTab() === 'encounters'" class="tab-content">
        <div class="tab-action-bar">
          <div>
            <h3>Patient Encounters (FR-VIS-001, FR-VIS-002)</h3>
            <p class="subtitle">OPD Consultations, Emergency attendances, and Inpatient admissions.</p>
          </div>
          <button (click)="showNewEncounterModal.set(true)" class="btn-primary">
            ➕ Register New Encounter
          </button>
        </div>

        <!-- Encounter Creation Form Modal -->
        <div *ngIf="showNewEncounterModal()" class="modal-overlay">
          <div class="modal-box">
            <div class="modal-header">
              <h4>Create Patient Encounter</h4>
              <button (click)="showNewEncounterModal.set(false)" class="btn-close">✕</button>
            </div>
            <form (ngSubmit)="onCreateEncounter()" class="modal-body">
              <div class="form-group">
                <label>Encounter Type *</label>
                <select [(ngModel)]="newEncounter.type" name="type" required>
                  <option value="OPD">OPD (Outpatient Department)</option>
                  <option value="Emergency">Emergency Intake</option>
                  <option value="Inpatient">Inpatient Admission</option>
                  <option value="FollowUp">Follow-up Consultation</option>
                </select>
              </div>

              <div class="form-group">
                <label>Department *</label>
                <select [(ngModel)]="newEncounter.department" name="department" required>
                  <option value="Medical Oncology">Medical Oncology</option>
                  <option value="Surgical Oncology">Surgical Oncology</option>
                  <option value="Radiation Oncology">Radiation Oncology</option>
                  <option value="Palliative Care">Palliative Care</option>
                  <option value="Gynecological Oncology">Gynecological Oncology</option>
                  <option value="Pathology & Laboratory">Pathology & Laboratory</option>
                </select>
              </div>

              <div class="form-group">
                <label>Facility *</label>
                <input type="text" [(ngModel)]="newEncounter.facility" name="facility" required />
              </div>

              <div class="form-group">
                <label>Attending Clinician</label>
                <input type="text" [(ngModel)]="newEncounter.attendingClinicianName" name="doctor" placeholder="e.g. Dr. Sunita Koirala" />
              </div>

              <div class="form-group">
                <label>Reason for Visit / Chief Complaint</label>
                <textarea [(ngModel)]="newEncounter.reasonForVisit" name="reason" rows="2" placeholder="e.g. Follow-up cycle 3 chemo assessment"></textarea>
              </div>

              <div class="modal-footer">
                <button type="submit" [disabled]="submittingEncounter()" class="btn-primary">
                  {{ submittingEncounter() ? 'Creating...' : 'Save Encounter' }}
                </button>
                <button type="button" (click)="showNewEncounterModal.set(false)" class="btn-cancel">
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>

        <!-- Encounters List -->
        <div *ngIf="encounters().length === 0" class="empty-state">
          No clinical encounters recorded yet. Click "Register New Encounter" to log an episode.
        </div>

        <div *ngIf="encounters().length > 0" class="encounters-timeline">
          <div *ngFor="let enc of encounters()" class="encounter-card">
            <div class="enc-header">
              <div class="enc-left">
                <span class="enc-type-badge" [attr.data-type]="enc.type">{{ enc.type }}</span>
                <span class="enc-num">{{ enc.encounterNumber }}</span>
                <span class="enc-dept">• {{ enc.department }}</span>
              </div>
              <span class="enc-status">{{ enc.status }}</span>
            </div>
            <div class="enc-body">
              <div class="enc-date">Date: {{ enc.startDate | date:'mediumDate' }}</div>
              <div class="enc-clinician" *ngIf="enc.attendingClinicianName">Attending: <strong>{{ enc.attendingClinicianName }}</strong></div>
              <div class="enc-reason" *ngIf="enc.reasonForVisit">"{{ enc.reasonForVisit }}"</div>
            </div>
            <div class="enc-footer">
              <span>📄 {{ enc.documentsCount }} document(s)</span>
              <div>
                <button (click)="openUploadForEncounter(enc.id, 'file')" class="btn-text">
                  ➕ Upload Scans
                </button>
                <button (click)="openUploadForEncounter(enc.id, 'scanner')" class="btn-text btn-scanner-link">
                  🖨️ Hardware Scan
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- TAB 3: Uploaded Documents & Scans -->
      <div *ngIf="activeTab() === 'documents'" class="tab-content">
        <div class="tab-action-bar">
          <div>
            <h3>Longitudinal Scans & Clinical Documents (FR-DOC-001 - FR-DOC-004)</h3>
            <p class="subtitle">Multi-file ingestion, cryptographic hashing, and immutable versioning.</p>
          </div>
          <div class="action-buttons-group">
            <button (click)="openUploadModal('file')" class="btn-secondary">
              📁 Choose Files to Upload
            </button>
            <button (click)="openUploadModal('scanner')" class="btn-primary btn-scanner">
              🖨️ Direct Hardware Scan
            </button>
          </div>
        </div>

        <!-- Multi-File Upload Modal -->
        <div *ngIf="showUploadModal()" class="modal-overlay">
          <div class="modal-box modal-lg">
            <div class="modal-header">
              <h4>{{ uploadMode() === 'scanner' ? 'Physical Hardware Scanner Acquisition' : 'Upload Medical Documents / Scans' }} (FR-DOC-001)</h4>
              <button (click)="showUploadModal.set(false)" class="btn-close">✕</button>
            </div>
            <form (ngSubmit)="onUploadFiles()" class="modal-body">
              <!-- Source Selection Tabs -->
              <div class="ingest-mode-toggle">
                <button type="button" 
                        [class.active]="uploadMode() === 'file'" 
                        (click)="uploadMode.set('file')" 
                        class="mode-toggle-btn">
                  📁 Digital File Upload (PDF / JPG / PNG)
                </button>
                <button type="button" 
                        [class.active]="uploadMode() === 'scanner'" 
                        (click)="switchToScannerMode()" 
                        class="mode-toggle-btn">
                  🖨️ Physical Scanner Device (WIA / USB / ADF)
                </button>
              </div>

              <div class="form-grid">
                <div class="form-group">
                  <label>Associate with Encounter</label>
                  <select [(ngModel)]="uploadMetadata.encounterId" name="encounterId">
                    <option [ngValue]="null">⚠️ Unassigned Historical Chart (Supervisor Review Required)</option>
                    <option *ngFor="let e of encounters()" [value]="e.id">
                      {{ e.encounterNumber }} ({{ e.type }} - {{ e.department }})
                    </option>
                  </select>
                </div>

                <div class="form-group">
                  <label>Document Category *</label>
                  <select [(ngModel)]="uploadMetadata.category" name="category" required>
                    <option value="DischargeSummary">Discharge Summary</option>
                    <option value="PathologyLabReport">Pathology / Biopsy Report</option>
                    <option value="RadiologyReport">Radiology / CT / MRI Scan Report</option>
                    <option value="Prescription">Prescription / Medication Chart</option>
                    <option value="OperativeNote">Surgical / Operative Note</option>
                    <option value="ClinicalProgressNote">Clinical Progress Note</option>
                    <option value="GeneralMedicalChart">General Medical Chart</option>
                  </select>
                </div>

                <div class="form-group">
                  <label>Clinical Date on Report *</label>
                  <input type="date" [(ngModel)]="uploadMetadata.clinicalDate" name="clinicalDate" required />
                </div>

                <div class="form-group">
                  <label>Primary Language</label>
                  <select [(ngModel)]="uploadMetadata.language" name="language">
                    <option value="en">English</option>
                    <option value="ne">Nepali (Devanagari)</option>
                    <option value="mixed">Mixed (English + Nepali)</option>
                  </select>
                </div>
              </div>

              <!-- Option 1: File Drag-and-Drop Area -->
              <div *ngIf="uploadMode() === 'file'" class="upload-dropzone">
                <input 
                  type="file" 
                  #fileInput 
                  (change)="onFilesSelected($event)" 
                  multiple 
                  accept=".pdf,.jpg,.jpeg,.png,.tiff" 
                  class="file-input-hidden" 
                />
                <div class="dropzone-ui" (click)="fileInput.click()">
                  <span class="dropzone-icon">📁</span>
                  <p class="dropzone-text">Click to choose or drag & drop files here</p>
                  <small>Supported: PDF, JPG, PNG, TIFF (Up to 50MB per file with magic-byte validation)</small>
                </div>
              </div>

              <!-- Option 2: Direct Hardware Scanner Control Panel -->
              <div *ngIf="uploadMode() === 'scanner'" class="scanner-panel">
                <div class="scanner-header">
                  <div class="scanner-badge-group">
                    <span class="scanner-online-badge">● Bridge Online (:5100)</span>
                    <span class="scanner-wia-badge">WIA 2.0 Native</span>
                  </div>
                  <button type="button" (click)="loadScannerDevices()" class="btn-refresh-scanners" title="Rescan for USB/Network devices">
                    🔄 Rescan Hardware
                  </button>
                </div>

                <div *ngIf="loadingScanners()" class="scanner-loading-state">
                  Detecting connected scanners via WIA driver service...
                </div>

                <div *ngIf="!loadingScanners()" class="scanner-form-grid">
                  <div class="form-group full-width">
                    <label>Selected Scanner Device</label>
                    <select [(ngModel)]="selectedDeviceId" name="scannerDeviceId">
                      <option *ngFor="let s of scannerDevices()" [value]="s.deviceId">
                        {{ s.name }} ({{ s.manufacturer }}) {{ s.isSimulated ? '[Medical ADF Simulator]' : '[USB/WIA Device]' }}
                      </option>
                    </select>
                  </div>

                  <div class="form-group">
                    <label>Paper Feed Source</label>
                    <select [(ngModel)]="scanSource" name="scanSource">
                      <option value="Feeder">Automatic Document Feeder (ADF)</option>
                      <option value="Flatbed">Flatbed Platen Glass</option>
                    </select>
                  </div>

                  <div class="form-group">
                    <label>Optical Resolution (DPI)</label>
                    <select [(ngModel)]="scanResolution" name="scanResolution">
                      <option [ngValue]="150">150 DPI (Fast Draft)</option>
                      <option [ngValue]="200">200 DPI (Standard Document)</option>
                      <option [ngValue]="300">300 DPI (Medical OCR Standard)</option>
                      <option [ngValue]="600">600 DPI (High Precision Histology)</option>
                    </select>
                  </div>

                  <div class="form-group">
                    <label>Color Mode</label>
                    <select [(ngModel)]="scanColorMode" name="scanColorMode">
                      <option value="Color">24-bit Full Color</option>
                      <option value="Greyscale">8-bit Greyscale</option>
                      <option value="BlackAndWhite">1-bit Black & White (Binary)</option>
                    </select>
                  </div>

                  <div class="form-group">
                    <label>Duplex Scanning</label>
                    <select [(ngModel)]="scanDuplex" name="scanDuplex">
                      <option [ngValue]="false">Single-Sided</option>
                      <option [ngValue]="true">2-Sided (Duplex Sheet-Feed)</option>
                    </select>
                  </div>
                </div>

                <div class="scanner-action-row">
                  <button type="button" 
                          (click)="triggerHardwareScan()" 
                          [disabled]="isScanning() || scannerDevices().length === 0" 
                          class="btn-scan-now">
                    <span *ngIf="!isScanning()">⚡ Scan Page(s) Now</span>
                    <span *ngIf="isScanning()">⏳ Acquiring Scan from Feeder...</span>
                  </button>
                  <span class="scan-hint">Places paper into queue below. Multiple pages can be fed sequentially.</span>
                </div>

                <div *ngIf="scanSuccessMessage()" class="scan-success-alert">
                  ✅ {{ scanSuccessMessage() }}
                </div>

                <div *ngIf="scanError()" class="scan-error-alert">
                  ❌ {{ scanError() }}
                </div>
              </div>

              <!-- Selected Files Queue -->
              <div *ngIf="selectedFiles.length > 0" class="selected-files-list">
                <div class="list-title">Selected for Ingestion ({{ selectedFiles.length }} file(s)):</div>
                <div *ngFor="let f of selectedFiles; let i = index" class="file-item">
                  <span>📄 {{ f.name }} ({{ (f.size / 1024).toFixed(1) }} KB)</span>
                  <button type="button" (click)="removeSelectedFile(i)" class="btn-remove">✕</button>
                </div>
              </div>

              <div *ngIf="uploadErrors().length > 0" class="upload-errors-box">
                <div *ngFor="let err of uploadErrors()" class="err-line">⚠️ {{ err }}</div>
              </div>

              <div class="modal-footer">
                <button type="submit" [disabled]="submittingUpload() || selectedFiles.length === 0" class="btn-primary">
                  {{ submittingUpload() ? 'Validating & Uploading...' : 'Upload & Queue for AI OCR' }}
                </button>
                <button type="button" (click)="showUploadModal.set(false)" class="btn-cancel">
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>

        <!-- Amendment Modal -->
        <div *ngIf="showAmendModal()" class="modal-overlay">
          <div class="modal-box">
            <div class="modal-header">
              <h4>Amend Document / Upload Replacement Version (FR-DOC-004)</h4>
              <button (click)="showAmendModal.set(false)" class="btn-close">✕</button>
            </div>
            <form (ngSubmit)="onAmendSubmit()" class="modal-body">
              <p class="info-note">
                Original file is immutable and preserved in storage. A new version will be created with an audit trail.
              </p>
              <div class="form-group">
                <label>Replacement File *</label>
                <input type="file" (change)="onAmendFileSelected($event)" accept=".pdf,.jpg,.jpeg,.png,.tiff" required />
              </div>
              <div class="form-group">
                <label>Reason for Amendment *</label>
                <textarea [(ngModel)]="amendReason" name="reason" rows="2" placeholder="e.g. Re-scanned missing page, clinician signed copy" required></textarea>
              </div>
              <div class="modal-footer">
                <button type="submit" [disabled]="submittingAmend()" class="btn-primary">
                  {{ submittingAmend() ? 'Amending...' : 'Save New Version' }}
                </button>
                <button type="button" (click)="showAmendModal.set(false)" class="btn-cancel">
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>

        <!-- Documents Table -->
        <div *ngIf="documents().length === 0" class="empty-state">
          No medical scans or records uploaded for this patient. Click "Upload Medical Documents" to ingest historical charts.
        </div>

        <table *ngIf="documents().length > 0" class="docs-table">
          <thead>
            <tr>
              <th>Category</th>
              <th>File Name</th>
              <th>Clinical Date</th>
              <th>Version</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let doc of documents()">
              <td>
                <span class="category-pill">{{ doc.category }}</span>
                <div *ngIf="doc.latestOcr && doc.latestOcr.predictedCategory !== doc.category" class="ai-cat-suggest">
                  🤖 AI: {{ doc.latestOcr.predictedCategory }} ({{ (doc.latestOcr.classificationConfidence * 100).toFixed(0) }}%)
                </div>
              </td>
              <td>
                <button type="button" (click)="openViewModal(doc)" class="doc-name-clickable" title="Click to preview scan in popup">
                  📄 <strong>{{ doc.currentVersion?.originalFileName || 'Document' }}</strong>
                </button>
                <div class="doc-sub">SHA256: <code>{{ doc.currentVersion?.sha256Checksum?.substring(0, 16) }}...</code></div>
              </td>
              <td>{{ doc.clinicalDate }}</td>
              <td>
                <span class="version-badge">v{{ doc.currentVersionNumber }}</span>
              </td>
              <td>
                <span class="status-pill" [attr.data-status]="doc.status">
                  <ng-container *ngIf="doc.status === 'Indexed'">✅ Indexed</ng-container>
                  <ng-container *ngIf="doc.status === 'RequiresReview'">⚠️ Needs Review</ng-container>
                  <ng-container *ngIf="doc.status === 'OcrQueued'">⏳ OCR Queued</ng-container>
                  <ng-container *ngIf="doc.status !== 'Indexed' && doc.status !== 'RequiresReview' && doc.status !== 'OcrQueued'">{{ doc.status }}</ng-container>
                </span>
                <div *ngIf="doc.latestOcr" class="ocr-score-pill" [class.low]="doc.latestOcr.ocrConfidenceScore < 0.70">
                  OCR: {{ (doc.latestOcr.ocrConfidenceScore * 100).toFixed(0) }}% ({{ doc.latestOcr.languageDetected }})
                </div>
              </td>
              <td class="table-actions">
                <button type="button" (click)="openViewModal(doc)" class="btn-view-doc" title="Preview document scan in popup">
                  👁️ View Scan
                </button>
                <button (click)="openOcrModal(doc)" class="btn-ocr-view" title="View OCR text & AI insights">
                  🤖 AI OCR
                </button>
                <a [href]="getDownloadUrl(doc.id)" target="_blank" class="btn-download" title="Download file">
                  ⬇️ Download
                </a>
                <button (click)="openAmendModal(doc.id)" class="btn-amend" title="Create version amendment">
                  ✏️ Amend
                </button>
              </td>
            </tr>
          </tbody>
        </table>

        <!-- In-Browser Document Viewer Pop-up Modal -->
        <div *ngIf="selectedViewDoc()" class="modal-overlay" (click)="closeViewModal()">
          <div class="modal-box modal-viewer" (click)="$event.stopPropagation()">
            <div class="modal-header viewer-header">
              <div class="viewer-title-group">
                <div class="viewer-main-title">
                  <span class="viewer-icon">📄</span>
                  <h3>{{ selectedViewDoc()?.currentVersion?.originalFileName || 'Document Scan' }}</h3>
                </div>
                <div class="viewer-meta-tags">
                  <span class="category-pill">{{ selectedViewDoc()?.category }}</span>
                  <span class="version-badge">Version {{ selectedViewDoc()?.currentVersionNumber || 1 }}</span>
                  <span class="date-tag">📅 {{ selectedViewDoc()?.clinicalDate }}</span>
                  <span class="status-pill" [attr.data-status]="selectedViewDoc()?.status">
                    {{ selectedViewDoc()?.status === 'Indexed' ? '✅ Verified' : '⚠️ ' + selectedViewDoc()?.status }}
                  </span>
                </div>
              </div>
              <div class="viewer-header-actions">
                <a [href]="getDownloadUrl(selectedViewDoc()!.id)" target="_blank" class="btn-popup-download" title="Download file to computer">
                  ⬇️ Download
                </a>
                <a [href]="rawViewUrl" target="_blank" class="btn-popup-newtab" title="Open full scan in new browser tab">
                  🗗 Open in New Tab
                </a>
                <button type="button" (click)="closeViewModal()" class="btn-close" title="Close preview">✕</button>
              </div>
            </div>

            <div class="modal-body viewer-body">
              <div class="viewer-frame-container">
                <iframe 
                  *ngIf="safeViewUrl" 
                  [src]="safeViewUrl" 
                  class="document-iframe" 
                  title="Document Preview Frame">
                </iframe>
              </div>
            </div>

            <div class="modal-footer viewer-footer">
              <div class="viewer-footer-left">
                <span><strong>File Size:</strong> {{ formatBytes(selectedViewDoc()?.currentVersion?.fileSize) }}</span>
                <span class="divider">•</span>
                <span><strong>MIME Type:</strong> {{ selectedViewDoc()?.currentVersion?.contentType || 'application/pdf' }}</span>
                <span class="divider">•</span>
                <span><strong>SHA-256:</strong> <code>{{ selectedViewDoc()?.currentVersion?.sha256Checksum?.substring(0, 20) }}...</code></span>
              </div>
              <div class="viewer-footer-right">
                <button type="button" (click)="openOcrModal(selectedViewDoc()!); closeViewModal()" class="btn-secondary">
                  🤖 Inspect AI OCR Text
                </button>
                <button type="button" (click)="closeViewModal()" class="btn-primary">
                  Close Preview
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- OCR & AI Insights Modal -->
        <div *ngIf="selectedOcrDoc()" class="modal-overlay">
          <div class="modal-box modal-lg">
            <div class="modal-header">
              <h4>AI OCR &amp; Clinical Insights (FR-OCR-001 - FR-OCR-003)</h4>
              <button (click)="selectedOcrDoc.set(null)" class="btn-close">✕</button>
            </div>
            <div class="modal-body">
              <div class="ocr-modal-summary">
                <div>
                  <strong>Document:</strong> {{ selectedOcrDoc()?.currentVersion?.originalFileName }}
                </div>
                <div>
                  <strong>Category:</strong> {{ selectedOcrDoc()?.category }}
                  <span *ngIf="selectedOcrDoc()?.latestOcr" class="ai-pill">
                    AI Predicted: {{ selectedOcrDoc()?.latestOcr?.predictedCategory }} ({{ ((selectedOcrDoc()?.latestOcr?.classificationConfidence || 0) * 100).toFixed(0) }}%)
                  </span>
                </div>
                <div>
                  <strong>OCR Confidence:</strong> 
                  <span [class.text-warning]="(selectedOcrDoc()?.latestOcr?.ocrConfidenceScore || 0) < 0.70">
                    {{ ((selectedOcrDoc()?.latestOcr?.ocrConfidenceScore || 0) * 100).toFixed(0) }}% ({{ selectedOcrDoc()?.latestOcr?.languageDetected }})
                  </span>
                </div>
              </div>

              <div *ngIf="selectedOcrDoc()?.latestOcr?.keywordsDetected?.length" class="keywords-row">
                <label>Matched Oncology Keywords:</label>
                <div class="keyword-tags">
                  <span *ngFor="let kw of selectedOcrDoc()!.latestOcr!.keywordsDetected" class="kw-tag">
                    {{ kw }}
                  </span>
                </div>
              </div>

              <div class="text-tabs">
                <button type="button" [class.active]="ocrModalTab === 'full'" (click)="ocrModalTab = 'full'">
                  Extracted Clinical Text
                </button>
                <button type="button" [class.active]="ocrModalTab === 'nepali'" (click)="ocrModalTab = 'nepali'">
                  Nepali Script (देवनागरी)
                </button>
              </div>

              <div *ngIf="ocrModalTab === 'full'">
                <pre class="ocr-text-display">{{ selectedOcrDoc()?.latestOcr?.extractedText || 'No OCR text extracted yet.' }}</pre>
              </div>

              <div *ngIf="ocrModalTab === 'nepali'">
                <div class="nepali-box">
                  {{ selectedOcrDoc()?.latestOcr?.extractedNepaliText || 'कुनै नेपाली देवनागरी पाठ भेटिएन (No specific Devanagari text isolated)' }}
                </div>
              </div>
            </div>
            <div class="modal-footer">
              <button *ngIf="selectedOcrDoc()?.status === 'RequiresReview'" (click)="goToReviewQueue()" class="btn-primary">
                🔍 Go to Supervisor Verification Workbench
              </button>
              <button type="button" (click)="selectedOcrDoc.set(null)" class="btn-cancel">
                Close
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div *ngIf="loading()" class="loading-state">
      Loading patient longitudinal record...
    </div>
  `,
  styles: [`
    .page-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 2rem 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .back-link {
      color: #0284c7;
      text-decoration: none;
      font-size: 0.875rem;
      font-weight: 500;
      display: inline-block;
      margin-bottom: 0.75rem;
    }
    .header-main {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      border-bottom: 1px solid #e2e8f0;
      padding-bottom: 1.5rem;
      margin-bottom: 1.5rem;
    }
    .patient-title h2 {
      margin: 0 0 0.5rem 0;
      color: #0f172a;
      font-size: 1.75rem;
    }
    .mrn-tag {
      background: #e0f2fe;
      color: #0369a1;
      font-family: monospace;
      font-weight: 700;
      padding: 0.25rem 0.6rem;
      border-radius: 4px;
      font-size: 0.85rem;
      margin-right: 0.5rem;
    }
    .provenance-tag {
      background: #f1f5f9;
      color: #475569;
      padding: 0.25rem 0.6rem;
      border-radius: 4px;
      font-size: 0.8rem;
      margin-right: 0.5rem;
      border: 1px solid #e2e8f0;
    }
    .restricted-tag {
      background: #fef2f2;
      color: #b91c1c;
      padding: 0.25rem 0.6rem;
      border-radius: 4px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .patient-meta code {
      background: #f8fafc;
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
      font-size: 0.75rem;
      color: #64748b;
    }
    .demographics-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 1rem;
      margin-bottom: 2rem;
    }
    .demo-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1.25rem;
      box-shadow: 0 1px 2px rgba(0,0,0,0.05);
    }
    .demo-label {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      text-transform: uppercase;
      font-weight: 600;
      margin-bottom: 0.35rem;
    }
    .demo-val {
      display: block;
      color: #0f172a;
      font-weight: 600;
      font-size: 1.05rem;
    }
    .demo-sub {
      display: block;
      color: #94a3b8;
      font-size: 0.75rem;
      margin-top: 0.25rem;
    }
    .workspace-tabs {
      display: flex;
      gap: 0.5rem;
      border-bottom: 1px solid #e2e8f0;
      margin-bottom: 1.5rem;
    }
    .tab-button {
      padding: 0.75rem 1.25rem;
      background: none;
      border: none;
      font-size: 0.875rem;
      font-weight: 600;
      color: #64748b;
      cursor: pointer;
      border-bottom: 2px solid transparent;
    }
    .tab-button.active {
      color: #0284c7;
      border-bottom-color: #0284c7;
    }
    .tab-button.upcoming {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .tab-action-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }
    .tab-action-bar h3 {
      margin: 0;
      color: #0f172a;
      font-size: 1.15rem;
    }
    .subtitle {
      margin: 0.2rem 0 0 0;
      color: #64748b;
      font-size: 0.85rem;
    }
    .btn-primary {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.65rem 1.25rem;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.875rem;
      cursor: pointer;
    }
    .btn-primary:hover:not(:disabled) {
      background: #0369a1;
    }
    .btn-primary:disabled {
      opacity: 0.6;
    }
    .btn-cancel {
      background: #f1f5f9;
      color: #475569;
      border: 1px solid #cbd5e1;
      padding: 0.65rem 1rem;
      border-radius: 6px;
      cursor: pointer;
      font-size: 0.875rem;
    }
    .section-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1.5rem;
      margin-bottom: 1.5rem;
    }
    .details-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1.5rem;
      font-size: 0.875rem;
    }
    .row-label {
      display: block;
      color: #64748b;
      font-size: 0.75rem;
      margin-bottom: 0.25rem;
    }
    .encounters-timeline {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .encounter-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 1.25rem;
      box-shadow: 0 1px 2px rgba(0,0,0,0.05);
    }
    .enc-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 0.75rem;
    }
    .enc-type-badge {
      background: #e0f2fe;
      color: #0369a1;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 700;
      margin-right: 0.5rem;
    }
    .enc-type-badge[data-type="Emergency"] {
      background: #fee2e2;
      color: #b91c1c;
    }
    .enc-type-badge[data-type="Inpatient"] {
      background: #fef3c7;
      color: #b45309;
    }
    .enc-num {
      font-family: monospace;
      font-weight: 700;
      color: #0f172a;
    }
    .enc-dept {
      color: #64748b;
      font-size: 0.85rem;
    }
    .enc-status {
      font-size: 0.75rem;
      color: #16a34a;
      font-weight: 600;
    }
    .enc-body {
      font-size: 0.875rem;
      color: #334155;
      margin-bottom: 0.75rem;
    }
    .enc-reason {
      font-style: italic;
      color: #64748b;
      margin-top: 0.35rem;
    }
    .enc-footer {
      display: flex;
      justify-content: space-between;
      border-top: 1px solid #f1f5f9;
      padding-top: 0.75rem;
      font-size: 0.8rem;
      color: #64748b;
    }
    .btn-text {
      background: none;
      border: none;
      color: #0284c7;
      cursor: pointer;
      font-weight: 600;
      font-size: 0.8rem;
    }
    /* Modals */
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
    .modal-box {
      background: #ffffff;
      border-radius: 10px;
      width: 100%;
      max-width: 500px;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.2);
      overflow: hidden;
    }
    .modal-lg {
      max-width: 680px;
    }
    .modal-header {
      padding: 1.25rem 1.5rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #e2e8f0;
    }
    .modal-header h4 {
      margin: 0;
      color: #0f172a;
      font-size: 1.1rem;
    }
    .btn-close {
      background: none;
      border: none;
      font-size: 1.25rem;
      cursor: pointer;
      color: #64748b;
    }
    .modal-body {
      padding: 1.5rem;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    .form-group label {
      display: block;
      font-size: 0.8rem;
      font-weight: 600;
      color: #334155;
      margin-bottom: 0.35rem;
    }
    .form-group input, .form-group select, .form-group textarea {
      width: 100%;
      box-sizing: border-box;
      padding: 0.6rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.875rem;
    }
    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1rem;
    }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }
    .upload-dropzone {
      margin-top: 1rem;
      border: 2px dashed #cbd5e1;
      border-radius: 8px;
      padding: 1.5rem;
      text-align: center;
      cursor: pointer;
      background: #f8fafc;
      transition: background 0.15s;
    }
    .upload-dropzone:hover {
      background: #f1f5f9;
      border-color: #0284c7;
    }
    .dropzone-icon {
      font-size: 2rem;
    }
    .dropzone-text {
      margin: 0.5rem 0 0.25rem 0;
      font-weight: 600;
      color: #334155;
      font-size: 0.9rem;
    }
    .file-input-hidden {
      display: none;
    }
    .selected-files-list {
      margin-top: 1rem;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 6px;
      padding: 0.75rem;
    }
    .list-title {
      font-size: 0.75rem;
      font-weight: 700;
      color: #475569;
      margin-bottom: 0.5rem;
    }
    .file-item {
      display: flex;
      justify-content: space-between;
      font-size: 0.8rem;
      padding: 0.25rem 0;
      color: #1e293b;
    }
    .btn-remove {
      background: none;
      border: none;
      color: #ef4444;
      cursor: pointer;
      font-weight: 700;
    }
    .upload-errors-box {
      margin-top: 1rem;
      background: #fef2f2;
      border: 1px solid #fecaca;
      border-radius: 6px;
      padding: 0.75rem;
      font-size: 0.8rem;
      color: #b91c1c;
    }
    /* Docs Table */
    .docs-table {
      width: 100%;
      border-collapse: collapse;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      overflow: hidden;
      font-size: 0.875rem;
    }
    .docs-table th {
      background: #f8fafc;
      padding: 0.75rem 1rem;
      text-align: left;
      border-bottom: 1px solid #e2e8f0;
      color: #475569;
    }
    .docs-table td {
      padding: 0.75rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      color: #1e293b;
    }
    .category-pill {
      background: #f1f5f9;
      color: #334155;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      border: 1px solid #e2e8f0;
    }
    .version-badge {
      background: #fef3c7;
      color: #92400e;
      font-family: monospace;
      font-size: 0.75rem;
      font-weight: 700;
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
    }
    .status-pill {
      font-size: 0.75rem;
      font-weight: 600;
      color: #0369a1;
    }
    .status-pill[data-status="RequiresReview"] {
      color: #d97706;
    }
    .btn-download {
      color: #0284c7;
      text-decoration: none;
      font-weight: 600;
      font-size: 0.75rem;
      margin-right: 0.5rem;
    }
    .btn-amend {
      background: none;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      padding: 0.2rem 0.5rem;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .doc-name-clickable {
      background: none;
      border: none;
      padding: 0;
      color: #0284c7;
      cursor: pointer;
      font-size: 0.85rem;
      text-align: left;
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
    .doc-name-clickable:hover {
      text-decoration: underline;
      color: #0369a1;
    }
    .btn-view-doc {
      background: #eff6ff;
      color: #1d4ed8;
      border: 1px solid #bfdbfe;
      border-radius: 4px;
      padding: 0.25rem 0.55rem;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      margin-right: 0.4rem;
      transition: all 0.15s ease;
    }
    .btn-view-doc:hover {
      background: #dbeafe;
      border-color: #93c5fd;
    }
    .modal-viewer {
      max-width: 95vw;
      width: 1150px;
      height: 90vh;
      display: flex;
      flex-direction: column;
      padding: 0 !important;
      overflow: hidden;
      background: #ffffff;
      border-radius: 12px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
    }
    .viewer-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.85rem 1.25rem;
      border-bottom: 1px solid #e2e8f0;
      background: #f8fafc;
    }
    .viewer-title-group h3 {
      margin: 0;
      font-size: 1.1rem;
      color: #0f172a;
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .viewer-meta-tags {
      display: flex;
      gap: 0.5rem;
      align-items: center;
      margin-top: 0.35rem;
      font-size: 0.75rem;
    }
    .viewer-header-actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .btn-popup-download, .btn-popup-newtab {
      padding: 0.35rem 0.7rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      text-decoration: none;
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      transition: all 0.15s ease;
    }
    .btn-popup-download {
      background: #0284c7;
      color: #ffffff;
    }
    .btn-popup-download:hover {
      background: #0369a1;
    }
    .btn-popup-newtab {
      background: #ffffff;
      color: #0f172a;
      border: 1px solid #cbd5e1;
    }
    .btn-popup-newtab:hover {
      background: #f1f5f9;
    }
    .viewer-body {
      flex: 1;
      padding: 0 !important;
      background: #334155;
      overflow: hidden;
      display: flex;
    }
    .viewer-frame-container {
      width: 100%;
      height: 100%;
      display: flex;
    }
    .document-iframe {
      width: 100%;
      height: 100%;
      border: none;
      background: #ffffff;
    }
    .viewer-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 1.25rem;
      background: #f8fafc;
      border-top: 1px solid #e2e8f0;
      font-size: 0.75rem;
      color: #64748b;
    }
    .viewer-footer-left {
      display: flex;
      align-items: center;
      gap: 0.5rem;
    }
    .viewer-footer-right {
      display: flex;
      align-items: center;
      gap: 0.6rem;
    }
    .info-note {
      font-size: 0.8rem;
      color: #64748b;
      margin-bottom: 1rem;
    }
    .empty-state {
      padding: 3rem;
      text-align: center;
      color: #64748b;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
    }
    .mono {
      font-family: monospace;
    }
    .action-buttons-group {
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .btn-secondary {
      background: #f8fafc;
      color: #334155;
      border: 1px solid #cbd5e1;
      padding: 0.6rem 1.25rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      font-size: 0.875rem;
      transition: all 0.2s ease;
    }
    .btn-secondary:hover {
      background: #f1f5f9;
      border-color: #94a3b8;
    }
    .btn-scanner {
      background: #0d9488 !important;
      border-color: #0d9488 !important;
    }
    .btn-scanner:hover {
      background: #0f766e !important;
    }
    .btn-scanner-link {
      color: #0d9488 !important;
      margin-left: 0.75rem;
    }
    .ingest-mode-toggle {
      display: flex;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      overflow: hidden;
      margin-bottom: 1.5rem;
      background: #f1f5f9;
      padding: 3px;
      gap: 4px;
    }
    .mode-toggle-btn {
      flex: 1;
      padding: 0.65rem 1rem;
      border: none;
      background: transparent;
      color: #475569;
      font-size: 0.875rem;
      font-weight: 600;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s ease;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }
    .mode-toggle-btn.active {
      background: #ffffff;
      color: #0f172a;
      box-shadow: 0 1px 3px rgba(0,0,0,0.1);
    }
    .scanner-panel {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      border-radius: 8px;
      padding: 1.25rem;
      margin-bottom: 1.5rem;
    }
    .scanner-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1rem;
      padding-bottom: 0.75rem;
      border-bottom: 1px solid #e2e8f0;
    }
    .scanner-badge-group {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .scanner-online-badge {
      font-size: 0.75rem;
      font-weight: 700;
      color: #16a34a;
      background: #dcfce7;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      border: 1px solid #bbf7d0;
    }
    .scanner-wia-badge {
      font-size: 0.75rem;
      font-weight: 600;
      color: #0369a1;
      background: #e0f2fe;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
    }
    .btn-refresh-scanners {
      background: none;
      border: 1px solid #cbd5e1;
      border-radius: 4px;
      padding: 0.25rem 0.5rem;
      font-size: 0.75rem;
      cursor: pointer;
      color: #475569;
    }
    .btn-refresh-scanners:hover {
      background: #ffffff;
    }
    .scanner-form-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 1rem;
      margin-bottom: 1.25rem;
    }
    .scanner-form-grid .full-width {
      grid-column: span 2;
    }
    .scanner-action-row {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-top: 1rem;
    }
    .btn-scan-now {
      background: #0d9488;
      color: #ffffff;
      border: none;
      padding: 0.75rem 1.5rem;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.95rem;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      transition: background 0.2s;
    }
    .btn-scan-now:hover:not(:disabled) {
      background: #0f766e;
    }
    .btn-scan-now:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .scan-hint {
      font-size: 0.8rem;
      color: #64748b;
    }
    .scan-success-alert {
      margin-top: 1rem;
      background: #f0fdf4;
      border: 1px solid #bbf7d0;
      color: #15803d;
      padding: 0.6rem 0.85rem;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 500;
    }
    .scan-error-alert {
      margin-top: 1rem;
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.6rem 0.85rem;
      border-radius: 6px;
      font-size: 0.85rem;
    }
    .scanner-loading-state {
      padding: 1.5rem;
      text-align: center;
      color: #64748b;
      font-style: italic;
    }
    .ai-cat-suggest {
      font-size: 0.7rem;
      color: #0369a1;
      margin-top: 0.2rem;
      font-weight: 500;
    }
    .ocr-score-pill {
      font-size: 0.7rem;
      color: #15803d;
      margin-top: 0.2rem;
      font-weight: 600;
    }
    .ocr-score-pill.low {
      color: #b45309;
    }
    .btn-ocr-view {
      background: #e0f2fe;
      color: #0369a1;
      border: 1px solid #bae6fd;
      border-radius: 4px;
      padding: 0.2rem 0.5rem;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      margin-right: 0.5rem;
    }
    .btn-ocr-view:hover {
      background: #bae6fd;
    }
    .ocr-modal-summary {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 0.75rem 1rem;
      border-radius: 6px;
      margin-bottom: 1rem;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      font-size: 0.85rem;
    }
    .ai-pill {
      background: #e0f2fe;
      color: #0369a1;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      margin-left: 0.5rem;
    }
    .text-warning {
      color: #b45309;
      font-weight: 700;
    }
    .keywords-row {
      margin-bottom: 1rem;
    }
    .keywords-row label {
      font-size: 0.75rem;
      font-weight: 600;
      color: #64748b;
      display: block;
      margin-bottom: 0.35rem;
    }
    .keyword-tags {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
    }
    .kw-tag {
      background: #f1f5f9;
      color: #334155;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.75rem;
      border: 1px solid #e2e8f0;
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
    .ocr-text-display {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      padding: 1rem;
      font-family: monospace;
      font-size: 0.8rem;
      max-height: 280px;
      overflow-y: auto;
      white-space: pre-wrap;
      line-height: 1.5;
    }
    .nepali-box {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      padding: 1rem;
      font-size: 0.95rem;
      line-height: 1.6;
      max-height: 280px;
      overflow-y: auto;
    }

    /* Oncology Journey & Timeline Styles */
    .tab-highlight {
      color: #0284c7;
      font-weight: 700;
    }
    .cancer-summary-banner {
      background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
      color: #ffffff;
      padding: 1.5rem;
      border-radius: 12px;
      margin-bottom: 1.5rem;
      box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
    }
    .cancer-header-title h3 {
      margin: 0.25rem 0 1rem 0;
      font-size: 1.35rem;
      font-weight: 700;
      color: #f8fafc;
    }
    .trajectory-tag {
      font-size: 0.75rem;
      background: #0369a1;
      color: #e0f2fe;
      padding: 0.2rem 0.6rem;
      border-radius: 9999px;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .cancer-metrics-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 1.4fr;
      gap: 1.25rem;
    }
    .metric-box {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 1rem;
      display: flex;
      flex-direction: column;
    }
    .metric-label {
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: #94a3b8;
      margin-bottom: 0.4rem;
      font-weight: 600;
    }
    .metric-main {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.35rem;
    }
    .metric-val {
      font-size: 1.1rem;
      font-weight: 700;
      color: #ffffff;
    }
    .badge-stage {
      background: #dc2626;
      color: #ffffff;
      padding: 0.25rem 0.65rem;
      border-radius: 6px;
      font-size: 1rem;
    }
    .code-pill {
      font-size: 0.75rem;
      background: #334155;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      color: #38bdf8;
      font-family: monospace;
    }
    .metric-sub {
      font-size: 0.8rem;
      color: #cbd5e1;
    }
    .biomarker-pills-row {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem;
    }
    .bio-pill {
      font-size: 0.78rem;
      padding: 0.25rem 0.6rem;
      border-radius: 6px;
      background: #1e293b;
      border: 1px solid #475569;
      color: #f1f5f9;
    }
    .bio-pill.bio-pos {
      background: #831843;
      border-color: #db2777;
      color: #fdf2f8;
    }
    .bio-pill.bio-neg {
      background: #1e3a5f;
      border-color: #0284c7;
      color: #f0f9ff;
    }

    .timeline-controls-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
      flex-wrap: wrap;
      gap: 0.75rem;
    }
    .filter-pills {
      display: flex;
      gap: 0.4rem;
      flex-wrap: wrap;
    }
    .filter-pill {
      background: #f1f5f9;
      border: 1px solid #cbd5e1;
      color: #475569;
      padding: 0.35rem 0.85rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .filter-pill.active {
      background: #0284c7;
      border-color: #0284c7;
      color: #ffffff;
    }
    .btn-refresh-timeline {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      padding: 0.4rem 0.9rem;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      color: #0284c7;
      cursor: pointer;
    }

    /* Vertical Timeline */
    .vertical-timeline-track {
      position: relative;
      padding-left: 2.5rem;
      margin-bottom: 2rem;
    }
    .vertical-timeline-track::before {
      content: '';
      position: absolute;
      left: 1.15rem;
      top: 0;
      bottom: 0;
      width: 3px;
      background: #e2e8f0;
    }
    .timeline-event-item {
      position: relative;
      margin-bottom: 1.75rem;
    }
    .timeline-marker {
      position: absolute;
      left: -2.5rem;
      top: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .timeline-dot {
      width: 2.35rem;
      height: 2.35rem;
      border-radius: 50%;
      background: #ffffff;
      border: 3px solid #94a3b8;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1rem;
      z-index: 2;
      box-shadow: 0 2px 4px rgba(0,0,0,0.06);
    }
    .dot-consult { border-color: #0284c7; background: #e0f2fe; }
    .dot-pathology { border-color: #9333ea; background: #f3e8ff; }
    .dot-surgery { border-color: #dc2626; background: #fee2e2; }
    .dot-chemo { border-color: #059669; background: #d1fae5; }
    .dot-radiation { border-color: #d97706; background: #fef3c7; }
    .dot-restaging { border-color: #475569; background: #f1f5f9; }

    .timeline-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 10px;
      padding: 1.25rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      transition: box-shadow 0.2s;
    }
    .timeline-card:hover {
      box-shadow: 0 4px 6px -1px rgba(0,0,0,0.08);
    }
    .milestone-highlight .timeline-card {
      border-left: 4px solid #0284c7;
    }
    .timeline-card-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 0.75rem;
      border-bottom: 1px solid #f1f5f9;
      padding-bottom: 0.65rem;
    }
    .event-title-area h4 {
      margin: 0.2rem 0;
      font-size: 1.05rem;
      color: #0f172a;
    }
    .milestone-badge {
      display: inline-block;
      font-size: 0.72rem;
      font-weight: 700;
      background: #fef08a;
      color: #854d0e;
      padding: 0.15rem 0.55rem;
      border-radius: 4px;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .event-dept {
      font-size: 0.8rem;
      color: #64748b;
    }
    .event-date-badge {
      text-align: right;
    }
    .ev-date {
      display: block;
      font-weight: 700;
      color: #334155;
      font-size: 0.85rem;
    }
    .ev-time {
      display: block;
      color: #94a3b8;
      font-size: 0.75rem;
    }
    .event-summary {
      color: #334155;
      font-size: 0.9rem;
      line-height: 1.5;
      margin-bottom: 0.85rem;
    }
    .event-entities-container {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      padding: 0.85rem;
      margin-bottom: 0.85rem;
    }
    .entities-label {
      display: block;
      font-size: 0.75rem;
      font-weight: 700;
      color: #475569;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      margin-bottom: 0.5rem;
    }
    .entity-chips-list {
      display: flex;
      flex-wrap: wrap;
      gap: 0.45rem;
    }
    .ent-chip {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      padding: 0.3rem 0.65rem;
      border-radius: 6px;
      font-size: 0.8rem;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      transition: all 0.2s;
    }
    .ent-chip:hover {
      border-color: #0284c7;
      background: #f0f9ff;
    }
    .ent-chip.ent-verified {
      border-color: #86efac;
      background: #f0fdf4;
    }
    .chip-type {
      font-size: 0.7rem;
      color: #64748b;
    }
    .chip-val {
      color: #0f172a;
    }
    .chip-code {
      font-size: 0.72rem;
      background: #e2e8f0;
      padding: 0.1rem 0.35rem;
      border-radius: 4px;
      color: #334155;
      font-family: monospace;
    }
    .chip-check {
      color: #16a34a;
      font-weight: 700;
    }
    .timeline-card-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: 0.5rem;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .enc-pill {
      font-size: 0.75rem;
      color: #64748b;
      background: #f1f5f9;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
    }
    .status-verified-pill {
      font-size: 0.75rem;
      color: #16a34a;
      background: #dcfce7;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .btn-inspect-scan {
      background: #f0f9ff;
      border: 1px solid #bae6fd;
      color: #0369a1;
      padding: 0.3rem 0.65rem;
      border-radius: 5px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .empty-timeline-state {
      text-align: center;
      padding: 3rem 1.5rem;
      background: #f8fafc;
      border: 1px dashed #cbd5e1;
      border-radius: 12px;
      color: #64748b;
    }
    .empty-icon {
      font-size: 2.5rem;
      margin-bottom: 0.5rem;
    }

    /* Entity Table Badges */
    .entity-type-badge {
      display: inline-block;
      font-size: 0.72rem;
      font-weight: 700;
      padding: 0.2rem 0.55rem;
      border-radius: 4px;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .badge-diag { background: #fce7f3; color: #9d174d; }
    .badge-stage-tag { background: #fee2e2; color: #991b1b; }
    .badge-bio { background: #e0e7ff; color: #3730a3; }
    .badge-chemo { background: #dcfce7; color: #166534; }
    .badge-surg { background: #ffedd5; color: #9a3412; }
    .badge-rad { background: #fef3c7; color: #92400e; }
    .badge-other { background: #f1f5f9; color: #475569; }

    .entity-value-text {
      font-weight: 600;
      color: #0f172a;
    }
    .ent-attr-preview {
      margin-top: 0.25rem;
      font-size: 0.72rem;
      color: #64748b;
    }
    .conf-bar-wrapper {
      width: 50px;
      height: 6px;
      background: #e2e8f0;
      border-radius: 3px;
      overflow: hidden;
      display: inline-block;
      margin-right: 0.35rem;
      vertical-align: middle;
    }
    .conf-bar-fill {
      height: 100%;
      background: #10b981;
    }
    .badge-verified {
      color: #16a34a;
      background: #dcfce7;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .badge-unverified {
      color: #d97706;
      background: #fef3c7;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .btn-verify-entity {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.35rem 0.75rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-edit-ent {
      background: #f1f5f9;
      color: #334155;
      border: 1px solid #cbd5e1;
    }
    .verification-lead {
      color: #475569;
      font-size: 0.85rem;
      margin-bottom: 1rem;
    }
    .ocr-snippet-box {
      background: #f8fafc;
      border: 1px solid #cbd5e1;
      padding: 0.65rem;
      border-radius: 6px;
      font-style: italic;
      color: #334155;
      font-size: 0.82rem;
    }
  `]
})
export class PatientDetailsComponent implements OnInit {
  patient = signal<PatientDto | null>(null);
  encounters = signal<EncounterDto[]>([]);
  documents = signal<DocumentDto[]>([]);
  timeline = signal<PatientTimelineDto | null>(null);
  entities = signal<ClinicalEntityDto[]>([]);
  activeTab = signal<'timeline' | 'entities' | 'demographics' | 'encounters' | 'documents'>('timeline');
  loading = signal(true);
  loadingTimeline = signal(false);
  timelineFilter = signal<'all' | 'milestones' | 'pathology' | 'surgery' | 'chemo'>('all');
  selectedVerifyEntity = signal<ClinicalEntityDto | null>(null);
  submittingVerifyEntity = signal(false);
  verifyReq: VerifyClinicalEntityRequest = {
    confirmedName: '',
    confirmedValue: '',
    confirmedCode: '',
    verificationNotes: ''
  };

  // Encounter modal state
  showNewEncounterModal = signal(false);
  submittingEncounter = signal(false);
  newEncounter: CreateEncounterRequest = {
    patientId: '',
    type: 'OPD',
    facility: 'BPKMCH Main Campus',
    department: 'Medical Oncology',
    startDate: new Date().toISOString(),
    attendingClinicianName: '',
    reasonForVisit: ''
  };

  // Upload modal state
  showUploadModal = signal(false);
  uploadMode = signal<'file' | 'scanner'>('file');
  submittingUpload = signal(false);
  selectedFiles: File[] = [];
  uploadErrors = signal<string[]>([]);
  uploadMetadata = {
    encounterId: null as string | null,
    category: 'GeneralMedicalChart' as DocumentCategory,
    clinicalDate: new Date().toISOString().substring(0, 10),
    language: 'en',
    confidentialityLevel: 'Normal' as ConfidentialityLevel
  };

  // Hardware Scanner state
  scannerDevices = signal<ScannerDeviceDto[]>([]);
  loadingScanners = signal(false);
  isScanning = signal(false);
  scanError = signal<string | null>(null);
  scanSuccessMessage = signal<string | null>(null);
  selectedDeviceId = '';
  scanResolution = 300;
  scanColorMode: 'Color' | 'Greyscale' | 'BlackAndWhite' = 'Color';
  scanSource: 'Feeder' | 'Flatbed' = 'Feeder';
  scanDuplex = false;

  // Amend modal state
  showAmendModal = signal(false);
  submittingAmend = signal(false);
  amendDocId: string | null = null;
  amendFile: File | null = null;
  amendReason = '';

  // OCR modal state
  selectedOcrDoc = signal<DocumentDto | null>(null);
  ocrModalTab: 'full' | 'nepali' = 'full';

  // Document Viewer popup modal state
  selectedViewDoc = signal<DocumentDto | null>(null);
  safeViewUrl: SafeResourceUrl | null = null;
  rawViewUrl: string = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private patientService: PatientService,
    private encounterService: EncounterService,
    private documentService: DocumentService,
    private scannerService: ScannerService,
    private clinicalEntityService: ClinicalEntityService,
    public authService: AuthService,
    private sanitizer: DomSanitizer
  ) {}

  openViewModal(doc: DocumentDto): void {
    this.selectedViewDoc.set(doc);
    const url = this.documentService.getViewUrl(doc.id, doc.currentVersionNumber);
    this.rawViewUrl = url;
    this.safeViewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(url);
  }

  closeViewModal(): void {
    this.selectedViewDoc.set(null);
    this.safeViewUrl = null;
    this.rawViewUrl = '';
  }

  formatBytes(bytes?: number): string {
    if (!bytes) return 'N/A';
    const kb = bytes / 1024;
    return kb > 1024 ? (kb / 1024).toFixed(1) + ' MB' : kb.toFixed(1) + ' KB';
  }

  openOcrModal(doc: DocumentDto): void {
    this.selectedOcrDoc.set(doc);
    this.ocrModalTab = 'full';
  }

  goToReviewQueue(): void {
    this.selectedOcrDoc.set(null);
    this.router.navigate(['/review-queue']);
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.newEncounter.patientId = id;
      this.loadAllData(id);
    }
  }

  loadAllData(patientId: string): void {
    this.loading.set(true);
    this.patientService.getById(patientId).subscribe({
      next: p => {
        this.patient.set(p);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });

    this.encounterService.getEncountersByPatient(patientId).subscribe({
      next: encs => this.encounters.set(encs)
    });

    this.documentService.getDocumentsByPatient(patientId).subscribe({
      next: docs => this.documents.set(docs)
    });

    this.loadTimeline(patientId);
    this.loadEntities(patientId);
  }

  loadTimeline(patientId?: string): void {
    const id = patientId || this.patient()?.id;
    if (!id) return;
    this.loadingTimeline.set(true);
    this.clinicalEntityService.getPatientTimeline(id).subscribe({
      next: t => {
        this.timeline.set(t);
        this.loadingTimeline.set(false);
      },
      error: () => this.loadingTimeline.set(false)
    });
  }

  loadEntities(patientId?: string): void {
    const id = patientId || this.patient()?.id;
    if (!id) return;
    this.clinicalEntityService.getEntitiesByPatient(id).subscribe({
      next: ents => this.entities.set(ents)
    });
  }

  refreshTimeline(): void {
    this.loadTimeline();
  }

  refreshEntities(): void {
    this.loadEntities();
  }

  filteredTimelineEvents(): TimelineEventDto[] {
    const events = this.timeline()?.events || [];
    const filter = this.timelineFilter();
    if (filter === 'all') return events;
    if (filter === 'milestones') return events.filter(e => e.milestone);
    if (filter === 'pathology') return events.filter(e => e.eventType.includes('Pathology') || e.title.includes('Biopsy') || e.title.includes('Pathology'));
    if (filter === 'surgery') return events.filter(e => e.eventType.includes('Operative') || e.title.includes('Surgery') || e.title.includes('Procedure'));
    if (filter === 'chemo') return events.filter(e => e.eventType.includes('Prescription') || e.title.includes('Chemo'));
    return events;
  }

  getEventDotClass(ev: TimelineEventDto): string {
    if (ev.milestone === 'InitialConsult') return 'dot-consult';
    if (ev.milestone === 'DiagnosticBiopsy' || ev.milestone === 'PathologyConfirmation' || ev.eventType.includes('Pathology')) return 'dot-pathology';
    if (ev.milestone === 'DefinitiveSurgery' || ev.eventType.includes('Operative')) return 'dot-surgery';
    if (ev.milestone === 'AdjuvantChemotherapy' || ev.title.includes('Chemo')) return 'dot-chemo';
    if (ev.milestone === 'RadiationTherapy') return 'dot-radiation';
    return 'dot-consult';
  }

  getEventIcon(ev: TimelineEventDto): string {
    if (ev.milestone === 'InitialConsult') return '🎯';
    if (ev.milestone === 'DiagnosticBiopsy') return '🔬';
    if (ev.milestone === 'PathologyConfirmation') return '📑';
    if (ev.milestone === 'DefinitiveSurgery' || ev.eventType.includes('Operative')) return '⚕️';
    if (ev.milestone === 'AdjuvantChemotherapy' || ev.title.includes('Chemo')) return '💊';
    if (ev.milestone === 'RadiationTherapy') return '📡';
    if (ev.milestone === 'RemissionOrSurveillance') return '🔄';
    return '📋';
  }

  getEntityTypeClass(type: string): string {
    switch (type) {
      case 'Diagnosis': return 'badge-diag';
      case 'TnmStaging':
      case 'CancerStage': return 'badge-stage-tag';
      case 'Biomarker': return 'badge-bio';
      case 'ChemotherapyRegimen':
      case 'Medication': return 'badge-chemo';
      case 'SurgicalProcedure': return 'badge-surg';
      case 'RadiationTherapy': return 'badge-rad';
      default: return 'badge-other';
    }
  }

  openVerifyEntityModal(ent: ClinicalEntityDto): void {
    this.selectedVerifyEntity.set(ent);
    this.verifyReq = {
      confirmedName: ent.name,
      confirmedValue: ent.value,
      confirmedCode: ent.code || '',
      verificationNotes: ''
    };
  }

  submitVerifyEntity(): void {
    const ent = this.selectedVerifyEntity();
    if (!ent) return;

    this.submittingVerifyEntity.set(true);
    this.clinicalEntityService.verifyEntity(ent.id, this.verifyReq).subscribe({
      next: updated => {
        this.submittingVerifyEntity.set(false);
        this.selectedVerifyEntity.set(null);
        this.entities.update(list => list.map(e => e.id === updated.id ? updated : e));
        this.loadTimeline();
      },
      error: () => {
        this.submittingVerifyEntity.set(false);
      }
    });
  }

  openDocFromTimeline(docId: string): void {
    const doc = this.documents().find(d => d.id === docId);
    if (doc) {
      this.openOcrModal(doc);
    }
  }

  onCreateEncounter(): void {
    const p = this.patient();
    if (!p) return;

    this.submittingEncounter.set(true);
    this.newEncounter.patientId = p.id;

    this.encounterService.createEncounter(p.id, this.newEncounter).subscribe({
      next: created => {
        this.submittingEncounter.set(false);
        this.showNewEncounterModal.set(false);
        this.encounters.update(list => [created, ...list]);
      },
      error: () => {
        this.submittingEncounter.set(false);
      }
    });
  }

  openUploadModal(mode: 'file' | 'scanner' = 'file'): void {
    this.uploadMode.set(mode);
    this.scanError.set(null);
    this.scanSuccessMessage.set(null);
    this.showUploadModal.set(true);
    if (mode === 'scanner') {
      this.loadScannerDevices();
    }
  }

  switchToScannerMode(): void {
    this.uploadMode.set('scanner');
    this.scanError.set(null);
    this.scanSuccessMessage.set(null);
    if (this.scannerDevices().length === 0) {
      this.loadScannerDevices();
    }
  }

  loadScannerDevices(): void {
    this.loadingScanners.set(true);
    this.scanError.set(null);
    this.scannerService.getDevices().subscribe({
      next: devices => {
        this.scannerDevices.set(devices);
        if (devices.length > 0 && !this.selectedDeviceId) {
          const defaultDev = devices.find(d => d.isDefault) || devices[0];
          this.selectedDeviceId = defaultDev.deviceId;
        }
        this.loadingScanners.set(false);
      },
      error: err => {
        this.loadingScanners.set(false);
        this.scanError.set(err.message || 'Unable to communicate with local hardware scanner bridge.');
      }
    });
  }

  triggerHardwareScan(): void {
    this.isScanning.set(true);
    this.scanError.set(null);
    this.scanSuccessMessage.set(null);

    this.scannerService.acquireScan({
      deviceId: this.selectedDeviceId || null,
      resolutionDpi: Number(this.scanResolution),
      colorMode: this.scanColorMode,
      source: this.scanSource,
      duplex: this.scanDuplex
    }).subscribe({
      next: result => {
        this.isScanning.set(false);
        if (!result.success || !result.base64Data) {
          this.scanError.set(result.errorMessage || 'Scan acquisition failed.');
          return;
        }

        const scannedFile = this.scannerService.base64ToFile(
          result.base64Data,
          result.fileName || `Scan_${Date.now()}.pdf`,
          result.contentType || 'application/pdf'
        );

        this.selectedFiles = [...this.selectedFiles, scannedFile];
        this.scanSuccessMessage.set(`Acquired scan: ${scannedFile.name} (${(scannedFile.size / 1024).toFixed(1)} KB). Added to ingestion queue.`);
      },
      error: err => {
        this.isScanning.set(false);
        this.scanError.set(err.message || 'Communication with scanner failed. Check USB connection.');
      }
    });
  }

  openUploadForEncounter(encounterId: string, mode: 'file' | 'scanner' = 'file'): void {
    this.uploadMetadata.encounterId = encounterId;
    this.activeTab.set('documents');
    this.openUploadModal(mode);
  }

  onFilesSelected(event: any): void {
    if (event.target.files) {
      this.selectedFiles = Array.from(event.target.files);
    }
  }

  removeSelectedFile(index: number): void {
    this.selectedFiles.splice(index, 1);
  }

  onUploadFiles(): void {
    const p = this.patient();
    if (!p || this.selectedFiles.length === 0) return;

    this.submittingUpload.set(true);
    this.uploadErrors.set([]);

    const formData = new FormData();
    if (this.uploadMetadata.encounterId) {
      formData.append('encounterId', this.uploadMetadata.encounterId);
    }
    formData.append('category', this.uploadMetadata.category);
    formData.append('clinicalDate', this.uploadMetadata.clinicalDate);
    formData.append('language', this.uploadMetadata.language);
    formData.append('confidentialityLevel', this.uploadMetadata.confidentialityLevel);

    for (const file of this.selectedFiles) {
      formData.append('files', file, file.name);
    }

    this.documentService.uploadDocuments(p.id, formData).subscribe({
      next: res => {
        this.submittingUpload.set(false);
        if (res.errors?.length > 0) {
          this.uploadErrors.set(res.errors);
        }
        if (res.createdDocuments?.length > 0) {
          this.documents.update(existing => [...res.createdDocuments, ...existing]);
          if (res.errors?.length === 0) {
            this.showUploadModal.set(false);
            this.selectedFiles = [];
          }
        }
      },
      error: err => {
        this.submittingUpload.set(false);
        this.uploadErrors.set([err.error?.message || 'Upload failed. Check file types and sizes.']);
      }
    });
  }

  openAmendModal(docId: string): void {
    this.amendDocId = docId;
    this.amendFile = null;
    this.amendReason = '';
    this.showAmendModal.set(true);
  }

  onAmendFileSelected(event: any): void {
    if (event.target.files?.length > 0) {
      this.amendFile = event.target.files[0];
    }
  }

  onAmendSubmit(): void {
    if (!this.amendDocId || !this.amendFile || !this.amendReason) return;

    this.submittingAmend.set(true);
    const formData = new FormData();
    formData.append('file', this.amendFile, this.amendFile.name);
    formData.append('reasonForAmendment', this.amendReason);

    this.documentService.amendDocument(this.amendDocId, formData).subscribe({
      next: updated => {
        this.submittingAmend.set(false);
        this.showAmendModal.set(false);
        this.documents.update(docs => docs.map(d => d.id === updated.id ? updated : d));
      },
      error: () => {
        this.submittingAmend.set(false);
      }
    });
  }

  getDownloadUrl(docId: string): string {
    return this.documentService.getDownloadUrl(docId);
  }

  getViewUrl(docId: string): string {
    return this.documentService.getViewUrl(docId);
  }
}
