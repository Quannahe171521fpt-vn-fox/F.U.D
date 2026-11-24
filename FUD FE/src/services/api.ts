import type {
  TermManagementItem,
  TermDetailItem,
  CreateTermPayload,
  UpdateTermPayload,
  SaveSchedulePayload,
} from '../types/management';
import { authFetch } from './apiClient';
import { env } from '../config/env';
const BASE_URL = env.apiBaseUrl;

async function parseJsonSafe(res: Response): Promise<any> {
  const text = await res.text();
  if (!text || !text.trim()) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch (err) {
    throw new Error(`Dữ liệu máy chủ không đúng định dạng (HTTP ${res.status})`);
  }
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors?: string[];
}

async function safeParseResponse<T = any>(res: Response, defaultError = 'Lỗi yêu cầu máy chủ'): Promise<ApiResponse<T>> {
  if (res.status === 401) {
    throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
  }

  let text = '';
  try {
    text = await res.text();
  } catch {
    throw new Error(`${defaultError} (Mã lỗi: ${res.status})`);
  }

  let parsed: any = null;
  if (text && text.trim()) {
    try {
      parsed = JSON.parse(text);
    } catch {
      // ignore
    }
  }

  if (res.status === 403) {
    const serverMsg = parsed?.message || (parsed?.errors && parsed.errors.join(', '));
    throw new Error(serverMsg || 'Bạn không có quyền truy cập chức năng này (Yêu cầu tài khoản Ban Quản lý hoặc Admin).');
  }

  if (!text || !text.trim()) {
    if (!res.ok) {
      throw new Error(`${defaultError} (Mã lỗi: ${res.status})`);
    }
    return { success: true, message: '', data: null as any };
  }

  if (parsed !== null) {
    return parsed;
  }
  throw new Error(`${defaultError} (Mã phản hồi: ${res.status})`);
}

// ── Equipment API (UC28) ──────────────────────────────────
export const equipmentApi = {
  getAll: async (params?: { search?: string; condition?: string; page?: number; pageSize?: number }) => {
    const query = new URLSearchParams();
    if (params?.search) query.append('search', params.search);
    if (params?.condition && params.condition !== 'All') query.append('condition', params.condition);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await authFetch(`${BASE_URL}/facilities/equipment?${query.toString()}`);
    if (!res.ok) throw new Error('Không thể tải danh sách thiết bị');
    const json: ApiResponse<PagedResult<any>> = await parseJsonSafe(res);
    return json.data;
  },

  getById: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/${id}`);
    const json = await parseJsonSafe(res);
    return json.data;
  },

  create: async (payload: {
    assetCode: string;
    catalogId: number;
    roomId: number;
    bedId?: number | null;
    condition: string;
    purchaseDate?: string;
    note?: string;
  }) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi tạo thiết bị');
    }
    return json.data;
  },

  update: async (id: number, payload: {
    catalogId: number;
    roomId: number;
    bedId?: number | null;
    condition: string;
    purchaseDate?: string;
    note?: string;
  }) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi cập nhật thiết bị');
    }
    return json.data;
  },

  move: async (id: number, payload: { roomId: number; bedId?: number | null }) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/${id}/move`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi di chuyển thiết bị');
    }
    return json.data;
  },

  changeCondition: async (id: number, condition: string) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/${id}/condition`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ condition }),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi đổi tình trạng');
    }
    return json.data;
  },

  liquidate: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/${id}/liquidate`, {
      method: 'POST',
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi thanh lý thiết bị');
    }
    return json;
  },

  getCatalogs: async () => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/catalogs`);
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  createCatalog: async (payload: { category: string; name: string }) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/catalogs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi thêm danh mục');
    }
    return json.data;
  },

  deleteCatalog: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/catalogs/${id}`, {
      method: 'DELETE',
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi xóa danh mục');
    }
    return json;
  },

  getLocations: async () => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/locations`);
    const json = await parseJsonSafe(res);
    return json.data || [];
  },
};

// ── News API (UC29) ──────────────────────────────────────
export const newsApi = {
  getAll: async (params?: { search?: string; status?: string; category?: string; page?: number; pageSize?: number }) => {
    const query = new URLSearchParams();
    if (params?.search) query.append('search', params.search);
    if (params?.status && params.status !== 'All') query.append('status', params.status);
    if (params?.category && params.category !== 'All') query.append('category', params.category);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await authFetch(`${BASE_URL}/facilities/news?${query.toString()}`);
    if (!res.ok) throw new Error('Không thể tải danh sách tin tức');
    const json: ApiResponse<PagedResult<any>> = await parseJsonSafe(res);
    return json.data;
  },

  getPublished: async () => {
    const res = await authFetch(`${BASE_URL}/facilities/news/published`);
    if (!res.ok) throw new Error('Không thể tải tin đã xuất bản');
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  getById: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/news/${id}`);
    const json = await parseJsonSafe(res);
    return json.data;
  },

  create: async (payload: {
    title: string;
    content: string;
    category?: string;
    action?: string; // 'Draft' | 'Publish'
    attachmentUrls?: string[];
  }) => {
    const res = await authFetch(`${BASE_URL}/facilities/news`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi tạo tin tức');
    }
    return json.data;
  },

  update: async (id: number, payload: {
    title: string;
    content: string;
    category?: string;
    action?: string;
    attachmentUrls?: string[];
  }) => {
    const res = await authFetch(`${BASE_URL}/facilities/news/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi cập nhật tin tức');
    }
    return json.data;
  },

  delete: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/news/${id}`, {
      method: 'DELETE',
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi xóa bản tin');
    }
    return json;
  },

  publish: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/news/${id}/publish`, {
      method: 'POST',
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi xuất bản tin tức');
    }
    return json;
  },

  unpublish: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/news/${id}/unpublish`, {
      method: 'POST',
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi gỡ xuất bản');
    }
    return json;
  },
};

// ── Dorm Info API (Database) ──────────────────────────────
export const dormInfoApi = {
  getBuildings: async () => {
    const res = await authFetch(`${BASE_URL}/facilities/dorm-info/buildings`);
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  getRoomTypes: async () => {
    const res = await authFetch(`${BASE_URL}/facilities/dorm-info/room-types`);
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  getFaqs: async () => {
    const res = await authFetch(`${BASE_URL}/facilities/dorm-info/faqs`);
    const json = await parseJsonSafe(res);
    return json.data || [];
  },
};



// Pricing API (UC12: Set Pricing) 

// ── Pricing API (UC12: Set Pricing) ───────────────────────
export const pricingApi = {
  getTerms: async () => {
    const res = await authFetch(`${BASE_URL}/pricing/terms`, {
      credentials: 'include',
    });
    const json = await safeParseResponse<any[]>(res, 'Không thể tải danh sách học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Không thể tải danh sách học kỳ');
    }
    return json.data || [];
  },

  getPricingByTerm: async (termId: number) => {
    const res = await authFetch(`${BASE_URL}/pricing/terms/${termId}`, {
      credentials: 'include',
    });
    const json = await safeParseResponse<any>(res, 'Không thể tải bảng giá học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Không thể tải bảng giá học kỳ');
    }
    return json.data;
  },

  savePricing: async (payload: {
    termId: number;
    items: { dormId: number; roomTypeId: number; currentPrice: number }[];
    confirmApplyToNewBookingsOnly?: boolean;
  }) => {
    const res = await authFetch(`${BASE_URL}/pricing/save`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(payload),
    });
    if (res.status === 409) {
      const json = await safeParseResponse<any>(res, 'Học kỳ đã bắt đầu');
      const err: any = new Error(json.message || 'Registration already started');
      err.status = 409;
      throw err;
    }
    const json = await safeParseResponse<any>(res, 'Lỗi khi lưu bảng giá');
    if (!res.ok || !json.success) {
      throw new Error(json.message || (json.errors && json.errors.join(', ')) || 'Lỗi khi lưu bảng giá');
    }
    return json.data;
  },

  saveUtilityPrices: async (payload: { termId: number; items: { meterType: string; unitPrice: number }[] }) => {
    const res = await authFetch(`${BASE_URL}/pricing/save-utility`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<any>(res, 'Lỗi khi lưu giá điện/nước');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi lưu giá điện/nước');
    return json.data;
  },
};



// ── Violation API (UC22: Record Violation) ──────────────────
export interface ViolationType {
  id: number;
  name: string;
}

export interface RoomStudent {
  studentId: number;
  studentCode: string;
  fullName: string;
  bedCode: string;
  statusDescription: string;
  residencyId?: number;
}

export interface ViolationEvidence {
  id: number;
  fileUrl: string;
  source: string;
}

export interface ViolationItem {
  id: number;
  code: string;
  studentId: number;
  studentCode: string;
  studentName: string;
  roomId: number;
  roomNumber: string;
  blockName: string;
  violationTypeId: number;
  violationTypeName: string;
  occurrenceTime: string;
  description: string;
  initialLevel?: string;
  finalLevel?: string;
  managerNote?: string;
  requestDeadline?: string;
  status: string;
  recordedByName?: string;
  evaluatedByName?: string;
  finalizedTime?: string;
  termName?: string;
  evidences: ViolationEvidence[];
  priorViolationsCount?: number;
  studentAccountStatus?: string;
  suspensionReason?: string;
  linkedRequestId?: number;
}

// ── Violation API (UC19, UC20, UC21, UC22, UC23) ───────────────
export interface GuardBlockItem {
  id: number;
  name: string;
  code: string;
  dormId: number;
  dormName: string;
}

