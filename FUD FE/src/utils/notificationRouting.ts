export type UserPrimaryRole = 'Admin' | 'Manager' | 'Guard' | 'Student';

export type AppView =
  | 'home'
  | 'announcements'
  | 'management'
  | 'admin'
  | 'guard'
  | 'student'
  | 'notifications';

export type GuardTab =
  | 'checkin'
  | 'checkin-list'
  | 'bed-transfer'
  | 'bed-transfer-list'
  | 'create'
  | 'list'
  | 'ew'
  | 'overview';

export type ManagerTab =
  | 'equipment'
  | 'news'
  | 'pricing'
  | 'terms'
  | 'violations'
  | 'dorm'
  | 'blocks'
  | 'rooms'
  | 'beds'
  | 'requests'
  | 'ew'
  | 'bookings'
  | 'keeping'
  | 'impersonate';

export type StudentTab =
  | 'profile'
  | 'keeping'
  | 'booking'
  | 'residency-history'
  | 'checkin-records'
  | 'student-ew'
  | 'payment'
  | 'announcements'
  | 'request-list'
  | 'submit-request'
  | 'notices'
  | 'history';

export type NotificationTarget =
  | { role: 'Guard'; tabId: GuardTab; actionLabel: string }
  | { role: 'Manager'; tabId: ManagerTab; actionLabel: string }
  | { role: 'Student'; tabId: StudentTab; actionLabel: string };

const GUARD_TABS: readonly string[] = [
  'checkin',
  'checkin-list',
  'bed-transfer',
  'bed-transfer-list',
  'create',
  'list',
  'ew',
  'overview',
];

const MANAGER_TABS: readonly string[] = [
  'equipment',
  'news',
  'pricing',
  'terms',
  'violations',
  'dorm',
  'blocks',
  'rooms',
  'beds',
  'requests',
  'ew',
  'bookings',
  'keeping',
  'impersonate',
];

const STUDENT_TABS: readonly string[] = [
  'profile',
  'keeping',
  'booking',
  'residency-history',
  'checkin-records',
  'student-ew',
  'payment',
  'announcements',
  'request-list',
  'submit-request',
  'notices',
  'history',
];

export function isGuardTab(value: string): value is GuardTab {
  return GUARD_TABS.includes(value);
}

export function isManagerTab(value: string): value is ManagerTab {
  return MANAGER_TABS.includes(value);
}

export function isStudentTab(value: string): value is StudentTab {
  return STUDENT_TABS.includes(value);
}

