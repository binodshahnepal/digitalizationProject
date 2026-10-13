import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { PatientSearchComponent } from './components/patient-search/patient-search.component';
import { PatientRegisterComponent } from './components/patient-register/patient-register.component';
import { PatientDetailsComponent } from './components/patient-details/patient-details.component';
import { ReviewQueueComponent } from './components/review-queue/review-queue.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'patients', component: PatientSearchComponent, canActivate: [authGuard] },
  { path: 'patients/register', component: PatientRegisterComponent, canActivate: [authGuard] },
  { path: 'patients/:id', component: PatientDetailsComponent, canActivate: [authGuard] },
  { path: 'review-queue', component: ReviewQueueComponent, canActivate: [authGuard] },
  { path: '', redirectTo: 'patients', pathMatch: 'full' },
  { path: '**', redirectTo: 'patients' }
];
