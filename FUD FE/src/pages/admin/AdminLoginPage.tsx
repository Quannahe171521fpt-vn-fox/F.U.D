import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import '../../styles/admin.css';

export function AdminLoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const isLoading = useAuthStore((s) => s.isLoading);
  const error = useAuthStore((s) => s.error);

  const [usernameOrEmail, setUsernameOrEmail] = useState('');
  const [password, setPassword] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await login(usernameOrEmail.trim(), password);
      navigate('/admin/accounts', { replace: true });
    } catch {
      /* error shown via store */
    }
  };

  return (
    <div className="admin-login">
      <div className="admin-login-card">
        {/* Header */}
        <div className="admin-login-header">
          <div
            className="admin-brand-mark"
            style={{ margin: '0 auto', width: 40, height: 40, borderRadius: 10 }}
          >
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="1.9"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6" />
            </svg>
          </div>
          <p className="admin-brand-name admin-login-title">F.U.D Admin</p>
          <p className="admin-login-sub">Đăng nhập để vào không gian quản trị</p>
        </div>

        {/* Body */}
        <div className="admin-login-body">
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
            <div className="a-field">
              <label htmlFor="admin-username" className="a-label a-label--required">
                Tài khoản hoặc email
              </label>
              <input
                id="admin-username"
                className="a-input"
                value={usernameOrEmail}
                onChange={(e) => setUsernameOrEmail(e.target.value)}
                required
                autoComplete="username"
                placeholder="username hoặc email@fud.edu"
              />
            </div>

            <div className="a-field">
              <label htmlFor="admin-password" className="a-label a-label--required">
                Mật khẩu
              </label>
              <input
                id="admin-password"
                className="a-input"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                autoComplete="current-password"
                placeholder="••••••••"
              />
            </div>

            {error && (
              <div className="a-notice a-notice--amber" role="alert">
                {error}
              </div>
            )}

            <button
              type="submit"
              disabled={isLoading}
              className="admin-login-submit"
              style={{ marginTop: 4 }}
            >
              {isLoading && (
                <span
                  className="a-spinner"
                  style={{ width: 14, height: 14, borderColor: 'rgba(255,255,255,0.5)', borderTopColor: 'transparent' }}
                  aria-hidden="true"
                />
              )}
              {isLoading ? 'Đang đăng nhập…' : 'Đăng nhập'}
            </button>
          </form>

          <p
            style={{
              marginTop: 16,
              textAlign: 'center',
              fontSize: 11.5,
              color: 'var(--a-ink-3)',
              lineHeight: 1.6,
            }}
          >
            Tài khoản Admin được lấy từ cấu hình{' '}
            <code
              style={{
                background: 'var(--a-surface-sub)',
                border: '1px solid var(--a-rule)',
                borderRadius: 4,
                padding: '1px 5px',
                fontSize: 11,
                fontFamily: 'var(--a-font-mono)',
              }}
            >
              SeedAdmin
            </code>{' '}
            phía backend.
          </p>
        </div>
      </div>
    </div>
  );
}

export default AdminLoginPage;