export const violationApi = {
  getTypes: async (): Promise<ViolationType[]> => {
    const res = await authFetch(`${BASE_URL}/violations/types`);
    if (!res.ok) throw new Error('Không thể tải danh mục loại vi phạm');
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  getStudentsByRoom: async (roomId: number): Promise<RoomStudent[]> => {
    const res = await authFetch(`${BASE_URL}/violations/room-students/${roomId}`);
    if (!res.ok) throw new Error('Không thể tải danh sách sinh viên trong phòng');
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  searchStudent: async (studentCode: string, roomId?: number): Promise<RoomStudent> => {
    const query = new URLSearchParams({ studentCode });
    if (roomId) query.append('roomId', roomId.toString());
    const res = await authFetch(`${BASE_URL}/violations/search-student?${query.toString()}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Không tìm thấy sinh viên');
    }
    return json.data;
  },

  recordViolation: async (formData: FormData): Promise<ViolationItem[]> => {
    const res = await authFetch(`${BASE_URL}/violations/record`, {
      method: 'POST',
      body: formData,
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || !json.success) {
      throw new Error(json.message || (json.errors && json.errors.join(', ')) || 'Không thể ghi nhận vi phạm');
    }
    return json.data;
  },

  getViolations: async (params?: { roomId?: number; studentId?: number; status?: string }): Promise<ViolationItem[]> => {
    const query = new URLSearchParams();
    if (params?.roomId) query.append('roomId', params.roomId.toString());
    if (params?.studentId) query.append('studentId', params.studentId.toString());
    if (params?.status) query.append('status', params.status);
    const res = await authFetch(`${BASE_URL}/violations?${query.toString()}`);
    if (!res.ok) throw new Error('Không thể tải danh sách vi phạm');
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  getById: async (id: number): Promise<ViolationItem> => {
    const res = await authFetch(`${BASE_URL}/violations/${id}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || `Không tìm thấy biên bản vi phạm (HTTP ${res.status})`);
    }
    return json?.data;
  },

  // UC19: Thông báo vi phạm của sinh viên
  getStudentNotices: async (): Promise<ViolationItem[]> => {
    const res = await authFetch(`${BASE_URL}/violations/student/notices`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải thông báo vi phạm');
    }
    return json?.data || [];
  },

  // UC21: Lịch sử vi phạm của sinh viên
  getStudentHistory: async (params?: { term?: string; status?: string }): Promise<ViolationItem[]> => {
    const query = new URLSearchParams();
    if (params?.term) query.append('term', params.term);
    if (params?.status) query.append('status', params.status);
    const res = await authFetch(`${BASE_URL}/violations/student/history?${query.toString()}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải lịch sử vi phạm');
    }
    return json?.data || [];
  },

  // UC23: Ban Quản lý định mức vi phạm ban đầu
  evaluateViolation: async (violationId: number, payload: { initialLevel: string; managerNote?: string }): Promise<ViolationItem> => {
    const res = await authFetch(`${BASE_URL}/violations/${violationId}/evaluate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || `Lỗi khi định mức vi phạm (HTTP ${res.status})`);
    }
    return json?.data;
  },

  // Mở khóa đình chỉ book phòng & giữ phòng cho sinh viên
  unsuspendStudent: async (studentId: number, note?: string): Promise<void> => {
    const res = await authFetch(`${BASE_URL}/violations/unsuspend-student/${studentId}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ note }),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || `Không thể mở khóa tài khoản (HTTP ${res.status})`);
    }
  },

  // Guard dorm-scoped blocks
  getGuardBlocks: async (): Promise<GuardBlockItem[]> => {
    const res = await authFetch(`${BASE_URL}/violations/guard-blocks`);
    if (!res.ok) throw new Error('Không thể tải danh sách Block');
    const json = await parseJsonSafe(res);
    return json.data || [];
  },
};

// ── Term Management & Registration Schedule API ───────────
export const termManagementApi = {
  getAll: async (): Promise<TermManagementItem[]> => {
    const res = await authFetch(`${BASE_URL}/manage/terms`);
    const json = await safeParseResponse<TermManagementItem[]>(res, 'Không thể tải danh sách học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Không thể tải danh sách học kỳ');
    }
    return json.data || [];
  },

  getById: async (id: number): Promise<TermDetailItem> => {
    const res = await authFetch(`${BASE_URL}/manage/terms/${id}`);
    const json = await safeParseResponse<TermDetailItem>(res, 'Không thể tải chi tiết học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Không thể tải chi tiết học kỳ');
    }
    return json.data;
  },

  create: async (payload: CreateTermPayload): Promise<TermDetailItem> => {
    const res = await authFetch(`${BASE_URL}/manage/terms`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<TermDetailItem>(res, 'Lỗi khi tạo học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || (json.errors && json.errors.join(', ')) || 'Lỗi khi tạo học kỳ');
    }
    return json.data;
  },

  update: async (id: number, payload: UpdateTermPayload): Promise<TermDetailItem> => {
    const res = await authFetch(`${BASE_URL}/manage/terms/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<TermDetailItem>(res, 'Lỗi khi cập nhật học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || (json.errors && json.errors.join(', ')) || 'Lỗi khi cập nhật học kỳ');
    }
    return json.data;
  },

  delete: async (id: number): Promise<void> => {
    const res = await authFetch(`${BASE_URL}/manage/terms/${id}`, {
      method: 'DELETE',
    });
    const json = await safeParseResponse<void>(res, 'Không thể xóa học kỳ');
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Không thể xóa học kỳ');
    }
  },

  saveSchedule: async (termId: number, payload: SaveSchedulePayload): Promise<TermDetailItem> => {
    const res = await authFetch(`${BASE_URL}/manage/terms/${termId}/schedule`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<TermDetailItem>(res, 'Lỗi khi lưu lịch trình đăng ký');
    if (!res.ok || !json.success) {
      throw new Error(json.message || (json.errors && json.errors.join(', ')) || 'Lỗi khi lưu lịch trình đăng ký');
    }
    return json.data;
  },

  togglePeriod: async (termId: number, periodId: number, active: boolean): Promise<void> => {
    const res = await authFetch(`${BASE_URL}/manage/terms/${termId}/periods/${periodId}/toggle?active=${active}`, {
      method: 'PATCH',
    });
    const json = await safeParseResponse<void>(res, 'Lỗi khi bật/tắt đợt đăng ký');
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi bật/tắt đợt đăng ký');
    }
  },
};

// ── Blocks & Rooms API ──────────────────────────────────────
export interface BlockItem {
  id: number;
  name: string;
  code?: string;
  genderRestriction?: string;
}

export interface RoomItem {
  id: number;
  blockId: number;
  roomNumber: string;
  floor: number;
  status: string;
}

export const facilityCommonApi = {
  getBlocks: async (): Promise<BlockItem[]> => {
    const res = await authFetch(`${BASE_URL}/blocks?pageSize=100`);
    if (!res.ok) throw new Error('Không thể tải danh sách Block');
    const json = await parseJsonSafe(res);
    return json.data?.items || json.data || [];
  },

  getRoomsByBlock: async (blockId: number): Promise<RoomItem[]> => {
    const res = await authFetch(`${BASE_URL}/blocks/${blockId}/rooms`);
    if (!res.ok) throw new Error('Không thể tải danh sách phòng');
    const json = await parseJsonSafe(res);
    return json.data || [];
  },

  getAllRooms: async (): Promise<RoomItem[]> => {
    const res = await authFetch(`${BASE_URL}/rooms?pageSize=200`);
    if (!res.ok) throw new Error('Không thể tải danh sách phòng');
    const json = await parseJsonSafe(res);
    return json.data?.items || json.data || [];
  },
};

// ── Dorm Management API ────────────────────────────────────
export interface DormItem {
  id: number;
  campusId: number;
  campusName: string;
  code: string;
  name: string;
  address?: string;
  status: string;
  totalBlocks: number;
}

export const dormApi = {
  getAll: async (params?: { campusName?: string; dormName?: string; status?: string; page?: number; pageSize?: number }): Promise<PagedResult<DormItem>> => {
    const query = new URLSearchParams();
    if (params?.campusName) query.append('campusName', params.campusName);
    if (params?.dormName) query.append('dormName', params.dormName);
    if (params?.status && params.status !== 'All') query.append('status', params.status);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await authFetch(`${BASE_URL}/dorms?${query.toString()}`);
    const json = await safeParseResponse<PagedResult<DormItem>>(res, 'Không thể tải danh sách KTX');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách KTX');
    return json.data;
  },

  getById: async (id: number): Promise<DormItem> => {
    const res = await authFetch(`${BASE_URL}/dorms/${id}`);
    const json = await safeParseResponse<DormItem>(res, 'Không thể tải thông tin KTX');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải thông tin KTX');
    return json.data;
  },

  create: async (payload: { campusId: number; code: string; name: string; address?: string }): Promise<DormItem> => {
    const res = await authFetch(`${BASE_URL}/dorms`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<DormItem>(res, 'Lỗi khi tạo KTX');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi tạo KTX');
    return json.data;
  },

  update: async (id: number, payload: { name: string; address?: string }): Promise<DormItem> => {
    const res = await authFetch(`${BASE_URL}/dorms/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<DormItem>(res, 'Lỗi khi cập nhật KTX');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi cập nhật KTX');
    return json.data;
  },

  updateStatus: async (id: number, status: 'Active' | 'Inactive'): Promise<DormItem> => {
    const res = await authFetch(`${BASE_URL}/dorms/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    });
    const json = await safeParseResponse<DormItem>(res, 'Lỗi khi đổi trạng thái KTX');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi trạng thái KTX');
    return json.data;
  },

  softDelete: async (id: number): Promise<void> => {
    const res = await authFetch(`${BASE_URL}/dorms/${id}`, { method: 'DELETE' });
    const json = await safeParseResponse<void>(res, 'Lỗi khi xóa KTX');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi xóa KTX');
  },
};

// ── Block Management API ────────────────────────────────────
export interface BlockDto {
  id: number;
  dormId: number;
  dormName: string;
  code: string;
  name: string;
  numberOfFloors?: number;
  genderType: string;
  status: string;
  totalRooms: number;
}

