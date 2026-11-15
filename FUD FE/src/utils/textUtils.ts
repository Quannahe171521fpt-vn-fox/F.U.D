/**
 * Chuyển chuỗi tiếng Việt có dấu thành không dấu, viết thường
 */
export function removeVietnameseTones(str: string): string {
  if (!str) return '';
  let res = str.toLowerCase();
  res = res.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  res = res.replace(/[đĐ]/g, 'd');
  return res.trim();
}
