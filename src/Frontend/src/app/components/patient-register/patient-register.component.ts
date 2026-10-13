import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PatientService } from '../../services/patient.service';
import { CreatePatientRequest, DuplicateMatchCandidate, Gender } from '../../models/patient.models';

@Component({
  selector: 'app-patient-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-container">
      <div class="page-header">
        <a routerLink="/patients" class="back-link">← Back to Patient Directory</a>
        <h2>New Patient Registration</h2>
        <p class="subtitle">Assign immutable internal ID and verify duplicate checks against BPKMCH registry (FR-PAT-001, FR-PAT-002)</p>
      </div>

      <!-- Duplicate Detection Warning Modal / Alert -->
      <div *ngIf="duplicateCandidates().length > 0" class="duplicate-alert-banner">
        <div class="alert-icon">⚠️</div>
        <div class="alert-content">
          <h3>Potential Duplicate Records Detected!</h3>
          <p>
            The system found <strong>{{ duplicateCandidates().length }}</strong> existing patient(s) matching this demographic criteria.
            Per BPKMCH clinical policy (FR-PAT-002), silent record merging is prohibited.
          </p>

          <div class="duplicate-cards">
            <div *ngFor="let d of duplicateCandidates()" class="dup-card">
              <div class="dup-header">
                <span class="dup-mrn">MRN: {{ d.mrn }}</span>
                <span class="dup-score">Match Score: {{ (d.matchScore * 100).toFixed(0) }}%</span>
              </div>
              <div class="dup-body">
                <div class="dup-name">{{ d.fullName }}</div>
                <div class="dup-meta">DOB: {{ d.dateOfBirth }} • Phone: {{ d.phoneNumber || 'N/A' }}</div>
                <div class="dup-reason">{{ d.matchReason }}</div>
              </div>
              <div class="dup-actions">
                <button type="button" (click)="useExistingRecord(d.existingPatientId)" class="btn-reuse">
                  Use This Existing Record
                </button>
              </div>
            </div>
          </div>

          <div class="alert-override-actions">
            <button type="button" (click)="confirmOverrideDuplicate()" class="btn-override">
              Verified Non-Duplicate: Proceed with Registration
            </button>
            <button type="button" (click)="dismissDuplicates()" class="btn-cancel">
              Cancel & Modify Info
            </button>
          </div>
        </div>
      </div>

      <!-- Registration Form -->
      <div class="form-card">
        <div *ngIf="generalError()" class="error-banner">
          {{ generalError() }}
        </div>

        <form (ngSubmit)="onSubmitRegistration(false)">
          <div class="form-section">
            <h3>1. Core Identifiers (FR-PAT-001)</h3>
            <div class="form-grid">
              <div class="form-group">
                <label>Hospital Registration No. (MRN) *</label>
                <input 
                  type="text" 
                  [(ngModel)]="formData.mrn" 
                  name="mrn" 
                  placeholder="e.g. BPKMCH-2026-1049" 
                  required 
                />
                <small>Unique identifier within BPKMCH authority.</small>
              </div>

              <div class="form-group">
                <label>National ID / Citizenship No.</label>
                <input 
                  type="text" 
                  [(ngModel)]="formData.nationalId" 
                  name="nationalId" 
                  placeholder="e.g. 12-01-78-01234" 
                />
              </div>

              <div class="form-group">
                <label>Identity Provenance *</label>
                <select [(ngModel)]="formData.identityProvenance" name="identityProvenance" required>
                  <option value="Citizenship Certificate">Citizenship Certificate</option>
                  <option value="National Identity Card">National Identity Card</option>
                  <option value="OPD Registration Desk">OPD Registration Desk</option>
                  <option value="Emergency Intake">Emergency Intake</option>
                  <option value="Self-Reported">Self-Reported</option>
                </select>
              </div>
            </div>
          </div>

          <div class="form-section">
            <h3>2. Patient Demographics</h3>
            <div class="form-grid">
              <div class="form-group">
                <label>First Name *</label>
                <input type="text" [(ngModel)]="formData.firstName" name="firstName" required />
              </div>

              <div class="form-group">
                <label>Middle Name</label>
                <input type="text" [(ngModel)]="formData.middleName" name="middleName" />
              </div>

              <div class="form-group">
                <label>Last Name *</label>
                <input type="text" [(ngModel)]="formData.lastName" name="lastName" required />
              </div>

              <div class="form-group">
                <label>Date of Birth *</label>
                <input type="date" [(ngModel)]="formData.dateOfBirth" name="dateOfBirth" required />
              </div>

              <div class="form-group">
                <label>Gender *</label>
                <select [(ngModel)]="formData.gender" name="gender" required>
                  <option value="Male">Male</option>
                  <option value="Female">Female</option>
                  <option value="Other">Other</option>
                  <option value="Unknown">Unknown</option>
                </select>
              </div>

              <div class="form-group">
                <label>Primary Phone Number</label>
                <input type="tel" [(ngModel)]="formData.phoneNumber" name="phoneNumber" placeholder="98XXXXXXXX" />
              </div>
            </div>
          </div>

          <div class="form-section">
            <h3>3. Address & Geography (Nepal Context)</h3>
            <div class="form-grid">
              <div class="form-group">
                <label>District</label>
                <input type="text" [(ngModel)]="formData.district" name="district" placeholder="e.g. Chitwan, Nawalpur" />
              </div>

              <div class="form-group">
                <label>Municipality</label>
                <input type="text" [(ngModel)]="formData.municipality" name="municipality" placeholder="e.g. Bharatpur Metropolitan" />
              </div>

              <div class="form-group">
                <label>Ward No.</label>
                <input type="text" [(ngModel)]="formData.ward" name="ward" placeholder="e.g. 10" />
              </div>

              <div class="form-group full-width">
                <label>Full Address / Landmark</label>
                <input type="text" [(ngModel)]="formData.addressLine" name="addressLine" placeholder="Tole, Village, or Chowk" />
              </div>
            </div>
          </div>

          <div class="form-section">
            <h3>4. Emergency Contact</h3>
            <div class="form-grid">
              <div class="form-group">
                <label>Contact Person Name</label>
                <input type="text" [(ngModel)]="formData.emergencyContactName" name="emergencyContactName" />
              </div>

              <div class="form-group">
                <label>Relation</label>
                <input type="text" [(ngModel)]="formData.emergencyContactRelation" name="emergencyContactRelation" placeholder="e.g. Spouse, Son, Mother" />
              </div>

              <div class="form-group">
                <label>Contact Phone</label>
                <input type="tel" [(ngModel)]="formData.emergencyContactPhone" name="emergencyContactPhone" />
              </div>
            </div>
          </div>

          <div class="form-actions">
            <button type="submit" [disabled]="submitting()" class="btn-submit">
              {{ submitting() ? 'Verifying & Registering...' : 'Register Patient Record' }}
            </button>
            <button type="button" routerLink="/patients" class="btn-cancel-form">
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      max-width: 900px;
      margin: 0 auto;
      padding: 2rem 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .back-link {
      color: #0284c7;
      text-decoration: none;
      font-size: 0.875rem;
      font-weight: 500;
      margin-bottom: 0.5rem;
      display: inline-block;
    }
    h2 {
      margin: 0;
      color: #0f172a;
      font-size: 1.5rem;
      font-weight: 700;
    }
    .subtitle {
      margin: 0.25rem 0 1.5rem 0;
      color: #64748b;
      font-size: 0.875rem;
    }
    .duplicate-alert-banner {
      background: #fffbeb;
      border: 1px solid #fde68a;
      border-radius: 8px;
      padding: 1.5rem;
      margin-bottom: 2rem;
      display: flex;
      gap: 1rem;
    }
    .alert-icon {
      font-size: 2rem;
    }
    .alert-content h3 {
      margin: 0 0 0.5rem 0;
      color: #92400e;
      font-size: 1.15rem;
    }
    .alert-content p {
      margin: 0 0 1rem 0;
      color: #78350f;
      font-size: 0.875rem;
      line-height: 1.4;
    }
    .duplicate-cards {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      margin-bottom: 1.25rem;
    }
    .dup-card {
      background: #ffffff;
      border: 1px solid #fef3c7;
      border-radius: 6px;
      padding: 1rem;
      box-shadow: 0 1px 2px rgba(0,0,0,0.05);
    }
    .dup-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.5rem;
    }
    .dup-mrn {
      font-family: monospace;
      font-weight: 700;
      color: #0f172a;
    }
    .dup-score {
      background: #fef08a;
      color: #854d0e;
      font-size: 0.75rem;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
      font-weight: 600;
    }
    .dup-name {
      font-weight: 600;
      color: #1e293b;
    }
    .dup-meta {
      font-size: 0.75rem;
      color: #64748b;
      margin: 0.2rem 0;
    }
    .dup-reason {
      font-size: 0.75rem;
      color: #b45309;
      font-weight: 500;
    }
    .dup-actions {
      margin-top: 0.75rem;
    }
    .btn-reuse {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.4rem 0.85rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .alert-override-actions {
      display: flex;
      gap: 0.75rem;
    }
    .btn-override {
      background: #d97706;
      color: #ffffff;
      border: none;
      padding: 0.6rem 1rem;
      border-radius: 6px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-cancel {
      background: #ffffff;
      color: #475569;
      border: 1px solid #cbd5e1;
      padding: 0.6rem 1rem;
      border-radius: 6px;
      font-size: 0.85rem;
      cursor: pointer;
    }
    .form-card {
      background: #ffffff;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      padding: 2rem;
      box-shadow: 0 1px 3px rgba(0,0,0,0.1);
    }
    .form-section {
      margin-bottom: 2rem;
      padding-bottom: 1.5rem;
      border-bottom: 1px solid #f1f5f9;
    }
    .form-section:last-of-type {
      border-bottom: none;
    }
    .form-section h3 {
      margin: 0 0 1rem 0;
      color: #0f172a;
      font-size: 1rem;
      font-weight: 600;
    }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 1.25rem;
    }
    .full-width {
      grid-column: 1 / -1;
    }
    .form-group label {
      display: block;
      color: #334155;
      font-size: 0.8rem;
      font-weight: 600;
      margin-bottom: 0.35rem;
    }
    .form-group input, .form-group select {
      width: 100%;
      box-sizing: border-box;
      padding: 0.65rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.9rem;
    }
    .form-group small {
      font-size: 0.7rem;
      color: #64748b;
      margin-top: 0.25rem;
      display: block;
    }
    .form-actions {
      display: flex;
      gap: 1rem;
      margin-top: 2rem;
    }
    .btn-submit {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.75rem 1.75rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-submit:disabled {
      opacity: 0.6;
    }
    .btn-cancel-form {
      background: #f1f5f9;
      color: #475569;
      border: 1px solid #cbd5e1;
      padding: 0.75rem 1.25rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .error-banner {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.85rem;
      border-radius: 6px;
      margin-bottom: 1.5rem;
      font-size: 0.875rem;
    }
  `]
})
export class PatientRegisterComponent {
  formData: CreatePatientRequest = {
    mrn: '',
    nationalId: '',
    firstName: '',
    middleName: '',
    lastName: '',
    dateOfBirth: '',
    gender: 'Male',
    phoneNumber: '',
    email: '',
    district: 'Chitwan',
    municipality: 'Bharatpur',
    ward: '',
    addressLine: '',
    emergencyContactName: '',
    emergencyContactPhone: '',
    emergencyContactRelation: '',
    identityProvenance: 'OPD Registration Desk',
    forceCreateIfDuplicate: false
  };

  submitting = signal(false);
  duplicateCandidates = signal<DuplicateMatchCandidate[]>([]);
  generalError = signal<string | null>(null);

  constructor(
    private patientService: PatientService,
    private router: Router
  ) {}

  onSubmitRegistration(force: boolean = false): void {
    if (!this.formData.mrn || !this.formData.firstName || !this.formData.lastName || !this.formData.dateOfBirth) {
      this.generalError.set('Please fill out all required fields (MRN, First Name, Last Name, Date of Birth).');
      return;
    }

    this.submitting.set(true);
    this.generalError.set(null);
    this.formData.forceCreateIfDuplicate = force;

    this.patientService.create(this.formData).subscribe({
      next: created => {
        this.submitting.set(false);
        this.router.navigate(['/patients', created.id]);
      },
      error: err => {
        this.submitting.set(false);
        if (err.status === 409 && err.error?.potentialDuplicates?.length > 0) {
          // Duplicate warning triggered!
          this.duplicateCandidates.set(err.error.potentialDuplicates);
        } else {
          this.generalError.set(err.error?.errorMessage || err.error?.message || 'Registration failed. Verify MRN uniqueness.');
        }
      }
    });
  }

  useExistingRecord(existingId: string): void {
    this.router.navigate(['/patients', existingId]);
  }

  confirmOverrideDuplicate(): void {
    this.onSubmitRegistration(true);
  }

  dismissDuplicates(): void {
    this.duplicateCandidates.set([]);
  }
}
