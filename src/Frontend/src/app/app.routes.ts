import { Routes } from '@angular/router';
import { LandingComponent } from './components/landing/landing.component';
import { LoginComponent } from './components/login/login.component';
import { PatientSearchComponent } from './components/patient-search/patient-search.component';
import { PatientRegisterComponent } from './components/patient-register/patient-register.component';
import { PatientDetailsComponent } from './components/patient-details/patient-details.component';
import { ReviewQueueComponent } from './components/review-queue/review-queue.component';
import { AdminOperationsComponent } from './components/admin-operations/admin-operations.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', component: LandingComponent },
  { path: 'landing', component: LandingComponent },
  { path: 'login', component: LoginComponent },
  { path: 'patients', component: PatientSearchComponent, canActivate: [authGuard] },
  { path: 'patients/register', component: PatientRegisterComponent, canActivate: [authGuard] },
  { path: 'patients/:id', component: PatientDetailsComponent, canActivate: [authGuard] },
  { path: 'review-queue', component: ReviewQueueComponent, canActivate: [authGuard] },
  { path: 'admin', component: AdminOperationsComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];

