import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="login-wrapper">
      <!-- Top Accent Electric Blue Notch -->
      <div class="cyber-notch"></div>

      <!-- Corner Matrix Accents -->
      <div class="corner-decor top-left">
        <div class="cyan-sq"></div>
        <div class="dot-pat"></div>
      </div>
      <div class="corner-decor bottom-right">
        <div class="dot-pat"></div>
        <div class="bracket-sq"></div>
      </div>

      <!-- Cyber Login Card -->
      <div class="login-card">
        <div class="hospital-brand">
          <div class="cube-brand-icon">
            <div class="cube-facet"></div>
          </div>
          <h2>B.P. Koirala Memorial Cancer Hospital</h2>
          <p class="subtitle">AI Records Digitization &amp; Clinical Intelligence</p>
          <div class="badge-cluster">
            <span class="badge">Pilot Deployment</span>
            <span class="badge-cyan">Air-Gapped Vault</span>
          </div>
        </div>

        <div *ngIf="errorMessage()" class="alert-error">
          ⚠️ {{ errorMessage() }}
        </div>

        <form (ngSubmit)="onLogin()">
          <div class="form-group">
            <label for="username">Username</label>
            <div class="input-wrap">
              <span class="input-ico">👤</span>
              <input 
                id="username" 
                type="text" 
                [(ngModel)]="username" 
                name="username" 
                placeholder="e.g. doctor, clerk, supervisor, admin" 
                required 
              />
            </div>
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <div class="input-wrap">
              <span class="input-ico">🔑</span>
              <input 
                id="password" 
                type="password" 
                [(ngModel)]="password" 
                name="password" 
                placeholder="••••••••" 
                required 
              />
            </div>
          </div>

          <button type="submit" [disabled]="loading()" class="btn-primary">
            <span *ngIf="loading()">Authenticating with Vault...</span>
            <span *ngIf="!loading()">⚡ Secure Login</span>
          </button>
        </form>

        <div class="quick-profiles">
          <p class="section-title">Quick Demo Logins (SRS Personas):</p>
          <div class="profile-buttons">
            <button type="button" (click)="setProfile('clerk', 'Password@123')" class="btn-chip">
              <span class="chip-icon">📋</span>
              <div class="chip-info">
                <strong>Registration Clerk</strong>
                <small>Intake &amp; scan uploads</small>
              </div>
            </button>
            <button type="button" (click)="setProfile('doctor', 'Password@123')" class="btn-chip">
              <span class="chip-icon">🩺</span>
              <div class="chip-info">
                <strong>Treating Clinician</strong>
                <small>Timeline &amp; AI insights</small>
              </div>
            </button>
            <button type="button" (click)="setProfile('supervisor', 'Password@123')" class="btn-chip">
              <span class="chip-icon">🛡️</span>
              <div class="chip-info">
                <strong>Records Supervisor</strong>
                <small>OCR QA verification</small>
              </div>
            </button>
            <button type="button" (click)="setProfile('admin', 'Password@123')" class="btn-chip">
              <span class="chip-icon">⚙️</span>
              <div class="chip-info">
                <strong>Hospital Admin</strong>
                <small>Bulk batch &amp; audits</small>
              </div>
            </button>
          </div>
        </div>

        <div class="landing-link-row">
          <a routerLink="/landing" class="link-landing">
            🌐 View Futuristic Hospital Landing Page &rarr;
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrapper {
      position: relative;
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: radial-gradient(ellipse at 35% 45%, #0e1e5b 0%, #070d2b 55%, #030617 100%);
      padding: 1.5rem;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      overflow: hidden;
      box-sizing: border-box;
    }

    .cyber-notch {
      position: absolute;
      top: 0;
      right: 20%;
      width: 220px;
      height: 14px;
      background: #2563eb;
      box-shadow: 0 0 15px rgba(37, 99, 235, 0.7);
      border-bottom-left-radius: 4px;
      border-bottom-right-radius: 4px;
    }

    .corner-decor {
      position: absolute;
      pointer-events: none;
    }
    .corner-decor.top-left {
      top: 24px;
      left: 24px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .corner-decor.bottom-right {
      bottom: 24px;
      right: 24px;
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 6px;
    }
    .cyan-sq {
      width: 20px;
      height: 20px;
      background: #38bdf8;
      box-shadow: 0 0 10px rgba(56, 189, 248, 0.8);
    }
    .bracket-sq {
      width: 22px;
      height: 22px;
      border-right: 3px solid #38bdf8;
      border-bottom: 3px solid #38bdf8;
    }
    .dot-pat {
      width: 40px;
      height: 28px;
      background-image: radial-gradient(#38bdf8 1.5px, transparent 1.5px);
      background-size: 7px 7px;
      opacity: 0.8;
    }

    .login-card {
      position: relative;
      z-index: 5;
      background: rgba(10, 20, 58, 0.88);
      border: 1.5px solid rgba(0, 229, 255, 0.35);
      width: 100%;
      max-width: 500px;
      border-radius: 16px;
      padding: 2.5rem;
      box-shadow: 0 25px 50px rgba(0, 0, 0, 0.7), 0 0 30px rgba(0, 229, 255, 0.2);
      backdrop-filter: blur(14px);
      box-sizing: border-box;
    }

    .hospital-brand {
      text-align: center;
      margin-bottom: 2rem;
    }
    .cube-brand-icon {
      width: 40px;
      height: 40px;
      margin: 0 auto 0.75rem auto;
      transform: rotate(45deg);
      background: linear-gradient(135deg, #8b5cf6 0%, #00f2fe 100%);
      box-shadow: 0 0 18px rgba(0, 242, 254, 0.6);
      border-radius: 6px;
    }
    h2 {
      color: #ffffff;
      font-size: 1.25rem;
      font-weight: 700;
      margin: 0 0 0.4rem 0;
    }
    .subtitle {
      color: #94a3b8;
      font-size: 0.85rem;
      margin: 0 0 0.85rem 0;
    }
    .badge-cluster {
      display: flex;
      justify-content: center;
      gap: 0.5rem;
    }
    .badge {
      display: inline-block;
      background: rgba(56, 189, 248, 0.15);
      border: 1px solid #38bdf8;
      color: #38bdf8;
      padding: 0.2rem 0.65rem;
      border-radius: 9999px;
      font-size: 0.72rem;
      font-weight: 600;
    }
    .badge-cyan {
      display: inline-block;
      background: rgba(0, 229, 255, 0.15);
      border: 1px solid #00e5ff;
      color: #00e5ff;
      padding: 0.2rem 0.65rem;
      border-radius: 9999px;
      font-size: 0.72rem;
      font-weight: 600;
    }

    .form-group {
      margin-bottom: 1.25rem;
    }
    label {
      display: block;
      color: #94a3b8;
      font-size: 0.82rem;
      font-weight: 600;
      margin-bottom: 0.375rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .input-wrap {
      position: relative;
      display: flex;
      align-items: center;
    }
    .input-ico {
      position: absolute;
      left: 0.75rem;
      font-size: 0.95rem;
      color: #64748b;
    }
    input {
      width: 100%;
      box-sizing: border-box;
      padding: 0.75rem 0.85rem 0.75rem 2.4rem;
      background: rgba(15, 23, 42, 0.85);
      border: 1.5px solid rgba(56, 189, 248, 0.3);
      border-radius: 8px;
      font-size: 0.95rem;
      color: #ffffff;
      transition: all 0.2s;
    }
    input:focus {
      outline: none;
      border-color: #00f2fe;
      box-shadow: 0 0 15px rgba(0, 242, 254, 0.4);
      background: rgba(15, 23, 42, 0.95);
    }
    .btn-primary {
      width: 100%;
      background: linear-gradient(90deg, #0284c7 0%, #00f2fe 100%);
      color: #070d2b;
      border: none;
      padding: 0.85rem;
      border-radius: 8px;
      font-weight: 700;
      font-size: 1rem;
      cursor: pointer;
      box-shadow: 0 0 20px rgba(0, 242, 254, 0.4);
      transition: all 0.2s;
    }
    .btn-primary:hover:not(:disabled) {
      box-shadow: 0 0 30px rgba(0, 242, 254, 0.75);
      background: linear-gradient(90deg, #00f2fe 0%, #38ef7d 100%);
    }
    .btn-primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .alert-error {
      background: rgba(220, 38, 38, 0.2);
      border: 1px solid #ef4444;
      color: #fca5a5;
      padding: 0.75rem;
      border-radius: 8px;
      font-size: 0.85rem;
      margin-bottom: 1.25rem;
    }
    .quick-profiles {
      margin-top: 2rem;
      padding-top: 1.5rem;
      border-top: 1px solid rgba(56, 189, 248, 0.2);
    }
    .section-title {
      font-size: 0.75rem;
      color: #38bdf8;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      margin-bottom: 0.75rem;
    }
    .profile-buttons {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 0.65rem;
    }
    .btn-chip {
      background: rgba(15, 23, 42, 0.75);
      border: 1px solid rgba(56, 189, 248, 0.25);
      padding: 0.6rem;
      border-radius: 8px;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 0.5rem;
      text-align: left;
      transition: all 0.2s;
    }
    .btn-chip:hover {
      background: rgba(56, 189, 248, 0.15);
      border-color: #00f2fe;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 242, 254, 0.2);
    }
    .chip-icon {
      font-size: 1.25rem;
    }
    .chip-info strong {
      display: block;
      font-size: 0.8rem;
      color: #f8fafc;
    }
    .chip-info small {
      display: block;
      font-size: 0.68rem;
      color: #94a3b8;
    }
    .landing-link-row {
      margin-top: 1.5rem;
      text-align: center;
    }
    .link-landing {
      color: #00f2fe;
      font-size: 0.82rem;
      font-weight: 600;
      text-decoration: none;
      transition: color 0.15s;
    }
    .link-landing:hover {
      color: #38ef7d;
      text-decoration: underline;
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
