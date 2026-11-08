import type {
  TermManagementItem,
  TermDetailItem,
  CreateTermPayload,
  UpdateTermPayload,
  SaveSchedulePayload,
} from '../types/management';
import { authFetch } from './apiClient';
const BASE_URL = '/api';

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
  if (res.status === 403) {
    throw new Error('Bạn không có quyền truy cập chức năng này (Yêu cầu tài khoản Ban Quản lý hoặc Admin).');
  }

  let text = '';
  try {
    text = await res.text();
  } catch {
    throw new Error(`${defaultError} (Mã lỗi: ${res.status})`);
  }

  if (!text || !text.trim()) {
    if (!res.ok) {
      throw new Error(`${defaultError} (Mã lỗi: ${res.status})`);
    }
    return { success: true, message: '', data: null as any };
  }

  try {
    return JSON.parse(text);
  } catch {
    throw new Error(`${defaultError} (Mã phản hồi: ${res.status})`);
  }
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

  getRepairHistory: async (id: number) => {
    const res = await authFetch(`${BASE_URL}/facilities/equipment/${id}/repair-history`);
    const json = await parseJsonSafe(res);
    return json.data || [];
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
    if (!res.ok) throw new Error('Không thể tải danh sách tòa');
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
    const json = await safeParseResponse<PagedResult<BlockDto>>(res, 'Không thể tải danh sách tòa nhà');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách tòa nhà');
    return json.data;
  },

  getById: async (id: number): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}`);
    const json = await safeParseResponse<BlockDto>(res, 'Không thể tải thông tin tòa nhà');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải thông tin tòa nhà');
    return json.data;
  },

  getRooms: async (id: number): Promise<RoomDto[]> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}/rooms`);
    const json = await safeParseResponse<RoomDto[]>(res, 'Không thể tải danh sách phòng của tòa');
    if (!res.ok || !json.success) throw new Error(json.message || 'Không thể tải danh sách phòng của tòa');
    return json.data;
  },

  create: async (payload: CreateBlockPayload): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi tạo tòa nhà');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi tạo tòa nhà');
    return json.data;
  },

  update: async (id: number, payload: UpdateBlockPayload): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi cập nhật tòa nhà');
    if (!res.ok || !json.success) throw new Error(json.message || (json.errors?.join(', ')) || 'Lỗi khi cập nhật tòa nhà');
    return json.data;
  },

  updateStatus: async (id: number, status: 'Active' | 'Maintenance'): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi đổi trạng thái tòa nhà');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi trạng thái tòa nhà');
    return json.data;
  },

  updateGender: async (id: number, genderType: 'Male' | 'Female'): Promise<BlockDto> => {
    const res = await authFetch(`${BASE_URL}/blocks/${id}/gender`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ genderType }),
    });
    const json = await safeParseResponse<BlockDto>(res, 'Lỗi khi đổi giới tính tòa nhà');
    if (!res.ok || !json.success) throw new Error(json.message || 'Lỗi khi đổi giới tính tòa nhà');
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


// ── Requests API (Booking, BedTransfer, Checkout, Complaint, Others) ───────────
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
    content: string;
    resolutionNote?: string;
    evidences: string[];
  };
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

  create: async (formData: FormData): Promise<RequestItemDto> => {
    const res = await authFetch(`${BASE_URL}/requests`, {
      method: 'POST',
      body: formData,
    });
    const json = await parseJsonSafe(res);
    if (!res.ok) throw new Error(json?.message || 'Không thể gửi đơn yêu cầu');
    return json?.data || json;
  },

  process: async (id: number, payload: { decision: string; note?: string; adjustedLevel?: string }): Promise<RequestItemDto> => {
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
};
