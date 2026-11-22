import {
  TOKEN_KEY,
  type SessionExpirationReason,
  broadcastAuthEvent,
  clearSessionStorage,
  getSessionExpirationReason,
  saveSessionMeta,
} from './sessionManager';
import { env } from '../config/env';

let memoryAccessToken: string | null = (() => {
  try {
    return typeof localStorage !== 'undefined' ? localStorage.getItem(TOKEN_KEY) : null;
  } catch {
    return null;
  }
})();

let memoryImpersonationToken: string | null = null;

export const setImpersonationToken = (token: string | null) => {
  memoryImpersonationToken = token;
};

export const getImpersonationToken = (): string | null => {
  return memoryImpersonationToken;
};

let refreshCooldownUntil = 0;
let refreshPromise: Promise<string | null> | null = null;

export const setAccessToken = (token: string | null) => {
  memoryAccessToken = token;
  if (token) {
    refreshCooldownUntil = 0;
  }
  try {
    if (typeof localStorage !== 'undefined') {
      if (token) {
        localStorage.setItem(TOKEN_KEY, token);
      } else {
        localStorage.removeItem(TOKEN_KEY);
      }
    }
  } catch {
    // storage unavailable
  }
};

export const getAccessToken = (): string | null => {
  if (typeof localStorage !== 'undefined') {
    try {
      memoryAccessToken = localStorage.getItem(TOKEN_KEY);
    } catch {
      return memoryAccessToken;
    }
  }
  return memoryAccessToken;
};

function dispatchSessionExpired(reason: SessionExpirationReason): void {
  memoryAccessToken = null;
  // Broadcast BEFORE clearing storage so sibling tabs receive EXPIRED reason before TOKEN_KEY removal event.
  broadcastAuthEvent({ type: 'EXPIRED', reason, at: Date.now() });
  clearSessionStorage();
  if (typeof window !== 'undefined') {
    window.dispatchEvent(new CustomEvent('fud:session-expired', { detail: { reason } }));
  }
}

export async function refreshAccessToken(expectedOldToken?: string): Promise<string | null> {
  const localExpired = getSessionExpirationReason();
  if (localExpired) {
    dispatchSessionExpired(localExpired);
    return null;
  }

  if (Date.now() < refreshCooldownUntil) {
    return null;
  }

  const current = getAccessToken();
  if (expectedOldToken && current && current !== expectedOldToken) {
    return current;
  }

  if (refreshPromise) {
    return refreshPromise;
  }

  const runRefresh = async (): Promise<string | null> => {
    try {
      const latest = getAccessToken();
      if (expectedOldToken && latest && latest !== expectedOldToken) {
        return latest;
      }

      const expiredInsideLock = getSessionExpirationReason();
      if (expiredInsideLock) {
        dispatchSessionExpired(expiredInsideLock);
        return null;
      }

      const res = await fetch(`${env.apiBaseUrl}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
      });

      if (res.status === 401 || res.status === 403) {
        dispatchSessionExpired('server');
        return null;
      }

      if (!res.ok) {
        refreshCooldownUntil = Date.now() + 30_000;
        return null;
      }

      const json = await res.json();
      const newToken: string | null = json?.data?.accessToken ?? null;
      if (!newToken) {
        dispatchSessionExpired('server');
        return null;
      }

      setAccessToken(newToken);
      saveSessionMeta({
        idleTimeoutMinutes: json.data?.idleTimeoutMinutes,
        absoluteExpiresAt: json.data?.absoluteExpiresAt,
      });
      broadcastAuthEvent({ type: 'TOKEN_UPDATED', token: newToken, at: Date.now() });
      refreshCooldownUntil = 0;
      return newToken;
    } catch {
      refreshCooldownUntil = Date.now() + 30_000;
      return null;
    }
  };

  refreshPromise = (async () => {
    try {
      if (typeof navigator !== 'undefined' && navigator.locks?.request) {
        return await navigator.locks.request('fud_auth_refresh', runRefresh);
      }
      return await runRefresh();
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export async function authFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const headers = new Headers(options.headers || {});

  const token = memoryImpersonationToken || getAccessToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let res = await fetch(url, { ...options, headers, credentials: 'include' });

  // Nếu gặp 401: Tự động gọi refresh token và retry 1 lần duy nhất (chỉ khi trước đó đã có token và KHÔNG ở chế độ impersonate)
  if (res.status === 401 && token && !memoryImpersonationToken) {
    const latestToken = getAccessToken();
    const newToken =
      latestToken && latestToken !== token
        ? latestToken
        : await refreshAccessToken(token);
    if (newToken) {
      headers.set('Authorization', `Bearer ${newToken}`);
      res = await fetch(url, { ...options, headers, credentials: 'include' });
    }
  }

  return res;
}