export interface BlockQuery {
  dormName?: string;
  blockName?: string;
  genderType?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateBlockPayload {
  dormId: number;
  code: string;
  name: string;
  numberOfFloors?: number;
  genderType: string;
}

export interface UpdateBlockPayload {
  name: string;
  numberOfFloors?: number;
}

export const blockApi = {
  getAll: async (params?: BlockQuery): Promise<PagedResult<BlockDto>> => {
    const query = new URLSearchParams();
    if (params?.dormName) query.append('dormName', params.dormName);
    if (params?.blockName) query.append('blockName', params.blockName);
    if (params?.genderType && params.genderType !== 'All') query.append('genderType', params.genderType);
    if (params?.status && params.status !== 'All') query.append('status', params.status);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await authFetch(`${BASE_URL}/blocks?${query.toString()}`);
    const json = await safeParseResponse<PagedResult<BlockDto>>(res, 'Không thể tải danh sách Block');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách Block');
    return json.data;
  },

  getById: async (id: number): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}`);
    const json = await safeParseResponse<BlockDto>(res, 'Không thể tải thông tin Block ');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải thông tin Block ');
    return json.data;
  },

  getRooms: async (id: number): Promise<RoomDto[]> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}/rooms`);
    const json = await safeParseResponse<RoomDto[]>(res, 'Không thể tải danh sách phòng của Block');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách phòng của Block');
    return json.data;
  },

  create: async (payload: CreateBlockPayload): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi tạo Block');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi tạo Block');
    return json.data;
  },

  update: async (id: number, payload: UpdateBlockPayload): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi cập nhật Block');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi cập nhật Block');
    return json.data;
  },

  updateStatus: async (id: number, status: 'Active' | 'Maintenance'): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi đổi trạng thái Block');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi trạng thái Block');
    return json.data;
  },

  updateGender: async (id: number, genderType: 'Male' | 'Female'): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}/gender`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ genderType }),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi đổi giới tính Block');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi giới tính Block');
    return json.data;
  },
};

// ── Room Management API ────────────────────────────────────
export interface RoomDto {
  id: number;
  blockId: number;
  blockName: string;
  blockGenderType: string;
  roomTypeId: number;
  roomTypeName: string;
  bedCapacity: number;
  roomNumber: string;
  floorLevel?: number;
  status: string;
  totalBeds: number;
  freeBeds: number;
  occupiedBeds: number;      // số giường có sinh viên ở thực tế
  isEditable: boolean;       // = occupiedBeds === 0 (do backend tính)
}

export interface RoomTypeLookup {
  id: number;
  name: string;
  bedCapacity: number;
}

export interface RoomQuery {
  blockName?: string;
  roomNumber?: string;
  roomTypeId?: number;
  genderType?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateRoomPayload {
  blockId: number;
  roomTypeId: number;
  roomNumber: string;
  floorLevel?: number;
}

export interface UpdateRoomPayload {
  roomTypeId: number;
  roomNumber: string;
  floorLevel?: number;
}

export const roomApi = {
  getAll: async (params?: RoomQuery): Promise<PagedResult<RoomDto>> => {
    const query = new URLSearchParams();
    if (params?.blockName) query.append('blockName', params.blockName);
    if (params?.roomNumber) query.append('roomNumber', params.roomNumber);
    if (params?.roomTypeId) query.append('roomTypeId', params.roomTypeId.toString());
    if (params?.genderType && params.genderType !== 'All') query.append('genderType', params.genderType);
    if (params?.status && params.status !== 'All') query.append('status', params.status);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await authFetch(`${BASE_URL}/rooms?${query.toString()}`);
    const json = await safeParseResponse<PagedResult<RoomDto>>(res, 'Không thể tải danh sách phòng');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách phòng');
    return json.data;
  },

  getById: async (id: number): Promise<RoomDto> => {
    const res = await authFetch(`${BASE_URL}/rooms/${id}`);
    const json = await safeParseResponse<RoomDto>(res, 'Không thể tải thông tin phòng');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải thông tin phòng');
    return json.data;
  },

  getTypes: async (): Promise<RoomTypeLookup[]> => {
    const res = await authFetch(`${BASE_URL}/rooms/types`);
    const json = await safeParseResponse<RoomTypeLookup[]>(res, 'Không thể tải danh sách loại phòng');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách loại phòng');
    return json.data;
  },

  create: async (payload: CreateRoomPayload): Promise<RoomDto> => {
    const res = await authFetch(`${BASE_URL}/rooms`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<RoomDto>(res, 'Lỗi khi tạo phòng');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi tạo phòng');
    return json.data;
  },

  update: async (id: number, payload: UpdateRoomPayload): Promise<RoomDto> => {
    const res = await authFetch(`${BASE_URL}/rooms/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<RoomDto>(res, 'Lỗi khi cập nhật phòng');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi cập nhật phòng');
    return json.data;
  },

  updateStatus: async (id: number, status: 'Active' | 'Maintenance'): Promise<RoomDto> => {
    const res = await authFetch(`${BASE_URL}/rooms/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    });
    const json = await safeParseResponse<RoomDto>(res, 'Lỗi khi đổi trạng thái phòng');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi trạng thái phòng');
    return json.data;
  },
};

// ── Bed Management API ─────────────────────────────────────
export interface BedDto {
  id: number;
  roomId: number;
  roomNumber: string;
  blockName: string;
  bedCode: string;
  positionInRoom?: string;
  status: string; // 'Free' | 'Holding' | 'Pending Transfer' | 'Occupied' | 'Maintenance'
}

export interface BedQuery {
  roomId?: number;
  bedCode?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateBedPayload {
  roomId: number;
  bedCode: string;
  positionInRoom?: string;
}

export interface UpdateBedPayload {
  bedCode: string;
  positionInRoom?: string;
}

export const bedApi = {
  getAll: async (params?: BedQuery): Promise<PagedResult<BedDto>> => {
    const query = new URLSearchParams();
    if (params?.roomId) query.append('roomId', params.roomId.toString());
    if (params?.bedCode) query.append('bedCode', params.bedCode);
    if (params?.status && params.status !== 'All') query.append('status', params.status);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await authFetch(`${BASE_URL}/beds?${query.toString()}`);
    const json = await safeParseResponse<PagedResult<BedDto>>(res, 'Không thể tải danh sách giường');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách giường');
    return json.data;
  },

  getById: async (id: number): Promise<BedDto> => {
    const res = await authFetch(`${BASE_URL}/beds/${id}`);
    const json = await safeParseResponse<BedDto>(res, 'Không thể tải thông tin giường');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải thông tin giường');
    return json.data;
  },

  create: async (payload: CreateBedPayload): Promise<BedDto> => {
    const res = await authFetch(`${BASE_URL}/beds`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BedDto>(res, 'Lỗi khi tạo giường');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi tạo giường');
    return json.data;
  },

  update: async (id: number, payload: UpdateBedPayload): Promise<BedDto> => {
    const res = await authFetch(`${BASE_URL}/beds/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BedDto>(res, 'Lỗi khi cập nhật giường');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi cập nhật giường');
    return json.data;
  },

  updateStatus: async (id: number, status: 'Free' | 'Maintenance'): Promise<BedDto> => {
    const res = await authFetch(`${BASE_URL}/beds/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    });
    const json = await safeParseResponse<BedDto>(res, 'Lỗi khi đổi trạng thái giường');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi trạng thái giường');
    return json.data;
  },
};


// ── Requests API (Booking, BedTransfer, Checkout, Complaint, Others) ───────────
export interface BedTransferHandoverItemDto {
  id: number;
  equipmentId: number;
  equipmentCode: string;
  equipmentName: string;
  category: string;
  itemCondition: 'Good' | 'Damaged' | 'Missing' | string;
  description?: string;
  compensationAmount: number;
}

export interface BedTransferHandoverInfoDto {
  id: number;
  handoverCode: string;
  status: string;
  inspectorName: string;
  inspectionNote?: string;
  totalCompensation: number;
  studentAcceptedFlag: boolean;
  studentAcceptedTime?: string;
  completedTime?: string;
  items: BedTransferHandoverItemDto[];
}

export interface BedTransferMeterReadingDto {
  id: number;
  meterType: 'Electricity' | 'Water' | string;
  previousValue: number;
  currentValue: number;
  consumption: number;
  unitPrice: number;
  totalCost: number;
  studentShareCost: number;
  residentCount: number;
  recordedTime: string;
  note?: string;
  evidenceImageUrl?: string;
}

export interface BedTransferInvoiceSummaryDto {
  id: number;
  invoiceNumber: string;
  invoiceType: string;
  status: string;
  totalAmount: number;
  issueDate: string;
  dueDate?: string;
  description?: string;
}

export interface GuardLookupDto {
  userId: number;
  fullName: string;
  guardCode: string;
  phoneNumber?: string;
  assignedDormId: number;
  assignedDormName: string;
}

export interface TransferInspectionTaskDto {
  requestId: number;
  requestCode: string;
  status: string;
  studentId: number;
  studentName: string;
  studentCode: string;
  studentPhone?: string;
  fromResidencyId: number;
  fromDormName: string;
  fromBlockName: string;
  fromRoomId: number;
  fromRoomNumber: string;
  fromBedId: number;
  fromBedCode: string;
  toDormName: string;
  toBlockName: string;
  toRoomNumber: string;
  toBedCode: string;
  transferDate?: string;
  transferReason: string;
  assignedGuardId?: number;
  assignedGuardName?: string;
  previousElectricityReading: number;
  previousWaterReading: number;
  electricityUnitPrice: number;
  waterUnitPrice: number;
  activeResidentCount: number;
  handoverCompleted: boolean;
  handover?: BedTransferHandoverInfoDto;
  meterReadings: BedTransferMeterReadingDto[];
}

export interface SubmitTransferInspectionPayload {
  currentElectricityReading: number;
  currentWaterReading: number;
  electricityEvidenceImageUrl?: string;
  waterEvidenceImageUrl?: string;
  note?: string;
  items: Array<{
    equipmentId: number;
    itemCondition: 'Good' | 'Damaged' | 'Missing';
    description?: string;
    compensationAmount: number;
  }>;
}

export interface BedTransferDetailDto {
  fromResidencyId: number;
  fromBedId: number;
  fromRoomId: number;
  fromDormId: number;
  fromBedCode: string;
  fromRoomNumber: string;
  fromBlockCode: string;
  fromBlockName: string;
  fromDormName: string;
  toBedId: number;
  toBedCode: string;
  toRoomNumber: string;
  toBlockCode: string;
  toBlockName: string;
  toDormName: string;
  toRoomTypeName: string;
  transferReason: string;
  transferDate?: string;
  oldBedDailyRate?: number;
  newBedDailyRate?: number;
  daysStayed?: number;
  daysRemaining?: number;
  oldBedAmount?: number;
  newBedAmount?: number;
  amountAlreadyPaid?: number;
  totalAmount?: number;
  priceDifference?: number;
  assignedGuardId?: number;
  assignedGuardName?: string;
  assignedGuardCode?: string;
  reviewedById?: number;
  reviewedTime?: string;
  newResidencyId?: number;
  handover?: BedTransferHandoverInfoDto;
  meterReadings?: BedTransferMeterReadingDto[];
  utilityShareAmount?: number;
  equipmentCompensationAmount?: number;
  totalOutstandingAmount?: number;
  refundDueAmount?: number;
  invoices?: BedTransferInvoiceSummaryDto[];
}

export interface CurrentResidencyTransferDto {
  residencyId: number;
  termId: number;
  termName: string;
  academicYear: string;
  termStartDate: string;
  termEndDate: string;
  dormName: string;
  blockCode: string;
  blockName: string;
  roomId: number;
  roomNumber: string;
  roomTypeName: string;
  bedId: number;
  bedCode: string;
  positionInRoom?: string;
  checkInDate?: string;
  currentTermPrice?: number;
}

export interface AvailableTransferBedDto {
  bedId: number;
  bedCode: string;
  positionInRoom?: string;
  roomId: number;
  roomNumber: string;
  floorLevel?: number;
  roomTypeId: number;
  roomTypeName: string;
  bedCapacity: number;
  blockId: number;
  blockCode: string;
  blockName: string;
  dormId: number;
  dormCode: string;
  dormName: string;
  termPrice?: number;
}

export interface BedTransferContextDto {
  eligible: boolean;
  ineligibleReason?: string;
  currentResidency?: CurrentResidencyTransferDto;
  availableBeds: AvailableTransferBedDto[];
}

export interface RequestItemDto {
  id: number;
  requestCode: string;
  studentId: number;
  studentName: string;
  studentCode: string;
  roomInfo: string;
  requestType: string;
  status: string;
  reason?: string;
  rejectReason?: string;
  createdTime: string;
  processedTime?: string;
  processedByName?: string;
  studentAccountStatus?: string;
  suspensionReason?: string;
  complaint?: {
    targetType: string;
    violationId?: number;
    violationCode?: string;
    violationTypeName?: string;
    initialLevel?: string;
    finalLevel?: string;
    violationStatus?: string;
    content: string;
    resolutionNote?: string;
    evidences: string[];
  };
  bedTransfer?: BedTransferDetailDto;
}

export const requestApi = {
  getAll: async (params?: { requestType?: string; status?: string }): Promise<RequestItemDto[]> => {
    const query = new URLSearchParams();
    if (params?.requestType) query.append('requestType', params.requestType);
    if (params?.status) query.append('status', params.status);
    const res = await authFetch(`${BASE_URL}/requests?${query.toString()}`);
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể tải danh sách đơn yêu cầu');
    return Array.isArray(json) ? json : json?.data || [];
  },

  getMyRequests: async (): Promise<RequestItemDto[]> => {
    const res = await authFetch(`${BASE_URL}/requests/my-requests`);
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể tải danh sách đơn của bạn');
    return Array.isArray(json) ? json : json?.data || [];
  },

  getBedTransferContext: async (): Promise<BedTransferContextDto> => {
    const res = await authFetch(`${BASE_URL}/requests/bed-transfer/context`);
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể tải thông tin chuyển giường');
    return json?.data || json;
  },

  create: async (formData: FormData): Promise<RequestItemDto> => {
    const res = await authFetch(`${BASE_URL}/requests`, {
      method: 'POST',
      body: formData,
    });
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể gửi đơn yêu cầu');
    return json?.data || json;
  },

  getGuards: async (dormId?: number): Promise<GuardLookupDto[]> => {
    const query = dormId ? `?dormId=${dormId}` : '';
    const res = await authFetch(`${BASE_URL}/requests/guards${query}`);
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể tải danh sách Bảo vệ');
    return Array.isArray(json) ? json : json?.data || [];
  },

  getGuardTransferTasks: async (): Promise<TransferInspectionTaskDto[]> => {
    const res = await authFetch(`${BASE_URL}/requests/bed-transfer/guard-tasks`);
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể tải nhiệm vụ kiểm tra chuyển giường');
    return Array.isArray(json) ? json : json?.data || [];
  },

  submitTransferInspection: async (
    requestId: number,
    payload: SubmitTransferInspectionPayload
  ): Promise<TransferInspectionTaskDto> => {
    const res = await authFetch(`${BASE_URL}/requests/${requestId}/transfer-inspection`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể gửi biên bản kiểm tra chuyển giường');
    return json?.data || json;
  },

  process: async (
    id: number,
    payload: {
      decision: string;
      note?: string;
      adjustedLevel?: string;
      assignedGuardId?: number;
      transferDate?: string;
    }
  ): Promise<RequestItemDto> => {
    const res = await authFetch(`${BASE_URL}/requests/${id}/process`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể xử lý đơn yêu cầu');
    return json?.data || json;
  },
};


// ── Check-in Handover API (Guard) ─────────────────────────────
export interface PendingCheckinStudent {
  bookingRequestId?: number;
  residencyId?: number;
  studentId: number;
  studentCode: string;
  studentName: string;
  gender: string;
  phone?: string;
  email?: string;
  cohort?: string;
  major?: string;
  blockId: number;
  blockCode: string;
  blockName: string;
  roomId: number;
  roomNumber: string;
  bedId: number;
  bedCode: string;
  termId: number;
  termName: string;
  bookingStatus: string;
}

export interface RoomEquipmentCheckin {
  equipmentId: number;
  catalogId: number;
  catalogName: string;
  category: string;
  assetCode: string;
  bedId?: number;
  bedCode?: string;
  currentCondition: 'Good' | 'Damaged' | 'Missing';
  note?: string;
}

export interface CheckinItemInput {
  equipmentId: number;
  itemCondition: 'Good' | 'Damaged' | 'Missing';
  note?: string;
}

export interface CreateCheckinHandoverPayload {
  bookingRequestId?: number;
  residencyId?: number;
  studentId: number;
  roomId: number;
  bedId: number;
  studentTitle?: string;
  otherItems?: string;
  electricityL?: string;
  electricityR?: string;
  waterL?: string;
  waterR?: string;
  items: CheckinItemInput[];
}

export interface CheckinHandoverItemDetail {
  id: number;
  equipmentId: number;
  catalogName: string;
  category: string;
  assetCode: string;
  bedCode?: string;
  quantity: number;
  itemCondition: 'Good' | 'Damaged' | 'Missing';
  note?: string;
}

export interface CheckinHandoverDetail {
  id: number;
  residencyId: number;
  studentId: number;
  studentCode: string;
  studentName: string;
  studentTitle: string;
  guardId: number;
  guardCode: string;
  guardName: string;
  blockId: number;
  blockCode: string;
  blockName: string;
  roomId: number;
  roomNumber: string;
  bedId: number;
  bedCode: string;
  termName: string;
  status: string;
  hasDamageOrMissingFlag: boolean;
  otherItems?: string;
  electricityL?: string;
  electricityR?: string;
  waterL?: string;
  waterR?: string;
  createdTime: string;
  completedTime?: string;
  items: CheckinHandoverItemDetail[];
}

export const checkinApi = {
  getPendingStudents: async (params?: { blockId?: number; roomId?: number; search?: string }): Promise<PendingCheckinStudent[]> => {
    const query = new URLSearchParams();
    if (params?.blockId) query.append('blockId', params.blockId.toString());
    if (params?.roomId) query.append('roomId', params.roomId.toString());
    if (params?.search) query.append('search', params.search);
    const res = await authFetch(`${BASE_URL}/checkin/pending-students?${query.toString()}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải danh sách sinh viên chờ Check-in');
    }
    return json?.data || [];
  },

  getRoomEquipments: async (roomId: number, bedId?: number): Promise<RoomEquipmentCheckin[]> => {
    const query = bedId ? `?bedId=${bedId}` : '';
    const res = await authFetch(`${BASE_URL}/checkin/room-equipments/${roomId}${query}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải danh sách thiết bị của phòng');
    }
    return json?.data || [];
  },

  completeCheckin: async (payload: CreateCheckinHandoverPayload): Promise<CheckinHandoverDetail> => {
    const res = await authFetch(`${BASE_URL}/checkin/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Lỗi khi thực hiện Check-in');
    }
    return json?.data;
  },

  getHistory: async (params?: { blockId?: number; roomId?: number; search?: string }): Promise<CheckinHandoverDetail[]> => {
    const query = new URLSearchParams();
    if (params?.blockId) query.append('blockId', params.blockId.toString());
    if (params?.roomId) query.append('roomId', params.roomId.toString());
    if (params?.search) query.append('search', params.search);
    const res = await authFetch(`${BASE_URL}/checkin/history?${query.toString()}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải lịch sử biên bản Check-in');
    }
    return json?.data || [];
  },

  getById: async (id: number): Promise<CheckinHandoverDetail> => {
    const res = await authFetch(`${BASE_URL}/checkin/${id}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không tìm thấy biên bản bàn giao');
    }
    return json?.data;
  },

  getMyCheckins: async (): Promise<CheckinHandoverDetail[]> => {
    const res = await authFetch(`${BASE_URL}/checkin/my-checkins`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải danh sách biên bản Check-in của bạn');
    }
    return json?.data || [];
  },
};


