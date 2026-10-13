import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  PatientDto, 
  CreatePatientRequest, 
  PatientCreationResult, 
  DuplicateMatchCandidate, 
  PatientSearchResult 
} from '../models/patient.models';
import { getApiBaseUrl } from '../config/api.config';

@Injectable({
  providedIn: 'root'
})
export class PatientService {
  private readonly apiUrl = getApiBaseUrl() + '/patients';

  constructor(private http: HttpClient) {}

  search(query?: string, pageNumber: number = 1, pageSize: number = 15): Observable<PatientSearchResult> {
    let params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    if (query && query.trim().length > 0) {
      params = params.set('query', query.trim());
    }

    return this.http.get<PatientSearchResult>(`${this.apiUrl}/search`, { params });
  }

  getById(id: string): Observable<PatientDto> {
    return this.http.get<PatientDto>(`${this.apiUrl}/${id}`);
  }

  getByMrn(mrn: string): Observable<PatientDto> {
    return this.http.get<PatientDto>(`${this.apiUrl}/by-mrn/${encodeURIComponent(mrn)}`);
  }

  checkDuplicates(request: CreatePatientRequest): Observable<DuplicateMatchCandidate[]> {
    return this.http.post<DuplicateMatchCandidate[]>(`${this.apiUrl}/check-duplicates`, request);
  }

  create(request: CreatePatientRequest): Observable<PatientDto> {
    return this.http.post<PatientDto>(this.apiUrl, request);
  }

  update(id: string, updateData: any): Observable<PatientDto> {
    return this.http.put<PatientDto>(`${this.apiUrl}/${id}`, updateData);
  }
}
