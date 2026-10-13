import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { 
  ClinicalRagResponseDto, 
  SuggestedQuestionDto, 
  AskAssistantRequest 
} from '../models/rag.models';

@Injectable({
  providedIn: 'root'
})
export class RagService {
  private readonly baseUrl = 'http://localhost:5000/api/v1';

  constructor(private http: HttpClient) {}

  askAssistant(patientId: string, request: AskAssistantRequest): Observable<ClinicalRagResponseDto> {
    return this.http.post<ClinicalRagResponseDto>(
      `${this.baseUrl}/patients/${patientId}/ai-assistant/ask`, 
      request
    );
  }

  getSuggestedQuestions(patientId: string): Observable<SuggestedQuestionDto[]> {
    return this.http.get<SuggestedQuestionDto[]>(
      `${this.baseUrl}/patients/${patientId}/ai-assistant/suggested-questions`
    );
  }
}
