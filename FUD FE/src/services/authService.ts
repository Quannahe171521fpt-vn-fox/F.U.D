import http from './http';
import type { ApiResponse } from '../types/api';

export interface LoginPayload {
  usernameOrEmail: string;
  password: string;
}

export interface AuthSession {
  userId: number;
  username: string;
  fullName: string;
  email: string;
  status: string;
  roles: string[];
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}

export const authService = {
  async login(payload: LoginPayload): Promise<AuthSession> {
    const response = await http.post<ApiResponse<AuthSession>>('/auth/login', payload);
    const body = response.data;
    if (!body?.success || !body.data) {
      throw new Error(body?.message || 'Đăng nhập thất bại');
    }
    return body.data;
  },

  async refresh(refreshToken: string): Promise<AuthSession> {
    const response = await http.post<ApiResponse<AuthSession>>('/auth/refresh', { refreshToken });
    const body = response.data;
    if (!body?.success || !body.data) {
      throw new Error(body?.message || 'Làm mới phiên đăng nhập thất bại');
    }
    return body.data;
  },
};

export default authService;