// ── Residency History API (Student) ───────────────────────────
export type ResidencyStatusType = 'Reserved' | 'Active' | 'Ended' | 'Cancelled';
export type ResidencyRegistrationType = 'Booking' | 'Keeping' | 'Transfer';

export interface ResidencyHistoryItem {
  id: number;
  residencyCode: string;
  studentId: number;
  status: ResidencyStatusType | string;
  registrationType?: ResidencyRegistrationType | string;
  termId: number;
  termName: string;
  academicYear: string;
  termStartDate: string;
  termEndDate: string;
  campusName: string;
  dormCode: string;
  dormName: string;
  blockId: number;
  blockCode: string;
  blockName: string;
  roomId: number;
  roomNumber: string;
  floorLevel?: number;
  roomTypeName: string;
  bedCapacity: number;
  bedId: number;
  bedCode: string;
  positionInRoom?: string;
  checkInDate?: string;
  plannedCheckOutDate?: string;
  actualCheckOutDate?: string;
  sourceRequestId?: number;
  hasCheckinHandover: boolean;
  checkinHandoverId?: number;
  violationsCount: number;
}

export interface ResidencyTransferRecord {
  requestId: number;
  direction: 'Outgoing' | 'Incoming' | string;
  status: string;
  fromRoomNumber: string;
  fromBlockCode: string;
  fromBedCode: string;
  toRoomNumber: string;
  toBlockCode: string;
  toBedCode: string;
  transferReason: string;
  transferDate?: string;
  priceDifference?: number;
  createdTime: string;
}

export interface ResidencyCheckoutRecord {
  requestId: number;
  status: string;
  expectedCheckOutDate: string;
  reason?: string;
  isExceptionalCase: boolean;
  exceptionNote?: string;
  createdTime: string;
  reviewedTime?: string;
  checkoutHandoverId?: number;
  checkoutHandoverStatus?: string;
  totalCompensation?: number;
}

export interface ResidencyInvoiceSummary {
  id: number;
  invoiceNumber: string;
  invoiceType: string;
  amount: number;
  status: string;
  issuedTime: string;
  dueDate: string;
  paidTime?: string;
}

export interface ResidencyHistoryDetail extends ResidencyHistoryItem {
  sourceRequestStatus?: string;
  sourceRequestCreatedTime?: string;
  checkinHandoverStatus?: string;
  checkinCompletedTime?: string;
  checkinGuardName?: string;
  hasDamageOrMissingAtCheckin: boolean;
  transfers: ResidencyTransferRecord[];
  checkouts: ResidencyCheckoutRecord[];
  invoices: ResidencyInvoiceSummary[];
}

// ── EW (Electricity & Water) API ─────────────────────────
export interface BillingPeriodLookupDto {
  id: number;
  termId?: number;
  targetMonth: number;
  targetYear: number;
  displayName: string;
  status: string; // "Open" | "Calculated" | "Closed"
  termName: string;
}

