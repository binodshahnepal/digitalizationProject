import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginRequest, LoginResponse, UserProfile } from '../models/auth.models';
import { getApiBaseUrl } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = getApiBaseUrl() + '/auth';
  private readonly TOKEN_KEY = 'bpkmch_token';
  private readonly USER_KEY = 'bpkmch_user';

  private readonly tokenSignal = signal<string | null>(localStorage.getItem(this.TOKEN_KEY));
  private readonly userSignal = signal<UserProfile | null>(
    localStorage.getItem(this.USER_KEY) ? JSON.parse(localStorage.getItem(this.USER_KEY)!) : null
  );

  readonly token = this.tokenSignal.asReadonly();
  readonly currentUser = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => !!this.tokenSignal());

  readonly isClerk = computed(() => this.hasRole('RegistrationClerk'));
  readonly isDoctor = computed(() => this.hasRole('TreatingClinician'));
  readonly isSupervisor = computed(() => this.hasRole('MedicalRecordsSupervisor'));
  readonly isAdmin = computed(() => this.hasRole('HospitalAdmin') || this.hasRole('ITAdmin'));

  constructor(private http: HttpClient) {}

  login(credentials: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/login`, credentials).pipe(
      tap(response => {
        localStorage.setItem(this.TOKEN_KEY, response.token);
        const profile: UserProfile = {
          username: response.username,
          fullName: response.fullName,
          department: response.department,
          roles: response.roles
        };
        localStorage.setItem(this.USER_KEY, JSON.stringify(profile));
        this.tokenSignal.set(response.token);
        this.userSignal.set(profile);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.tokenSignal.set(null);
    this.userSignal.set(null);
  }

  hasRole(role: string): boolean {
    const user = this.userSignal();
    return !!user && user.roles.includes(role);
  }
}
