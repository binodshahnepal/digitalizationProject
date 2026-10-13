export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  userId: string;
  username: string;
  fullName: string;
  department: string;
  roles: string[];
  expiresAt: string;
}

export interface UserProfile {
  username: string;
  fullName: string;
  department: string;
  roles: string[];
}
