import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { EncounterDto, CreateEncounterRequest, ClinicalEventDto } from '../models/encounter.models';
import { getApiBaseUrl } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class EncounterService {
  private readonly baseUrl = getApiBaseUrl();

  constructor(private http: HttpClient) {}

  getEncountersByPatient(patientId: string): Observable<EncounterDto[]> {
    return this.http.get<EncounterDto[]>(`${this.baseUrl}/patients/${patientId}/encounters`);
  }

  getEncounterById(id: string): Observable<EncounterDto> {
    return this.http.get<EncounterDto>(`${this.baseUrl}/encounters/${id}`);
  }

  createEncounter(patientId: string, request: CreateEncounterRequest): Observable<EncounterDto> {
    return this.http.post<EncounterDto>(`${this.baseUrl}/patients/${patientId}/encounters`, request);
  }

  getClinicalEvents(encounterId: string): Observable<ClinicalEventDto[]> {
    return this.http.get<ClinicalEventDto[]>(`${this.baseUrl}/encounters/${encounterId}/events`);
  }
}
