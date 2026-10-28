import { useState } from 'react';
import Modal from '../ui/Modal';
import { NOTIFICATION_STATUS_LABELS, SUSPENSION_REASON_LABELS, formatDateTime } from '../../utils/accountMeta';
import type { AccountDetail } from '../../types/account';

interface RestoreDialogProps {
  isOpen: boolean;
  onClose: () => void;
  account: AccountDetail;
  isSaving: boolean;
  onSubmit: (reason: string) => Promise<void>;
}

function Badge({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`admin-badge admin-badge--${tone}`}>{children}</span>;
}

/**
 * Dialog khôi phục tài khoản — hiển thị lý do và lịch sử tạm ngưng trước khi xác nhận.
 */
export function RestoreDialog({ isOpen, onClose, account, isSaving, onSubmit }: RestoreDialogProps) {
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const suspensionHistory = (account.statusHistories ?? [])
    .filter((h) => h.newStatus === 'Suspended')
    .slice()
    .reverse();

  const handleSubmit = async () => {
    if (reason.trim().length < 5) {
      setError('Lý do khôi phục phải có ít nhất 5 ký tự');
      return;
    }
    setError(null);
    try {
      await onSubmit(reason.trim());
      onClose();
    } catch {
      /* lỗi đã hiển thị qua toast */
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Khôi phục tài khoản"
      description={`Tài khoản ${account.username} — ${account.fullName}`}
      size="lg"
      footer={
        <>
          <button type="button" onClick={onClose} disabled={isSaving} className="a-btn a-btn--md a-btn--secondary">
            Hủy
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={isSaving}
            className="a-btn a-btn--md a-btn--success"
          >
            {isSaving && (
              <span
                className="a-spinner"
                style={{ width: 13, height: 13, borderColor: 'rgba(255,255,255,0.5)', borderTopColor: 'transparent' }}
                aria-hidden="true"
              />
            )}
            {isSaving ? 'Đang xử lý…' : 'Xác nhận khôi phục'}
          </button>
        </>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
        {/* Info notice */}
        <div className="a-notice a-notice--green">
          Tài khoản sẽ trở lại trạng thái <strong>Đang hoạt động</strong>. Vai trò hiện có được giữ
          nguyên — hệ thống không tự động cấp lại quyền Admin.
        </div>

        {/* Suspension history */}
        <div>
          <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--a-ink-2)', marginBottom: 8 }}>
            Lịch sử tạm ngưng
          </p>
          {suspensionHistory.length === 0 ? (
            <p style={{ fontSize: 13, color: 'var(--a-ink-3)' }}>Không có lịch sử tạm ngưng.</p>
          ) : (
            <ul style={{ margin: 0, padding: 0, listStyle: 'none' }}>
              {suspensionHistory.map((h) => (
                <li key={h.id} className="a-history-item">
                  <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 6 }}>
                    <Badge tone="rose">
                      {SUSPENSION_REASON_LABELS[h.reason ?? ''] ?? h.reason ?? 'Không rõ'}
                    </Badge>
                    <span style={{ fontSize: 12.5, color: 'var(--a-ink-3)' }}>
                      {formatDateTime(h.changedAt)}
                    </span>
                    {h.changedByName && (
                      <span style={{ fontSize: 12, color: 'var(--a-ink-3)' }}>bởi {h.changedByName}</span>
                    )}
                  </div>
                  {h.notes && (
                    <p style={{ marginTop: 4, fontSize: 12, color: 'var(--a-ink-2)' }}>{h.notes}</p>
                  )}
                  <p style={{ marginTop: 3, fontSize: 11.5, color: 'var(--a-ink-3)' }}>
                    Thông báo email: {NOTIFICATION_STATUS_LABELS[h.notificationStatus] ?? h.notificationStatus}
                  </p>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Restore reason */}
        <div className="a-field">
          <label className="a-label a-label--required">Lý do khôi phục</label>
          <textarea
            className={`a-textarea${error ? ' a-textarea--error' : ''}`}
            rows={3}
            value={reason}
            onChange={(e) => {
              setReason(e.target.value);
              setError(null);
            }}
            placeholder="Ví dụ: Vi phạm đã được xử lý, kiểm tra hệ thống hoàn tất"
          />
          <p className="a-hint">Tối thiểu 5 ký tự, sẽ được lưu vào lịch sử và gửi kèm email thông báo</p>
          {error && <p className="a-error-msg" role="alert">{error}</p>}
        </div>
      </div>
    </Modal>
  );
}

export default RestoreDialog;
