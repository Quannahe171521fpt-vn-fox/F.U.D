import { useState } from 'react';
import Modal from '../ui/Modal';
import { ROLE_LABELS } from '../../utils/accountMeta';
import type { AccountDetail, RoleOption, SystemRole } from '../../types/account';

interface AssignRolesModalProps {
  isOpen: boolean;
  onClose: () => void;
  account: AccountDetail;
  roleOptions: RoleOption[];
  isSaving: boolean;
  onSubmit: (roleNames: SystemRole[]) => Promise<void>;
}

function Badge({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`admin-badge admin-badge--${tone}`}>{children}</span>;
}

/**
 * Modal cấp / đổi / thu hồi vai trò.
 * Component được mount/unmount theo target nên state luôn khởi tạo từ tài khoản hiện tại.
 */
export function AssignRolesModal({
  isOpen,
  onClose,
  account,
  roleOptions,
  isSaving,
  onSubmit,
}: AssignRolesModalProps) {
  const [selected, setSelected] = useState<SystemRole[]>([...account.roles]);
  const [error, setError] = useState<string | null>(null);

  const toggleRole = (role: SystemRole) => {
    setSelected((prev) =>
      prev.includes(role) ? prev.filter((r) => r !== role) : [...prev, role]
    );
  };

  const handleSubmit = async () => {
    if (selected.length === 0) {
      setError('Phải giữ lại ít nhất một vai trò');
      return;
    }
    setError(null);
    try {
      await onSubmit(selected);
      onClose();
    } catch {
      /* lỗi đã hiển thị qua toast */
    }
  };

  const hasChanges =
    account.roles.length !== selected.length ||
    account.roles.some((r) => !selected.includes(r));

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Phân quyền tài khoản"
      description={`Tài khoản ${account.username} — ${account.fullName}`}
      footer={
        <>
          <button type="button" onClick={onClose} disabled={isSaving} className="a-btn a-btn--md a-btn--secondary">
            Hủy
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={isSaving || !hasChanges}
            className="a-btn a-btn--md a-btn--primary"
          >
            {isSaving && (
              <span
                className="a-spinner"
                style={{ width: 13, height: 13, borderColor: 'rgba(255,255,255,0.5)', borderTopColor: 'transparent' }}
                aria-hidden="true"
              />
            )}
            {isSaving ? 'Đang lưu…' : 'Lưu thay đổi'}
          </button>
        </>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        {/* Current roles */}
        <div>
          <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--a-ink-2)', marginBottom: 8 }}>
            Vai trò hiện tại
          </p>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {account.roles.length === 0 ? (
              <span style={{ fontSize: 13, color: 'var(--a-ink-3)' }}>Chưa có vai trò</span>
            ) : (
              account.roles.map((role) => (
                <Badge key={role} tone="slate">
                  {ROLE_LABELS[role] ?? role}
                </Badge>
              ))
            )}
          </div>
        </div>

        {/* Role options */}
        <div>
          <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--a-ink-2)', marginBottom: 8 }}>
            Vai trò sau khi cập nhật
          </p>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            {roleOptions.map((option) => {
              const checked = selected.includes(option.name);
              return (
                <label
                  key={option.id}
                  className={`a-role-card${checked ? ' a-role-card--checked' : ''}`}
                >
                  <input
                    type="checkbox"
                    checked={checked}
                    onChange={() => toggleRole(option.name)}
                  />
                  <span>
                    <span style={{ display: 'block', fontSize: 13.5, fontWeight: 600, color: 'var(--a-ink)' }}>
                      {ROLE_LABELS[option.name] ?? option.name}
                    </span>
                    {option.description && (
                      <span style={{ display: 'block', fontSize: 12, color: 'var(--a-ink-3)', marginTop: 2 }}>
                        {option.description}
                      </span>
                    )}
                  </span>
                </label>
              );
            })}
          </div>
        </div>

        {error && (
          <p style={{ fontSize: 12.5, fontWeight: 600, color: 'var(--a-rose)' }} role="alert">
            {error}
          </p>
        )}

        <div className="a-notice a-notice--slate">
          Việc thu hồi quyền Admin của chính bạn hoặc của Admin cuối cùng sẽ bị hệ thống từ chối.
        </div>
      </div>
    </Modal>
  );
}

export default AssignRolesModal;
