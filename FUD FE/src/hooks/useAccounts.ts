import { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import accountService from '../services/accountService';
import { extractErrorMessages } from '../services/http';
import type {
  AccountDetail,
  AccountListItem,
  AccountQuery,
  AccountStatusResult,
  AssignRolesPayload,
  CreateAccountPayload,
  PagedResult,
  RoleOption,
  SuspensionReason,
} from '../types/account';

const DEFAULT_PAGE_SIZE = 10;

/**
 * Hook quản lý toàn bộ state của màn hình Quản lý tài khoản:
 * danh sách, tìm kiếm/lọc/phân trang, chi tiết và các thao tác hành động.
 */
export function useAccounts() {
  const [items, setItems] = useState<AccountListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [page, setPageState] = useState(1);
  const [pageSize] = useState(DEFAULT_PAGE_SIZE);
  const [keyword, setKeywordState] = useState('');
  const [status, setStatusState] = useState<AccountQuery['status']>('');
  const [roleName, setRoleNameState] = useState<AccountQuery['roleName']>('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [roles, setRoles] = useState<RoleOption[]>([]);
  const [detail, setDetail] = useState<AccountDetail | null>(null);
  const [isDetailLoading, setIsDetailLoading] = useState(false);

  const query: AccountQuery = useMemo(
    () => ({ keyword, status, roleName, page, pageSize }),
    [keyword, status, roleName, page, pageSize]
  );

  const queryRef = useMemo(() => query, [query]);

  const fetchList = useCallback(async (q: AccountQuery) => {
    setIsLoading(true);
    setError(null);
    try {
      const result: PagedResult<AccountListItem> = await accountService.list(q);
      setItems(result.items ?? []);
      setTotalCount(result.totalCount ?? 0);
      setTotalPages(result.totalPages ?? 0);
    } catch (err) {
      const messages = extractErrorMessages(err);
      setError(messages.join(' '));
      toast.error(messages.join(' '));
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Nạp danh sách khi bộ lọc / trang thay đổi. Dùng microtask để không setState
  // đồng bộ trong thân effect (tránh cascading render).
  useEffect(() => {
    let cancelled = false;
    void Promise.resolve().then(async () => {
      if (cancelled) return;
      await fetchList(queryRef);
    });
    return () => {
      cancelled = true;
    };
  }, [fetchList, queryRef]);

  useEffect(() => {
    let cancelled = false;
    accountService
      .getRoles()
      .then((data) => {
        if (!cancelled) setRoles(data);
      })
      .catch(() => {
        /* danh sách vai trò không critical — bỏ qua */
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const setKeyword = useCallback((value: string) => {
    setKeywordState(value);
    setPageState(1);
  }, []);

  const setStatus = useCallback((value: AccountQuery['status']) => {
    setStatusState(value);
    setPageState(1);
  }, []);

  const setRoleName = useCallback((value: AccountQuery['roleName']) => {
    setRoleNameState(value);
    setPageState(1);
  }, []);

  const setPage = useCallback((value: number) => {
    setPageState(value);
  }, []);

  const loadDetail = useCallback(async (id: number) => {
    setIsDetailLoading(true);
    try {
      const data = await accountService.getById(id);
      setDetail(data);
      return data;
    } catch (err) {
      const messages = extractErrorMessages(err);
      toast.error(messages.join(' '));
      return null;
    } finally {
      setIsDetailLoading(false);
    }
  }, []);

  const createAccount = useCallback(
    async (payload: CreateAccountPayload) => {
      try {
        const created = await accountService.create(payload);
        toast.success(`Đã tạo tài khoản "${created.username}"`);
        await fetchList(queryRef);
        return created;
      } catch (err) {
        const messages = extractErrorMessages(err);
        toast.error(messages.join(' '));
        throw err;
      }
    },
    [fetchList, queryRef]
  );

  const assignRoles = useCallback(
    async (id: number, payload: AssignRolesPayload) => {
      try {
        const updated = await accountService.assignRoles(id, payload);
        toast.success('Cập nhật vai trò thành công');
        await fetchList(queryRef);
        setDetail((prev) => (prev?.id === id ? updated : prev));
        return updated;
      } catch (err) {
        const messages = extractErrorMessages(err);
        toast.error(messages.join(' '));
        throw err;
      }
    },
    [fetchList, queryRef]
  );

  const suspendAccount = useCallback(
    async (id: number, reason: SuspensionReason, notes?: string) => {
      try {
        const result: AccountStatusResult = await accountService.suspend(id, { reason, notes });
        toast.success(result.message || 'Đã tạm ngưng tài khoản');
        await fetchList(queryRef);
        if (detail?.id === id) await loadDetail(id);
        return result;
      } catch (err) {
        const messages = extractErrorMessages(err);
        toast.error(messages.join(' '));
        throw err;
      }
    },
    [detail, fetchList, loadDetail, queryRef]
  );

  const restoreAccount = useCallback(
    async (id: number, reason: string) => {
      try {
        const result: AccountStatusResult = await accountService.restore(id, { reason });
        toast.success(result.message || 'Đã khôi phục tài khoản');
        await fetchList(queryRef);
        if (detail?.id === id) await loadDetail(id);
        return result;
      } catch (err) {
        const messages = extractErrorMessages(err);
        toast.error(messages.join(' '));
        throw err;
      }
    },
    [detail, fetchList, loadDetail, queryRef]
  );

  return {
    items,
    totalCount,
    totalPages,
    page,
    pageSize,
    keyword,
    status,
    roleName,
    isLoading,
    error,
    roles,
    detail,
    isDetailLoading,
    setKeyword,
    setStatus,
    setRoleName,
    setPage,
    refresh: () => fetchList(queryRef),
    loadDetail,
    setDetail,
    createAccount,
    assignRoles,
    suspendAccount,
    restoreAccount,
  };
}

export default useAccounts;