export interface EwReadingDetailItem {
  readingId: number;
  roomId: number;
  roomNumber: string;
  floorLevel?: number;
  meterType: string;
  previousValue: number;
  currentValue: number;
  consumption: number;
  source: string;
  status: string;
  note?: string;
  evidenceImageUrl?: string;
  recordedTime: string;
  recordedByName?: string;
}

export interface MeterPhotoScanResponseDto {
  isMeterImage: boolean;
  readingValue?: number;
  rawDigits?: string;
  detectedMeterType: 'Electricity' | 'Water' | 'Unknown' | string;
  meterTypeMismatch: boolean;
  confidence: number;
  confidenceLevel: 'High' | 'Medium' | 'Low' | string;
  aiNotes?: string;
  evidenceImageUrl?: string;
  previousValue?: number;
  estimatedConsumption?: number;
  validationStatus: 'Valid' | 'Warning' | 'Error' | string;
  warnings?: string[];
}

export interface RoomReadingStatusDto {
  roomId: number;
  roomNumber: string;
  hasReading: boolean;
  currentValue?: number;
  recordedAt?: string;
  status?: string;
}

export interface BlockProgressItemDto {
  blockCode: string;
  blockName: string;
  dormName: string;
  electricityLeft?: RoomReadingStatusDto;
  electricityRight?: RoomReadingStatusDto;
  waterLeft?: RoomReadingStatusDto;
  waterRight?: RoomReadingStatusDto;
}

export interface BlockProgressResponseDto {
  billingPeriodId: number;
  displayName: string;
  totalBlocks: number;
  completedBlocks: number;
  missingBlocks: number;
  blocks: BlockProgressItemDto[];
}

export interface RoomOldReadingDto {
  roomId: number;
  roomNumber: string;
  oldReading: number;
  alreadyRecorded: boolean;
  existingCurrentValue?: number;
  existingEvidenceImageUrl?: string;
  existingSource?: string;
}

export interface BlockFormDataResponseDto {
  blockCode: string;
  billingPeriodId: number;
  displayPeriod: string;
  electricityLeft?: RoomOldReadingDto;
  electricityRight?: RoomOldReadingDto;
  waterLeft?: RoomOldReadingDto;
  waterRight?: RoomOldReadingDto;
}

export interface ManualConfirmRequestDto {
  billingPeriodId: number;
  blockCode: string;
  electricityLeftNewValue?: number;
  electricityRightNewValue?: number;
  waterLeftNewValue?: number;
  waterRightNewValue?: number;
  electricityLeftEvidenceUrl?: string;
  electricityRightEvidenceUrl?: string;
  waterLeftEvidenceUrl?: string;
  waterRightEvidenceUrl?: string;
  electricityLeftSource?: 'Manual' | 'PhotoOCR';
  electricityRightSource?: 'Manual' | 'PhotoOCR';
  waterLeftSource?: 'Manual' | 'PhotoOCR';
  waterRightSource?: 'Manual' | 'PhotoOCR';
  note?: string;
  importOnlyValid?: boolean;
}

export interface ReadingConfirmResultDto {
  readingId?: number;
  roomNumber: string;
  meterType: string;
  status: string;
  anomalyReason?: string;
}

export interface ManualConfirmResponseDto {
  saved: number;
  flagged: number;
  skipped: number;
  managerAlerted: boolean;
  results: ReadingConfirmResultDto[];
}

export interface EwUsageRow {
  id: number;
  dormName?: string;
  block: string;
  tenBlock?: string;
  blockCode?: string;
  blockName?: string;
  roomNumber?: string;
  termId?: number;
  billingPeriodId?: number;
  periodStatus?: string;
  targetMonth?: number;
  targetYear?: number;
  loaiSuDung: string;    // "Electricity" | "Water"
  ngayTao: string | null;
  hocKy: string;
  meterNumberDisplay: string;
  currentMonthConsumption?: number;
  totalConsumption: number;
  donvi?: string;
  readings?: EwReadingDetailItem[];
}

export interface ExcelPreviewRowDto {
  rowIndex: number;
  dorm: string;
  blockCode: string;
  meterType: string;
  dateStr: string;
  leftValue?: number;
  rightValue?: number;
  leftRoomId?: number;
  rightRoomId?: number;
  leftRoomNumber: string;
  rightRoomNumber: string;
  leftOldReading: number;
  rightOldReading: number;
  leftConsumption: number;
  rightConsumption: number;
  status: string; // "Valid" | "Warning" | "Error"
  errorMessage?: string;
  flagReason?: string;
}

export interface ExcelPreviewResponseDto {
  totalRows: number;
  validRows: number;
  warningRows: number;
  errorRows: number;
  alreadyHasDataInPeriod: boolean;
  rows: ExcelPreviewRowDto[];
}

export interface ImportBatchItemDto {
  id: number;
  billingPeriodId: number;
  billingPeriodDisplay: string;
  blockCode: string;
  guardName: string;
  method: string;
  fileName?: string;
  totalRows: number;
  errorRows: number;
  importTime: string;
}

export interface UsageAnomalyItemDto {
  anomalyId: number;
  readingId: number;
  billingPeriodId?: number;
  billingPeriodDisplay: string;
  roomId: number;
  roomNumber: string;
  blockCode: string;
  blockName: string;
  dormName: string;
  meterType: string;
  previousValue: number;
  currentValue: number;
  consumption: number;
  quota: number;
  overUsage: number;
  flagReason: string;
  status: string; // 'Flagged' | 'Resolved'
  resolution?: string; // 'Accepted' | 'Corrected'
  managerNote?: string;
  guardName: string;
  guardPhone?: string;
  createdTime: string;
  resolvedTime?: string;
  resolvedByName?: string;
}

export interface ResolveAnomalyResponseDto {
  anomalyId: number;
  readingId: number;
  status: string;
  resolution: string;
  finalReadingValue: number;
  finalConsumption: number;
  surchargeCalculated: boolean;
  debtNotificationsSent: number;
  sharePerStudent?: number;
  message: string;
}

export interface BillingCalculationResultDto {
  billingPeriodId: number;
  roomsCalculated: number;
  roomsSkippedFlagged: number;
  invoicesCreated: number;
  notificationsSent: number;
  message: string;
}

export interface StudentTermOptionDto {
  termId: number;
  termName: string;
  academicYear: string;
  isCurrent: boolean;
}

export interface StudentEwMonthlyRowDto {
  block: string;
  roomNumber?: string;
  monthYear: string;
  targetMonth: number;
  targetYear: number;
  recordDate?: string;
  type: string; // 'Điện' | 'Nước'
  rawType: string; // 'Electricity' | 'Water'
  roomTotalUsage: number;
  yourUsage: number;
  quotaMonthly: number;
  yourOverUsage: number;
}

export interface StudentEwReportDto {
  termId: number;
  termName: string;
  academicYear: string;
  blockId: number;
  blockCode: string;
  blockName: string;
  roomNumber: string;
  numberOfCheckedInResidents: number;
  roommates: string[];
  rows: StudentEwMonthlyRowDto[];
  totalElectricUsage: number;
  electricQuota: number;
  totalElectricOverUsage: number;
  electricUnitPrice: number;
  electricOverUsagePayment: number;
  totalWaterUsage: number;
  waterQuota: number;
  totalWaterOverUsage: number;
  waterUnitPrice: number;
  waterOverUsagePayment: number;
}

