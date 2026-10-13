export type EncounterType = 'OPD' | 'Emergency' | 'Inpatient' | 'FollowUp';
export type EncounterStatus = 'Active' | 'Discharged' | 'Completed' | 'Cancelled';

export interface EncounterDto {
  id: string;
  patientId: string;
  encounterNumber: string;
  type: EncounterType;
  status: EncounterStatus;
  facility: string;
  department: string;
  startDate: string;
  endDate?: string;
  attendingClinicianName?: string;
  reasonForVisit?: string;
  clinicalEventsCount: number;
  documentsCount: number;
  createdAt: string;
}

export interface CreateEncounterRequest {
  patientId: string;
  type: EncounterType;
  facility: string;
  department: string;
  startDate: string;
  attendingClinicianName?: string;
  reasonForVisit?: string;
}

export interface ClinicalEventDto {
  id: string;
  encounterId: string;
  eventType: string;
  eventTime: string;
  author: string;
  department: string;
  description?: string;
  documentsCount: number;
}
