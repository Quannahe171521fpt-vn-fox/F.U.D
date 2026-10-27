import React, { createContext, useContext, useState, useEffect, type ReactNode } from 'react';
import type { AuthUser, LoginPayload, GoogleLoginPayload } from '../types/auth';
import { authApi } from '../api/authApi';

interface AuthContextType {
  user: AuthUser | null;
  token: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  login: (payload: LoginPayload) => Promise<AuthUser>;
  loginWithGoogle: (payload: GoogleLoginPayload) => Promise<AuthUser>;
  logout: () => void;
  clearError: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

const TOKEN_KEY = 'fud_access_token';
const USER_KEY = 'fud_user_info';

export const AuthProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Khôi phục phiên đăng nhập từ localStorage khi reload trang
  useEffect(() => {
    const initializeAuth = async () => {
      const savedToken = localStorage.getItem(TOKEN_KEY);
      const savedUserStr = localStorage.getItem(USER_KEY);

      if (savedToken && savedUserStr) {
        try {
          const parsedUser: AuthUser = JSON.parse(savedUserStr);
          setUser(parsedUser);
          setToken(savedToken);

          // Kiểm tra tính hợp lệ của token qua API /me
          try {
            const freshUser = await authApi.getCurrentUser(savedToken);
            setUser(freshUser);
            localStorage.setItem(USER_KEY, JSON.stringify(freshUser));
          } catch {
            // Token hết hạn hoặc không hợp lệ -> xóa bộ nhớ
            logout();
          }
        } catch {
          logout();
        }
      }
      setIsLoading(false);
    };

    initializeAuth();
  }, []);

  const login = async (payload: LoginPayload): Promise<AuthUser> => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await authApi.login(payload);
      setToken(response.accessToken);
      setUser(response.user);

      localStorage.setItem(TOKEN_KEY, response.accessToken);
      localStorage.setItem(USER_KEY, JSON.stringify(response.user));

      return response.user;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Đăng nhập không thành công.';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const loginWithGoogle = async (payload: GoogleLoginPayload): Promise<AuthUser> => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await authApi.loginWithGoogle(payload);
      setToken(response.accessToken);
      setUser(response.user);

      localStorage.setItem(TOKEN_KEY, response.accessToken);
      localStorage.setItem(USER_KEY, JSON.stringify(response.user));

      return response.user;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Đăng nhập Google thất bại.';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = () => {
    setUser(null);
    setToken(null);
    setError(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  };

  const clearError = () => setError(null);

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!user,
        isLoading,
        error,
        login,
        loginWithGoogle,
        logout,
        clearError,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
