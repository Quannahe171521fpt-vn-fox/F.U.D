export const TOKEN_KEY = 'fud_accessToken';
export const LAST_ACTIVITY_KEY = 'fud_lastActivityAt';
export const SESSION_META_KEY = 'fud_sessionMeta';

const AUTH_STORAGE_EVENT_KEY = 'fud_auth_sync_event';
const AUTH_CHANNEL_NAME = 'fud_auth';

export const DEFAULT_IDLE_TIMEOUT_MINUTES = 30;
export const ACTIVITY_THROTTLE_MS = 5000;
export const POLLING_IDLE_GRACE_MS = 5 * 60_000;

export type SessionExpirationReason = 'idle' | 'absolute' | 'server';

export interface SessionMeta {
  idleTimeoutMinutes: number;
  absoluteExpiresAt?: string | null;
}

export type CrossTabAuthMessage = {
  type: 'LOGOUT' | 'EXPIRED' | 'TOKEN_UPDATED';
  reason?: SessionExpirationReason;
  token?: string | null;
  at: number;
};

let lastRecordedMemoryMs = 0;
let broadcastCounter = 0;

function safeGetItem(key: string): string | null {
  try {
    return typeof localStorage !== 'undefined' ? localStorage.getItem(key) : null;
  } catch {
    return null;
  }
}

function safeSetItem(key: string, value: string): void {
  try {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(key, value);
    }
  } catch {
    // storage unavailable
  }
}

function safeRemoveItem(key: string): void {
  try {
    if (typeof localStorage !== 'undefined') {
      localStorage.removeItem(key);
    }
  } catch {
    // storage unavailable
  }
}

export function parseUtcTimestampMs(value?: string | null): number | null {
  if (!value) return null;
  let normalized = value.trim();
  if (!normalized) return null;

  if (
    /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(normalized) &&
    !normalized.endsWith('Z') &&
    !/[+-]\d{2}:\d{2}$/.test(normalized)
  ) {
    normalized += 'Z';
  }

  const ms = Date.parse(normalized);
  return Number.isFinite(ms) ? ms : null;
}

export function getSessionMeta(): SessionMeta | null {
  const raw = safeGetItem(SESSION_META_KEY);
  if (!raw) return null;

  try {
    const parsed = JSON.parse(raw) as Partial<SessionMeta>;
    const idleTimeoutMinutes =
      typeof parsed.idleTimeoutMinutes === 'number' && parsed.idleTimeoutMinutes > 0
        ? parsed.idleTimeoutMinutes
        : DEFAULT_IDLE_TIMEOUT_MINUTES;

    return {
      idleTimeoutMinutes,
      absoluteExpiresAt:
        typeof parsed.absoluteExpiresAt === 'string' ? parsed.absoluteExpiresAt : null,
    };
  } catch {
    return null;
  }
}

export function saveSessionMeta(meta: Partial<SessionMeta>): SessionMeta {
  const existing = getSessionMeta();
  const idleTimeoutMinutes =
    typeof meta.idleTimeoutMinutes === 'number' && meta.idleTimeoutMinutes > 0
      ? meta.idleTimeoutMinutes
      : existing?.idleTimeoutMinutes ?? DEFAULT_IDLE_TIMEOUT_MINUTES;
  const absoluteExpiresAt =
    meta.absoluteExpiresAt !== undefined
      ? meta.absoluteExpiresAt
      : existing?.absoluteExpiresAt ?? null;

  const next: SessionMeta = { idleTimeoutMinutes, absoluteExpiresAt };
  safeSetItem(SESSION_META_KEY, JSON.stringify(next));
  return next;
}

export function getSessionExpirationReason(
  nowMs: number = Date.now()
): Exclude<SessionExpirationReason, 'server'> | null {
  const meta = getSessionMeta();
  const absMs = parseUtcTimestampMs(meta?.absoluteExpiresAt);
  if (absMs !== null && nowMs >= absMs) {
    return 'absolute';
  }

  const rawLastActivity = safeGetItem(LAST_ACTIVITY_KEY);
  if (rawLastActivity !== null) {
    const lastActivityMs = Number(rawLastActivity);
    if (Number.isFinite(lastActivityMs) && lastActivityMs > 0) {
      const idleMinutes =
        meta?.idleTimeoutMinutes && meta.idleTimeoutMinutes > 0
          ? meta.idleTimeoutMinutes
          : DEFAULT_IDLE_TIMEOUT_MINUTES;
      if (nowMs - lastActivityMs >= idleMinutes * 60_000) {
        return 'idle';
      }
    }
  }

  return null;
}

