import http from './http';
import type {
  AccountDetail,
  AccountListItem,
  AccountQuery,
  AccountStatusResult,
  ApiResponse,
  AssignRolesPayload,
  CreateAccountPayload,
  PagedResult,
  RestoreAccountPayload,
  RoleOption,
  SuspendAccountPayload,
} from '../types/account';

/**
 * Tất cả lời gọi API của module Account Management đều đi qua service này.
 * Component/page không gọi Axios trực tiếp.
 */

function unwrap<T>(response: { data: ApiResponse<T> }): T {
  const body = response.data;
  if (!body?.success) {
    throw new Error(body?.message || 'Thao tác thất bại');
  }
  return body.data as T;
}

export const accountService = {
  /** Danh sách tài khoản có tìm kiếm, lọc và phân trang. */
  async list(query: AccountQuery = {}): Promise<PagedResult<AccountListItem>> {
    const response = await http.get<ApiResponse<PagedResult<AccountListItem>>>('/admin/users', {
      params: {
        keyword: query.keyword || undefined,
        status: query.status || undefined,
        roleName: query.roleName || undefined,
        page: query.page ?? 1,
        pageSize: query.pageSize ?? 10,
      },
    });
    return unwrap(response);
  },

  /** Chi tiết tài khoản kèm lịch sử trạng thái và lịch sử vai trò. */
  async getById(id: number): Promise<AccountDetail> {
    const response = await http.get<ApiResponse<AccountDetail>>(`/admin/users/${id}`);
    return unwrap(response);
  },

  /** Danh sách vai trò hệ thống có thể cấp. */
  async getRoles(): Promise<RoleOption[]> {
    const response = await http.get<ApiResponse<RoleOption[]>>('/admin/users/roles');
    return unwrap(response);
  },

  /** Tạo tài khoản mới. */
  async create(payload: CreateAccountPayload): Promise<AccountDetail> {
    const response = await http.post<ApiResponse<AccountDetail>>('/admin/users', payload);
    return unwrap(response);
  },

  /** Cấp / đổi / thu hồi vai trò. */
  async assignRoles(id: number, payload: AssignRolesPayload): Promise<AccountDetail> {
    const response = await http.put<ApiResponse<AccountDetail>>(`/admin/users/${id}/roles`, payload);
    return unwrap(response);
  },

  /** Tạm ngưng tài khoản. */
  async suspend(id: number, payload: SuspendAccountPayload): Promise<AccountStatusResult> {
    const response = await http.post<ApiResponse<AccountStatusResult>>(`/admin/users/${id}/suspend`, payload);
    return unwrap(response);
  },

  /** Khôi phục tài khoản đang tạm ngưng. */
  async restore(id: number, payload: RestoreAccountPayload): Promise<AccountStatusResult> {
    const response = await http.post<ApiResponse<AccountStatusResult>>(`/admin/users/${id}/restore`, payload);
    return unwrap(response);
  },
};

export default accountService;
