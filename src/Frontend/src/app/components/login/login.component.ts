import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-wrapper">
      <div class="login-card">
        <div class="hospital-brand">
          <div class="brand-icon">🏥</div>
          <h2>B.P. Koirala Memorial Cancer Hospital</h2>
          <p class="subtitle">AI-Powered Patient Records Digitization & Intelligence</p>
          <span class="badge">Pilot Deployment • On-Premises</span>
        </div>

        <div *ngIf="errorMessage()" class="alert-error">
          {{ errorMessage() }}
        </div>

        <form (ngSubmit)="onLogin()">
          <div class="form-group">
            <label for="username">Username</label>
            <input 
              id="username" 
              type="text" 
              [(ngModel)]="username" 
              name="username" 
              placeholder="e.g. clerk, doctor, supervisor" 
              required 
            />
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input 
              id="password" 
              type="password" 
              [(ngModel)]="password" 
              name="password" 
              placeholder="••••••••" 
              required 
            />
          </div>

          <button type="submit" [disabled]="loading()" class="btn-primary">
            <span *ngIf="loading()">Signing in...</span>
            <span *ngIf="!loading()">Secure Login</span>
          </button>
        </form>

        <div class="quick-profiles">
          <p class="section-title">Quick Demo Logins (SRS Personas):</p>
          <div class="profile-buttons">
            <button type="button" (click)="setProfile('clerk', 'Password@123')" class="btn-chip">
              📋 Registration Clerk
            </button>
            <button type="button" (click)="setProfile('doctor', 'Password@123')" class="btn-chip">
              🩺 Treating Clinician
            </button>
            <button type="button" (click)="setProfile('supervisor', 'Password@123')" class="btn-chip">
              🛡️ Records Supervisor
            </button>
            <button type="button" (click)="setProfile('admin', 'Password@123')" class="btn-chip">
              ⚙️ Hospital Admin
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrapper {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
      padding: 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .login-card {
      background: #ffffff;
      width: 100%;
      max-width: 480px;
      border-radius: 12px;
      padding: 2.5rem;
      box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.25);
    }
    .hospital-brand {
      text-align: center;
      margin-bottom: 2rem;
    }
    .brand-icon {
      font-size: 2.5rem;
      margin-bottom: 0.5rem;
    }
    h2 {
      color: #0f172a;
      font-size: 1.25rem;
      font-weight: 700;
      margin: 0 0 0.5rem 0;
    }
    .subtitle {
      color: #64748b;
      font-size: 0.875rem;
      margin: 0 0 0.75rem 0;
    }
    .badge {
      display: inline-block;
      background: #e0f2fe;
      color: #0369a1;
      padding: 0.25rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.75rem;
      font-weight: 600;
    }
    .form-group {
      margin-bottom: 1.25rem;
    }
    label {
      display: block;
      color: #334155;
      font-size: 0.875rem;
      font-weight: 500;
      margin-bottom: 0.375rem;
    }
    input {
      width: 100%;
      box-sizing: border-box;
      padding: 0.65rem 0.85rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.95rem;
      transition: border-color 0.15s;
    }
    input:focus {
      outline: none;
      border-color: #0284c7;
      box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
    }
    .btn-primary {
      width: 100%;
      background: #0284c7;
      color: #ffffff;
      border: none;
      padding: 0.75rem;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.95rem;
      cursor: pointer;
      transition: background 0.15s;
    }
    .btn-primary:hover:not(:disabled) {
      background: #0369a1;
    }
    .btn-primary:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .alert-error {
      background: #fef2f2;
      border: 1px solid #fecaca;
      color: #b91c1c;
      padding: 0.75rem;
      border-radius: 6px;
      font-size: 0.875rem;
      margin-bottom: 1.25rem;
    }
    .quick-profiles {
      margin-top: 2rem;
      padding-top: 1.5rem;
      border-top: 1px solid #e2e8f0;
    }
    .section-title {
      font-size: 0.75rem;
      color: #64748b;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 0.75rem;
    }
    .profile-buttons {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.5rem;
    }
    .btn-chip {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 0.5rem;
      border-radius: 6px;
      font-size: 0.75rem;
      color: #334155;
      cursor: pointer;
      text-align: left;
      transition: all 0.15s;
    }
    .btn-chip:hover {
      background: #f1f5f9;
      border-color: #cbd5e1;
    }
  `]
})
export class LoginComponent {
  username = '';
  password = '';
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(private authService: AuthService, private router: Router) {}

  setProfile(u: string, p: string) {
    this.username = u;
    this.password = p;
    this.errorMessage.set(null);
  }

  onLogin() {
    if (!this.username || !this.password) return;

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/patients']);
      },
      error: err => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message || 'Authentication failed. Please verify credentials.');
      }
    });
  }
}
