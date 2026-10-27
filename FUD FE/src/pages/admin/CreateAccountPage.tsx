import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import accountService from '../../services/accountService';
import { extractErrorMessages } from '../../services/http';
import { ROLE_LABELS } from '../../utils/accountMeta';
import type { SystemRole } from '../../types/account';

interface FormState {
  username: string;
  fullName: string;
  email: string;
  phone: string;
  password: string;
  confirmPassword: string;
  roleName: SystemRole;
}

const INITIAL_FORM: FormState = {
  username: '',
  fullName: '',
  email: '',
  phone: '',
  password: '',
  confirmPassword: '',
  roleName: 'Student',
};

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const USERNAME_PATTERN = /^[a-zA-Z0-9._-]+$/;
const PASSWORD_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/;

function Field({
  label,
  required,
  children,
  error,
  hint,
}: {
  label: string;
  required?: boolean;
  children: React.ReactNode;
  error?: string;
  hint?: string;
}) {
  return (
    <div className="a-field">
      <label className={`a-label${required ? ' a-label--required' : ''}`}>{label}</label>
      {children}
      {hint && !error && <p className="a-hint">{hint}</p>}
      {error && <p className="a-error-msg" role="alert">{error}</p>}
    </div>
  );
}

export function CreateAccountPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState<FormState>(INITIAL_FORM);
  const [errors, setErrors] = useState<Partial<Record<keyof FormState, string>>>({});
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const setField = <K extends keyof FormState>(field: K, value: FormState[K]) => {
    setForm((prev) => ({ ...prev, [field]: value }));
    setErrors((prev) => ({ ...prev, [field]: undefined }));
    setServerErrors([]);
  };

  const validate = (): boolean => {
    const next: Partial<Record<keyof FormState, string>> = {};

    if (!form.username.trim()) next.username = 'Username là bắt buộc';
    else if (form.username.trim().length < 3) next.username = 'Username phải có ít nhất 3 ký tự';
    else if (!USERNAME_PATTERN.test(form.username.trim()))
      next.username = 'Username chỉ gồm chữ, số và các ký tự . _ -';

    if (!form.fullName.trim()) next.fullName = 'Họ tên là bắt buộc';
    else if (form.fullName.trim().length > 100) next.fullName = 'Họ tên không vượt quá 100 ký tự';

    if (!form.email.trim()) next.email = 'Email là bắt buộc';
    else if (!EMAIL_PATTERN.test(form.email.trim())) next.email = 'Email không đúng định dạng';

    if (form.phone.trim() && form.phone.trim().length > 20)
      next.phone = 'Số điện thoại không vượt quá 20 ký tự';

    if (!form.password) next.password = 'Mật khẩu là bắt buộc';
    else if (!PASSWORD_PATTERN.test(form.password))
      next.password = 'Mật khẩu tối thiểu 8 ký tự, có chữ hoa, chữ thường và số';

    if (form.password !== form.confirmPassword) next.confirmPassword = 'Xác nhận mật khẩu không khớp';

    if (!form.roleName) next.roleName = 'Vai trò ban đầu là bắt buộc';

    setErrors(next);
    return Object.keys(next).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setServerErrors([]);
    if (!validate()) return;

    setIsSubmitting(true);
    try {
      await accountService.create({
        username: form.username.trim(),
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || undefined,
        password: form.password,
        roleName: form.roleName,
      });
      navigate('/admin/accounts');
    } catch (err) {
      setServerErrors(extractErrorMessages(err));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <>
      {/* Page header */}
      <div className="admin-page-header">
        <div>
          <Link to="/admin/accounts" className="a-link" style={{ fontSize: 12.5 }}>
            ← Quay lại danh sách
          </Link>
          <h1 className="admin-title" style={{ marginTop: 8 }}>Tạo tài khoản</h1>
          <p className="admin-subtitle">Tài khoản mới sẽ hoạt động ngay với vai trò được chọn.</p>
        </div>
      </div>

      {/* Page body */}
      <div className="admin-page-body">
        {serverErrors.length > 0 && (
          <div
            className="a-notice a-notice--amber"
            role="alert"
            style={{ marginBottom: 16 }}
          >
            <p style={{ fontWeight: 600, marginBottom: 4, fontSize: 13 }}>Không thể tạo tài khoản</p>
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {serverErrors.map((msg, i) => (
                <li key={i} style={{ fontSize: 12.5 }}>{msg}</li>
              ))}
            </ul>
          </div>
        )}

        <form
          onSubmit={handleSubmit}
          className="admin-form-card"
          noValidate
        >
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))',
              gap: '16px 20px',
            }}
          >
            <Field
              label="Username"
              required
              error={errors.username}
              hint="Dùng để đăng nhập, duy nhất trong hệ thống"
            >
              <input
                className={`a-input${errors.username ? ' a-input--error' : ''}`}
                value={form.username}
                onChange={(e) => setField('username', e.target.value)}
                autoComplete="off"
                placeholder="vd: nguyen.van.a"
              />
            </Field>

            <Field label="Họ tên" required error={errors.fullName}>
              <input
                className={`a-input${errors.fullName ? ' a-input--error' : ''}`}
                value={form.fullName}
                onChange={(e) => setField('fullName', e.target.value)}
                autoComplete="off"
                placeholder="vd: Nguyễn Văn A"
              />
            </Field>

            <Field
              label="Email"
              required
              error={errors.email}
              hint="Dùng để gửi thông báo, duy nhất trong hệ thống"
            >
              <input
                className={`a-input${errors.email ? ' a-input--error' : ''}`}
                type="email"
                value={form.email}
                onChange={(e) => setField('email', e.target.value)}
                autoComplete="off"
                placeholder="vd: a@fud.edu.vn"
              />
            </Field>

            <Field label="Số điện thoại" error={errors.phone}>
              <input
                className={`a-input${errors.phone ? ' a-input--error' : ''}`}
                value={form.phone}
                onChange={(e) => setField('phone', e.target.value)}
                autoComplete="off"
                placeholder="vd: 0901 234 567"
              />
            </Field>

            <Field
              label="Mật khẩu"
              required
              error={errors.password}
              hint="Tối thiểu 8 ký tự, có chữ hoa, chữ thường và số"
            >
              <input
                className={`a-input${errors.password ? ' a-input--error' : ''}`}
                type="password"
                value={form.password}
                onChange={(e) => setField('password', e.target.value)}
                autoComplete="new-password"
                placeholder="••••••••"
              />
            </Field>

            <Field label="Xác nhận mật khẩu" required error={errors.confirmPassword}>
              <input
                className={`a-input${errors.confirmPassword ? ' a-input--error' : ''}`}
                type="password"
                value={form.confirmPassword}
                onChange={(e) => setField('confirmPassword', e.target.value)}
                autoComplete="new-password"
                placeholder="••••••••"
              />
            </Field>
          </div>

          <div style={{ marginTop: 16 }}>
            <Field
              label="Vai trò ban đầu"
              required
              error={errors.roleName}
              hint="Chỉ có thể chọn 1 vai trò ban đầu; sau đó có thể cấp thêm qua Phân quyền"
            >
              <select
                className={`a-select${errors.roleName ? ' a-select--error' : ''}`}
                value={form.roleName}
                onChange={(e) => setField('roleName', e.target.value as SystemRole)}
                style={{ maxWidth: 320 }}
              >
                {(['Student', 'Manager', 'Guard', 'Admin'] as SystemRole[]).map((r) => (
                  <option key={r} value={r}>{ROLE_LABELS[r]}</option>
                ))}
              </select>
            </Field>
          </div>

          <div
            style={{
              display: 'flex',
              justifyContent: 'flex-end',
              gap: 8,
              marginTop: 24,
              paddingTop: 20,
              borderTop: '1px solid var(--a-rule)',
            }}
          >
            <Link to="/admin/accounts">
              <button type="button" className="a-btn a-btn--md a-btn--secondary" disabled={isSubmitting}>
                Hủy
              </button>
            </Link>
            <button
              type="submit"
              className="a-btn a-btn--md a-btn--primary"
              disabled={isSubmitting}
            >
              {isSubmitting && (
                <span
                  className="a-spinner"
                  style={{ width: 13, height: 13, borderColor: 'rgba(255,255,255,0.5)', borderTopColor: 'transparent' }}
                  aria-hidden="true"
                />
              )}
              {isSubmitting ? 'Đang tạo…' : 'Tạo tài khoản'}
            </button>
          </div>
        </form>
      </div>
    </>
  );
}

export default CreateAccountPage;
