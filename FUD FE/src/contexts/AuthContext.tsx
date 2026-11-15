import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { fetchCurrentUser, logoutApi } from '../services/authApi';
import {
  getAccessToken,
  refreshAccessToken,
  setAccessToken as setApiClientAccessToken,
} from '../services/apiClient';
import {
  DEFAULT_IDLE_TIMEOUT_MINUTES,
  LAST_ACTIVITY_KEY,
  USER_KEY,
  type SessionExpirationReason,
  type SessionMeta,
  broadcastAuthEvent,
  clearSessionStorage,
  getSessionExpirationReason,
  getSessionMeta,
  recordUserActivity,
  saveSessionMeta,
  subscribeCrossTabAuth,
} from '../services/sessionManager';

export interface UserInfo {
  id: number;
  code: string;
  fullName: string;
  email: string;
  gender?: string;
  primaryRole: 'Admin' | 'Manager' | 'Guard' | 'Student';
  roles: string[];
  accountStatus?: string;
  suspensionReason?: string;
  dormId?: number;
  dormName?: string;
}

interface AuthContextType {
  currentUser: UserInfo | null;
  accessToken: string | null;
  isLoading: boolean;
  sessionMessage: string | null;
  clearSessionMessage: () => void;
  loginSuccess: (token: string, user: UserInfo, meta?: Partial<SessionMeta>) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

function getExpirationMessage(reason: SessionExpirationReason): string {
  if (reason === 'idle') {
    const minutes = getSessionMeta()?.idleTimeoutMinutes ?? DEFAULT_IDLE_TIMEOUT_MINUTES;
    return `Phiên đăng nhập đã tự động kết thúc do không thao tác trong ${minutes} phút.`;
  }
  if (reason === 'absolute') {
    return 'Phiên đăng nhập đã đạt giới hạn tối đa. Vui lòng đăng nhập lại.';
  }
  return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';
}

function persistCurrentUser(user: UserInfo | null): void {
  try {
    if (typeof localStorage === 'undefined') return;
    if (user) {
      localStorage.setItem(USER_KEY, JSON.stringify(user));
    } else {
      localStorage.removeItem(USER_KEY);
    }
  } catch {
    // storage unavailable
  }
}

function hasStoredUserKey(): boolean {
  try {
    return typeof localStorage !== 'undefined' && Boolean(localStorage.getItem(USER_KEY));
  } catch {
    return false;
  }
}

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<UserInfo | null>(() => {
    try {
      const saved = typeof localStorage !== 'undefined' ? localStorage.getItem(USER_KEY) : null;
      return saved ? JSON.parse(saved) : null;
    } catch {
      return null;
    }
  });
  const [accessToken, setAccessTokenState] = useState<string | null>(() => getAccessToken());
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [sessionMessage, setSessionMessage] = useState<string | null>(null);

  const authSeqRef = useRef(0);
  const isLoggingOutRef = useRef(false);

  const clearSessionMessage = useCallback(() => {
    setSessionMessage(null);
  }, []);

