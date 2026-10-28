import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import axios from 'axios';
import authService, { type AuthSession } from '../services/authService';
import { tokenStorage, refreshTokenStorage, UNAUTHORIZED_EVENT } from '../services/http';
import type { ApiResponse } from '../types/api';

interface AuthState {
  session: AuthSession | null;
  isLoading: boolean;
  error: string | null;

  login: (usernameOrEmail: string, password: string) => Promise<void>;
  logout: () => void;
  hasRole: (role: string) => boolean;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      session: null,
      isLoading: false,
      error: null,

      async login(usernameOrEmail, password) {
        set({ isLoading: true, error: null });
        try {
          const session = await authService.login({ usernameOrEmail, password });
          tokenStorage.set(session.accessToken);
          refreshTokenStorage.set(session.refreshToken);
          set({ session, isLoading: false, error: null });
        } catch (err) {
          let message = 'Đăng nhập thất bại';
          if (axios.isAxiosError(err)) {
            const data = err.response?.data as ApiResponse | undefined;
            message = data?.message || err.message || message;
          } else if (err instanceof Error) {
            message = err.message;
          }
          set({ isLoading: false, error: message });
          throw err;
        }
      },

      logout() {
        tokenStorage.clear();
        set({ session: null, error: null });
      },

      hasRole(role: string) {
        const roles = get().session?.roles ?? [];
        return roles.includes(role);
      },
    }),
    {
      name: 'fud.auth',
      partialize: (state) => ({ session: state.session }),
    }
  )
);

/** Đã đăng nhập = có session đã lưu VÀ còn access token trong storage. */
export function selectIsAuthenticated(state: AuthState): boolean {
  return !!state.session && !!tokenStorage.get();
}

// Đồng bộ khi API trả 401 (token hết hạn / bị thu hồi) → đăng xuất ngay.
if (typeof window !== 'undefined') {
  window.addEventListener(UNAUTHORIZED_EVENT, () => {
    useAuthStore.getState().logout();
  });
}