export const ewApi = {
  getBillingPeriods: async (): Promise<BillingPeriodLookupDto[]> => {
    const res = await authFetch(`${BASE_URL}/ew/billing-periods`);
    const json = await safeParseResponse<BillingPeriodLookupDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách kỳ thanh toán');
    return json.data || [];
  },

  getUsages: async (params?: {
    blockCode?: string;
    meterType?: string;
    month?: number;
    year?: number;
    termId?: number;
    billingPeriodId?: number;
  }): Promise<EwUsageRow[]> => {
    const query = new URLSearchParams();
    if (params?.blockCode) query.append('blockCode', params.blockCode);
    if (params?.meterType) query.append('meterType', params.meterType);
    if (params?.month) query.append('month', params.month.toString());
    if (params?.year) query.append('year', params.year.toString());
    if (params?.termId) query.append('termId', params.termId.toString());
    if (params?.billingPeriodId) query.append('billingPeriodId', params.billingPeriodId.toString());
    const qs = query.toString();
    const res = await authFetch(`${BASE_URL}/ew/usages${qs ? `?${qs}` : ''}`);
    const json = await safeParseResponse<EwUsageRow[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải dữ liệu sử dụng');
    return json.data || [];
  },

  previewImport: async (file: File, billingPeriodId: number): Promise<ExcelPreviewResponseDto> => {
    const form = new FormData();
    form.append('file', file);
    form.append('billingPeriodId', billingPeriodId.toString());
    const res = await authFetch(`${BASE_URL}/ew/import/preview`, { method: 'POST', body: form });
    const json = await safeParseResponse<ExcelPreviewResponseDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể xem trước dữ liệu');
    return json.data;
  },

  uploadImport: async (file: File, billingPeriodId: number): Promise<{ savedReadings: number; message: string }> => {
    const form = new FormData();
    form.append('file', file);
    form.append('billingPeriodId', billingPeriodId.toString());
    const res = await authFetch(`${BASE_URL}/ew/import/upload`, { method: 'POST', body: form });
    const json = await safeParseResponse<any>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Import thất bại');
    return json.data;
  },

  confirmImport: async (payload: {
    billingPeriodId: number;
    fileName: string;
    importOnlyValid: boolean;
    overwriteExisting: boolean;
    rows: ExcelPreviewRowDto[];
  }): Promise<{ savedReadings: number; message: string }> => {
    const res = await authFetch(`${BASE_URL}/ew/import/confirm`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<any>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Xác nhận import thất bại');
    return json.data;
  },

  getImportBatches: async (billingPeriodId?: number): Promise<ImportBatchItemDto[]> => {
    const qs = billingPeriodId ? `?billingPeriodId=${billingPeriodId}` : '';
    const res = await authFetch(`${BASE_URL}/ew/import/batches${qs}`);
    const json = await safeParseResponse<ImportBatchItemDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải lịch sử import');
    return json.data || [];
  },

  downloadTemplate: async (billingPeriodId?: number): Promise<void> => {
    const qs = billingPeriodId ? `?billingPeriodId=${billingPeriodId}` : '';
    const res = await authFetch(`${BASE_URL}/ew/import/template${qs}`);
    if (!res.ok) {
      throw new Error('Không thể tải file mẫu Excel');
    }
    const blob = await res.blob();
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'Ew_Template.xlsx');
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  updateReading: async (id: number, currentValue: number, note?: string): Promise<{ success: boolean; message: string }> => {
    const res = await authFetch(`${BASE_URL}/ew/readings/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currentValue, note }),
    });
    const json = await safeParseResponse<{ success: boolean; message: string }>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể cập nhật chỉ số');
    return json;
  },

  getMyTerms: async (): Promise<StudentTermOptionDto[]> => {
    const res = await authFetch(`${BASE_URL}/ew/my-terms`);
    const json = await safeParseResponse<StudentTermOptionDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách học kỳ');
    return json.data || [];
  },

  getMyUsage: async (termId?: number): Promise<StudentEwReportDto | null> => {
    const qs = termId ? `?termId=${termId}` : '';
    const res = await authFetch(`${BASE_URL}/ew/my-usage${qs}`);
    const json = await safeParseResponse<StudentEwReportDto | null>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải thông tin điện nước');
    return json.data || null;
  },

  getAnomalies: async (params?: { billingPeriodId?: number; status?: string }): Promise<UsageAnomalyItemDto[]> => {
    const query = new URLSearchParams();
    if (params?.billingPeriodId) query.append('billingPeriodId', params.billingPeriodId.toString());
    if (params?.status) query.append('status', params.status);
    const qs = query.toString();
    const res = await authFetch(`${BASE_URL}/ew/anomalies${qs ? `?${qs}` : ''}`);
    const json = await safeParseResponse<UsageAnomalyItemDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách cảnh báo bất thường');
    return json.data || [];
  },

  resolveAnomaly: async (
    id: number,
    payload: { action: 'Confirm' | 'Adjust'; adjustedValue?: number; managerNote?: string }
  ): Promise<ResolveAnomalyResponseDto> => {
    const res = await authFetch(`${BASE_URL}/ew/anomalies/${id}/resolve`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<ResolveAnomalyResponseDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Lỗi khi xử lý cảnh báo bất thường');
    return json.data || ({ message: json.message } as ResolveAnomalyResponseDto);
  },

  calculatePeriodSurcharges: async (billingPeriodId: number): Promise<BillingCalculationResultDto> => {
    const res = await authFetch(`${BASE_URL}/ew/billing-periods/${billingPeriodId}/calculate`, {
      method: 'POST',
    });
    const json = await safeParseResponse<BillingCalculationResultDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Lỗi khi tính phụ trội điện nước');
    return json.data || ({ message: json.message } as BillingCalculationResultDto);
  },

  getBlockProgress: async (billingPeriodId: number): Promise<BlockProgressResponseDto> => {
    const res = await authFetch(`${BASE_URL}/ew/billing-periods/${billingPeriodId}/blocks`);
    const json = await safeParseResponse<BlockProgressResponseDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải tiến độ ghi chỉ số các block');
    return json.data;
  },

  getBlockFormData: async (blockCode: string, billingPeriodId: number): Promise<BlockFormDataResponseDto> => {
    const query = new URLSearchParams({
      blockCode,
      billingPeriodId: billingPeriodId.toString(),
    });
    const res = await authFetch(`${BASE_URL}/ew/readings/block-form?${query.toString()}`);
    const json = await safeParseResponse<BlockFormDataResponseDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải thông tin công tơ của block');
    return json.data;
  },

  confirmManualReading: async (payload: ManualConfirmRequestDto): Promise<ManualConfirmResponseDto> => {
    const res = await authFetch(`${BASE_URL}/ew/readings/manual/confirm`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<ManualConfirmResponseDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể lưu chỉ số điện nước');
    return json.data;
  },

  scanMeterPhoto: async (
    photo: File,
    options?: {
      expectedMeterType?: 'Electricity' | 'Water';
      previousValue?: number;
      saveEvidence?: boolean;
    }
  ): Promise<MeterPhotoScanResponseDto> => {
    const form = new FormData();
    form.append('photo', photo);
    if (options?.expectedMeterType) {
      form.append('expectedMeterType', options.expectedMeterType);
    }
    if (options?.previousValue !== undefined && !Number.isNaN(options.previousValue)) {
      form.append('previousValue', options.previousValue.toString());
    }
    if (options?.saveEvidence !== undefined) {
      form.append('saveEvidence', options.saveEvidence ? 'true' : 'false');
    }

    const res = await authFetch(`${BASE_URL}/ew/readings/scan-photo`, {
      method: 'POST',
      body: form,
    });
    const json = await safeParseResponse<MeterPhotoScanResponseDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể nhận diện chỉ số từ ảnh công tơ');
    return json.data;
  },
};

export const residencyApi = {
  getMyHistory: async (params?: {
    status?: string;
    term?: string;
    registrationType?: string;
    search?: string;
  }): Promise<ResidencyHistoryItem[]> => {
    const query = new URLSearchParams();
    if (params?.status) query.append('status', params.status);
    if (params?.term) query.append('term', params.term);
    if (params?.registrationType) query.append('registrationType', params.registrationType);
    if (params?.search) query.append('search', params.search);

    const qs = query.toString();
    const res = await authFetch(`${BASE_URL}/residencies/my-history${qs ? `?${qs}` : ''}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không thể tải lịch sử lưu trú của bạn');
    }
    return json?.data || [];
  },

  getMyHistoryById: async (id: number): Promise<ResidencyHistoryDetail> => {
    const res = await authFetch(`${BASE_URL}/residencies/my-history/${id}`);
    const json = await parseJsonSafe(res);
    if (!res.ok || (json && json.success === false)) {
      throw new Error(json?.message || 'Không tìm thấy chi tiết hồ sơ lưu trú');
    }
    return json?.data;
  },
};


// ── Admin Account API (Quản lý tài khoản) ──────────────────
export interface AccountItem {
  id: number;
  username: string;
  fullName: string;
  email: string;
  phone?: string | null;
  gender?: string | null;
  status: 'Pending' | 'Active' | 'Suspended' | string;
  suspensionReason?: string | null;
  suspendedAt?: string | null;
  suspendedById?: number | null;
  createdAt: string;
  updatedAt?: string | null;
  roles: string[];
  primaryRole: string;
}

export interface AccountStatusHistoryItem {
  id: number;
  oldStatus: string;
  newStatus: string;
  reason?: string | null;
  notes?: string | null;
  changedBy: number;
  changedByName?: string | null;
  changedAt: string;
  notificationStatus: string;
}

export interface RoleChangeHistoryItem {
  id: number;
  action: string;
  oldRoles?: string | null;
  newRoles?: string | null;
  changedBy: number;
  changedByName?: string | null;
  changedAt: string;
}

export interface AccountDetail extends AccountItem {
  statusHistory: AccountStatusHistoryItem[];
  roleHistory: RoleChangeHistoryItem[];
}

export interface CreateAccountPayload {
  username: string;
  fullName: string;
  email: string;
  password: string;
  phone?: string;
  gender?: string;
  roles: string[];
}

async function adminAccountRequest<T>(url: string, init: RequestInit | undefined, defaultError: string): Promise<T> {
  const res = await authFetch(url, init);
  const json = await safeParseResponse<T>(res, defaultError);
  if (!res.ok || !json.success) {
    const detail = json.errors && json.errors.length > 0 ? json.errors.join('; ') : '';
    throw new Error(detail || json.message || defaultError);
  }
  return json.data;
}

const jsonInit = (method: string, body: unknown): RequestInit => ({
  method,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
});

export const accountApi = {
  getAll: (params?: { search?: string; status?: string; role?: string; page?: number; pageSize?: number }) => {
    const query = new URLSearchParams();
    if (params?.search) query.append('search', params.search);
    if (params?.status) query.append('status', params.status);
    if (params?.role) query.append('role', params.role);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());
    return adminAccountRequest<PagedResult<AccountItem>>(
      `${BASE_URL}/admin/users?${query.toString()}`, undefined, 'Không thể tải danh sách tài khoản');
  },

  getById: (id: number) =>
    adminAccountRequest<AccountDetail>(`${BASE_URL}/admin/users/${id}`, undefined, 'Không thể tải chi tiết tài khoản'),

  create: (payload: CreateAccountPayload) =>
    adminAccountRequest<AccountItem>(`${BASE_URL}/admin/users`, jsonInit('POST', payload), 'Lỗi khi tạo tài khoản'),

  assignRoles: (id: number, roles: string[]) =>
    adminAccountRequest<AccountItem>(`${BASE_URL}/admin/users/${id}/roles`, jsonInit('PUT', { roles }), 'Lỗi khi cập nhật vai trò'),

  suspend: (id: number, payload: { reason: string; notes?: string }) =>
    adminAccountRequest<AccountItem>(`${BASE_URL}/admin/users/${id}/suspend`, jsonInit('POST', payload), 'Lỗi khi tạm ngưng tài khoản'),

  restore: (id: number, payload: { reason?: string }) =>
    adminAccountRequest<AccountItem>(`${BASE_URL}/admin/users/${id}/restore`, jsonInit('POST', payload), 'Lỗi khi khôi phục tài khoản'),
};

// ── Audit Log API (Admin) ───────────────────────────────────
export interface AuditLogItem {
  id: number;
  logType: 'Impersonation' | 'RoleChange' | 'AccountStatus' | 'System';
  actorId?: number;
  actorName?: string;
  actorCode?: string;
  targetUserId?: number;
  targetUserName?: string;
  targetUserCode?: string;
  actionType?: string;
  targetEntity?: string;
  targetId?: string;
  oldValue?: string;
  newValue?: string;
  ipAddress?: string;
  userAgent?: string;
  createdAt: string;
  notes?: string;
}

export interface AuditLogDetail extends AuditLogItem {
  rawPayload?: string;
}

export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  logType?: string;
  actorId?: number;
  fromDate?: string;
  toDate?: string;
  search?: string;
}

export const auditApi = {
  getAll: (params?: AuditLogQuery) => {
    const query = new URLSearchParams();
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());
    if (params?.logType && params.logType !== 'All') query.append('logType', params.logType);
    if (params?.actorId) query.append('actorId', params.actorId.toString());
    if (params?.fromDate) query.append('fromDate', params.fromDate);
    if (params?.toDate) query.append('toDate', params.toDate);
    if (params?.search) query.append('search', params.search);
    return adminAccountRequest<PagedResult<AuditLogItem>>(
      `${BASE_URL}/admin/audit-logs?${query.toString()}`,
      undefined,
      'Không thể tải nhật ký kiểm toán'
    );
  },

  getDetail: (id: number, logType?: string) => {
    const query = new URLSearchParams();
    if (logType) query.append('logType', logType);
    return adminAccountRequest<AuditLogDetail>(
      `${BASE_URL}/admin/audit-logs/${id}?${query.toString()}`,
      undefined,
      'Không thể tải chi tiết nhật ký kiểm toán'
    );
  },
};


