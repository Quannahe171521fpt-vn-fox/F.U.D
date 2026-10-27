export interface StudentProfile {
  major?: string;
  cohort?: string;
  currentBedId?: number;
  currentBedNumber?: string;
  currentRoomCode?: string;
  currentBlockName?: string;
}

export interface AuthUser {
  id: number;
  code: string;
  fullName: string;
  email: string;
  phone?: string;
  gender: 'Male' | 'Female';
  accountStatus: string;
  roles: string[];
  studentInfo?: StudentProfile;
}

export interface LoginPayload {
  identifier: string;
  password: string;
  rememberMe?: boolean;
}

export interface GoogleLoginPayload {
  idToken: string;
}

export interface AuthResponse {
  success: boolean;
  message?: string;
  accessToken: string;
  refreshToken?: string;
  expiresAt: string;
  user: AuthUser;
}
