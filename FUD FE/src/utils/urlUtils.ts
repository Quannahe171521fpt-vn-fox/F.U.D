export function toSafeHref(raw?: string | null, allowBlob = false): string | null {
  if (!raw) return null;
  const trimmed = raw.trim();
  if (!trimmed) return null;
  try {
    const base = typeof window !== 'undefined' ? window.location.origin : 'https://localhost';
    const parsed = new URL(trimmed, base);
    if (parsed.protocol === 'https:' || parsed.protocol === 'http:') return parsed.href;
    if (allowBlob && parsed.protocol === 'blob:') return parsed.href;
    return null;
  } catch {
    return null;
  }
}
