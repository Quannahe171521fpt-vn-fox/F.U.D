/** Kiểu dữ liệu tài khoản — khớp với DTO của backend. */

export type { ApiResponse } from './api';

export type AccountStatus = 'Pending' | 'Active' | 'Suspended';

export type SystemRole = 'Student' | 'Manager' | 'Guard' | 'Admin';

export type SuspensionReason = 'Violation' | 'Graduation' | 'Termination' | 'Other';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface AccountListItem {
  id: number;
  username: string;
  fullName: string;
  email: string;
  phone?: string | null;
  status: AccountStatus;
  roles: SystemRole[];
  createdAt: string;
  suspendedAt?: string | null;
  suspensionReason?: string | null;
}

export interface AccountStatusHistory {
  id: number;
  oldStatus: AccountStatus;
  newStatus: AccountStatus;
  reason?: string | null;
  notes?: string | null;
  changedById: number;
  changedByName?: string | null;
  changedAt: string;
  notificationStatus: 'None' | 'Sent' | 'Failed';
}

export interface RoleChangeHistory {
  id: number;
  action: 'Grant' | 'Adjust' | 'Revoke';
  oldRoles?: string | null;
  newRoles?: string | null;
  changedById: number;
  changedByName?: string | null;
  changedAt: string;
}

export interface AccountDetail {
  id: number;
  username: string;
  fullName: string;
  email: string;
  phone?: string | null;
  status: AccountStatus;
  roles: SystemRole[];
  createdAt: string;
  updatedAt: string;
  suspendedAt?: string | null;
  suspensionReason?: string | null;
  suspendedById?: number | null;
  suspendedByName?: string | null;
  statusHistories: AccountStatusHistory[];
  roleHistories: RoleChangeHistory[];
}

export interface RoleOption {
  id: number;
  name: SystemRole;
  description?: string | null;
}

export interface AccountStatusResult {
  id: number;
  username: string;
  status: AccountStatus;
  roles: SystemRole[];
  suspendedAt?: string | null;
  suspensionReason?: string | null;
  notificationStatus: 'None' | 'Sent' | 'Failed';
  message: string;
}

export interface AccountQuery {
  keyword?: string;
  status?: AccountStatus | '';
  roleName?: SystemRole | '';
  page?: number;
  pageSize?: number;
}

export interface CreateAccountPayload {
  username: string;
  fullName: string;
  email: string;
  phone?: string;
  password: string;
  roleName: SystemRole;
}

export interface AssignRolesPayload {
  roleNames: SystemRole[];
}

export interface SuspendAccountPayload {
  reason: SuspensionReason;
  notes?: string;
}

export interface RestoreAccountPayload {
  reason: string;
}
