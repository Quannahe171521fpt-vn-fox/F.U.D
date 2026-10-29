const BASE_URL = 'http://localhost:5145/api';

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

// ── Equipment API (UC28) ──────────────────────────────────
export const equipmentApi = {
  getAll: async (params?: { search?: string; condition?: string; page?: number; pageSize?: number }) => {
    const query = new URLSearchParams();
    if (params?.search) query.append('search', params.search);
    if (params?.condition && params.condition !== 'All') query.append('condition', params.condition);
    if (params?.page) query.append('page', params.page.toString());
    if (params?.pageSize) query.append('pageSize', params.pageSize.toString());

    const res = await fetch(`${BASE_URL}/facilities/equipment?${query.toString()}`);
    if (!res.ok) throw new Error('Không thể tải danh sách thiết bị');
    const json: ApiResponse<PagedResult<any>> = await res.json();
    return json.data;
  },

  getById: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/${id}`);
    const json = await res.json();
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
    const res = await fetch(`${BASE_URL}/facilities/equipment`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await res.json();
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
    const res = await fetch(`${BASE_URL}/facilities/equipment/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi cập nhật thiết bị');
    }
    return json.data;
  },

  move: async (id: number, payload: { roomId: number; bedId?: number | null }) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/${id}/move`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi di chuyển thiết bị');
    }
    return json.data;
  },

  changeCondition: async (id: number, condition: string) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/${id}/condition`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ condition }),
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi đổi tình trạng');
    }
    return json.data;
  },

  liquidate: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/${id}/liquidate`, {
      method: 'POST',
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi thanh lý thiết bị');
    }
    return json;
  },

  getRepairHistory: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/${id}/repair-history`);
    const json = await res.json();
    return json.data || [];
  },

  getCatalogs: async () => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/catalogs`);
    const json = await res.json();
    return json.data || [];
  },

  createCatalog: async (payload: { category: string; name: string }) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/catalogs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi thêm danh mục');
    }
    return json.data;
  },

  deleteCatalog: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/catalogs/${id}`, {
      method: 'DELETE',
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi xóa danh mục');
    }
    return json;
  },

  getLocations: async () => {
    const res = await fetch(`${BASE_URL}/facilities/equipment/locations`);
    const json = await res.json();
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

    const res = await fetch(`${BASE_URL}/facilities/news?${query.toString()}`);
    if (!res.ok) throw new Error('Không thể tải danh sách tin tức');
    const json: ApiResponse<PagedResult<any>> = await res.json();
    return json.data;
  },

  getPublished: async () => {
    const res = await fetch(`${BASE_URL}/facilities/news/published`);
    if (!res.ok) throw new Error('Không thể tải tin đã xuất bản');
    const json = await res.json();
    return json.data || [];
  },

  getById: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/news/${id}`);
    const json = await res.json();
    return json.data;
  },

  create: async (payload: {
    title: string;
    content: string;
    category?: string;
    action?: string; // 'Draft' | 'Publish'
    attachmentUrls?: string[];
  }) => {
    const res = await fetch(`${BASE_URL}/facilities/news`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await res.json();
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
    const res = await fetch(`${BASE_URL}/facilities/news/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi cập nhật tin tức');
    }
    return json.data;
  },

  delete: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/news/${id}`, {
      method: 'DELETE',
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi xóa bản tin');
    }
    return json;
  },

  publish: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/news/${id}/publish`, {
      method: 'POST',
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi xuất bản tin tức');
    }
    return json;
  },

  unpublish: async (id: number) => {
    const res = await fetch(`${BASE_URL}/facilities/news/${id}/unpublish`, {
      method: 'POST',
    });
    const json = await res.json();
    if (!res.ok || !json.success) {
      throw new Error(json.message || 'Lỗi khi gỡ xuất bản');
    }
    return json;
  },
};

// ── Dorm Info API (Database) ──────────────────────────────
export const dormInfoApi = {
  getBuildings: async () => {
    const res = await fetch(`${BASE_URL}/facilities/dorm-info/buildings`);
    const json = await res.json();
    return json.data || [];
  },

  getRoomTypes: async () => {
    const res = await fetch(`${BASE_URL}/facilities/dorm-info/room-types`);
    const json = await res.json();
    return json.data || [];
  },

  getFaqs: async () => {
    const res = await fetch(`${BASE_URL}/facilities/dorm-info/faqs`);
    const json = await res.json();
    return json.data || [];
  },
};

