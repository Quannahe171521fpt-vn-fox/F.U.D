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

const USER_KEY = 'fud_currentUser';

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<UserInfo | null>(() => {
    try {
      const saved = localStorage.getItem(USER_KEY);
      return saved ? JSON.parse(saved) : null;
    } catch {
      return null;
    }
  });
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const loggedInRef = React.useRef(false);

  useEffect(() => {
    let isMounted = true;

    async function initAuth() {
      const token = getAccessToken();
      // Nếu hoàn toàn không có token, chuyển thẳng sang trạng thái sẵn sàng (guest), không gọi /me gây 401
      if (!token) {
        if (isMounted) {
          if (!loggedInRef.current) {
            setCurrentUser(null);
            try {
              localStorage.removeItem(USER_KEY);
            } catch {}
          }
          setIsLoading(false);
        }
        return;
      }

      try {
        const user = await fetchCurrentUser();
        if (isMounted) {
          if (loggedInRef.current) return;

          if (user) {
            setCurrentUser(user);
            try {
              localStorage.setItem(USER_KEY, JSON.stringify(user));
            } catch {}
          } else {
            setCurrentUser(null);
            try {
              localStorage.removeItem(USER_KEY);
            } catch {}
          }
        }
      } catch {
        if (isMounted && !loggedInRef.current) {
          setCurrentUser(null);
          try {
            localStorage.removeItem(USER_KEY);
          } catch {}
        }
      } finally {
        if (isMounted) setIsLoading(false);
      }
    }

    initAuth();

    const handleExpired = () => {
      if (isMounted && !loggedInRef.current) {
        setCurrentUser(null);
        try {
          localStorage.removeItem(USER_KEY);
        } catch {}
      }
    };
    window.addEventListener('fud:session-expired', handleExpired);

    return () => {
      isMounted = false;
      window.removeEventListener('fud:session-expired', handleExpired);
    };
  }, []);

  const loginSuccess = (token: string, user: UserInfo) => {
    loggedInRef.current = true;
    setAccessToken(token);
    setCurrentUser(user);
    try {
      localStorage.setItem(USER_KEY, JSON.stringify(user));
    } catch {}
    setIsLoading(false);
  };

  const logout = async () => {
    loggedInRef.current = false;
    try {
      await logoutApi();
    } finally {
      setAccessToken(null);
      setCurrentUser(null);
      try {
        localStorage.removeItem(USER_KEY);
      } catch {}
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
