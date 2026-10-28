import Modal from '../ui/Modal';
import {
  NOTIFICATION_STATUS_LABELS,
  ROLE_ACTION_LABELS,
  ROLE_LABELS,
  STATUS_LABELS,
  SUSPENSION_REASON_LABELS,
  formatDateTime,
} from '../../utils/accountMeta';
import type { AccountDetail, AccountStatus } from '../../types/account';

interface AccountDetailModalProps {
  isOpen: boolean;
  onClose: () => void;
  account: AccountDetail | null;
  isLoading: boolean;
  onEditRoles: () => void;
}

const STATUS_TONE: Record<AccountStatus, string> = {
  Active: 'green',
  Pending: 'amber',
  Suspended: 'rose',
};

function Badge({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`admin-badge admin-badge--${tone}`}>{children}</span>;
}

export function AccountDetailModal({
  isOpen,
  onClose,
  account,
  isLoading,
  onEditRoles,
}: AccountDetailModalProps) {
  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Chi tiết tài khoản"
      description={account ? `${account.username} — ${account.fullName}` : undefined}
      size="lg"
      footer={
        <>
          <button type="button" onClick={onClose} className="a-btn a-btn--md a-btn--secondary">
            Đóng
          </button>
          {account && (
            <button type="button" onClick={onEditRoles} className="a-btn a-btn--md a-btn--primary">
              Phân quyền
            </button>
          )}
        </>
      }
    >
      {isLoading || !account ? (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: '40px 0' }}>
          <span
            className="a-spinner"
            style={{ width: 22, height: 22, color: 'var(--a-accent)' }}
            aria-label="Đang tải…"
          />
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
          {/* Basic info */}
          <dl
            className="admin-dl"
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))',
              gap: '16px 20px',
            }}
          >
            {[
              { label: 'Username', value: account.username },
              { label: 'Họ tên', value: account.fullName },
              { label: 'Email', value: account.email },
              { label: 'Số điện thoại', value: account.phone || '—' },
              { label: 'Tạo lúc', value: formatDateTime(account.createdAt) },
              { label: 'Cập nhật lúc', value: formatDateTime(account.updatedAt) },
            ].map(({ label, value }) => (
              <div key={label}>
                <dt>{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}

            <div>
              <dt>Trạng thái</dt>
              <dd style={{ marginTop: 6 }}>
                <Badge tone={(STATUS_TONE[account.status] ?? 'slate')}>
                  {STATUS_LABELS[account.status] ?? account.status}
                </Badge>
              </dd>
            </div>

            <div>
              <dt>Vai trò</dt>
              <dd style={{ marginTop: 6, display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                {account.roles.length === 0 ? (
                  <span style={{ fontSize: 13, color: 'var(--a-ink-3)' }}>—</span>
                ) : (
                  account.roles.map((role) => (
                    <Badge key={role} tone="violet">
                      {ROLE_LABELS[role] ?? role}
                    </Badge>
                  ))
                )}
              </dd>
            </div>
          </dl>

          {/* Status history */}
          <div>
            <h3 style={{ fontSize: 13, fontWeight: 600, color: 'var(--a-ink)', marginBottom: 10 }}>
              Lịch sử trạng thái
            </h3>
            {(account.statusHistories ?? []).length === 0 ? (
              <p style={{ fontSize: 13, color: 'var(--a-ink-3)' }}>Chưa có thay đổi trạng thái nào.</p>
            ) : (
              <ul style={{ margin: 0, padding: 0, listStyle: 'none' }}>
                {account.statusHistories.map((h) => (
                  <li key={h.id} className="a-history-item">
                    <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 6 }}>
                      <span style={{ fontSize: 12.5, color: 'var(--a-ink-3)' }}>
                        {STATUS_LABELS[h.oldStatus] ?? h.oldStatus}
                      </span>
                      <span style={{ color: 'var(--a-ink-3)', fontSize: 12 }}>→</span>
                      <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--a-ink)' }}>
                        {STATUS_LABELS[h.newStatus] ?? h.newStatus}
                      </span>
                      {h.reason && (
                        <Badge tone="slate">
                          {SUSPENSION_REASON_LABELS[h.reason] ?? h.reason}
                        </Badge>
                      )}
                    </div>
                    <p style={{ marginTop: 4, fontSize: 11.5, color: 'var(--a-ink-3)' }}>
                      {formatDateTime(h.changedAt)}
                      {h.changedByName ? ` · bởi ${h.changedByName}` : ''}
                      {` · email: ${NOTIFICATION_STATUS_LABELS[h.notificationStatus] ?? h.notificationStatus}`}
                    </p>
                    {h.notes && <p style={{ marginTop: 3, fontSize: 12, color: 'var(--a-ink-2)' }}>{h.notes}</p>}
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* Role history */}
          <div>
            <h3 style={{ fontSize: 13, fontWeight: 600, color: 'var(--a-ink)', marginBottom: 10 }}>
              Lịch sử vai trò
            </h3>
            {(account.roleHistories ?? []).length === 0 ? (
              <p style={{ fontSize: 13, color: 'var(--a-ink-3)' }}>Chưa có thay đổi vai trò nào.</p>
            ) : (
              <ul style={{ margin: 0, padding: 0, listStyle: 'none' }}>
                {account.roleHistories.map((h) => (
                  <li key={h.id} className="a-history-item">
                    <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 6 }}>
                      <Badge tone="sky">{ROLE_ACTION_LABELS[h.action] ?? h.action}</Badge>
                      <span style={{ fontSize: 12.5, color: 'var(--a-ink-3)' }}>{h.oldRoles || '—'}</span>
                      <span style={{ color: 'var(--a-ink-3)', fontSize: 12 }}>→</span>
                      <span style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--a-ink)' }}>{h.newRoles || '—'}</span>
                    </div>
                    <p style={{ marginTop: 4, fontSize: 11.5, color: 'var(--a-ink-3)' }}>
                      {formatDateTime(h.changedAt)}
                      {h.changedByName ? ` · bởi ${h.changedByName}` : ''}
                    </p>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}

export default AccountDetailModal;
