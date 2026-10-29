import React, { createContext, useContext, useEffect, useState } from 'react';
import { fetchCurrentUser, logoutApi } from '../services/authApi';
import { getAccessToken, setAccessToken } from '../services/apiClient';

export interface UserInfo {
  id: number;
  code: string;
  fullName: string;
  email: string;
  primaryRole: 'Admin' | 'Manager' | 'Guard' | 'Student';
  roles: string[];
}

interface AuthContextType {
  currentUser: UserInfo | null;
  accessToken: string | null;
  isLoading: boolean;
  loginSuccess: (token: string, user: UserInfo) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<UserInfo | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    let isMounted = true;

    async function initAuth() {
      try {
        const user = await fetchCurrentUser();
        if (user && isMounted) {
          setCurrentUser(user);
        }
      } finally {
        if (isMounted) setIsLoading(false);
      }
    }

    initAuth();
    return () => { isMounted = false; };
  }, []);

  const loginSuccess = (token: string, user: UserInfo) => {
    setAccessToken(token);
    setCurrentUser(user);
  };

  const logout = async () => {
    try {
      await logoutApi();
    } finally {
      setCurrentUser(null);
    }
  };

  return (
    <AuthContext.Provider
      value={{
        currentUser,
        accessToken: getAccessToken(),
        isLoading,
        loginSuccess,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth phải được sử dụng bên trong AuthProvider');
  return ctx;
};
