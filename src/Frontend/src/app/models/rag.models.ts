export interface ClinicalCitationDto {
  documentId: string;
  documentTitle: string;
  sectionHeader?: string;
  citationLabel: string;
  snippet: string;
  relevanceScore: number;
}

export interface ClinicalRagResponseDto {
  query: string;
  answerMarkdown: string;
  citations: ClinicalCitationDto[];
  groundedConfidence: number;
  refusalTriggered: boolean;
  durationMs: number;
  modelName: string;
}

export interface SuggestedQuestionDto {
  category: string;
  questionText: string;
  icon: string;
}

export interface AskAssistantRequest {
  query: string;
  language?: string;
  minRelevance?: number;
  strictGroundedOnly?: boolean;
}

export interface ChatMessage {
  id: string;
  sender: 'user' | 'assistant';
  text: string;
  timestamp: Date;
  response?: ClinicalRagResponseDto;
  isLoading?: boolean;
  hasError?: boolean;
  errorMessage?: string;
}
