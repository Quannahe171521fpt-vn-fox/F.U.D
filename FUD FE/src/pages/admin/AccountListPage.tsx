import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import useAccounts from '../../hooks/useAccounts';
import AssignRolesModal from '../../components/admin/AssignRolesModal';
import SuspendDialog from '../../components/admin/SuspendDialog';
import RestoreDialog from '../../components/admin/RestoreDialog';
import AccountDetailModal from '../../components/admin/AccountDetailModal';
import { ROLE_LABELS, STATUS_LABELS, formatDateTime } from '../../utils/accountMeta';
import type { AccountDetail, AccountStatus, SystemRole } from '../../types/account';

/* ---------- Inline micro-components ---------- */

function Badge({
  tone,
  children,
}: {
  tone: 'green' | 'amber' | 'rose' | 'violet' | 'sky' | 'slate' | 'teal';
  children: React.ReactNode;
}) {
  return <span className={`admin-badge admin-badge--${tone}`}>{children}</span>;
}

function Btn({
  variant = 'secondary',
  size = 'sm',
  disabled,
  onClick,
  children,
}: {
  variant?: 'primary' | 'secondary' | 'danger' | 'success';
  size?: 'sm' | 'md';
  disabled?: boolean;
  onClick?: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className={`a-btn a-btn--${size} a-btn--${variant}`}
    >
      {children}
    </button>
  );
}

/* ---------- Main page ---------- */

