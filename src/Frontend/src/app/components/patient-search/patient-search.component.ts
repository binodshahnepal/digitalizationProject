import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { PatientService } from '../../services/patient.service';
import { AuthService } from '../../services/auth.service';
import { PatientDto } from '../../models/patient.models';

@Component({
  selector: 'app-patient-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Patient Registry & Longitudinal Directory</h2>
          <p class="subtitle">Search and manage master patient records for BPKMCH</p>
        </div>
        <div class="header-actions" *ngIf="canRegister()">
          <button (click)="navigateToRegister()" class="btn-create">
            ➕ Register New Patient
          </button>
        </div>
      </div>

      <!-- Search Toolbar -->
      <div class="search-card">
        <form (ngSubmit)="performSearch()" class="search-form">
          <div class="input-wrapper">
            <span class="search-icon">🔍</span>
            <input 
              type="text" 
              [(ngModel)]="searchQuery" 
              name="searchQuery"
              placeholder="Search by MRN, Patient Name, National ID, or Phone..." 
              class="search-input"
            />
          </div>
          <button type="submit" [disabled]="loading()" class="btn-search">
            {{ loading() ? 'Searching...' : 'Search Records' }}
          </button>
          <button type="button" (click)="resetSearch()" class="btn-reset">
            Reset
          </button>
        </form>
      </div>

      <!-- Patient Table -->
      <div class="table-card">
        <div *ngIf="loading()" class="loading-state">
          Loading patient directory...
        </div>

        <div *ngIf="!loading() && patients().length === 0" class="empty-state">
          <p>No patient records found matching your query.</p>
          <button *ngIf="canRegister()" (click)="navigateToRegister()" class="btn-link">
            Register new patient with this criteria?
          </button>
        </div>

        <table *ngIf="!loading() && patients().length > 0" class="patient-table">
          <thead>
            <tr>
              <th>MRN</th>
              <th>Patient Name</th>
              <th>Age / Gender</th>
              <th>Contact / District</th>
              <th>Provenance</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let p of patients()" [class.restricted-row]="p.isRestricted">
              <td>
                <span class="mrn-badge">{{ p.mrn }}</span>
              </td>
              <td>
                <div class="patient-name">{{ p.fullName }}</div>
                <div class="patient-sub" *ngIf="p.nationalId">NID: {{ p.nationalId }}</div>
              </td>
              <td>
                {{ p.age }} yrs • {{ p.gender }}
                <div class="patient-sub">DOB: {{ p.dateOfBirth }}</div>
              </td>
              <td>
                {{ p.phoneNumber || 'N/A' }}
                <div class="patient-sub">{{ p.district || 'Nepal' }}</div>
              </td>
              <td>
                <span class="provenance-tag">{{ p.identityProvenance }}</span>
              </td>
              <td>
                <span *ngIf="p.isRestricted" class="status-restricted">Restricted</span>
                <span *ngIf="!p.isRestricted" class="status-active">Active</span>
              </td>
              <td>
                <button (click)="viewPatient(p.id)" class="btn-view">
                  View Timeline & Docs ➔
                </button>
              </td>
            </tr>
          </tbody>
        </table>

        <!-- Pagination -->
        <div class="pagination-bar" *ngIf="totalPages() > 1">
          <span>Page {{ currentPage() }} of {{ totalPages() }} (Total: {{ totalCount() }})</span>
          <div class="pagination-buttons">
            <button [disabled]="currentPage() === 1" (click)="goToPage(currentPage() - 1)">Previous</button>
            <button [disabled]="currentPage() === totalPages()" (click)="goToPage(currentPage() + 1)">Next</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .page-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 2rem 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 1.5rem;
    }
    h2 {
      margin: 0;
      color: #0f172a;
      font-size: 1.5rem;
      font-weight: 700;
    }
    .subtitle {
      margin: 0.25rem 0 0 0;
      color: #64748b;
      font-size: 0.875rem;
    }
    .btn-create {
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.65rem 1.25rem;
      border-radius: 6px;
      font-weight: 600;
      cursor: pointer;
      font-size: 0.875rem;
      transition: background 0.15s;
    }
    .btn-create:hover {
      background: #0369a1;
    }
    .search-card {
      background: #ffffff;
      padding: 1.25rem;
      border-radius: 8px;
      box-shadow: 0 1px 3px rgba(0,0,0,0.1);
      margin-bottom: 1.5rem;
      border: 1px solid #e2e8f0;
    }
    .search-form {
      display: flex;
      gap: 0.75rem;
    }
    .input-wrapper {
      position: relative;
      flex: 1;
    }
    .search-icon {
      position: absolute;
      left: 0.75rem;
      top: 50%;
      transform: translateY(-50%);
      font-size: 0.9rem;
      opacity: 0.5;
    }
    .search-input {
      width: 100%;
      box-sizing: border-box;
      padding: 0.65rem 0.75rem 0.65rem 2.25rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.95rem;
    }
    .search-input:focus {
      outline: none;
      border-color: #0284c7;
    }
    .btn-search {
      background: #0f172a;
      color: #ffffff;
      border: none;
      padding: 0 1.25rem;
      border-radius: 6px;
      font-weight: 500;
      cursor: pointer;
    }
    .btn-reset {
      background: #f1f5f9;
      color: #475569;
      border: 1px solid #cbd5e1;
      padding: 0 1rem;
      border-radius: 6px;
      cursor: pointer;
    }
    .table-card {
      background: #ffffff;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.1);
      overflow: hidden;
    }
    .patient-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.875rem;
    }
    th {
      background: #f8fafc;
      color: #475569;
      font-weight: 600;
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #e2e8f0;
    }
    td {
      padding: 0.85rem 1rem;
      border-bottom: 1px solid #f1f5f9;
      color: #1e293b;
    }
    tr:hover {
      background: #f8fafc;
    }
    .mrn-badge {
      background: #e0f2fe;
      color: #0369a1;
      font-family: monospace;
      font-weight: 600;
      padding: 0.2rem 0.5rem;
      border-radius: 4px;
    }
    .patient-name {
      font-weight: 600;
      color: #0f172a;
    }
    .patient-sub {
      font-size: 0.75rem;
      color: #64748b;
      margin-top: 0.15rem;
    }
    .provenance-tag {
      background: #f1f5f9;
      color: #475569;
      font-size: 0.75rem;
      padding: 0.2rem 0.4rem;
      border-radius: 4px;
      border: 1px solid #e2e8f0;
    }
    .status-active {
      color: #16a34a;
      font-weight: 600;
      font-size: 0.75rem;
    }
    .status-restricted {
      color: #dc2626;
      font-weight: 600;
      font-size: 0.75rem;
    }
    .btn-view {
      background: transparent;
      border: 1px solid #0284c7;
      color: #0284c7;
      padding: 0.35rem 0.75rem;
      border-radius: 4px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
    }
    .btn-view:hover {
      background: #0284c7;
      color: #ffffff;
    }
    .loading-state, .empty-state {
      padding: 3rem;
      text-align: center;
      color: #64748b;
    }
    .btn-link {
      background: none;
      border: none;
      color: #0284c7;
      text-decoration: underline;
      cursor: pointer;
      font-size: 0.875rem;
      margin-top: 0.5rem;
    }
    .pagination-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 0.75rem 1rem;
      background: #f8fafc;
      font-size: 0.75rem;
      color: #64748b;
    }
    .pagination-buttons button {
      padding: 0.25rem 0.75rem;
      margin-left: 0.5rem;
      border: 1px solid #cbd5e1;
      background: #ffffff;
      border-radius: 4px;
      cursor: pointer;
    }
  `]
})
export class PatientSearchComponent implements OnInit {
  searchQuery = '';
  patients = signal<PatientDto[]>([]);
  totalCount = signal(0);
  currentPage = signal(1);
  totalPages = signal(1);
  loading = signal(false);

  constructor(
    private patientService: PatientService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.performSearch();
  }

  canRegister(): boolean {
    return this.authService.isClerk() || this.authService.isSupervisor() || this.authService.isAdmin();
  }

  performSearch(page: number = 1): void {
    this.loading.set(true);
    this.currentPage.set(page);

    this.patientService.search(this.searchQuery, page, 15).subscribe({
      next: res => {
        this.patients.set(res.items);
        this.totalCount.set(res.totalCount);
        this.totalPages.set(res.totalPages);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      }
    });
  }

  resetSearch(): void {
    this.searchQuery = '';
    this.performSearch(1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages()) {
      this.performSearch(p);
    }
  }

  viewPatient(id: string): void {
    this.router.navigate(['/patients', id]);
  }

  navigateToRegister(): void {
    this.router.navigate(['/patients/register']);
  }
}