// ── Keeping API (UC02 – Giữ phòng, Student) ────────────────
export interface KeepingCurrentStay {
  bedId: number;
  dormName: string;
  blockName: string;
  roomNumber: string;
  bedCode: string;
  currentTermId: number;
  currentTerm: string;
  status: string;
}

export interface KeepingNextTerm {
  id: number;
  name: string;
  academicYear: string;
  startDate: string;
  endDate: string;
}

export interface KeepingFee {
  amount: number;
  currency: string;
  months: number;
  description: string;
}

export interface KeepingPeriod {
  start: string;
  end: string;
}

export interface KeepingEligibility {
  eligible: boolean;
  reasonCode?: string | null;
  message?: string | null;
  currentStay?: KeepingCurrentStay | null;
  nextTerm?: KeepingNextTerm | null;
  fee?: KeepingFee | null;
  period?: KeepingPeriod | null;
}

export interface KeepingBed {
  bedId: number;
  bedCode: string;
  dormName: string;
  blockName: string;
  roomNumber: string;
}

export interface KeepingRequestResult {
  requestId: number;
  status: string;
  requestType: string;
  nextTerm?: KeepingNextTerm | null;
  bed?: KeepingBed | null;
  feeAmount?: number | null;
  currency: string;
  createdAt: string;
  /** Id hóa đơn phí giữ phòng đã tạo (dùng để redirect sang màn hình thanh toán). */
  invoiceId?: number;
  /** true nếu đang chờ sinh viên thanh toán phí giữ phòng. */
  requiresPayment?: boolean;
  /** true nếu đã thanh toán và đang chờ Ban Quản lý duyệt. */
  awaitingManagerApproval?: boolean;
}

async function keepingRequest<T>(url: string, init: RequestInit | undefined, defaultError: string): Promise<T> {
  const res = await authFetch(url, init);
  if (res.status === 403) {
    throw new Error('Chức năng giữ phòng chỉ dành cho tài khoản Sinh viên.');
  }
  const json = await safeParseResponse<T>(res, defaultError);
  if (!res.ok || !json.success) {
    const detail = json.errors && json.errors.length > 0 ? json.errors.join('; ') : '';
    throw new Error(json.message || detail || defaultError);
  }
  return json.data;
}

export const keepingApi = {
  getEligibility: () =>
    keepingRequest<KeepingEligibility>(`${BASE_URL}/keeping/eligibility`, undefined, 'Không thể kiểm tra điều kiện giữ phòng'),

  create: (agreedToRegulations: boolean) =>
    keepingRequest<KeepingRequestResult>(
      `${BASE_URL}/keeping/requests`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ agreedToRegulations }),
      },
      'Lỗi khi giữ phòng'
    ),

  getById: (id: number) =>
    keepingRequest<KeepingRequestResult>(`${BASE_URL}/keeping/requests/${id}`, undefined, 'Không thể tải yêu cầu giữ phòng'),
};

// ── Keeping Manager API (duyệt / từ chối yêu cầu giữ phòng) ──────
export interface KeepingRequestManagerDto {
  requestId: number;
  bookingRequestId?: number;
  invoiceId?: number;
  invoiceNumber?: string;
  studentId: number;
  studentCode: string;
  studentName: string;
  studentGender: string;
  status: string;
  invoiceStatus?: string;
  isPaid: boolean;
  bedId: number;
  bedCode: string;
  dormName: string;
  blockName: string;
  roomNumber: string;
  termId: number;
  termName: string;
  feeAmount: number;
  currency: string;
  createdTime: string;
  processedTime?: string;
  processedByName?: string;
  rejectReason?: string;
}

export const keepingManagerApi = {
  getPending: () =>
    keepingRequest<KeepingRequestManagerDto[]>(
      `${BASE_URL}/keeping/manager/requests`,
      undefined,
      'Không thể tải danh sách yêu cầu giữ phòng chờ duyệt'
    ),

  approve: (id: number) =>
    keepingRequest<KeepingRequestResult>(
      `${BASE_URL}/keeping/manager/requests/${id}/approve`,
      { method: 'POST', headers: { 'Content-Type': 'application/json' } },
      'Lỗi khi phê duyệt yêu cầu giữ phòng'
    ),

  reject: (id: number, rejectReason: string) =>
    keepingRequest<KeepingRequestResult>(
      `${BASE_URL}/keeping/manager/requests/${id}/reject`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ rejectReason }),
      },
      'Lỗi khi từ chối yêu cầu giữ phòng'
    ),
};

// ── Booking Room API ──────────────────────────────────────
export interface BookingEligibilityDto {
  eligible: boolean;
  ineligibleReason?: string;
  isBookingPeriodOpen: boolean;
  periodStart?: string;
  periodEnd?: string;
  nextTermId?: number;
  nextTermName?: string;
  studentGender?: string;
  currentBooking?: BookingRequestItemDto;
}

export interface BookingAvailableDormDto {
  dormId: number;
  dormName: string;
  dormCode: string;
  totalAvailableBeds: number;
}

export interface BookingAvailableBlockDto {
  blockId: number;
  blockName: string;
  blockCode: string;
  genderType: string;
  dormId: number;
  totalAvailableBeds: number;
}

export interface BookingAvailableRoomDto {
  roomId: number;
  roomNumber: string;
  floorLevel?: number;
  roomTypeId: number;
  roomTypeName: string;
  bedCapacity: number;
  freeBeds: number;
  blockId: number;
}

export interface BookingAvailableBedDto {
  bedId: number;
  bedCode: string;
  positionInRoom?: string;
  roomId: number;
  roomNumber: string;
  termPrice?: number;
}

export interface BookingRequestItemDto {
  requestId: number;
  status: string;
  bedId: number;
  bedCode: string;
  roomNumber: string;
  blockName: string;
  dormName: string;
  termId: number;
  termName: string;
  termPrice?: number;
  createdTime: string;
  paymentDeadline: string;
  agreementToRegulations: boolean;
  rejectReason?: string;
  /** Id hóa đơn phí phòng đã tạo (dùng để redirect sang màn hình thanh toán). */
  invoiceId?: number;
  /** true nếu đơn đang chờ sinh viên thanh toán. */
  requiresPayment?: boolean;
}

export interface BookingRequestManagerDto {
  id: number;
  studentId: number;
  studentCode: string;
  studentName: string;
  studentGender: string;
  bedId: number;
  bedCode: string;
  roomNumber: string;
  blockName: string;
  dormName: string;
  termId: number;
  termName: string;
  termPrice?: number;
  status: string;
  createdTime: string;
  processedTime?: string;
  processedByName?: string;
  rejectReason?: string;
}

export const bookingApi = {
  getEligibility: async (): Promise<BookingEligibilityDto> => {
    const res = await authFetch(`${BASE_URL}/booking/eligibility`);
    const json = await safeParseResponse<BookingEligibilityDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể kiểm tra điều kiện đặt phòng');
    return json.data;
  },

  getAvailableDorms: async (): Promise<BookingAvailableDormDto[]> => {
    const res = await authFetch(`${BASE_URL}/booking/available-dorms`);
    const json = await safeParseResponse<BookingAvailableDormDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách tòa KTX');
    return json.data || [];
  },

  getAvailableBlocks: async (dormId: number): Promise<BookingAvailableBlockDto[]> => {
    const res = await authFetch(`${BASE_URL}/booking/available-blocks?dormId=${dormId}`);
    const json = await safeParseResponse<BookingAvailableBlockDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách Block');
    return json.data || [];
  },

  getAvailableRooms: async (blockId: number): Promise<BookingAvailableRoomDto[]> => {
    const res = await authFetch(`${BASE_URL}/booking/available-rooms?blockId=${blockId}`);
    const json = await safeParseResponse<BookingAvailableRoomDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách phòng');
    return json.data || [];
  },

  getAvailableBeds: async (roomId: number): Promise<BookingAvailableBedDto[]> => {
    const res = await authFetch(`${BASE_URL}/booking/available-beds?roomId=${roomId}`);
    const json = await safeParseResponse<BookingAvailableBedDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách giường');
    return json.data || [];
  },

  createBooking: async (payload: { bedId: number; agreementToRegulations: boolean }): Promise<BookingRequestItemDto> => {
    const res = await authFetch(`${BASE_URL}/booking/requests`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BookingRequestItemDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Lỗi khi gửi yêu cầu đặt phòng');
    return json.data;
  },

  getManagerRequests: async (status?: string): Promise<BookingRequestManagerDto[]> => {
    const query = status && status !== 'All' ? `?status=${status}` : '';
    const res = await authFetch(`${BASE_URL}/booking/manager/requests${query}`);
    const json = await safeParseResponse<BookingRequestManagerDto[]>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải danh sách đơn đặt phòng');
    return json.data || [];
  },

  processBooking: async (id: number, decision: 'Approved' | 'Rejected', rejectReason?: string): Promise<BookingRequestManagerDto> => {
    const res = await authFetch(`${BASE_URL}/booking/manager/requests/${id}/process`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ decision, rejectReason }),
    });
    const json = await safeParseResponse<BookingRequestManagerDto>(res);
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể xử lý đơn đặt phòng');
    return json.data;
  },
};

// ── In-App Notifications API ─────────────────────────────────
export interface NotificationItem {
  id: number;
  title: string;
  content?: string | null;
  referenceType?: string | null;
  referenceId?: string | null;
  channel: string;
  readFlag: boolean;
  readTime?: string | null;
  sentTime: string;
}

