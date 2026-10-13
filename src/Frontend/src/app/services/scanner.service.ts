import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { ScannerDeviceDto, ScanRequest, ScanResultDto } from '../models/scanner.models';

@Injectable({
  providedIn: 'root'
})
export class ScannerService {
  private readonly bridgeUrl = 'http://localhost:5100/api/scanner';

  constructor(private http: HttpClient) {}

  getDevices(): Observable<ScannerDeviceDto[]> {
    return this.http.get<ScannerDeviceDto[]>(`${this.bridgeUrl}/devices`).pipe(
      catchError(err => {
        console.warn('Scanner Bridge service is not responding on port 5100:', err);
        return throwError(() => new Error('Scanner Bridge service is offline or not reachable on localhost:5100.'));
      })
    );
  }

  acquireScan(request: ScanRequest): Observable<ScanResultDto> {
    return this.http.post<ScanResultDto>(`${this.bridgeUrl}/scan`, request).pipe(
      catchError(err => {
        console.error('Scan acquisition failed:', err);
        return throwError(() => new Error('Scan acquisition failed. Ensure scanner hardware is connected and powered on.'));
      })
    );
  }

  base64ToFile(base64Data: string, fileName: string, contentType: string = 'application/pdf'): File {
    const byteCharacters = atob(base64Data);
    const byteNumbers = new Array(byteCharacters.length);
    for (let i = 0; i < byteCharacters.length; i++) {
      byteNumbers[i] = byteCharacters.charCodeAt(i);
    }
    const byteArray = new Uint8Array(byteNumbers);
    const blob = new Blob([byteArray], { type: contentType });
    return new File([blob], fileName, { type: contentType });
  }
}
