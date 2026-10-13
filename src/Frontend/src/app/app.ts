import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule, RouterOutlet } from '@angular/router';
import { AuthService } from './services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterModule, RouterOutlet],
  template: `
    <div class="app-layout">
      <!-- Hospital Navigation Bar -->
      <header class="app-header" *ngIf="authService.isAuthenticated()">
        <div class="header-brand" routerLink="/patients" title="BPKMCH AI Records Portal">
          <div class="brand-icon-emblem">
            <svg class="brand-svg-logo" viewBox="0 0 40 40" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <linearGradient id="navBrandGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stop-color="#00f2fe"/>
                  <stop offset="100%" stop-color="#38ef7d"/>
                </linearGradient>
              </defs>
              <rect x="2" y="2" width="36" height="36" rx="9" fill="#09122c" stroke="#00e5ff" stroke-width="1.8"/>
              <path d="M16 10 H24 V16 H30 V24 H24 V30 H16 V24 H10 V16 H16 Z" fill="url(#navBrandGrad)"/>
              <circle cx="20" cy="20" r="3.2" fill="#ffffff"/>
              <circle cx="20" cy="20" r="1.5" fill="#060c1e"/>
              <circle cx="6" cy="6" r="1.2" fill="#00f2fe"/>
              <circle cx="34" cy="6" r="1.2" fill="#00f2fe"/>
              <circle cx="6" cy="34" r="1.2" fill="#00f2fe"/>
              <circle cx="34" cy="34" r="1.2" fill="#00f2fe"/>
            </svg>
            <span class="pulse-ring"></span>
          </div>
          <div class="brand-titles">
            <h1>B.P. Koirala Memorial Cancer Hospital</h1>
            <span class="brand-sub">AI Records Digitization &amp; Intelligence System (Pilot)</span>
          </div>
        </div>

        <nav class="header-nav">
          <a routerLink="/landing" routerLinkActive="active" class="nav-link">
            🌐 Portal Home
          </a>
          <a routerLink="/patients" routerLinkActive="active" [routerLinkActiveOptions]="{exact: true}" class="nav-link">
            📁 Patient Directory
          </a>
          <a *ngIf="canRegister()" routerLink="/patients/register" routerLinkActive="active" class="nav-link">
            ➕ Register Patient
          </a>
          <a routerLink="/review-queue" routerLinkActive="active" class="nav-link nav-review">
            🔍 Supervisor Review Queue
          </a>
          <a routerLink="/admin" routerLinkActive="active" class="nav-link nav-admin">
            ⚙️ Ops & Bulk Batch (Sprint 8)
          </a>
        </nav>

        <div class="header-user">
          <div class="user-info">
            <span class="user-name">{{ authService.currentUser()?.fullName }}</span>
            <span class="user-role">{{ activeRoles() }}</span>
          </div>
          <button (click)="onLogout()" class="btn-logout" title="Sign out">
            🚪 Logout
          </button>
        </div>
      </header>

      <!-- Main Application Content -->
      <main class="app-main">
        <router-outlet></router-outlet>
      </main>
    </div>
  `,
  styles: [`
    .app-layout {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      background: #f8fafc;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }
    .app-header {
      background: #0f172a;
      color: #ffffff;
      padding: 0.75rem 1.5rem;
      display: flex;
      justify-content: space-between;
      align-items: center;
      box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    }
    .header-brand {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      cursor: pointer;
    }
    .brand-icon-emblem {
      position: relative;
      width: 38px;
      height: 38px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .brand-svg-logo {
      width: 100%;
      height: 100%;
      filter: drop-shadow(0 0 8px rgba(0, 242, 254, 0.65));
      transition: transform 0.2s ease, filter 0.2s ease;
    }
    .header-brand:hover .brand-svg-logo {
      transform: scale(1.08);
      filter: drop-shadow(0 0 14px rgba(0, 242, 254, 0.95));
    }
    .pulse-ring {
      position: absolute;
      width: 100%;
      height: 100%;
      border-radius: 9px;
      border: 1.5px solid rgba(0, 242, 254, 0.6);
      animation: emblemPulse 2.5s infinite;
      pointer-events: none;
    }
    @keyframes emblemPulse {
      0% { transform: scale(0.96); opacity: 0.85; }
      50% { transform: scale(1.15); opacity: 0; }
      100% { transform: scale(0.96); opacity: 0; }
    }
    .brand-titles h1 {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 700;
      color: #ffffff;
    }
    .brand-sub {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .header-nav {
      display: flex;
      gap: 1rem;
    }
    .nav-link {
      color: #cbd5e1;
      text-decoration: none;
      font-size: 0.85rem;
      font-weight: 500;
      padding: 0.4rem 0.75rem;
      border-radius: 4px;
      transition: all 0.15s;
    }
    .nav-link:hover, .nav-link.active {
      color: #ffffff;
      background: #1e293b;
    }
    .header-user {
      display: flex;
      align-items: center;
      gap: 1rem;
    }
    .user-info {
      text-align: right;
    }
    .user-name {
      display: block;
      font-size: 0.85rem;
      font-weight: 600;
      color: #f8fafc;
    }
    .user-role {
      display: block;
      font-size: 0.7rem;
      color: #38bdf8;
    }
    .btn-logout {
      background: #1e293b;
      border: 1px solid #334155;
      color: #e2e8f0;
      padding: 0.4rem 0.75rem;
      border-radius: 4px;
      font-size: 0.8rem;
      cursor: pointer;
      transition: background 0.15s;
    }
    .btn-logout:hover {
      background: #334155;
    }
    .app-main {
      flex: 1;
    }
  `]
})
export class App {
  constructor(public authService: AuthService, private router: Router) {}

  canRegister(): boolean {
    return this.authService.isClerk() || this.authService.isSupervisor() || this.authService.isAdmin();
  }

  activeRoles(): string {
    const roles = this.authService.currentUser()?.roles;
    return roles && roles.length > 0 ? roles.join(', ') : 'User';
  }

  onLogout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
