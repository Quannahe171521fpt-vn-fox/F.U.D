import React, { useState, useEffect, useCallback } from 'react';
import {
  accountApi,
  type AccountItem,
  type AccountDetail,
  type CreateAccountPayload,
} from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime } from '../../utils/dateUtils';
import { useAuth } from '../../contexts/AuthContext';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

const ALL_ROLES = ['Student', 'Manager', 'Guard', 'Admin'] as const;

const ROLE_LABELS: Record<string, string> = {
  Student: 'Sinh viên',
  Manager: 'Ban quản lý',
  Guard: 'Bảo vệ',
  Admin: 'Quản trị viên',
};

const STATUS_META: Record<string, { label: string; bg: string; color: string }> = {
  Active: { label: 'Hoạt động', bg: '#DCFCE7', color: '#15803D' },
  Pending: { label: 'Chờ kích hoạt', bg: '#FEF3C7', color: '#B45309' },
  Suspended: { label: 'Tạm ngưng', bg: '#FEE2E2', color: '#B91C1C' },
};

const SUSPEND_REASONS: { value: string; label: string }[] = [
  { value: 'violation', label: 'Vi phạm quy chế' },
  { value: 'graduation', label: 'Tốt nghiệp' },
  { value: 'termination', label: 'Chấm dứt hợp đồng / nội trú' },
  { value: 'other', label: 'Lý do khác' },
];

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '8px 12px',
  borderRadius: '6px',
  border: '1px solid #CBD5E1',
  fontSize: '13px',
  backgroundColor: '#FFF',
  boxSizing: 'border-box',
};

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: '11px',
  fontWeight: 700,
  color: '#475569',
  marginBottom: 4,
};

const primaryBtn: React.CSSProperties = {
  padding: '8px 16px',
  backgroundColor: '#EA580C',
  color: '#FFF',
  border: 'none',
  borderRadius: '6px',
  fontWeight: 600,
  fontSize: '13px',
  cursor: 'pointer',
};

const secondaryBtn: React.CSSProperties = {
  padding: '8px 16px',
  backgroundColor: '#F1F5F9',
  color: '#334155',
  border: '1px solid #CBD5E1',
  borderRadius: '6px',
  fontWeight: 600,
  fontSize: '13px',
  cursor: 'pointer',
};

const smallBtn = (bg: string, color: string, border = 'none'): React.CSSProperties => ({
  padding: '5px 10px',
  backgroundColor: bg,
  color,
  border,
  borderRadius: '6px',
  fontSize: '12px',
  fontWeight: 600,
  cursor: 'pointer',
  whiteSpace: 'nowrap',
});

const EMPTY_CREATE: CreateAccountPayload & { roles: string[] } = {
  username: '',
  fullName: '',
  email: '',
  password: '',
  phone: '',
  gender: '',
  roles: ['Student'],
};

type ModalKind = 'create' | 'detail' | 'roles' | 'suspend' | 'restore' | null;

const Modal: React.FC<{ title: string; subtitle: string; onClose: () => void; maxWidth?: number; children: React.ReactNode }> = ({
  title,
  subtitle,
  onClose,
  maxWidth = 560,
  children,
}) => (
  <div
    style={{
      position: 'fixed',
      inset: 0,
      backgroundColor: 'rgba(15, 23, 42, 0.5)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 9999,
      padding: '20px',
    }}
  >
    <div
      style={{
        backgroundColor: '#FFFFFF',
        borderRadius: '12px',
        maxWidth,
        width: '100%',
        maxHeight: '90vh',
        overflowY: 'auto',
        padding: '24px',
        boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <div>
          <span style={{ fontSize: '11px', fontWeight: 700, color: '#C2410C', textTransform: 'uppercase' }}>{subtitle}</span>
          <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>{title}</h3>
        </div>
        <button
          type="button"
          onClick={onClose}
          style={{ background: 'none', border: 'none', fontSize: '14px', cursor: 'pointer', color: '#94A3B8' }}
        >
          Đóng
        </button>
      </div>
      {children}
    </div>
  </div>
);

const RolePicker: React.FC<{ value: string[]; onChange: (roles: string[]) => void; disabled?: boolean }> = ({
  value,
  onChange,
  disabled,
}) => (
  <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
    {ALL_ROLES.map(role => {
      const checked = value.includes(role);
      return (
        <label
          key={role}
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '6px 12px',
            borderRadius: '9999px',
            fontSize: '12px',
            fontWeight: 600,
            cursor: disabled ? 'not-allowed' : 'pointer',
            border: `1px solid ${checked ? '#EA580C' : '#CBD5E1'}`,
            backgroundColor: checked ? '#FFF7ED' : '#FFF',
            color: checked ? '#C2410C' : '#475569',
          }}
        >
          <input
            type="checkbox"
            checked={checked}
            disabled={disabled}
            onChange={() => onChange(checked ? value.filter(r => r !== role) : [...value, role])}
          />
          {ROLE_LABELS[role]}
        </label>
      );
    })}
  </div>
);

