const TOKEN_KEY = 'fud_accessToken';

let memoryAccessToken: string | null = (() => {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
})();

let lastTokenSetTime = memoryAccessToken ? Date.now() : 0;
let isRefreshing = false;
let refreshPromise: Promise<string | null> | null = null;

export const setAccessToken = (token: string | null) => {
  memoryAccessToken = token;
  if (token) {
    lastTokenSetTime = Date.now();
  }
  try {
    if (token) {
      localStorage.setItem(TOKEN_KEY, token);
    } else {
      localStorage.removeItem(TOKEN_KEY);
    }
  } catch {
    // ignore
  }
};

export const getAccessToken = () => {
  if (!memoryAccessToken) {
    try {
      memoryAccessToken = localStorage.getItem(TOKEN_KEY);
      if (memoryAccessToken && !lastTokenSetTime) {
        lastTokenSetTime = Date.now();
      }
    } catch {
      memoryAccessToken = null;
    }
  }
  return memoryAccessToken;
};

async function refreshAccessToken(): Promise<string | null> {
  if (isRefreshing && refreshPromise) {
    return refreshPromise;
  }

  isRefreshing = true;
  refreshPromise = (async () => {
    try {
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        credentials: 'include',
      });

      if (!res.ok) {
        // Nếu token mới vừa được cấp trong vòng 5 giây, không xóa phiên vì lỗi có thể do request cũ
        if (Date.now() - lastTokenSetTime < 5000 && memoryAccessToken) {
          return memoryAccessToken;
        }
        setAccessToken(null);
        if (typeof window !== 'undefined') {
          window.dispatchEvent(new CustomEvent('fud:session-expired'));
        }
        return null;
      }

      const json = await res.json();
      const newToken = json.data?.accessToken ?? null;
      setAccessToken(newToken);
      return newToken;
    } catch {
      if (Date.now() - lastTokenSetTime < 5000 && memoryAccessToken) {
        return memoryAccessToken;
      }
      setAccessToken(null);
      if (typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('fud:session-expired'));
      }
      return null;
    } finally {
      isRefreshing = false;
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

export async function authFetch(url: string, options: RequestInit = {}): Promise<Response> {
  const headers = new Headers(options.headers || {});

  const token = getAccessToken();
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let res = await fetch(url, { ...options, headers, credentials: 'include' });

  // Nếu gặp 401: Tự động gọi refresh token và retry 1 lần duy nhất (chỉ khi trước đó đã có token)
  if (res.status === 401 && token) {
    const newToken = await refreshAccessToken();
    if (newToken && newToken !== token) {
      headers.set('Authorization', `Bearer ${newToken}`);
      res = await fetch(url, { ...options, headers, credentials: 'include' });
    }
  }

  return res;
}
