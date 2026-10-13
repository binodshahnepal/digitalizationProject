export type Gender = 'Male' | 'Female' | 'Other' | 'Unknown';

export interface PatientDto {
  id: string;
  mrn: string;
  nationalId?: string;
  firstName: string;
  middleName?: string;
  lastName: string;
  fullName: string;
  dateOfBirth: string;
  age: number;
  gender: Gender;
  phoneNumber?: string;
  email?: string;
  district?: string;
  municipality?: string;
  ward?: string;
  addressLine?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelation?: string;
  identityProvenance: string;
  isRestricted: boolean;
  mergedIntoPatientId?: string;
  createdAt: string;
  createdBy: string;
}

export interface DuplicateMatchCandidate {
  existingPatientId: string;
  mrn: string;
  fullName: string;
  dateOfBirth: string;
  phoneNumber?: string;
  matchScore: number;
  matchReason: string;
}

export interface CreatePatientRequest {
  mrn: string;
  nationalId?: string;
  firstName: string;
  middleName?: string;
  lastName: string;
  dateOfBirth: string;
  gender: Gender;
  phoneNumber?: string;
  email?: string;
  district?: string;
  municipality?: string;
  ward?: string;
  addressLine?: string;
  emergencyContactName?: string;
  emergencyContactPhone?: string;
  emergencyContactRelation?: string;
  identityProvenance: string;
  forceCreateIfDuplicate?: boolean;
}

export interface PatientCreationResult {
  success: boolean;
  patient?: PatientDto;
  potentialDuplicates: DuplicateMatchCandidate[];
  errorMessage?: string;
}

export interface PatientSearchResult {
  items: PatientDto[];
  totalCount: number;
  pageNumber: number;
  totalPages: number;
}
