import { useState } from 'react';
import Modal from '../ui/Modal';
import { SUSPENSION_REASON_LABELS } from '../../utils/accountMeta';
import type { AccountDetail, SuspensionReason } from '../../types/account';

interface SuspendDialogProps {
  isOpen: boolean;
  onClose: () => void;
  account: AccountDetail;
  isSaving: boolean;
  onSubmit: (reason: SuspensionReason, notes?: string) => Promise<void>;
}

const REASON_OPTIONS = (Object.keys(SUSPENSION_REASON_LABELS) as SuspensionReason[]).map((value) => ({
  value,
  label: SUSPENSION_REASON_LABELS[value],
}));

/**
 * Dialog tạm ngưng tài khoản — bắt buộc chọn lý do, có bước xác nhận.
 */
export function SuspendDialog({ isOpen, onClose, account, isSaving, onSubmit }: SuspendDialogProps) {
  const [reason, setReason] = useState<SuspensionReason | ''>('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async () => {
    if (!reason) {
      setError('Vui lòng chọn lý do tạm ngưng');
      return;
    }
    if (reason === 'Other' && !notes.trim()) {
      setError('Vui lòng nhập ghi chú chi tiết khi chọn lý do "Khác"');
      return;
    }
    setError(null);
    try {
      await onSubmit(reason, notes.trim() || undefined);
      onClose();
    } catch {
      /* lỗi đã hiển thị qua toast */
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Tạm ngưng tài khoản"
      description={`Tài khoản ${account.username} — ${account.fullName}`}
      size="md"
      footer={
        <>
          <button type="button" onClick={onClose} disabled={isSaving} className="a-btn a-btn--md a-btn--secondary">
            Hủy
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={isSaving}
            className="a-btn a-btn--md a-btn--danger"
          >
            {isSaving && (
              <span
                className="a-spinner"
                style={{ width: 13, height: 13, borderColor: 'rgba(255,255,255,0.5)', borderTopColor: 'transparent' }}
                aria-hidden="true"
              />
            )}
            {isSaving ? 'Đang xử lý…' : 'Xác nhận tạm ngưng'}
          </button>
        </>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        {/* Warning notice */}
        <div className="a-notice a-notice--amber">
          Tài khoản sẽ không thể đăng nhập và mọi phiên đang hoạt động sẽ bị thu hồi.
          Hệ thống sẽ gửi email thông báo tới{' '}
          <strong>{account.email}</strong>.
        </div>

        {/* Reason select */}
        <div className="a-field">
          <label className="a-label a-label--required">Lý do tạm ngưng</label>
          <select
            className={`a-select${error && !reason ? ' a-select--error' : ''}`}
            value={reason}
            onChange={(e) => {
              setReason(e.target.value as SuspensionReason | '');
              setError(null);
            }}
          >
            <option value="">— Chọn lý do —</option>
            {REASON_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
          {error && !reason && (
            <p className="a-error-msg" role="alert">{error}</p>
          )}
        </div>

        {/* Notes textarea */}
        <div className="a-field">
          <label className="a-label">
            Ghi chú chi tiết{reason === 'Other' ? ' *' : ''}
          </label>
          <textarea
            className={`a-textarea${error && reason ? ' a-textarea--error' : ''}`}
            rows={4}
            value={notes}
            onChange={(e) => {
              setNotes(e.target.value);
              setError(null);
            }}
            placeholder="Mô tả thêm về vi phạm / quyết định…"
          />
          <p className="a-hint">
            {reason === 'Other' ? 'Bắt buộc khi chọn lý do "Khác"' : 'Không bắt buộc'}
          </p>
          {error && reason && (
            <p className="a-error-msg" role="alert">{error}</p>
          )}
        </div>
      </div>
    </Modal>
  );
}

export default SuspendDialog;