export function AccountListPage() {
  const {
    items,
    totalCount,
    totalPages,
    page,
    keyword,
    status,
    roleName,
    isLoading,
    roles,
    setKeyword,
    setStatus,
    setRoleName,
    setPage,
    loadDetail,
    assignRoles,
    suspendAccount,
    restoreAccount,
  } = useAccounts();

  const [keywordInput, setKeywordInput] = useState(keyword);
  const [detailTarget, setDetailTarget] = useState<AccountDetail | null>(null);
  const [isDetailLoading, setIsDetailLoading] = useState(false);
  const [rolesTarget, setRolesTarget] = useState<AccountDetail | null>(null);
  const [suspendTarget, setSuspendTarget] = useState<AccountDetail | null>(null);
  const [restoreTarget, setRestoreTarget] = useState<AccountDetail | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setKeyword(keywordInput), 350);
    return () => clearTimeout(timer);
  }, [keywordInput, setKeyword]);

  const statusOptions = useMemo(
    () => [
      { value: '', label: 'Tất cả trạng thái' },
      { value: 'Active', label: STATUS_LABELS.Active },
      { value: 'Pending', label: STATUS_LABELS.Pending },
      { value: 'Suspended', label: STATUS_LABELS.Suspended },
    ],
    []
  );

  const roleOptions = useMemo(
    () => [
      { value: '', label: 'Tất cả vai trò' },
      ...(['Student', 'Manager', 'Guard', 'Admin'] as SystemRole[]).map((r) => ({
        value: r,
        label: ROLE_LABELS[r],
      })),
    ],
    []
  );

  const openDetail = async (id: number) => {
    setIsDetailLoading(true);
    const data = await loadDetail(id);
    setIsDetailLoading(false);
    if (data) setDetailTarget(data);
  };

  const openRoles = async (id: number) => {
    const data = await loadDetail(id);
    if (data) setRolesTarget(data);
  };

  const openSuspend = async (id: number) => {
    const data = await loadDetail(id);
    if (data) setSuspendTarget(data);
  };

  const openRestore = async (id: number) => {
    const data = await loadDetail(id);
    if (data) setRestoreTarget(data);
  };

  const handleAssignRoles = async (roleNames: SystemRole[]) => {
    if (!rolesTarget) return;
    setIsSaving(true);
    try { await assignRoles(rolesTarget.id, { roleNames }); }
    finally { setIsSaving(false); }
  };

  const handleSuspend = async (reason: 'Violation' | 'Graduation' | 'Termination' | 'Other', notes?: string) => {
    if (!suspendTarget) return;
    setIsSaving(true);
    try { await suspendAccount(suspendTarget.id, reason, notes); }
    finally { setIsSaving(false); }
  };

  const handleRestore = async (reason: string) => {
    if (!restoreTarget) return;
    setIsSaving(true);
    try { await restoreAccount(restoreTarget.id, reason); }
    finally { setIsSaving(false); }
  };

  return (
    <>
      {/* Page header */}
      <div className="admin-page-header">
        <div>
          <h1 className="admin-title">Tài khoản</h1>
          <p className="admin-subtitle">Theo dõi quyền truy cập và trạng thái người dùng trong ký túc xá.</p>
        </div>
        <Link to="/admin/accounts/new">
          <button type="button" className="a-btn a-btn--md a-btn--primary">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" aria-hidden="true">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Tạo tài khoản
          </button>
        </Link>
      </div>

      {/* Page body */}
      <div className="admin-page-body">
        {/* Toolbar */}
        <div className="admin-toolbar">
          <input
            value={keywordInput}
            onChange={(e) => setKeywordInput(e.target.value)}
            placeholder="Tìm username, họ tên hoặc email…"
            className="admin-control"
            aria-label="Tìm kiếm tài khoản"
          />
          <select
            value={status}
            onChange={(e) => setStatus(e.target.value as AccountStatus | '')}
            className="admin-control"
            aria-label="Lọc theo trạng thái"
          >
            {statusOptions.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          <select
            value={roleName}
            onChange={(e) => setRoleName(e.target.value as SystemRole | '')}
            className="admin-control"
            aria-label="Lọc theo vai trò"
          >
            {roleOptions.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>

        {/* Table panel */}
        <div className="admin-table-panel">
          {isLoading ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: '56px 0' }}>
              <span
                className="a-spinner"
                style={{ width: 24, height: 24, color: 'var(--a-accent)' }}
                aria-label="Đang tải…"
              />
            </div>
          ) : items.length === 0 ? (
            <div className="admin-empty">
              <div className="admin-empty-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" aria-hidden="true">
                  <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
                </svg>
              </div>
              <p className="admin-empty-title">Không tìm thấy tài khoản nào</p>
              <p className="admin-empty-desc">Thử thay đổi từ khóa tìm kiếm hoặc bộ lọc trạng thái / vai trò.</p>
              <Link to="/admin/accounts/new" style={{ marginTop: 8 }}>
                <button type="button" className="a-btn a-btn--sm a-btn--primary">Tạo tài khoản</button>
              </Link>
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table className="admin-table" style={{ minWidth: 880 }}>
                <thead>
                  <tr>
                    <th>Tài khoản</th>
                    <th>Email</th>
                    <th>Vai trò</th>
                    <th>Trạng thái</th>
                    <th>Ngày tạo</th>
                    <th style={{ textAlign: 'right' }}>Hành động</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <button type="button" onClick={() => void openDetail(item.id)} style={{ textAlign: 'left', background: 'none', border: 'none', padding: 0, cursor: 'pointer' }}>
                          <span className="account-name">{item.fullName}</span>
                          <span className="account-username">@{item.username}</span>
                        </button>
                      </td>
                      <td style={{ color: 'var(--a-ink-2)', fontSize: 13 }}>{item.email}</td>
                      <td>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                          {item.roles.map((role) => (
                            <Badge key={role} tone="violet">
                              {ROLE_LABELS[role] ?? role}
                            </Badge>
                          ))}
                        </div>
                      </td>
                      <td>
                        <Badge
                          tone={
                            item.status === 'Active' ? 'green'
                              : item.status === 'Suspended' ? 'rose'
                              : 'amber'
                          }
                        >
                          {STATUS_LABELS[item.status] ?? item.status}
                        </Badge>
                      </td>
                      <td style={{ fontSize: 12.5, color: 'var(--a-ink-3)', whiteSpace: 'nowrap' }}>
                        {formatDateTime(item.createdAt)}
                      </td>
                      <td>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 6 }}>
                          <Btn size="sm" variant="secondary" onClick={() => void openRoles(item.id)}>
                            Phân quyền
                          </Btn>
                          {item.status === 'Suspended' ? (
                            <Btn size="sm" variant="success" onClick={() => void openRestore(item.id)}>
                              Khôi phục
                            </Btn>
                          ) : (
                            <Btn size="sm" variant="danger" onClick={() => void openSuspend(item.id)}>
                              Tạm ngưng
                            </Btn>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Pagination */}
          {!isLoading && items.length > 0 && (
            <div className="admin-pagination">
              <p className="admin-pagination-info">
                Trang {page} / {Math.max(totalPages, 1)} — {totalCount} tài khoản
              </p>
              <div style={{ display: 'flex', gap: 6 }}>
                <Btn size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  ← Trước
                </Btn>
                <Btn size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                  Sau →
                </Btn>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Modals */}
      {detailTarget && (
        <AccountDetailModal
          isOpen
          onClose={() => setDetailTarget(null)}
          account={detailTarget}
          isLoading={isDetailLoading}
          onEditRoles={() => {
            setRolesTarget(detailTarget);
            setDetailTarget(null);
          }}
        />
      )}

      {rolesTarget && (
        <AssignRolesModal
          isOpen
          onClose={() => setRolesTarget(null)}
          account={rolesTarget}
          roleOptions={roles}
          isSaving={isSaving}
          onSubmit={handleAssignRoles}
        />
      )}

      {suspendTarget && (
        <SuspendDialog
          isOpen
          onClose={() => setSuspendTarget(null)}
          account={suspendTarget}
          isSaving={isSaving}
          onSubmit={handleSuspend}
        />
      )}

      {restoreTarget && (
        <RestoreDialog
          isOpen
          onClose={() => setRestoreTarget(null)}
          account={restoreTarget}
          isSaving={isSaving}
          onSubmit={handleRestore}
        />
      )}
    </>
  );
}

export default AccountListPage;
