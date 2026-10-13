import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DocumentDto, DocumentUploadResult, DocumentOcrResultDto, VerifyOcrRequest, DocumentReviewQueueItemDto } from '../models/document.models';

@Injectable({
  providedIn: 'root'
})
export class DocumentService {
  private readonly baseUrl = 'http://localhost:5000/api/v1';

  constructor(private http: HttpClient) {}

  getDocumentsByPatient(patientId: string): Observable<DocumentDto[]> {
    return this.http.get<DocumentDto[]>(`${this.baseUrl}/patients/${patientId}/documents`);
  }

  getDocumentsByEncounter(encounterId: string): Observable<DocumentDto[]> {
    return this.http.get<DocumentDto[]>(`${this.baseUrl}/encounters/${encounterId}/documents`);
  }

  uploadDocuments(patientId: string, formData: FormData): Observable<DocumentUploadResult> {
    return this.http.post<DocumentUploadResult>(`${this.baseUrl}/patients/${patientId}/documents/upload`, formData);
  }

  amendDocument(documentId: string, formData: FormData): Observable<DocumentDto> {
    return this.http.post<DocumentDto>(`${this.baseUrl}/documents/${documentId}/amend`, formData);
  }

  getDownloadUrl(documentId: string, versionNumber?: number): string {
    const query = versionNumber ? `?versionNumber=${versionNumber}` : '';
    return `${this.baseUrl}/documents/${documentId}/download${query}`;
  }

  getViewUrl(documentId: string, versionNumber?: number): string {
    const query = versionNumber ? `?versionNumber=${versionNumber}&inline=true` : '?inline=true';
    return `${this.baseUrl}/documents/${documentId}/view${query}`;
  }

  getReviewQueue(): Observable<DocumentReviewQueueItemDto[]> {
    return this.http.get<DocumentReviewQueueItemDto[]>(`${this.baseUrl}/documents/review-queue`);
  }

  getOcrResult(documentId: string): Observable<DocumentOcrResultDto> {
    return this.http.get<DocumentOcrResultDto>(`${this.baseUrl}/documents/${documentId}/ocr`);
  }

  verifyOcr(documentId: string, request: VerifyOcrRequest): Observable<DocumentDto> {
    return this.http.post<DocumentDto>(`${this.baseUrl}/documents/${documentId}/verify-ocr`, request);
  }

  reprocessOcr(documentId: string, versionNumber?: number): Observable<DocumentOcrResultDto> {
    const query = versionNumber ? `?versionNumber=${versionNumber}` : '';
    return this.http.post<DocumentOcrResultDto>(`${this.baseUrl}/documents/${documentId}/reprocess-ocr${query}`, {});
  }
}