const StatusBadge: React.FC<{ status: string }> = ({ status }) => {
  const meta = STATUS_META[status] || { label: status, bg: '#F1F5F9', color: '#475569' };
  return (
    <span
      style={{
        display: 'inline-block',
        padding: '4px 10px',
        borderRadius: '9999px',
        fontSize: '11px',
        fontWeight: 600,
        backgroundColor: meta.bg,
        color: meta.color,
        whiteSpace: 'nowrap',
      }}
    >
      {meta.label}
    </span>
  );
};

export const AccountManagement: React.FC = () => {
  const { currentUser } = useAuth();

  const [accounts, setAccounts] = useState<AccountItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Lọc & phân trang (server-side)
  const [searchInput, setSearchInput] = useState('');
  const [searchTerm, setSearchTerm] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [filterRole, setFilterRole] = useState('');
  const [currentPage, setCurrentPage] = useState(1);

  // Modal
  const [modal, setModal] = useState<ModalKind>(null);
  const [selected, setSelected] = useState<AccountItem | null>(null);
  const [detail, setDetail] = useState<AccountDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [modalError, setModalError] = useState('');

  // Form state
  const [createForm, setCreateForm] = useState(EMPTY_CREATE);
  const [editRoles, setEditRoles] = useState<string[]>([]);
  const [suspendReason, setSuspendReason] = useState('violation');
  const [suspendNotes, setSuspendNotes] = useState('');
  const [restoreReason, setRestoreReason] = useState('');

  // Debounce tìm kiếm
  useEffect(() => {
    const t = setTimeout(() => {
      setSearchTerm(searchInput.trim());
      setCurrentPage(1);
    }, 350);
    return () => clearTimeout(t);
  }, [searchInput]);

  const loadAccounts = useCallback(async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      const data = await accountApi.getAll({
        search: searchTerm || undefined,
        status: filterStatus || undefined,
        role: filterRole || undefined,
        page: currentPage,
        pageSize: PAGE_SIZE,
      });
      setAccounts(data.items);
      setTotalCount(data.totalCount);
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể tải danh sách tài khoản');
    } finally {
      setLoading(false);
    }
  }, [searchTerm, filterStatus, filterRole, currentPage]);

  useEffect(() => {
    loadAccounts();
  }, [loadAccounts]);

  const closeModal = () => {
    setModal(null);
    setSelected(null);
    setDetail(null);
    setModalError('');
  };

  const openCreate = () => {
    setCreateForm(EMPTY_CREATE);
    setModalError('');
    setSuccessMsg('');
    setModal('create');
  };

  const openDetail = async (acc: AccountItem) => {
    setSelected(acc);
    setDetail(null);
    setModalError('');
    setModal('detail');
    setDetailLoading(true);
    try {
      setDetail(await accountApi.getById(acc.id));
    } catch (err: any) {
      setModalError(err.message || 'Không thể tải chi tiết tài khoản');
    } finally {
      setDetailLoading(false);
    }
  };

  const openRoles = (acc: AccountItem) => {
    setSelected(acc);
    setEditRoles([...acc.roles]);
    setModalError('');
    setSuccessMsg('');
    setModal('roles');
  };

  const openSuspend = (acc: AccountItem) => {
    setSelected(acc);
    setSuspendReason('violation');
    setSuspendNotes('');
    setModalError('');
    setSuccessMsg('');
    setModal('suspend');
  };

  const openRestore = (acc: AccountItem) => {
    setSelected(acc);
    setRestoreReason('');
    setModalError('');
    setSuccessMsg('');
    setModal('restore');
  };

  const runAction = async (action: () => Promise<unknown>, okMsg: string) => {
    setSubmitting(true);
    setModalError('');
    try {
      await action();
      setSuccessMsg(okMsg);
      closeModal();
      await loadAccounts();
    } catch (err: any) {
      setModalError(err.message || 'Thao tác thất bại');
    } finally {
      setSubmitting(false);
    }
  };

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    const f = createForm;
    if (!f.username.trim() || !f.fullName.trim() || !f.email.trim() || !f.password) {
      setModalError('Vui lòng nhập đầy đủ Username, Họ tên, Email và Mật khẩu.');
      return;
    }
    if (f.password.length < 8 || !/[A-Z]/.test(f.password) || !/[a-z]/.test(f.password) || !/[0-9]/.test(f.password)) {
      setModalError('Mật khẩu tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và chữ số.');
      return;
    }
    if (f.roles.length === 0) {
      setModalError('Cần chọn ít nhất một vai trò ban đầu.');
      return;
    }
    runAction(
      () =>
        accountApi.create({
          username: f.username.trim(),
          fullName: f.fullName.trim(),
          email: f.email.trim(),
          password: f.password,
          phone: f.phone?.trim() || undefined,
          gender: f.gender || undefined,
          roles: f.roles,
        }),
      `Đã tạo tài khoản ${f.username.trim()} thành công.`
    );
  };

  const handleAssignRoles = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selected) return;
    if (editRoles.length === 0 && !window.confirm('Bạn đang thu hồi TOÀN BỘ vai trò của tài khoản này. Tiếp tục?')) return;
    runAction(() => accountApi.assignRoles(selected.id, editRoles), `Đã cập nhật vai trò cho ${selected.username}.`);
  };

  const handleSuspend = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selected) return;
    runAction(
      () => accountApi.suspend(selected.id, { reason: suspendReason, notes: suspendNotes.trim() || undefined }),
      `Đã tạm ngưng tài khoản ${selected.username}.`
    );
  };

  const handleRestore = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selected) return;
    runAction(
      () => accountApi.restore(selected.id, { reason: restoreReason.trim() || undefined }),
      `Đã khôi phục tài khoản ${selected.username}.`
    );
  };

  const isSelf = (acc: AccountItem) => currentUser?.id === acc.id;

  const modalErrorBox = modalError && (
    <div className="violation-alert violation-alert-error" style={{ marginBottom: 12 }}>
      {modalError}
    </div>
  );

  const infoBox: React.CSSProperties = {
    backgroundColor: '#F8FAFC',
    borderRadius: '8px',
    padding: '14px',
    fontSize: '13px',
    lineHeight: 1.8,
    marginBottom: 16,
  };

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* Tiêu đề */}
      <div style={{ marginBottom: 20, display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
            Quản lý tài khoản người dùng
          </h1>
          <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
            Tạo tài khoản, phân quyền vai trò, tạm ngưng và khôi phục tài khoản trong hệ thống KTX FUD
          </p>
        </div>
        <button type="button" style={primaryBtn} onClick={openCreate}>
          + Tạo tài khoản
        </button>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

      {/* Bộ lọc */}
      <div
        style={{
          display: 'flex',
          gap: '12px',
          alignItems: 'center',
          backgroundColor: '#FFFFFF',
          padding: '16px 20px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
          marginBottom: 16,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ minWidth: '180px' }}>
          <label style={labelStyle}>Vai trò</label>
          <select
            value={filterRole}
            onChange={e => {
              setFilterRole(e.target.value);
              setCurrentPage(1);
            }}
            style={inputStyle}
          >
            <option value="">Tất cả vai trò</option>
            {ALL_ROLES.map(r => (
              <option key={r} value={r}>{ROLE_LABELS[r]}</option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: '180px' }}>
          <label style={labelStyle}>Trạng thái</label>
          <select
            value={filterStatus}
            onChange={e => {
              setFilterStatus(e.target.value);
              setCurrentPage(1);
            }}
            style={inputStyle}
          >
            <option value="">Tất cả trạng thái</option>
            {Object.entries(STATUS_META).map(([k, v]) => (
              <option key={k} value={k}>{v.label}</option>
            ))}
          </select>
        </div>

        <div style={{ flex: 1, minWidth: '240px' }}>
          <label style={labelStyle}>Tìm theo username, họ tên, email</label>
          <input
            type="text"
            placeholder="Nhập thông tin tìm kiếm..."
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
            style={inputStyle}
          />
        </div>

        <div style={{ alignSelf: 'flex-end' }}>
          <button type="button" style={primaryBtn} onClick={loadAccounts}>
            Làm mới
          </button>
        </div>
      </div>

      {/* Bảng */}
      <div className="fud-table-wrapper">
        <table className="fud-table">
          <colgroup>
            <col style={{ width: '12%' }} />
            <col style={{ width: '20%' }} />
            <col style={{ width: '20%' }} />
            <col style={{ width: '16%' }} />
            <col style={{ width: '12%' }} />
            <col style={{ width: '20%' }} />
          </colgroup>
          <thead>
            <tr>
              <th>Username</th>
              <th>Họ tên</th>
              <th>Email</th>
              <th>Vai trò</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Đang nạp danh sách tài khoản...
                </td>
              </tr>
            ) : accounts.length === 0 ? (
              <tr>
                <td colSpan={6} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Không có tài khoản nào phù hợp.
                </td>
              </tr>
            ) : (
              accounts.map(acc => (
                <tr key={acc.id}>
                  <td>
                    <span className="cell-text-1line" title={acc.username} style={{ fontWeight: 700, color: '#C2410C' }}>
                      {acc.username}
                    </span>
                  </td>
                  <td>
                    <span className="cell-text-1line" title={acc.fullName} style={{ fontWeight: 600 }}>
                      {acc.fullName}
                    </span>
                  </td>
                  <td>
                    <span className="cell-text-1line" title={acc.email}>{acc.email}</span>
                  </td>
                  <td>
                    <span
                      className="cell-text-1line"
                      title={acc.roles.map(r => ROLE_LABELS[r] || r).join(', ')}
                      style={{ fontWeight: 600, color: '#0F172A' }}
                    >
                      {acc.roles.length > 0 ? acc.roles.map(r => ROLE_LABELS[r] || r).join(', ') : '—'}
                    </span>
                  </td>
                  <td>
                    <StatusBadge status={acc.status} />
                  </td>
                  <td>
                    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                      <button type="button" style={smallBtn('#F1F5F9', '#334155', '1px solid #CBD5E1')} onClick={() => openDetail(acc)}>
                        Chi tiết
                      </button>
                      <button type="button" style={smallBtn('#FFF7ED', '#C2410C', '1px solid #FED7AA')} onClick={() => openRoles(acc)}>
                        Vai trò
                      </button>
                      {acc.status === 'Suspended' ? (
                        <button type="button" style={smallBtn('#15803D', '#FFF')} onClick={() => openRestore(acc)}>
                          Khôi phục
                        </button>
                      ) : (
                        <button
                          type="button"
                          style={{ ...smallBtn('#DC2626', '#FFF'), opacity: isSelf(acc) ? 0.4 : 1 }}
                          disabled={isSelf(acc)}
                          title={isSelf(acc) ? 'Không thể tạm ngưng chính mình' : undefined}
                          onClick={() => openSuspend(acc)}
                        >
                          Tạm ngưng
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <Pagination
        currentPage={currentPage}
        totalItems={totalCount}
        pageSize={PAGE_SIZE}
        onPageChange={setCurrentPage}
        itemName="tài khoản"
      />

      {/* Modal tạo tài khoản */}
      {modal === 'create' && (
        <Modal title="Tạo tài khoản mới" subtitle="Quản lý tài khoản" onClose={closeModal}>
          <form onSubmit={handleCreate}>
            {modalErrorBox}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
              <div>
                <label style={labelStyle}>Username *</label>
                <input
                  style={inputStyle}
                  value={createForm.username}
                  maxLength={50}
                  onChange={e => setCreateForm(f => ({ ...f, username: e.target.value }))}
                />
              </div>
              <div>
                <label style={labelStyle}>Họ tên *</label>
                <input
                  style={inputStyle}
                  value={createForm.fullName}
                  maxLength={100}
                  onChange={e => setCreateForm(f => ({ ...f, fullName: e.target.value }))}
                />
              </div>
              <div>
                <label style={labelStyle}>Email *</label>
                <input
                  type="email"
                  style={inputStyle}
                  value={createForm.email}
                  maxLength={100}
                  onChange={e => setCreateForm(f => ({ ...f, email: e.target.value }))}
                />
              </div>
              <div>
                <label style={labelStyle}>Mật khẩu khởi tạo *</label>
                <input
                  type="password"
                  autoComplete="new-password"
                  style={inputStyle}
                  value={createForm.password}
                  onChange={e => setCreateForm(f => ({ ...f, password: e.target.value }))}
                />
              </div>
              <div>
                <label style={labelStyle}>Số điện thoại</label>
                <input
                  style={inputStyle}
                  value={createForm.phone}
                  maxLength={20}
                  onChange={e => setCreateForm(f => ({ ...f, phone: e.target.value }))}
                />
              </div>
              <div>
                <label style={labelStyle}>Giới tính</label>
                <select
                  style={inputStyle}
                  value={createForm.gender}
                  onChange={e => setCreateForm(f => ({ ...f, gender: e.target.value }))}
                >
                  <option value="">Không xác định</option>
                  <option value="Male">Nam</option>
                  <option value="Female">Nữ</option>
                </select>
              </div>
            </div>
            <div style={{ fontSize: '11px', color: '#64748B', marginBottom: 12 }}>
              Mật khẩu tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và chữ số.
            </div>
            <div style={{ marginBottom: 20 }}>
              <label style={labelStyle}>Vai trò ban đầu *</label>
              <RolePicker value={createForm.roles} onChange={roles => setCreateForm(f => ({ ...f, roles }))} />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <button type="button" style={secondaryBtn} onClick={closeModal} disabled={submitting}>Hủy</button>
              <button type="submit" style={primaryBtn} disabled={submitting}>
                {submitting ? 'Đang tạo...' : 'Tạo tài khoản'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Modal chi tiết */}
      {modal === 'detail' && selected && (
        <Modal title={`${selected.fullName} (${selected.username})`} subtitle="Chi tiết tài khoản" onClose={closeModal} maxWidth={680}>
          {modalErrorBox}
          {detailLoading || !detail ? (
            !modalError && <div style={{ padding: 20, textAlign: 'center', color: '#64748B' }}>Đang tải chi tiết...</div>
          ) : (
            <>
              <div style={infoBox}>
                <div><strong>Email:</strong> {detail.email}</div>
                <div><strong>Số điện thoại:</strong> {detail.phone || '—'}</div>
                <div>
                  <strong>Giới tính:</strong>{' '}
                  {detail.gender === 'Male' ? 'Nam' : detail.gender === 'Female' ? 'Nữ' : '—'}
                </div>
                <div>
                  <strong>Vai trò:</strong> {detail.roles.map(r => ROLE_LABELS[r] || r).join(', ') || '—'}
                </div>
                <div><strong>Trạng thái:</strong> <StatusBadge status={detail.status} /></div>
                {detail.status === 'Suspended' && (
                  <div>
                    <strong>Lý do tạm ngưng:</strong> {detail.suspensionReason || '—'}
                    {detail.suspendedAt && ` (${formatDateTime(detail.suspendedAt)})`}
                  </div>
                )}
                <div><strong>Ngày tạo:</strong> {formatDateTime(detail.createdAt)}</div>
              </div>

              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 6 }}>
                Lịch sử trạng thái
              </div>
              <div style={{ marginBottom: 16 }}>
                {detail.statusHistory.length === 0 ? (
                  <div style={{ fontSize: '13px', color: '#64748B' }}>Chưa có thay đổi trạng thái.</div>
                ) : (
                  detail.statusHistory.map(h => (
                    <div key={h.id} style={{ fontSize: '12px', padding: '8px 0', borderBottom: '1px solid #F1F5F9', lineHeight: 1.6 }}>
                      <strong>{STATUS_META[h.oldStatus]?.label || h.oldStatus} → {STATUS_META[h.newStatus]?.label || h.newStatus}</strong>
                      {h.reason && ` · Lý do: ${h.reason}`}
                      {h.notes && ` · Ghi chú: ${h.notes}`}
                      <div style={{ color: '#64748B' }}>
                        {h.changedByName || `#${h.changedBy}`} · {formatDateTime(h.changedAt)}
                      </div>
                    </div>
                  ))
                )}
              </div>

              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 6 }}>
                Lịch sử vai trò
              </div>
              <div>
                {detail.roleHistory.length === 0 ? (
                  <div style={{ fontSize: '13px', color: '#64748B' }}>Chưa có thay đổi vai trò.</div>
                ) : (
                  detail.roleHistory.map(h => (
                    <div key={h.id} style={{ fontSize: '12px', padding: '8px 0', borderBottom: '1px solid #F1F5F9', lineHeight: 1.6 }}>
                      <strong>{h.action}</strong>: {h.oldRoles || '(không)'} → {h.newRoles || '(không)'}
                      <div style={{ color: '#64748B' }}>
                        {h.changedByName || `#${h.changedBy}`} · {formatDateTime(h.changedAt)}
                      </div>
                    </div>
                  ))
                )}
              </div>
            </>
          )}
        </Modal>
      )}

      {/* Modal phân quyền */}
      {modal === 'roles' && selected && (
        <Modal title={`${selected.fullName} (${selected.username})`} subtitle="Phân quyền & vai trò" onClose={closeModal}>
          <form onSubmit={handleAssignRoles}>
            {modalErrorBox}
            <div style={{ ...infoBox, marginBottom: 12 }}>
              Vai trò hiện tại: <strong>{selected.roles.map(r => ROLE_LABELS[r] || r).join(', ') || 'Chưa có'}</strong>
            </div>
            <label style={labelStyle}>Vai trò mong muốn sau cập nhật</label>
            <RolePicker value={editRoles} onChange={setEditRoles} />
            <div style={{ fontSize: '11px', color: '#64748B', margin: '10px 0 20px' }}>
              Bạn không thể tự nâng/thu hồi quyền Admin của chính mình, và không thể thu hồi Admin cuối cùng của hệ thống.
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <button type="button" style={secondaryBtn} onClick={closeModal} disabled={submitting}>Hủy</button>
              <button type="submit" style={primaryBtn} disabled={submitting}>
                {submitting ? 'Đang lưu...' : 'Lưu vai trò'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Modal tạm ngưng */}
      {modal === 'suspend' && selected && (
        <Modal title={`${selected.fullName} (${selected.username})`} subtitle="Tạm ngưng tài khoản" onClose={closeModal}>
          <form onSubmit={handleSuspend}>
            {modalErrorBox}
            <div style={{ marginBottom: 12 }}>
              <label style={labelStyle}>Lý do tạm ngưng *</label>
              <select style={inputStyle} value={suspendReason} onChange={e => setSuspendReason(e.target.value)}>
                {SUSPEND_REASONS.map(r => (
                  <option key={r.value} value={r.value}>{r.label}</option>
                ))}
              </select>
            </div>
            <div style={{ marginBottom: 20 }}>
              <label style={labelStyle}>Ghi chú (tối đa 500 ký tự)</label>
              <textarea
                style={{ ...inputStyle, minHeight: 80, resize: 'vertical' }}
                value={suspendNotes}
                maxLength={500}
                onChange={e => setSuspendNotes(e.target.value)}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <button type="button" style={secondaryBtn} onClick={closeModal} disabled={submitting}>Hủy</button>
              <button type="submit" style={{ ...primaryBtn, backgroundColor: '#DC2626' }} disabled={submitting}>
                {submitting ? 'Đang xử lý...' : 'Xác nhận tạm ngưng'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {/* Modal khôi phục */}
      {modal === 'restore' && selected && (
        <Modal title={`${selected.fullName} (${selected.username})`} subtitle="Khôi phục tài khoản" onClose={closeModal}>
          <form onSubmit={handleRestore}>
            {modalErrorBox}
            {selected.suspensionReason && (
              <div style={{ ...infoBox, marginBottom: 12 }}>
                <strong>Lý do tạm ngưng trước đó:</strong> {selected.suspensionReason}
              </div>
            )}
            <div style={{ marginBottom: 20 }}>
              <label style={labelStyle}>Lý do khôi phục (không bắt buộc, tối đa 500 ký tự)</label>
              <textarea
                style={{ ...inputStyle, minHeight: 80, resize: 'vertical' }}
                value={restoreReason}
                maxLength={500}
                onChange={e => setRestoreReason(e.target.value)}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <button type="button" style={secondaryBtn} onClick={closeModal} disabled={submitting}>Hủy</button>
              <button type="submit" style={{ ...primaryBtn, backgroundColor: '#15803D' }} disabled={submitting}>
                {submitting ? 'Đang xử lý...' : 'Xác nhận khôi phục'}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
};

