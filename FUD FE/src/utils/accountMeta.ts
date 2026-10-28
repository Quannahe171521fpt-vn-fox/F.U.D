import type { AccountStatus, SystemRole } from '../types/account';

/** Nhãn tiếng Việt cho trạng thái tài khoản. */
export const STATUS_LABELS: Record<AccountStatus, string> = {
  Pending: 'Chờ kích hoạt',
  Active: 'Đang hoạt động',
  Suspended: 'Đã tạm ngưng',
};

/** Nhãn tiếng Việt cho vai trò hệ thống. */
export const ROLE_LABELS: Record<SystemRole, string> = {
  Student: 'Sinh viên',
  Manager: 'Quản lý',
  Guard: 'Bảo vệ',
  Admin: 'Quản trị',
};

/** Nhãn cho lý do tạm ngưng. */
export const SUSPENSION_REASON_LABELS: Record<string, string> = {
  Violation: 'Vi phạm chính sách',
  Graduation: 'Đã tốt nghiệp',
  Termination: 'Nghỉ việc / kết thúc hợp tác',
  Other: 'Khác',
};

/** Nhãn cho hành động đổi vai trò. */
export const ROLE_ACTION_LABELS: Record<string, string> = {
  Grant: 'Cấp quyền',
  Adjust: 'Điều chỉnh',
  Revoke: 'Thu hồi',
};

/** Nhãn cho trạng thái gửi thông báo. */
export const NOTIFICATION_STATUS_LABELS: Record<string, string> = {
  None: 'Chưa gửi',
  Sent: 'Đã gửi',
  Failed: 'Gửi thất bại',
};

/** Class Tailwind cho badge trạng thái tài khoản. */
export function statusBadgeClass(status: AccountStatus): string {
  switch (status) {
    case 'Active':
      return 'bg-emerald-100 text-emerald-700 ring-emerald-600/20';
    case 'Pending':
      return 'bg-amber-100 text-amber-700 ring-amber-600/20';
    case 'Suspended':
      return 'bg-rose-100 text-rose-700 ring-rose-600/20';
    default:
      return 'bg-slate-100 text-slate-700 ring-slate-600/20';
  }
}

/** Class Tailwind cho badge vai trò. */
export function roleBadgeClass(role: SystemRole): string {
  switch (role) {
    case 'Admin':
      return 'bg-violet-100 text-violet-700 ring-violet-600/20';
    case 'Manager':
      return 'bg-sky-100 text-sky-700 ring-sky-600/20';
    case 'Guard':
      return 'bg-orange-100 text-orange-700 ring-orange-600/20';
    case 'Student':
      return 'bg-teal-100 text-teal-700 ring-teal-600/20';
    default:
      return 'bg-slate-100 text-slate-700 ring-slate-600/20';
  }
}

/** Định dạng ngày giờ sang dd/MM/yyyy HH:mm. */
export function formatDateTime(value?: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
