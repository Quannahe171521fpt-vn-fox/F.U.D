/** Kiểu dữ liệu dùng chung cho các response API. */

export interface ApiResponse<T = unknown> {
  success: boolean;
  message: string;
  data?: T;
  errors?: string[];
}
