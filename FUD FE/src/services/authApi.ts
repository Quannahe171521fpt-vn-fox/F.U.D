import { authFetch, setAccessToken } from './apiClient';

export async function loginWithCredentials(identifier: string, password: string) {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ identifier, password }),
  });

  const json = await res.json();
  if (!res.ok) throw new Error(json.message || 'Đăng nhập thất bại');
  setAccessToken(json.data.accessToken);
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
  return json.data;
}

export async function logoutApi() {
  await fetch('/api/auth/logout', {
    method: 'POST',
    credentials: 'include',
  });
  setAccessToken(null);
}

export async function fetchCurrentUser() {
  const res = await authFetch('/api/auth/me');
  if (!res.ok) return null;
  const json = await res.json();
  return json.data;
}
