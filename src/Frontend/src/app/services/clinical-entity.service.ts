import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  ClinicalEntityDto, 
  VerifyClinicalEntityRequest, 
  PatientTimelineDto 
} from '../models/clinical-entity.models';
import { getApiBaseUrl } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class ClinicalEntityService {
  private readonly baseUrl = getApiBaseUrl();

  constructor(private http: HttpClient) {}

  getEntitiesByDocument(documentId: string): Observable<ClinicalEntityDto[]> {
    return this.http.get<ClinicalEntityDto[]>(`${this.baseUrl}/documents/${documentId}/entities`);
  }

  getEntitiesByPatient(patientId: string): Observable<ClinicalEntityDto[]> {
    return this.http.get<ClinicalEntityDto[]>(`${this.baseUrl}/patients/${patientId}/entities`);
  }

  verifyEntity(entityId: string, req: VerifyClinicalEntityRequest): Observable<ClinicalEntityDto> {
    return this.http.post<ClinicalEntityDto>(`${this.baseUrl}/entities/${entityId}/verify`, req);
  }

  getPatientTimeline(patientId: string): Observable<PatientTimelineDto> {
    return this.http.get<PatientTimelineDto>(`${this.baseUrl}/patients/${patientId}/timeline`);
  }
}
