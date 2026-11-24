import { authFetch } from './apiClient';
import type { UserInfo } from '../contexts/AuthContext';
import { env } from '../config/env';

export interface StudentLookupResult {
  id: number;
  code: string;
  fullName: string;
  email: string;
  phone?: string | null;
  gender?: string | null;
  accountStatus: string;
  suspensionReason?: string | null;
  createdAt?: string;
  primaryRole: 'Student';
  roles: string[];
  major?: string | null;
  cohort?: string | null;
  currentResidency?: {
    id: number;
    status: string;
    termName?: string | null;
    dormName?: string | null;
    blockName?: string | null;
    roomNumber?: string | null;
    bedCode?: string | null;
    checkInDate?: string | null;
    plannedCheckOutDate?: string | null;
  } | null;
}

export interface ImpersonationResponse {
  impersonationToken: string;
  expiresIn: number;
  student: UserInfo;
}

/**
 * Tra cứu thông tin sinh viên theo Email hoặc Mã sinh viên (Code).
 */
export async function lookupStudentApi(query: string): Promise<StudentLookupResult> {
  const res = await authFetch(`${env.apiBaseUrl}/manager/lookup-student?q=${encodeURIComponent(query.trim())}`);
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(json?.message || 'Không tìm thấy sinh viên nào với thông tin đã nhập.');
  }
  return json.data as StudentLookupResult;
}

/**
 * Tạo token đăng nhập hộ (Impersonate) sinh viên.
 */
export async function createImpersonationTokenApi(studentId: number): Promise<ImpersonationResponse> {
  const res = await authFetch(`${env.apiBaseUrl}/manager/impersonate/${studentId}`, {
    method: 'POST',
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok) {
    throw new Error(json?.message || 'Không thể khởi tạo phiên đăng nhập hộ sinh viên.');
  }
  return json.data as ImpersonationResponse;
}