export interface NotificationListResponse {
  items: NotificationItem[];
  unreadCount: number;
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UnreadCountResponse {
  unreadCount: number;
}

export interface MarkNotificationReadResponse {
  id: number;
  readFlag: boolean;
  readTime?: string | null;
  unreadCount: number;
}

export interface MarkAllNotificationsReadResponse {
  markedCount: number;
  unreadCount: number;
}

export type NotificationReadStatusFilter = 'all' | 'unread' | 'read';

export const notificationApi = {
  getList: async (
    params?: {
      unreadOnly?: boolean;
      readStatus?: NotificationReadStatusFilter;
      referenceType?: string;
      search?: string;
      page?: number;
      pageSize?: number;
    },
    options?: { signal?: AbortSignal }
  ): Promise<NotificationListResponse> => {
    const query = new URLSearchParams();
    if (params?.unreadOnly) query.append('unreadOnly', 'true');
    if (params?.readStatus && params.readStatus !== 'all') {
      query.append('readStatus', params.readStatus);
    }
    if (params?.referenceType && params.referenceType !== 'all') {
      query.append('referenceType', params.referenceType);
    }
    const trimmedSearch = params?.search?.trim();
    if (trimmedSearch) {
      query.append('search', trimmedSearch);
    }
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const qs = query.toString();
    const res = await authFetch(`${BASE_URL}/notifications${qs ? `?${qs}` : ''}`, {
      signal: options?.signal,
    });
    const json = await safeParseResponse<NotificationListResponse>(
      res,
      'Không thể tải danh sách thông báo'
    );
    if (!res.ok || json.success === false) {
      throw new Error(json.message || 'Không thể tải danh sách thông báo');
    }
    return json.data;
  },

  getUnreadCount: async (): Promise<number> => {
    const res = await authFetch(`${BASE_URL}/notifications/unread-count`);
    const json = await safeParseResponse<UnreadCountResponse>(res, 'Không thể tải số thông báo chưa đọc');
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể tải số thông báo chưa đọc');
    return json.data?.unreadCount ?? 0;
  },

  markAsRead: async (id: number): Promise<MarkNotificationReadResponse> => {
    const res = await authFetch(`${BASE_URL}/notifications/${id}/read`, {
      method: 'PATCH',
    });
    const json = await safeParseResponse<MarkNotificationReadResponse>(res, 'Không thể đánh dấu đã đọc');
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể đánh dấu đã đọc');
    return json.data;
  },

  markAllAsRead: async (): Promise<MarkAllNotificationsReadResponse> => {
    const res = await authFetch(`${BASE_URL}/notifications/read-all`, {
      method: 'PATCH',
    });
    const json = await safeParseResponse<MarkAllNotificationsReadResponse>(res, 'Không thể đánh dấu tất cả đã đọc');
    if (!res.ok || json.success === false) throw new Error(json.message || 'Không thể đánh dấu tất cả đã đọc');
    return json.data;
  },
};

// ── Student Payment API (UC-Payment – VietQR) ────────────────
export type InvoiceType = 'RoomFee' | 'Utility' | 'BedTransfer' | 'Compensation' | string;
export type InvoiceStatus = 'Unpaid' | 'Processing' | 'Paid' | 'Overdue' | 'Cancelled' | string;
export type PaymentSourceType = 'Booking' | 'Keeping' | 'Utility' | 'BedTransfer' | 'Checkout' | string;

export interface InvoiceItemDto {
  id: number;
  description: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface StudentInvoiceDto {
  id: number;
  studentId: number;
  termId: number;
  invoiceNumber: string;
  invoiceType: InvoiceType;
  adjustmentKind?: string | null;
  sourceType?: PaymentSourceType | null;
  sourceId?: number | null;
  amount: number;
  issuedTime: string;
  dueDate: string;
  paidTime?: string | null;
  status: InvoiceStatus;
  isOverdue: boolean;
  overdueDays: number;
  items: InvoiceItemDto[];
  hasActiveTransaction: boolean;
  activeTransactionId?: number | null;
  activeTransactionExpiryTime?: string | null;
  activeTransactionNotExpired: boolean;
}

export interface StudentInvoiceListResponseDto {
  items: StudentInvoiceDto[];
  totalOutstanding: number;
  overdueCount: number;
  serverTime: string;
}

export interface TransactionInvoiceDto {
  invoiceId: number;
  invoiceNumber: string;
  invoiceType: InvoiceType;
  allocatedAmount: number;
  dueDate: string;
  status: InvoiceStatus;
  items: InvoiceItemDto[];
}

export interface PaymentTransactionDto {
  id: number;
  studentId: number;
  method: string;
  gatewayReference: string;
  qrLink?: string | null;
  sePayQrLink?: string | null;
  sePayBankCode?: string;
  sePayAccountNumber?: string;
  sePayAccountName?: string;
  totalAmount: number;
  status: 'Processing' | 'Success' | 'Failed' | 'Cancelled' | 'Expired' | string;
  receiptNumber?: string | null;
  createdTime: string;
  expiryTime: string;
  paidTime?: string | null;
  callbackTime?: string | null;
  invoices: TransactionInvoiceDto[];
  remainingSeconds: number;
}

export interface PaymentTransactionStatusDto {
  transactionId: number;
  status: string;
  remainingSeconds: number;
  receipt?: PaymentSuccessDto | null;
}

export interface PaymentHistoryItemDto {
  transactionId: number;
  receiptNumber: string;
  gatewayReference: string;
  method: string;
  totalAmount: number;
  status: string;
  paidTime: string;
  invoices: TransactionInvoiceDto[];
}

export interface PaymentSuccessDto {
  transactionId: number;
  receiptNumber: string;
  gatewayReference: string;
  totalAmount: number;
  paidTime: string;
  callbackTime: string;
  invoices: TransactionInvoiceDto[];
  sideEffectNotes: string[];
}

export interface UnlockedInvoiceDto {
  invoiceId: number;
  invoiceNumber: string;
  previousStatus: string;
  currentStatus: string;
}

export interface PaymentCancelResultDto {
  transactionId: number;
  status: string;
  cancelledTime: string;
  unlockedInvoices: UnlockedInvoiceDto[];
}

export interface PaymentNavigationParams {
  invoiceId?: number;
  sourceType?: PaymentSourceType;
  sourceId?: number;
  tabFilter?: InvoiceType;
}

/**
 * Điều hướng từ các màn hình nghiệp vụ (Booking, Keeping, Utility, BedTransfer, Checkout, Lịch sử lưu trú)
 * sang màn hình Chọn khoản thanh toán (Choose Paid Item) kèm query parameter trên URL.
 */
export function navigateToStudentPayment(params?: PaymentNavigationParams): void {
  if (typeof window === 'undefined') return;
  const search = new URLSearchParams();
  if (params?.invoiceId && params.invoiceId > 0) {
    search.set('invoiceId', String(params.invoiceId));
  } else if (params?.sourceType && params?.sourceId && params.sourceId > 0) {
    search.set('sourceType', params.sourceType);
    search.set('sourceId', String(params.sourceId));
  }
  if (params?.tabFilter) {
    search.set('invoiceType', params.tabFilter);
  }
  const qs = search.toString();
  const nextUrl = `${window.location.pathname}${qs ? `?${qs}` : ''}`;
  window.history.pushState({}, '', nextUrl);
  window.dispatchEvent(new CustomEvent<PaymentNavigationParams>('fud:navigate-payment', { detail: params || {} }));
}

async function studentPaymentRequest<T>(url: string, init: RequestInit | undefined, defaultError: string): Promise<T> {
  const res = await authFetch(url, init);
  if (res.status === 403) {
    throw new Error('Chức năng thanh toán hóa đơn chỉ dành cho tài khoản Sinh viên.');
  }
  const json = await safeParseResponse<T>(res, defaultError);
  if (!res.ok || json.success === false) {
    const detail = json.errors && json.errors.length > 0 ? json.errors.join('; ') : '';
    throw new Error(json.message || detail || defaultError);
  }
  return json.data;
}

export const paymentApi = {
  getUnpaidInvoices: (): Promise<StudentInvoiceListResponseDto> =>
    studentPaymentRequest<StudentInvoiceListResponseDto>(
      `${BASE_URL}/student/payments/unpaid-invoices`,
      undefined,
      'Không thể tải danh sách hóa đơn cần thanh toán'
    ),

  getInvoiceBySource: (sourceType: string, sourceId: number): Promise<StudentInvoiceDto> => {
    const query = new URLSearchParams({
      sourceType,
      sourceId: String(sourceId),
    });
    return studentPaymentRequest<StudentInvoiceDto>(
      `${BASE_URL}/student/payments/by-source?${query.toString()}`,
      undefined,
      'Không tìm thấy hóa đơn tương ứng với nguồn phát sinh'
    );
  },

  createTransaction: (invoiceIds: number[]): Promise<PaymentTransactionDto> =>
    studentPaymentRequest<PaymentTransactionDto>(
      `${BASE_URL}/student/payments/create`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ invoiceIds }),
      },
      'Không thể khởi tạo giao dịch VietQR'
    ),

  cancelTransaction: (transactionId: number, reason?: string): Promise<PaymentCancelResultDto> =>
    studentPaymentRequest<PaymentCancelResultDto>(
      `${BASE_URL}/student/payments/${transactionId}/cancel`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(reason ? { reason } : {}),
      },
      'Không thể hủy giao dịch thanh toán'
    ),

  getTransactionStatus: (transactionId: number): Promise<PaymentTransactionStatusDto> =>
    studentPaymentRequest<PaymentTransactionStatusDto>(
      `${BASE_URL}/student/payments/${transactionId}/status`,
      undefined,
      'Không thể kiểm tra trạng thái giao dịch'
    ),

  getPaymentHistory: async (): Promise<PaymentHistoryItemDto[]> => {
    const mergedMap = new Map<string, PaymentHistoryItemDto>();

    // 1. Thử lấy từ endpoint GET /api/student/payments/history
    try {
      const apiHistory = await studentPaymentRequest<PaymentHistoryItemDto[]>(
        `${BASE_URL}/student/payments/history`,
        undefined,
        'Không thể tải lịch sử giao dịch'
      );
      for (const item of apiHistory || []) {
        mergedMap.set(item.receiptNumber || `TX-${item.transactionId}`, item);
      }
    } catch {
      // Fallback nếu server chưa cập nhật endpoint /history
    }

    // 2. Gộp thêm các hóa đơn có trạng thái Paid trong Lịch sử lưu trú
    try {
      const coveredInvIds = new Set<number>();
      for (const h of mergedMap.values()) {
        for (const inv of h.invoices || []) {
          coveredInvIds.add(inv.invoiceId);
        }
      }

      const residencies = await residencyApi.getMyHistory();
      const details = await Promise.all(
        (residencies || []).slice(0, 5).map((r) => residencyApi.getMyHistoryById(r.id).catch(() => null))
      );
      for (const det of details) {
        if (!det?.invoices) continue;
        for (const inv of det.invoices) {
          if (inv.status === 'Paid' && !coveredInvIds.has(inv.id)) {
            coveredInvIds.add(inv.id);
            const key = `RC-INV-${inv.id}`;
            mergedMap.set(key, {
              transactionId: inv.id,
              receiptNumber: key,
              gatewayReference: inv.invoiceNumber,
              method: 'VietQR · SePay',
              totalAmount: inv.amount,
              status: 'Success',
              paidTime: inv.paidTime || inv.issuedTime,
              invoices: [
                {
                  invoiceId: inv.id,
                  invoiceNumber: inv.invoiceNumber,
                  invoiceType: inv.invoiceType,
                  allocatedAmount: inv.amount,
                  dueDate: inv.dueDate,
                  status: 'Paid',
                  items: [],
                },
              ],
            });
          }
        }
      }
    } catch {
      // ignore
    }

    return Array.from(mergedMap.values()).sort(
      (a, b) => new Date(b.paidTime).getTime() - new Date(a.paidTime).getTime()
    );
  },
};