export function recordUserActivity(force = false): {
  recorded: boolean;
  expiredReason: Exclude<SessionExpirationReason, 'server'> | null;
} {
  const nowMs = Date.now();
  if (!force) {
    // Check expiration BEFORE updating lastActivityAt so returning from AFK cannot overwrite timeout.
    const expired = getSessionExpirationReason(nowMs);
    if (expired) {
      return { recorded: false, expiredReason: expired };
    }

    const storedMs = Number(safeGetItem(LAST_ACTIVITY_KEY) || 0);
    const effectiveLastMs = Number.isFinite(storedMs)
      ? Math.max(lastRecordedMemoryMs, storedMs)
      : lastRecordedMemoryMs;

    if (nowMs - effectiveLastMs < ACTIVITY_THROTTLE_MS) {
      return { recorded: false, expiredReason: null };
    }
  }

  lastRecordedMemoryMs = nowMs;
  safeSetItem(LAST_ACTIVITY_KEY, String(nowMs));
  return { recorded: true, expiredReason: null };
}

export function isUserIdleForPolling(graceMs: number = POLLING_IDLE_GRACE_MS): boolean {
  if (typeof document !== 'undefined' && document.hidden) {
    return true;
  }

  if (getSessionExpirationReason() !== null) {
    return true;
  }

  const rawLastActivity = safeGetItem(LAST_ACTIVITY_KEY);
  const lastActivityMs = rawLastActivity ? Number(rawLastActivity) : NaN;
  if (!Number.isFinite(lastActivityMs) || lastActivityMs <= 0) {
    recordUserActivity(true);
    return false;
  }

  return Date.now() - lastActivityMs >= graceMs;
}

export function clearLegacyAuthKeys(): void {
  safeRemoveItem('fud_currentUser');
  safeRemoveItem('fud_userRole');
  safeRemoveItem('fud_currentView');
}

export function clearSessionStorage(): void {
  lastRecordedMemoryMs = 0;
  safeRemoveItem(TOKEN_KEY);
  clearLegacyAuthKeys();
  safeRemoveItem(LAST_ACTIVITY_KEY);
  safeRemoveItem(SESSION_META_KEY);
}

export function broadcastAuthEvent(msg: CrossTabAuthMessage): void {
  const envelope = {
    ...msg,
    _nonce: `${msg.at}-${++broadcastCounter}-${Math.random().toString(36).slice(2, 8)}`,
  };

  try {
    if (typeof BroadcastChannel !== 'undefined') {
      const channel = new BroadcastChannel(AUTH_CHANNEL_NAME);
      channel.postMessage(envelope);
      channel.close();
    }
  } catch {
    // BroadcastChannel unavailable
  }

  try {
    if (typeof localStorage !== 'undefined') {
      localStorage.setItem(AUTH_STORAGE_EVENT_KEY, JSON.stringify(envelope));
      localStorage.removeItem(AUTH_STORAGE_EVENT_KEY);
    }
  } catch {
    // storage unavailable
  }
}

export function subscribeCrossTabAuth(handler: (msg: CrossTabAuthMessage) => void): () => void {
  const seenNonces = new Set<string>();
  let suppressTokenNullFallbackUntil = 0;

  const deliver = (raw: unknown) => {
    if (!raw || typeof raw !== 'object') return;
    const candidate = raw as CrossTabAuthMessage & { _nonce?: string };
    if (!candidate.type || typeof candidate.at !== 'number') return;

    if (candidate._nonce) {
      if (seenNonces.has(candidate._nonce)) return;
      seenNonces.add(candidate._nonce);
      if (seenNonces.size > 50) {
        const oldest = seenNonces.values().next().value;
        if (oldest) seenNonces.delete(oldest);
      }
    }

    if (candidate.type === 'EXPIRED' || candidate.type === 'LOGOUT') {
      suppressTokenNullFallbackUntil = Date.now() + 1000;
    }

    handler({
      type: candidate.type,
      reason: candidate.reason,
      token: candidate.token,
      at: candidate.at,
    });
  };

  let channel: BroadcastChannel | null = null;
  const onChannelMessage = (event: MessageEvent) => {
    deliver(event.data);
  };

  try {
    if (typeof BroadcastChannel !== 'undefined') {
      channel = new BroadcastChannel(AUTH_CHANNEL_NAME);
      channel.addEventListener('message', onChannelMessage);
    }
  } catch {
    channel = null;
  }

  const onStorage = (event: StorageEvent) => {
    if (event.key === AUTH_STORAGE_EVENT_KEY && event.newValue) {
      try {
        deliver(JSON.parse(event.newValue));
      } catch {
        // malformed payload
      }
      return;
    }

    if (
      (event.key === TOKEN_KEY && event.newValue === null) ||
      event.key === null
    ) {
      if (Date.now() < suppressTokenNullFallbackUntil) return;
      deliver({ type: 'LOGOUT', at: Date.now() });
    }
  };

  if (typeof window !== 'undefined') {
    window.addEventListener('storage', onStorage);
  }

  return () => {
    if (channel) {
      channel.removeEventListener('message', onChannelMessage);
      channel.close();
    }
    if (typeof window !== 'undefined') {
      window.removeEventListener('storage', onStorage);
    }
  };
}
