import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios';
import type { ApiResponse } from '../types/api';

/**
 * Axios instance dùng chung cho toàn bộ API calls.
 * - Tự gắn JWT vào header Authorization
 * - Xử lý 401 / 403 tập trung (tự làm mới token 1 lần, sau đó buộc đăng xuất)
 */
const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:7212/api',
  headers: { 'Content-Type': 'application/json' },
  timeout: 20000,
});

const TOKEN_STORAGE_KEY = 'fud.accessToken';
const REFRESH_STORAGE_KEY = 'fud.refreshToken';

export const tokenStorage = {
  get(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  },
  set(token: string): void {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
  },
  clear(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(REFRESH_STORAGE_KEY);
  },
};

export const refreshTokenStorage = {
  get(): string | null {
    return localStorage.getItem(REFRESH_STORAGE_KEY);
  },
  set(token: string): void {
    localStorage.setItem(REFRESH_STORAGE_KEY, token);
  },
};

/** Sự kiện phát ra khi API trả 401 — store auth dùng để đăng xuất. */
export const UNAUTHORIZED_EVENT = 'fud:unauthorized';

interface RetryableRequest extends InternalAxiosRequestConfig {
  _retried?: boolean;
}

http.interceptors.request.use((config) => {
  const token = tokenStorage.get();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/** Gọi trực tiếp endpoint refresh (không qua interceptor để tránh vòng lặp). */
async function tryRefreshToken(): Promise<string | null> {
  const refreshToken = refreshTokenStorage.get();
  if (!refreshToken) return null;

  try {
    const response = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
      `${http.defaults.baseURL}/auth/refresh`,
      { refreshToken },
      { headers: { 'Content-Type': 'application/json' } }
    );

    const body = response.data;
    if (!body?.success || !body.data?.accessToken) return null;

    tokenStorage.set(body.data.accessToken);
    if (body.data.refreshToken) refreshTokenStorage.set(body.data.refreshToken);
    return body.data.accessToken;
  } catch {
    return null;
  }
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiResponse>) => {
    const status = error.response?.status;
    const original = error.config as RetryableRequest | undefined;

    // 401: thử làm mới token 1 lần rồi thử lại request gốc.
    if (status === 401 && original && !original._retried) {
      const newToken = await tryRefreshToken();
      if (newToken) {
        original._retried = true;
        original.headers.Authorization = `Bearer ${newToken}`;
        return http.request(original);
      }
    }

    if (status === 401) {
      tokenStorage.clear();
      window.dispatchEvent(new CustomEvent(UNAUTHORIZED_EVENT));
    }

    return Promise.reject(error);
  }
);

/** Trích xuất danh sách lỗi từ response lỗi của backend (ApiResponse.Fail). */
export function extractErrorMessages(error: unknown): string[] {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as ApiResponse | undefined;
    if (data?.errors?.length) return data.errors;
    if (data?.message) return [data.message];
    if (error.message) return [error.message];
  }
  if (error instanceof Error) return [error.message];
  return ['Đã xảy ra lỗi, vui lòng thử lại'];
}

export default http;