export function resolveNotificationTarget(
  role: UserPrimaryRole,
  referenceType?: string | null
): NotificationTarget | null {
  if (!referenceType || role === 'Admin') {
    return null;
  }

  if (role === 'Student') {
    switch (referenceType) {
      case 'News':
        return { role, tabId: 'announcements', actionLabel: 'Xem Bảng tin KTX' };
      case 'Booking':
        return { role, tabId: 'booking', actionLabel: 'Xem Đặt phòng KTX' };
      case 'Keeping':
        return { role, tabId: 'keeping', actionLabel: 'Xem Giữ phòng KTX' };
      case 'Request':
        return { role, tabId: 'request-list', actionLabel: 'Xem Danh sách đơn từ' };
      case 'Violation':
        return { role, tabId: 'notices', actionLabel: 'Xem Thông báo vi phạm' };
      case 'Checkin':
        return { role, tabId: 'checkin-records', actionLabel: 'Xem Biên bản nhận phòng' };
      case 'Utility':
        return { role, tabId: 'student-ew', actionLabel: 'Xem Điện nước & Định mức' };
      case 'Invoice':
        return { role, tabId: 'payment', actionLabel: 'Mở Thanh toán & Hóa đơn' };
      case 'Account':
        return { role, tabId: 'profile', actionLabel: 'Xem Hồ sơ sinh viên' };
      default:
        return null;
    }
  }

  if (role === 'Manager') {
    switch (referenceType) {
      case 'Booking':
        return { role, tabId: 'bookings', actionLabel: 'Mở Duyệt đặt phòng' };
      case 'Keeping':
        return { role, tabId: 'keeping', actionLabel: 'Mở Duyệt giữ phòng' };
      case 'Request':
        return { role, tabId: 'requests', actionLabel: 'Mở Xử lý đơn yêu cầu' };
      case 'Violation':
        return { role, tabId: 'violations', actionLabel: 'Mở Quản lý vi phạm' };
      case 'Utility':
        return { role, tabId: 'ew', actionLabel: 'Mở Quản lý Điện & Nước' };
      case 'News':
        return { role, tabId: 'news', actionLabel: 'Mở Quản lý Tin tức' };
      default:
        return null;
    }
  }

  switch (referenceType) {
    case 'Request':
      return { role: 'Guard', tabId: 'bed-transfer', actionLabel: 'Mở Kiểm tra chuyển giường' };
    case 'Checkin':
      return { role: 'Guard', tabId: 'checkin', actionLabel: 'Mở Check-in nhận phòng' };
    case 'Violation':
      return { role: 'Guard', tabId: 'list', actionLabel: 'Mở Danh sách vi phạm' };
    case 'Utility':
      return { role: 'Guard', tabId: 'ew', actionLabel: 'Mở Nhập chỉ số điện – nước' };
    case 'Account':
      return { role: 'Guard', tabId: 'overview', actionLabel: 'Xem Thông tin cán bộ' };
    default:
      return null;
  }
}

export interface NotificationCategoryMeta {
  label: string;
  bg: string;
  color: string;
}

const CATEGORY_META: Record<string, NotificationCategoryMeta> = {
  News: { label: 'Tin tức KTX', bg: '#EFF6FF', color: '#1D4ED8' },
  Booking: { label: 'Đặt phòng', bg: '#FFF7ED', color: '#C2410C' },
  Keeping: { label: 'Giữ phòng', bg: '#F5F3FF', color: '#6D28D9' },
  Request: { label: 'Đơn từ', bg: '#F0FDF4', color: '#15803D' },
  Violation: { label: 'Vi phạm', bg: '#FEF2F2', color: '#B91C1C' },
  Checkin: { label: 'Nhận phòng', bg: '#ECFDF5', color: '#047857' },
  Utility: { label: 'Điện & Nước', bg: '#FEFCE8', color: '#A16207' },
  Invoice: { label: 'Hóa đơn', bg: '#FDF2F8', color: '#BE185D' },
  Account: { label: 'Tài khoản', bg: '#F1F5F9', color: '#334155' },
};

export function getNotificationCategoryMeta(referenceType?: string | null): NotificationCategoryMeta {
  if (referenceType && CATEGORY_META[referenceType]) {
    return CATEGORY_META[referenceType];
  }
  return { label: 'Hệ thống', bg: '#F1F5F9', color: '#475569' };
}

export interface NotificationCategoryFilterOption {
  value: string;
  label: string;
}

const ROLE_FILTER_CATEGORIES: Record<Exclude<UserPrimaryRole, 'Admin'>, readonly string[]> = {
  Student: ['Booking', 'Request', 'Violation', 'Checkin', 'Utility', 'Invoice', 'News', 'Account'],
  Manager: ['Booking', 'Request', 'Violation', 'Utility', 'News', 'Account'],
  Guard: ['Checkin', 'Violation', 'Request', 'Utility', 'Account'],
};

export function getNotificationFilterCategories(
  role?: UserPrimaryRole | string
): NotificationCategoryFilterOption[] {
  if (!role || role === 'Admin' || !(role in ROLE_FILTER_CATEGORIES)) {
    return [];
  }

  const keys = ROLE_FILTER_CATEGORIES[role as Exclude<UserPrimaryRole, 'Admin'>];
  return keys.map((key) => ({
    value: key,
    label: CATEGORY_META[key]?.label ?? key,
  }));
}
