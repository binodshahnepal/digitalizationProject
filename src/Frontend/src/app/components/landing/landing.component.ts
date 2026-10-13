import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="cyber-container">
      <!-- Top Accent Bar -->
      <div class="cyber-top-accent">
        <div class="cyber-bar"></div>
      </div>

      <!-- Corner Cyber Decorations -->
      <div class="corner-matrix top-left">
        <div class="cyan-square"></div>
        <div class="dot-grid"></div>
      </div>

      <div class="corner-matrix bottom-right">
        <div class="dot-grid"></div>
        <div class="bracket-accent"></div>
      </div>

      <div class="corner-matrix bottom-left">
        <div class="cyan-square"></div>
      </div>

      <!-- Ambient Floating Tech Glyphs -->
      <div class="tech-glyph glyph-cross-1">✕</div>
      <div class="tech-glyph glyph-cross-2">✕</div>
      <div class="tech-glyph glyph-cross-3">✕</div>
      <div class="tech-glyph glyph-circle-1"></div>
      <div class="tech-glyph glyph-circle-2"></div>
      <div class="tech-glyph glyph-circle-3"></div>
      <div class="tech-glyph glyph-square-1"></div>
      <div class="tech-glyph glyph-square-2"></div>

      <!-- Main Hero Showcase -->
      <section class="cyber-hero">
        <!-- LEFT: Glowing Circular Tech Portal with Vertical Circuit Traces -->
        <div class="portal-column">
          <!-- Top Motherboard PCB Traces -->
          <div class="trace-wrapper top-traces">
            <svg class="pcb-svg" viewBox="0 0 200 130" xmlns="http://www.w3.org/2000/svg">
              <path d="M100 130 V85 H60 V45 H30 V12" stroke="#00e5ff" stroke-width="2.5" fill="none" stroke-linecap="round" />
              <path d="M100 130 V65 H140 V35 H170 V12" stroke="#00e5ff" stroke-width="2.5" fill="none" stroke-linecap="round" />
              <path d="M85 130 V95 H40 V65" stroke="#00e5ff" stroke-width="2" fill="none" stroke-linecap="round" />
              <path d="M115 130 V95 H160 V55" stroke="#00e5ff" stroke-width="2" fill="none" stroke-linecap="round" />
              <circle cx="30" cy="12" r="4.5" fill="#00e5ff" />
              <circle cx="170" cy="12" r="4.5" fill="#00e5ff" />
              <circle cx="40" cy="65" r="3.5" fill="#00e5ff" />
              <circle cx="160" cy="55" r="3.5" fill="#00e5ff" />
            </svg>
          </div>

          <!-- Glowing Portal Ring -->
          <div class="portal-ring">
            <div class="portal-inner-glow">
              <!-- Holographic Floating Tech Badges -->
              <div class="holo-card holo-top-left">
                <span class="holo-dot"></span>
                <span>⚡ OCR 99.4%</span>
              </div>
              <div class="holo-card holo-top-right">
                <span class="holo-dot"></span>
                <span>🧬 ICD-O-3 Oncology</span>
              </div>
              <div class="holo-card holo-bottom-left">
                <span class="holo-dot"></span>
                <span>🇳🇵 Nepali + Eng</span>
              </div>
              <div class="holo-card holo-bottom-right">
                <span class="holo-dot"></span>
                <span>🔒 Air-Gapped Vault</span>
              </div>

              <!-- High-Tech Isometric Workstation SVG -->
              <svg class="isometric-laptop" viewBox="0 0 320 260" xmlns="http://www.w3.org/2000/svg">
                <defs>
                  <linearGradient id="screenGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stop-color="#00f2fe" />
                    <stop offset="100%" stop-color="#0066cc" />
                  </linearGradient>
                  <linearGradient id="bodyGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" stop-color="#1e293b" />
                    <stop offset="100%" stop-color="#0f172a" />
                  </linearGradient>
                  <linearGradient id="edgeGrad" x1="0%" y1="0%" x2="100%" y2="0%">
                    <stop offset="0%" stop-color="#00e5ff" />
                    <stop offset="100%" stop-color="#38bdf8" />
                  </linearGradient>
                </defs>

                <!-- Base Shadow -->
                <polygon points="60,205 160,245 260,205 160,165" fill="rgba(0, 229, 255, 0.2)" />

                <!-- Laptop Base Platform -->
                <polygon points="60,195 160,235 260,195 160,155" fill="#0f172a" stroke="#00e5ff" stroke-width="2" />
                <!-- Base Thickness -->
                <polygon points="60,195 60,203 160,243 160,235" fill="#091026" stroke="#00e5ff" stroke-width="1.5" />
                <polygon points="160,235 160,243 260,203 260,195" fill="#060c1d" stroke="#00e5ff" stroke-width="1.5" />

                <!-- Keyboard Deck -->
                <polygon points="80,188 160,220 240,188 160,158" fill="#1e293b" />
                <polygon points="120,208 160,224 200,208 160,192" fill="#09122c" stroke="#38bdf8" stroke-width="1" />

                <!-- Laptop Screen (Angled 3D) -->
                <!-- Screen Bezel -->
                <polygon points="75,80 245,80 245,175 75,175" fill="#0b132b" stroke="#00f2fe" stroke-width="2.5" />
                <!-- Screen Display Canvas -->
                <polygon points="82,87 238,87 238,168 82,168" fill="url(#screenGrad)" opacity="0.95" />

                <!-- Screen UI: Clinical Telemetry Wave & Data -->
                <!-- Grid Lines on Screen -->
                <line x1="82" y1="110" x2="238" y2="110" stroke="rgba(255,255,255,0.25)" stroke-width="0.8" />
                <line x1="82" y1="135" x2="238" y2="135" stroke="rgba(255,255,255,0.25)" stroke-width="0.8" />
                <line x1="120" y1="87" x2="120" y2="168" stroke="rgba(255,255,255,0.15)" stroke-width="0.8" />
                <line x1="160" y1="87" x2="160" y2="168" stroke="rgba(255,255,255,0.15)" stroke-width="0.8" />
                <line x1="200" y1="87" x2="200" y2="168" stroke="rgba(255,255,255,0.15)" stroke-width="0.8" />

                <!-- Dynamic Medical Telemetry Graph / Mountain Wave -->
                <path d="M85 145 Q 105 140 115 125 T 135 130 T 155 105 T 175 140 T 195 115 T 215 135 L 235 120 L 235 165 L 85 165 Z" 
                      fill="rgba(255,255,255,0.85)" />
                <path d="M85 145 Q 105 140 115 125 T 135 130 T 155 105 T 175 140 T 195 115 T 215 135 L 235 120" 
                      fill="none" stroke="#ffffff" stroke-width="2" />

                <!-- Hologram Circle Mini Radar -->
                <circle cx="102" cy="100" r="7" fill="none" stroke="#ffffff" stroke-width="1.5" />
                <line x1="102" y1="100" x2="107" y2="96" stroke="#ffffff" stroke-width="1.5" />

                <!-- Mini Bar Metrics on Screen -->
                <rect x="210" y="93" width="18" height="4" fill="#ffffff" rx="2" />
                <rect x="210" y="100" width="14" height="4" fill="#ffffff" rx="2" />
                <rect x="210" y="107" width="20" height="4" fill="#ffffff" rx="2" />
              </svg>
            </div>
          </div>

          <!-- Bottom Motherboard PCB Traces -->
          <div class="trace-wrapper bottom-traces">
            <svg class="pcb-svg" viewBox="0 0 200 130" xmlns="http://www.w3.org/2000/svg">
              <path d="M100 0 V45 H60 V85 H30 V118" stroke="#00e5ff" stroke-width="2.5" fill="none" stroke-linecap="round" />
              <path d="M100 0 V65 H140 V95 H170 V118" stroke="#00e5ff" stroke-width="2.5" fill="none" stroke-linecap="round" />
              <path d="M85 0 V35 H40 V75" stroke="#00e5ff" stroke-width="2" fill="none" stroke-linecap="round" />
              <path d="M115 0 V35 H160 V85" stroke="#00e5ff" stroke-width="2" fill="none" stroke-linecap="round" />
              <circle cx="30" cy="118" r="4.5" fill="#00e5ff" />
              <circle cx="170" cy="118" r="4.5" fill="#00e5ff" />
              <circle cx="40" cy="75" r="3.5" fill="#00e5ff" />
              <circle cx="160" cy="85" r="3.5" fill="#00e5ff" />
            </svg>
          </div>
        </div>

        <!-- RIGHT: Hero Typography, Branding & Fast Access -->
        <div class="hero-content">
          <!-- Logo & Institution Tag -->
          <div class="brand-badge-row">
            <div class="cube-logo">
              <div class="cube-facet front"></div>
              <div class="cube-facet top"></div>
              <div class="cube-facet right"></div>
            </div>
            <div class="brand-text-block">
              <span class="logo-title">BPKMCH AI-RECORDS</span>
              <span class="logo-sub">B.P. Koirala Memorial Cancer Hospital • Bharatpur</span>
            </div>
          </div>

          <!-- Bold Hero Headline Matching Exact Theme -->
          <div class="headline-block">
            <h1 class="headline-future">THE FUTURE OF</h1>
            <h2 class="headline-tech">HEALTHCARE DIGITIZATION</h2>
          </div>

          <!-- Futuristic Dotted Divider Line -->
          <div class="tech-divider">
            <span class="dot-dash">▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪ ▪</span>
          </div>

          <!-- Mission & System Narrative -->
          <p class="hero-description">
            Next-generation clinical records digitization, bilingual Optical Character Recognition (Devanagari Nepali &amp; English), 
            automated oncology ICD-O-3 entity extraction, and high-speed semantic retrieval designed for high-throughput cancer care.
          </p>

          <!-- Action / Persona Icon Pills (Matching the circular social icons in template) -->
          <div class="feature-pills-row">
            <button type="button" (click)="scrollToPortal()" class="circle-btn" title="Patient Registration System">
              <span class="icon-sym">📋</span>
            </button>
            <button type="button" (click)="scrollToPortal()" class="circle-btn" title="Doctor Search & Telemetry">
              <span class="icon-sym">🩺</span>
            </button>
            <button type="button" (click)="scrollToPortal()" class="circle-btn" title="Supervisor QA Review">
              <span class="icon-sym">🛡️</span>
            </button>
            <button type="button" (click)="scrollToPortal()" class="circle-btn" title="Ops & Batch Indexing">
              <span class="icon-sym">⚙️</span>
            </button>
            <span class="pills-label">Clinical Workflow Roles</span>
          </div>

          <!-- CTA Buttons -->
          <div class="cta-actions">
            <ng-container *ngIf="authService.isAuthenticated()">
              <a routerLink="/patients" class="btn-cyber-primary">
                🚀 Enter Patient Directory &rarr;
              </a>
              <a routerLink="/review-queue" class="btn-cyber-outline">
                🛡️ Open Review Queue
              </a>
            </ng-container>

            <ng-container *ngIf="!authService.isAuthenticated()">
              <button type="button" (click)="scrollToPortal()" class="btn-cyber-primary">
                ⚡ Launch Medical Portal
              </button>
              <button type="button" (click)="setPersona('doctor', 'Password@123')" class="btn-cyber-outline">
                🩺 Quick Doctor Demo
              </button>
            </ng-container>
          </div>
        </div>
      </section>

      <!-- INTERACTIVE PORTAL ACCESS CONSOLE -->
      <section id="portalSection" class="portal-console-section">
        <div class="console-card">
          <div class="console-header">
            <div class="console-title-group">
              <span class="terminal-badge">SECURE ACCESS</span>
              <h3>B.P. Koirala Memorial Hospital Portal</h3>
              <p>Select an SRS demo persona or sign in with your hospital credentials</p>
            </div>
            <div class="system-status-indicator">
              <span class="live-dot"></span>
              <span class="status-text">All AI Services Active (Ports 5000, 5200, 4200)</span>
            </div>
          </div>

          <!-- Quick Persona Launch Chips -->
          <div class="persona-chips-container">
            <span class="persona-group-title">SRS Demo Personas (1-Click Fill &amp; Login):</span>
            <div class="persona-grid">
              <button type="button" (click)="loginAs('clerk', 'Password@123')" class="persona-chip" [disabled]="loading()">
                <span class="persona-icon">📋</span>
                <div class="persona-meta">
                  <strong>Registration Clerk</strong>
                  <small>Intake, patient registration &amp; scan uploads</small>
                </div>
              </button>
              <button type="button" (click)="loginAs('doctor', 'Password@123')" class="persona-chip" [disabled]="loading()">
                <span class="persona-icon">🩺</span>
                <div class="persona-meta">
                  <strong>Treating Clinician</strong>
                  <small>Longitudinal timeline, cancer staging &amp; AI insights</small>
                </div>
              </button>
              <button type="button" (click)="loginAs('supervisor', 'Password@123')" class="persona-chip" [disabled]="loading()">
                <span class="persona-icon">🛡️</span>
                <div class="persona-meta">
                  <strong>Records Supervisor</strong>
                  <small>OCR QA verification &amp; categorization review</small>
                </div>
              </button>
              <button type="button" (click)="loginAs('admin', 'Password@123')" class="persona-chip" [disabled]="loading()">
                <span class="persona-icon">⚙️</span>
                <div class="persona-meta">
                  <strong>Hospital Admin</strong>
                  <small>Batch bulk intake, audit exports &amp; operations</small>
                </div>
              </button>
            </div>
          </div>

          <!-- Error Alert Banner -->
          <div *ngIf="errorMessage()" class="cyber-alert-error">
            ⚠️ {{ errorMessage() }}
          </div>

          <!-- Standard Credentials Form -->
          <form (ngSubmit)="onLogin()" class="cyber-form">
            <div class="form-row">
              <div class="form-field">
                <label for="cUsername">Username</label>
                <div class="input-wrapper">
                  <span class="input-icon">👤</span>
                  <input 
                    id="cUsername" 
                    type="text" 
                    [(ngModel)]="username" 
                    name="username" 
                    placeholder="e.g. doctor, clerk, supervisor, admin"
                    required 
                  />
                </div>
              </div>
              <div class="form-field">
                <label for="cPassword">Password</label>
                <div class="input-wrapper">
                  <span class="input-icon">🔑</span>
                  <input 
                    id="cPassword" 
                    type="password" 
                    [(ngModel)]="password" 
                    name="password" 
                    placeholder="••••••••"
                    required 
                  />
                </div>
              </div>
            </div>

            <div class="form-actions">
              <button type="submit" [disabled]="loading() || !username || !password" class="btn-submit-portal">
                <span *ngIf="loading()">Authenticating with Hospital Vault...</span>
                <span *ngIf="!loading()">⚡ Sign In to AI Records System</span>
              </button>
            </div>
          </form>
        </div>
      </section>

      <!-- SYSTEM CAPABILITY PILLARS -->
      <section class="capabilities-section">
        <div class="capabilities-grid">
          <div class="cap-card">
            <div class="cap-icon-box">🔤</div>
            <h4>Multilingual OCR Engine</h4>
            <p>High-fidelity Tesseract OCR tuned for Nepali Devanagari script and English medical lexicon with confidence scoring.</p>
          </div>
          <div class="cap-card">
            <div class="cap-icon-box">🧬</div>
            <h4>Oncology Entity Extraction</h4>
            <p>Automated extraction of ICD-O-3 morphology, primary tumor site, TNM cancer stage, and surgical histology findings.</p>
          </div>
          <div class="cap-card">
            <div class="cap-icon-box">🔍</div>
            <h4>Semantic Vector Intelligence</h4>
            <p>Dense clinical embeddings powered by pgvector &amp; BGE models for instant cross-record similarity discovery.</p>
          </div>
          <div class="cap-card">
            <div class="cap-icon-box">🛡️</div>
            <h4>Air-Gapped Vault Security</h4>
            <p>On-premises storage vault with physical purge lifecycle, granular audit logging, and role-based zero-trust security.</p>
          </div>
        </div>
      </section>

      <!-- Cyber Footer -->
      <footer class="cyber-footer">
        <p>B.P. Koirala Memorial Cancer Hospital • AI Records Digitization &amp; Intelligence System</p>
        <span class="footer-meta">Bharatpur, Chitwan, Nepal • .NET 9 Web API + FastAPI AI Worker + Angular 20</span>
      </footer>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
    }

    /* Outer Cyber Container with Theme Palette */
    .cyber-container {
      position: relative;
      min-height: 100vh;
      background: radial-gradient(ellipse at 25% 45%, #0e1e5b 0%, #070d2b 55%, #030617 100%);
      color: #ffffff;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
      overflow-x: hidden;
      padding: 0;
      box-sizing: border-box;
    }

    /* Top Accent Electric Blue Tab */
    .cyber-top-accent {
      position: absolute;
      top: 0;
      right: 18%;
      z-index: 10;
    }
    .cyber-bar {
      width: 260px;
      height: 18px;
      background: #2563eb;
      box-shadow: 0 4px 15px rgba(37, 99, 235, 0.6);
      border-bottom-left-radius: 4px;
      border-bottom-right-radius: 4px;
    }

    /* Corner Decorations Matching Template */
    .corner-matrix {
      position: absolute;
      z-index: 5;
      pointer-events: none;
    }
    .corner-matrix.top-left {
      top: 24px;
      left: 24px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .corner-matrix.bottom-right {
      bottom: 24px;
      right: 24px;
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 8px;
    }
    .corner-matrix.bottom-left {
      bottom: 24px;
      left: 24px;
    }

    .cyan-square {
      width: 24px;
      height: 24px;
      background: #38bdf8;
      box-shadow: 0 0 12px rgba(56, 189, 248, 0.8);
    }
    .bracket-accent {
      width: 28px;
      height: 28px;
      border-right: 4px solid #38bdf8;
      border-bottom: 4px solid #38bdf8;
      box-shadow: 0 0 10px rgba(56, 189, 248, 0.6);
    }
    .dot-grid {
      width: 48px;
      height: 36px;
      background-image: radial-gradient(#38bdf8 1.5px, transparent 1.5px);
      background-size: 8px 8px;
      opacity: 0.85;
    }

    /* Ambient Tech Glyphs */
    .tech-glyph {
      position: absolute;
      color: rgba(56, 189, 248, 0.55);
      font-family: monospace;
      font-weight: bold;
      pointer-events: none;
      z-index: 2;
      user-select: none;
    }
    .glyph-cross-1 { top: 120px; left: 42%; font-size: 14px; }
    .glyph-cross-2 { top: 220px; right: 28%; font-size: 12px; }
    .glyph-cross-3 { bottom: 180px; left: 38%; font-size: 14px; }
    .glyph-circle-1 {
      top: 140px; right: 22%;
      width: 14px; height: 14px;
      border: 1.5px solid rgba(0, 242, 254, 0.6);
      border-radius: 50%;
    }
    .glyph-circle-2 {
      top: 190px; right: 12%;
      width: 10px; height: 10px;
      border: 1.5px solid rgba(0, 242, 254, 0.4);
      border-radius: 50%;
    }
    .glyph-circle-3 {
      bottom: 220px; right: 18%;
      width: 12px; height: 12px;
      border: 1.5px solid rgba(0, 242, 254, 0.5);
      border-radius: 50%;
    }
    .glyph-square-1 {
      top: 240px; left: 10%;
      width: 10px; height: 10px;
      border: 1.5px solid rgba(56, 189, 248, 0.5);
    }
    .glyph-square-2 {
      bottom: 120px; right: 35%;
      width: 12px; height: 12px;
      border: 1.5px solid rgba(56, 189, 248, 0.5);
    }

    /* HERO SECTION */
    .cyber-hero {
      position: relative;
      z-index: 4;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 4.5rem;
      padding: 4.5rem 3rem 3rem 3rem;
      max-width: 1320px;
      margin: 0 auto;
      min-height: 85vh;
    }

    /* LEFT PORTAL COLUMN */
    .portal-column {
      position: relative;
      display: flex;
      flex-direction: column;
      align-items: center;
      flex-shrink: 0;
    }

    .trace-wrapper {
      width: 180px;
      height: 100px;
      display: flex;
      justify-content: center;
      filter: drop-shadow(0 0 6px rgba(0, 229, 255, 0.75));
    }
    .pcb-svg {
      width: 100%;
      height: 100%;
    }
    .top-traces {
      margin-bottom: -15px;
    }
    .bottom-traces {
      margin-top: -15px;
    }

    /* Circular Glowing Portal */
    .portal-ring {
      width: 360px;
      height: 360px;
      border-radius: 50%;
      border: 3.5px solid #00f2fe;
      box-shadow: 0 0 35px rgba(0, 242, 254, 0.55), inset 0 0 35px rgba(0, 242, 254, 0.25);
      position: relative;
      padding: 10px;
      background: radial-gradient(circle, #0e2775 0%, #061138 75%);
    }
    .portal-inner-glow {
      position: relative;
      width: 100%;
      height: 100%;
      border-radius: 50%;
      border: 1.5px dashed rgba(56, 189, 248, 0.55);
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: visible;
    }

    .isometric-laptop {
      width: 290px;
      height: 235px;
      filter: drop-shadow(0 15px 25px rgba(0,0,0,0.6));
    }

    /* Floating Holographic Cards Orbiting Portal */
    .holo-card {
      position: absolute;
      background: rgba(10, 24, 68, 0.88);
      border: 1px solid #00e5ff;
      border-radius: 20px;
      padding: 0.35rem 0.75rem;
      font-size: 0.72rem;
      font-weight: 700;
      color: #e0f2fe;
      display: flex;
      align-items: center;
      gap: 0.35rem;
      box-shadow: 0 4px 15px rgba(0, 229, 255, 0.3);
      backdrop-filter: blur(8px);
      z-index: 10;
      white-space: nowrap;
      animation: floatBadge 3.5s ease-in-out infinite alternate;
    }
    .holo-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: #00f2fe;
      box-shadow: 0 0 6px #00f2fe;
    }
    .holo-top-left {
      top: 15px;
      left: -20px;
      animation-delay: 0s;
    }
    .holo-top-right {
      top: 25px;
      right: -30px;
      animation-delay: 0.8s;
    }
    .holo-bottom-left {
      bottom: 25px;
      left: -25px;
      animation-delay: 1.5s;
    }
    .holo-bottom-right {
      bottom: 15px;
      right: -20px;
      animation-delay: 2.2s;
    }

    @keyframes floatBadge {
      0% { transform: translateY(0px); }
      100% { transform: translateY(-7px); }
    }

    /* RIGHT HERO CONTENT */
    .hero-content {
      flex: 1;
      max-width: 640px;
    }

    /* Logo & Institution Row */
    .brand-badge-row {
      display: flex;
      align-items: center;
      gap: 1rem;
      margin-bottom: 1.75rem;
    }

    /* 3D Isometric Gradient Cube */
    .cube-logo {
      width: 38px;
      height: 38px;
      position: relative;
      transform: rotate(45deg);
      border-radius: 6px;
      background: linear-gradient(135deg, #8b5cf6 0%, #00f2fe 100%);
      box-shadow: 0 0 20px rgba(0, 242, 254, 0.6);
      border: 1.5px solid rgba(255, 255, 255, 0.4);
    }
    .brand-text-block {
      display: flex;
      flex-direction: column;
    }
    .logo-title {
      font-size: 1.1rem;
      font-weight: 800;
      letter-spacing: 0.08em;
      color: #ffffff;
      text-transform: uppercase;
    }
    .logo-sub {
      font-size: 0.75rem;
      color: #38bdf8;
      letter-spacing: 0.04em;
    }

    /* Hero Headline Matching Exact Theme */
    .headline-block {
      margin-bottom: 1.25rem;
    }
    .headline-future {
      font-size: 2.85rem;
      font-weight: 900;
      color: #ffffff;
      letter-spacing: 0.06em;
      margin: 0;
      line-height: 1.1;
      text-transform: uppercase;
    }
    .headline-tech {
      font-size: 2.85rem;
      font-weight: 900;
      letter-spacing: 0.06em;
      margin: 0;
      line-height: 1.15;
      text-transform: uppercase;
      background: linear-gradient(90deg, #00f2fe 0%, #38ef7d 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      filter: drop-shadow(0 0 18px rgba(0, 242, 254, 0.45));
    }

    /* Tech Dotted Divider */
    .tech-divider {
      margin-bottom: 1.5rem;
    }
    .dot-dash {
      font-size: 0.85rem;
      color: #00f2fe;
      letter-spacing: 0.25em;
      opacity: 0.9;
    }

    /* Body Description */
    .hero-description {
      font-size: 0.96rem;
      line-height: 1.65;
      color: #94a3b8;
      margin: 0 0 2rem 0;
      max-width: 580px;
    }

    /* Circular Social / Action Buttons */
    .feature-pills-row {
      display: flex;
      align-items: center;
      gap: 0.85rem;
      margin-bottom: 2.25rem;
    }
    .circle-btn {
      width: 44px;
      height: 44px;
      border-radius: 50%;
      background: #00e5ff;
      border: none;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 0 15px rgba(0, 229, 255, 0.6);
      transition: all 0.2s ease;
    }
    .circle-btn:hover {
      transform: translateY(-3px) scale(1.05);
      box-shadow: 0 0 25px rgba(0, 229, 255, 0.9);
      background: #38ef7d;
    }
    .icon-sym {
      font-size: 1.15rem;
    }
    .pills-label {
      font-size: 0.75rem;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      margin-left: 0.5rem;
      font-weight: 600;
    }

    /* Call To Action Buttons */
    .cta-actions {
      display: flex;
      align-items: center;
      gap: 1.25rem;
      flex-wrap: wrap;
    }
    .btn-cyber-primary {
      background: linear-gradient(90deg, #0284c7 0%, #00f2fe 100%);
      color: #070d2b;
      font-size: 0.95rem;
      font-weight: 700;
      padding: 0.85rem 1.75rem;
      border-radius: 8px;
      border: none;
      cursor: pointer;
      text-decoration: none;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      box-shadow: 0 0 25px rgba(0, 242, 254, 0.5);
      transition: all 0.2s ease;
    }
    .btn-cyber-primary:hover {
      transform: translateY(-2px);
      box-shadow: 0 0 35px rgba(0, 242, 254, 0.8);
      background: linear-gradient(90deg, #00f2fe 0%, #38ef7d 100%);
    }
    .btn-cyber-outline {
      background: rgba(15, 23, 42, 0.6);
      border: 1.5px solid #38bdf8;
      color: #e0f2fe;
      font-size: 0.95rem;
      font-weight: 600;
      padding: 0.85rem 1.65rem;
      border-radius: 8px;
      cursor: pointer;
      text-decoration: none;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      transition: all 0.2s ease;
      backdrop-filter: blur(6px);
    }
    .btn-cyber-outline:hover {
      background: rgba(56, 189, 248, 0.15);
      border-color: #00f2fe;
      color: #ffffff;
    }

    /* SECURE PORTAL CONSOLE SECTION */
    .portal-console-section {
      padding: 2rem 2rem 4rem 2rem;
      max-width: 1100px;
      margin: 0 auto;
      position: relative;
      z-index: 5;
    }
    .console-card {
      background: rgba(10, 20, 58, 0.85);
      border: 1.5px solid rgba(0, 229, 255, 0.35);
      border-radius: 16px;
      padding: 2.25rem;
      box-shadow: 0 20px 45px rgba(0, 0, 0, 0.6), 0 0 30px rgba(0, 229, 255, 0.15);
      backdrop-filter: blur(14px);
    }
    .console-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 2rem;
      flex-wrap: wrap;
      gap: 1rem;
    }
    .terminal-badge {
      display: inline-block;
      background: rgba(0, 229, 255, 0.15);
      border: 1px solid #00e5ff;
      color: #00e5ff;
      padding: 0.2rem 0.6rem;
      border-radius: 4px;
      font-size: 0.7rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      margin-bottom: 0.4rem;
    }
    .console-title-group h3 {
      font-size: 1.35rem;
      font-weight: 700;
      color: #ffffff;
      margin: 0 0 0.35rem 0;
    }
    .console-title-group p {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 0;
    }
    .system-status-indicator {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid #334155;
      padding: 0.4rem 0.85rem;
      border-radius: 20px;
    }
    .live-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #10b981;
      box-shadow: 0 0 8px #10b981;
      animation: pulseGreen 2s infinite;
    }
    @keyframes pulseGreen {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.4; }
    }
    .status-text {
      font-size: 0.75rem;
      color: #cbd5e1;
      font-weight: 500;
    }

    /* Persona Chips Grid */
    .persona-chips-container {
      margin-bottom: 1.75rem;
    }
    .persona-group-title {
      display: block;
      font-size: 0.78rem;
      font-weight: 700;
      color: #38bdf8;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      margin-bottom: 0.75rem;
    }
    .persona-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
      gap: 0.75rem;
    }
    .persona-chip {
      background: rgba(15, 23, 42, 0.75);
      border: 1px solid rgba(56, 189, 248, 0.25);
      border-radius: 10px;
      padding: 0.75rem;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      cursor: pointer;
      text-align: left;
      transition: all 0.2s ease;
      color: #f1f5f9;
    }
    .persona-chip:hover:not(:disabled) {
      background: rgba(56, 189, 248, 0.15);
      border-color: #00f2fe;
      transform: translateY(-2px);
      box-shadow: 0 4px 15px rgba(0, 242, 254, 0.25);
    }
    .persona-chip:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .persona-icon {
      font-size: 1.5rem;
      flex-shrink: 0;
    }
    .persona-meta {
      display: flex;
      flex-direction: column;
    }
    .persona-meta strong {
      font-size: 0.85rem;
      color: #ffffff;
    }
    .persona-meta small {
      font-size: 0.72rem;
      color: #94a3b8;
      line-height: 1.3;
    }

    /* Cyber Alert */
    .cyber-alert-error {
      background: rgba(220, 38, 38, 0.2);
      border: 1px solid #ef4444;
      color: #fca5a5;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      font-size: 0.85rem;
      margin-bottom: 1.25rem;
    }

    /* Form Fields */
    .cyber-form {
      margin-top: 1.5rem;
      border-top: 1px solid rgba(56, 189, 248, 0.2);
      padding-top: 1.5rem;
    }
    .form-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 1.25rem;
      margin-bottom: 1.5rem;
    }
    .form-field label {
      display: block;
      font-size: 0.8rem;
      font-weight: 600;
      color: #94a3b8;
      margin-bottom: 0.4rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .input-wrapper {
      position: relative;
      display: flex;
      align-items: center;
    }
    .input-icon {
      position: absolute;
      left: 0.75rem;
      font-size: 0.9rem;
      color: #64748b;
    }
    .input-wrapper input {
      width: 100%;
      box-sizing: border-box;
      background: rgba(15, 23, 42, 0.8);
      border: 1.5px solid rgba(56, 189, 248, 0.3);
      border-radius: 8px;
      padding: 0.75rem 0.85rem 0.75rem 2.4rem;
      color: #ffffff;
      font-size: 0.95rem;
      transition: all 0.2s ease;
    }
    .input-wrapper input:focus {
      outline: none;
      border-color: #00f2fe;
      box-shadow: 0 0 15px rgba(0, 242, 254, 0.4);
      background: rgba(15, 23, 42, 0.95);
    }
    .btn-submit-portal {
      width: 100%;
      background: linear-gradient(90deg, #0284c7 0%, #00f2fe 100%);
      color: #070d2b;
      font-size: 1rem;
      font-weight: 700;
      padding: 0.85rem;
      border-radius: 8px;
      border: none;
      cursor: pointer;
      box-shadow: 0 0 20px rgba(0, 242, 254, 0.4);
      transition: all 0.2s ease;
    }
    .btn-submit-portal:hover:not(:disabled) {
      box-shadow: 0 0 30px rgba(0, 242, 254, 0.7);
      background: linear-gradient(90deg, #00f2fe 0%, #38ef7d 100%);
    }
    .btn-submit-portal:disabled {
      opacity: 0.55;
      cursor: not-allowed;
    }

    /* SYSTEM CAPABILITIES SECTION */
    .capabilities-section {
      padding: 2rem 2rem 4rem 2rem;
      max-width: 1200px;
      margin: 0 auto;
      position: relative;
      z-index: 4;
    }
    .capabilities-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1.5rem;
    }
    .cap-card {
      background: rgba(10, 20, 58, 0.6);
      border: 1px solid rgba(56, 189, 248, 0.2);
      border-radius: 12px;
      padding: 1.5rem;
      transition: all 0.2s ease;
    }
    .cap-card:hover {
      border-color: #00e5ff;
      transform: translateY(-3px);
      box-shadow: 0 8px 25px rgba(0, 229, 255, 0.2);
      background: rgba(10, 20, 58, 0.85);
    }
    .cap-icon-box {
      font-size: 1.75rem;
      margin-bottom: 0.75rem;
    }
    .cap-card h4 {
      font-size: 1.05rem;
      font-weight: 700;
      color: #ffffff;
      margin: 0 0 0.5rem 0;
    }
    .cap-card p {
      font-size: 0.82rem;
      line-height: 1.55;
      color: #94a3b8;
      margin: 0;
    }

    /* FOOTER */
    .cyber-footer {
      border-top: 1px solid rgba(56, 189, 248, 0.15);
      padding: 2rem;
      text-align: center;
      background: #030617;
      position: relative;
      z-index: 4;
    }
    .cyber-footer p {
      margin: 0 0 0.35rem 0;
      font-size: 0.85rem;
      color: #cbd5e1;
      font-weight: 600;
    }
    .footer-meta {
      font-size: 0.75rem;
      color: #64748b;
    }

    /* RESPONSIVE MEDIA QUERIES */
    @media (max-width: 1024px) {
      .cyber-hero {
        flex-direction: column;
        padding-top: 3.5rem;
        gap: 2.5rem;
        text-align: center;
      }
      .brand-badge-row {
        justify-content: center;
      }
      .feature-pills-row {
        justify-content: center;
      }
      .cta-actions {
        justify-content: center;
      }
      .hero-description {
        margin: 0 auto 2rem auto;
      }
      .form-row {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 640px) {
      .headline-future, .headline-tech {
        font-size: 1.95rem;
      }
      .portal-ring {
        width: 290px;
        height: 290px;
      }
      .isometric-laptop {
        width: 230px;
        height: 190px;
      }
      .holo-card {
        font-size: 0.65rem;
        padding: 0.25rem 0.5rem;
      }
      .cyber-hero {
        padding: 2.5rem 1rem;
      }
      .console-card {
        padding: 1.25rem;
      }
    }
  `]
})
export class LandingComponent {
  username = '';
  password = '';
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor(public authService: AuthService, private router: Router) {}

  scrollToPortal(): void {
    const el = document.getElementById('portalSection');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth' });
    }
  }

  setPersona(u: string, p: string): void {
    this.username = u;
    this.password = p;
    this.errorMessage.set(null);
    this.scrollToPortal();
  }

  loginAs(u: string, p: string): void {
    this.username = u;
    this.password = p;
    this.onLogin();
  }

  onLogin(): void {
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
