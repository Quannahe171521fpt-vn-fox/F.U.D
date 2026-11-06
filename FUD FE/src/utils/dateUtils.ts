/**
 * Tiện ích xử lý hiển thị ngày giờ chuẩn cho toàn bộ ứng dụng FUD
 * Đảm bảo đồng bộ múi giờ Việt Nam (UTC+7 / Local) kể cả khi chuỗi trả về từ C# backend chưa có hậu tố 'Z'.
 */

export function parseDate(dateStr?: string | Date | null): Date | null {
  if (!dateStr) return null;
  if (dateStr instanceof Date) return isNaN(dateStr.getTime()) ? null : dateStr;

  let s = dateStr.trim();
  // Nếu là ISO không có Z hay offset múi giờ (+/-), coi nó là UTC do backend DateTime.UtcNow lưu trữ
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(s) && !s.endsWith('Z') && !/[+-]\d{2}:\d{2}$/.test(s)) {
    s += 'Z';
  }
  const d = new Date(s);
  return isNaN(d.getTime()) ? null : d;
}

export function formatDateTime(dateStr?: string | Date | null, fallback = '—'): string {
  const d = parseDate(dateStr);
  if (!d) return fallback;
  return d.toLocaleString('vi-VN');
}

export function formatDate(dateStr?: string | Date | null, fallback = '—'): string {
  const d = parseDate(dateStr);
  if (!d) return fallback;
  return d.toLocaleDateString('vi-VN');
}