  const expireSession = useCallback(async (reason: SessionExpirationReason) => {
    if (isLoggingOutRef.current) return;
    isLoggingOutRef.current = true;
    authSeqRef.current += 1;

    try {
      broadcastAuthEvent({ type: 'EXPIRED', reason, at: Date.now() });
      setApiClientAccessToken(null);
      clearSessionStorage();
      setAccessTokenState(null);
      setCurrentUser(null);
      setSessionMessage(getExpirationMessage(reason));

      if (reason !== 'server') {
        try {
          await logoutApi();
        } catch {
          // best-effort backend cookie/token revocation
        }
      }
    } finally {
      isLoggingOutRef.current = false;
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function initAuth() {
      const seq = authSeqRef.current;
      const hasStoredUser = hasStoredUserKey();
      let token = getAccessToken();

      if (!hasStoredUser && !token) {
        if (!cancelled && seq === authSeqRef.current) {
          setCurrentUser(null);
          setAccessTokenState(null);
          setIsLoading(false);
        }
        return;
      }

      const expiredReason = getSessionExpirationReason();
      if (expiredReason !== null) {
        await expireSession(expiredReason);
        if (!cancelled && seq === authSeqRef.current) {
          setIsLoading(false);
        }
        return;
      }

      try {
        if (!token && hasStoredUser) {
          token = await refreshAccessToken();
          if (cancelled || seq !== authSeqRef.current) return;

          if (!token) {
            setApiClientAccessToken(null);
            clearSessionStorage();
            setCurrentUser(null);
            setAccessTokenState(null);
            return;
          }
        }

        const user = await fetchCurrentUser();
        if (cancelled || seq !== authSeqRef.current) return;

        if (user) {
          setCurrentUser(user);
          setAccessTokenState(getAccessToken());
          persistCurrentUser(user);
          try {
            if (typeof localStorage !== 'undefined' && !localStorage.getItem(LAST_ACTIVITY_KEY)) {
              recordUserActivity(true);
            }
          } catch {
            // storage unavailable
          }
        } else {
          setApiClientAccessToken(null);
          clearSessionStorage();
          setCurrentUser(null);
          setAccessTokenState(null);
        }
      } catch {
        if (!cancelled && seq === authSeqRef.current) {
          setApiClientAccessToken(null);
          clearSessionStorage();
          setCurrentUser(null);
          setAccessTokenState(null);
        }
      } finally {
        if (!cancelled && seq === authSeqRef.current) {
          setIsLoading(false);
        }
      }
    }

    void initAuth();

    const handleExpired = (event: Event) => {
      if (cancelled) return;
      const reason =
        (event as CustomEvent<{ reason?: SessionExpirationReason }>).detail?.reason ?? 'server';
      authSeqRef.current += 1;
      setCurrentUser(null);
      setAccessTokenState(null);
      setSessionMessage(getExpirationMessage(reason));
      setIsLoading(false);
    };
    window.addEventListener('fud:session-expired', handleExpired);

    const unsubscribeCrossTab = subscribeCrossTabAuth((msg) => {
      if (cancelled) return;
      if (msg.type === 'EXPIRED') {
        authSeqRef.current += 1;
        setApiClientAccessToken(null);
        clearSessionStorage();
        setCurrentUser(null);
        setAccessTokenState(null);
        setSessionMessage(getExpirationMessage(msg.reason ?? 'server'));
        setIsLoading(false);
      } else if (msg.type === 'LOGOUT') {
        authSeqRef.current += 1;
        setApiClientAccessToken(null);
        clearSessionStorage();
        setCurrentUser(null);
        setAccessTokenState(null);
        setSessionMessage(null);
        setIsLoading(false);
      } else if (msg.type === 'TOKEN_UPDATED') {
        setAccessTokenState(getAccessToken());
      }
    });

    return () => {
      cancelled = true;
      window.removeEventListener('fud:session-expired', handleExpired);
      unsubscribeCrossTab();
    };
  }, [expireSession]);

  const isLoggedIn = Boolean(currentUser);

  useEffect(() => {
    if (!isLoggedIn) return;

    const handleUserActivity = () => {
      const result = recordUserActivity(false);
      if (result.expiredReason) {
        void expireSession(result.expiredReason);
      }
    };

    const handleFocusOrVisibility = () => {
      if (typeof document !== 'undefined' && document.hidden) return;
      const reason = getSessionExpirationReason();
      if (reason) {
        void expireSession(reason);
      } else {
        recordUserActivity(false);
      }
    };

    const activityEvents: Array<keyof WindowEventMap> = [
      'pointerdown',
      'keydown',
      'mousemove',
      'scroll',
      'touchstart',
    ];
    for (const evt of activityEvents) {
      window.addEventListener(evt, handleUserActivity, { passive: true });
    }
    document.addEventListener('visibilitychange', handleFocusOrVisibility);
    window.addEventListener('focus', handleFocusOrVisibility);

    const checkInterval = window.setInterval(() => {
      const reason = getSessionExpirationReason();
      if (reason) {
        void expireSession(reason);
      }
    }, 15_000);

    return () => {
      window.clearInterval(checkInterval);
      for (const evt of activityEvents) {
        window.removeEventListener(evt, handleUserActivity);
      }
      document.removeEventListener('visibilitychange', handleFocusOrVisibility);
      window.removeEventListener('focus', handleFocusOrVisibility);
    };
  }, [isLoggedIn, expireSession]);

  const loginSuccess = useCallback((token: string, user: UserInfo, meta?: Partial<SessionMeta>) => {
    authSeqRef.current += 1;
    setApiClientAccessToken(token);
    if (meta) {
      saveSessionMeta(meta);
    }
    recordUserActivity(true);
    setAccessTokenState(token);
    setCurrentUser(user);
    persistCurrentUser(user);
    setSessionMessage(null);
    setIsLoading(false);
  }, []);

  const logout = useCallback(async () => {
    authSeqRef.current += 1;
    broadcastAuthEvent({ type: 'LOGOUT', at: Date.now() });
    try {
      await logoutApi();
    } finally {
      setApiClientAccessToken(null);
      clearSessionStorage();
      setAccessTokenState(null);
      setCurrentUser(null);
      setSessionMessage(null);
    }
  }, []);

  return (
    <AuthContext.Provider
      value={{
        currentUser,
        accessToken,
        isLoading,
        sessionMessage,
        clearSessionMessage,
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
