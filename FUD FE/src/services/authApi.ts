import { authFetch, setAccessToken } from './apiClient';
import { clearSessionStorage, recordUserActivity, saveSessionMeta } from './sessionManager';

export async function loginWithCredentials(account: string, password: string) {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ account, password }),
  });

  const json = await res.json();
  if (!res.ok) throw new Error(json.message || 'Đăng nhập thất bại');
  setAccessToken(json.data.accessToken);
  saveSessionMeta({
    idleTimeoutMinutes: json.data?.idleTimeoutMinutes,
    absoluteExpiresAt: json.data?.absoluteExpiresAt,
  });
  recordUserActivity(true);
  return json.data;
}

export async function loginWithGoogle(idToken: string) {
  const res = await fetch('/api/auth/google-login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ idToken }),
  });

  const json = await res.json();
  if (!res.ok) throw new Error(json.message || 'Đăng nhập Google thất bại');
  setAccessToken(json.data.accessToken);
  saveSessionMeta({
    idleTimeoutMinutes: json.data?.idleTimeoutMinutes,
    absoluteExpiresAt: json.data?.absoluteExpiresAt,
  });
  recordUserActivity(true);
  return json.data;
}

export async function logoutApi() {
  const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
  const timeoutId =
    controller && typeof window !== 'undefined'
      ? window.setTimeout(() => controller.abort(), 4000)
      : null;

  try {
    await fetch('/api/auth/logout', {
      method: 'POST',
      credentials: 'include',
      signal: controller?.signal,
    });
  } catch {
    // best-effort logout request; still clear local session in finally
  } finally {
    if (timeoutId !== null) {
      window.clearTimeout(timeoutId);
    }
    setAccessToken(null);
    clearSessionStorage();
  }
}

export async function fetchCurrentUser() {
  const res = await authFetch('/api/auth/me');
  if (!res.ok) return null;
  const json = await res.json();
  return json.data;
}
