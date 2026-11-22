import React, { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { fetchCurrentUser, logoutApi } from '../services/authApi';
import {
  getAccessToken,
  refreshAccessToken,
  setAccessToken as setApiClientAccessToken,
  setImpersonationToken,
} from '../services/apiClient';
import {
  DEFAULT_IDLE_TIMEOUT_MINUTES,
  LAST_ACTIVITY_KEY,
  type SessionExpirationReason,
  type SessionMeta,
  broadcastAuthEvent,
  clearLegacyAuthKeys,
  clearPaymentClientCaches,
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

export interface ImpersonationSession {
  student: UserInfo;
  token: string;
  expiresAt: number;
  managerId: number;
}

interface AuthContextType {
  currentUser: UserInfo | null;
  accessToken: string | null;
  isLoading: boolean;
  sessionMessage: string | null;
  impersonation: ImpersonationSession | null;
  isImpersonating: boolean;
  clearSessionMessage: () => void;
  loginSuccess: (token: string, user: UserInfo, meta?: Partial<SessionMeta>) => void;
  logout: () => Promise<void>;
  startImpersonation: (session: ImpersonationSession) => void;
  stopImpersonation: () => void;
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

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [currentUser, setCurrentUser] = useState<UserInfo | null>(null);
  const [accessToken, setAccessTokenState] = useState<string | null>(() => getAccessToken());
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [sessionMessage, setSessionMessage] = useState<string | null>(null);
  const [impersonation, setImpersonation] = useState<ImpersonationSession | null>(null);
  const isImpersonating = impersonation !== null;

  const startImpersonation = useCallback((session: ImpersonationSession) => {
    clearPaymentClientCaches();
    setImpersonation(session);
    setImpersonationToken(session.token);
  }, []);

  const stopImpersonation = useCallback(() => {
    clearPaymentClientCaches();
    setImpersonation(null);
    setImpersonationToken(null);
  }, []);

  useEffect(() => {
    if (!impersonation) return;
    const timeLeft = impersonation.expiresAt - Date.now();
    if (timeLeft <= 0) {
      stopImpersonation();
      return;
    }
    const timer = setTimeout(() => {
      stopImpersonation();
      alert('Phiên đăng nhập hộ sinh viên đã hết hạn (30 phút). Đã trở lại tài khoản Quản lý.');
    }, timeLeft);
    return () => clearTimeout(timer);
  }, [impersonation, stopImpersonation]);

  useEffect(() => {
    if (!currentUser && impersonation) {
      stopImpersonation();
    }
  }, [currentUser, impersonation, stopImpersonation]);

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
      setImpersonationToken(null);
      setImpersonation(null);
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
      clearLegacyAuthKeys();
      let token = getAccessToken();
      let hasLastActivityHint = false;
      try {
        hasLastActivityHint =
          typeof localStorage !== 'undefined' && Boolean(localStorage.getItem(LAST_ACTIVITY_KEY));
      } catch {
        hasLastActivityHint = false;
      }
      const hasSessionHint = Boolean(token) || Boolean(getSessionMeta()) || hasLastActivityHint;

      if (!hasSessionHint) {
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
        if (!token && hasSessionHint) {
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
      setImpersonationToken(null);
      setImpersonation(null);
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
        setImpersonationToken(null);
        setImpersonation(null);
        clearSessionStorage();
        setCurrentUser(null);
        setAccessTokenState(null);
        setSessionMessage(getExpirationMessage(msg.reason ?? 'server'));
        setIsLoading(false);
      } else if (msg.type === 'LOGOUT') {
        authSeqRef.current += 1;
        setApiClientAccessToken(null);
        setImpersonationToken(null);
        setImpersonation(null);
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
    setSessionMessage(null);
    setIsLoading(false);
  }, []);

  const logout = useCallback(async () => {
    authSeqRef.current += 1;
    setImpersonation(null);
    setImpersonationToken(null);
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
        impersonation,
        isImpersonating,
        clearSessionMessage,
        loginSuccess,
        logout,
        startImpersonation,
        stopImpersonation,
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
