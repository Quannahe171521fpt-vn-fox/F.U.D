import { authFetch, setAccessToken } from './apiClient';

async function parseAuthResponse(res: Response, defaultMsg: string) {
  const text = await res.text();
  try {
    return text ? JSON.parse(text) : {};
  } catch {
    throw new Error(`${defaultMsg} (HTTP ${res.status})`);
  }
}

export async function loginWithCredentials(account: string, password: string) {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ account, password }),
  });

  const json = await parseAuthResponse(res, 'Lỗi máy chủ khi đăng nhập');
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

  const json = await parseAuthResponse(res, 'Lỗi máy chủ khi đăng nhập Google');
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
