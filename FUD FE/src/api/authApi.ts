import type { AuthResponse, LoginPayload, GoogleLoginPayload, AuthUser } from '../types/auth';

const API_BASE_URL = import.meta.env.VITE_API_URL || '';

async function fetchWithFallback(endpoint: string, options: RequestInit): Promise<Response> {
  const urls = [
    `${API_BASE_URL}${endpoint}`,
    `https://localhost:7212${endpoint}`,
    `http://localhost:5145${endpoint}`,
  ];

  let lastError: Error | null = null;
  for (const url of urls) {
    try {
      const res = await fetch(url, options);
      return res;
    } catch (err) {
      lastError = err instanceof Error ? err : new Error(String(err));
    }
  }

  throw lastError || new Error('Không thể kết nối đến máy chủ Backend (Failed to fetch). Vui lòng kiểm tra dotnet run.');
}

export const authApi = {
  async login(payload: LoginPayload): Promise<AuthResponse> {
    const res = await fetchWithFallback('/api/v1/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

    const data = await res.json();
    if (!res.ok) {
      throw new Error(data.message || 'Đăng nhập không thành công.');
    }
    return data;
  },

  async loginWithGoogle(payload: GoogleLoginPayload): Promise<AuthResponse> {
    const res = await fetchWithFallback('/api/v1/auth/google', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(payload),
    });

    const data = await res.json();
    if (!res.ok) {
      throw new Error(data.message || 'Đăng nhập Google thất bại.');
    }
    return data;
  },

  async getCurrentUser(token: string): Promise<AuthUser> {
    const res = await fetchWithFallback('/api/v1/auth/me', {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`,
      },
    });

    const data = await res.json();
    if (!res.ok) {
      throw new Error(data.message || 'Không thể lấy thông tin người dùng.');
    }
    return data.user;
  },
};
