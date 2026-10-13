export type ClinicalEntityType = 
  | 'Diagnosis'
  | 'TnmStaging'
  | 'CancerStage'
  | 'HistologicGrade'
  | 'Biomarker'
  | 'ChemotherapyRegimen'
  | 'Medication'
  | 'SurgicalProcedure'
  | 'RadiationTherapy'
  | 'LabFinding';

export type MilestoneType = 
  | 'InitialConsult'
  | 'DiagnosticBiopsy'
  | 'PathologyConfirmation'
  | 'DefinitiveSurgery'
  | 'AdjuvantChemotherapy'
  | 'RadiationTherapy'
  | 'RestagingAssessment'
  | 'RemissionOrSurveillance';

export interface ClinicalEntityDto {
  id: string;
  patientId: string;
  documentId: string;
  encounterId?: string;
  entityType: ClinicalEntityType;
  category: string;
  name: string;
  value: string;
  code?: string;
  codeSystem?: string;
  confidenceScore: number;
  extractedText: string;
  textOffsetStart?: number;
  textOffsetEnd?: number;
  attributesJson: string;
  clinicalDate?: string;
  isVerified: boolean;
  verifiedBy?: string;
  verifiedAt?: string;
  createdAt: string;
}

export interface VerifyClinicalEntityRequest {
  confirmedName?: string;
  confirmedValue?: string;
  confirmedCode?: string;
  verificationNotes?: string;
}

export interface TimelineEventDto {
  id: string;
  eventDate: string;
  eventType: string;
  title: string;
  summary: string;
  department: string;
  clinician?: string;
  documentId?: string;
  documentCategory?: string;
  encounterId?: string;
  encounterNumber?: string;
  milestone?: MilestoneType;
  milestoneLabel?: string;
  entities: ClinicalEntityDto[];
  isVerified: boolean;
}

export interface PatientTimelineDto {
  patientId: string;
  patientName: string;
  mrn: string;
  primaryDiagnosis?: ClinicalEntityDto;
  clinicalStage?: ClinicalEntityDto;
  histologicGrade?: ClinicalEntityDto;
  biomarkers: ClinicalEntityDto[];
  events: TimelineEventDto[];
  totalEncounters: number;
  totalDocuments: number;
  totalEntities: number;
}
