import React, { useState } from 'react';
import { CloseIcon, UserIcon, ShieldIcon } from './Icons';
import { useAuth } from '../context/AuthContext';
import type { AuthUser } from '../types/auth';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
  onLoginSuccess?: (role: 'student' | 'staff', username: string) => void;
}

export const LoginModal: React.FC<LoginModalProps> = ({ isOpen, onClose, onLoginSuccess }) => {
  const { login, loginWithGoogle, isLoading } = useAuth();
  const [roleTab, setRoleTab] = useState<'student' | 'staff'>('student');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [loggedInUser, setLoggedInUser] = useState<AuthUser | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!username.trim() || !password.trim()) {
      setErrorMessage('Vui lòng nhập đầy đủ tài khoản và mật khẩu.');
      return;
    }

    try {
      const user = await login({
        identifier: username.trim(),
        password,
        rememberMe,
      });

      setLoggedInUser(user);
      setTimeout(() => {
        setLoggedInUser(null);
        onClose();
      }, 1500);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Đăng nhập không thành công.';
      setErrorMessage(msg);
    }
  };

  const handleGoogleLogin = async () => {
    setErrorMessage(null);
    try {
      // Gọi qua Google OAuth Token (sử dụng token hoặc idToken nhận từ Google SDK)
      const user = await loginWithGoogle({
        idToken: 'demo-google-id-token',
      });
      setLoggedInUser(user);
      setTimeout(() => {
        setLoggedInUser(null);
        onClose();
      }, 1500);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Đăng nhập Google thất bại.';
      setErrorMessage(msg);
    }
  };

  return (
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="modal-title-wrap">
            <h3 className="modal-title">Cổng Đăng Nhập KTX</h3>
            <p className="modal-subtitle">Hệ thống Quản lý Ký túc xá - On campus Dormitory</p>
          </div>
          <button type="button" className="modal-close-btn" onClick={onClose} aria-label="Close dialog">
            <CloseIcon size={20} />
          </button>
        </div>

        {/* Role Switcher Tabs */}
        <div className="role-tabs">
          <button
            type="button"
            className={`role-tab-btn ${roleTab === 'student' ? 'active' : ''}`}
            onClick={() => {
              setRoleTab('student');
              setErrorMessage(null);
            }}
          >
            <UserIcon size={16} />
            <span>Sinh viên</span>
          </button>
          <button
            type="button"
            className={`role-tab-btn ${roleTab === 'staff' ? 'active' : ''}`}
            onClick={() => {
              setRoleTab('staff');
              setErrorMessage(null);
            }}
          >
            <ShieldIcon size={16} />
            <span>Cán bộ Quản lý</span>
          </button>
        </div>

        {loggedInUser ? (
          <div className="login-success-view">
            <div className="success-bubble">✓</div>
            <h4>Đăng nhập thành công!</h4>
            <p>Chào mừng <strong>{loggedInUser.fullName}</strong> ({loggedInUser.code})</p>
            <div style={{ marginTop: '8px', fontSize: '0.875rem', color: '#15803d', backgroundColor: '#f0fdf4', padding: '6px 12px', borderRadius: '6px' }}>
              ✦ Hệ thống đã tự động nhận diện giới tính: <strong>{loggedInUser.gender === 'Male' ? 'Nam' : 'Nữ'}</strong> và khóa phạm vi phòng phù hợp.
            </div>
          </div>
        ) : (
          <form onSubmit={handleLoginSubmit} className="login-form">
            {errorMessage && (
              <div
                style={{
                  padding: '10px 14px',
                  backgroundColor: '#fef2f2',
                  border: '1px solid #f87171',
                  borderRadius: '6px',
                  color: '#b91c1c',
                  fontSize: '0.875rem',
                  marginBottom: '12px',
                }}
              >
                ⚠ {errorMessage}
              </div>
            )}

            <div className="form-field-group">
              <label htmlFor="login-username" className="form-label">
                {roleTab === 'student' ? 'Email FPT (@fpt.edu.vn) hoặc MSSV' : 'Tên tài khoản / Email cán bộ'}
              </label>
              <input
                id="login-username"
                type="text"
                required
                disabled={isLoading}
                placeholder={roleTab === 'student' ? 'annvhe180123@fpt.edu.vn hoặc HE180123' : 'admin_ktx@fe.edu.vn'}
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                className="form-input"
              />
            </div>

            <div className="form-field-group">
              <label htmlFor="login-password" className="form-label">Mật khẩu</label>
              <input
                id="login-password"
                type="password"
                required
                disabled={isLoading}
                placeholder="••••••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="form-input"
              />
            </div>

            <div className="login-options-row">
              <label className="checkbox-container">
                <input
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                />
                <span>Ghi nhớ đăng nhập</span>
              </label>
              <a href="#forgot" onClick={(e) => { e.preventDefault(); alert('Vui lòng liên hệ Ban Quản lý KTX hoặc quản trị viên IT để cấp lại mật khẩu.'); }} className="forgot-link">
                Quên mật khẩu?
              </a>
            </div>

            <button type="submit" className="login-submit-btn" disabled={isLoading}>
              {isLoading ? 'Đang xác thực...' : 'Đăng nhập hệ thống'}
            </button>

            {roleTab === 'student' && (
              <div className="oauth-login-divider">
                <span>Hoặc đăng nhập nhanh bằng</span>
                <button
                  type="button"
                  className="google-fpt-btn"
                  disabled={isLoading}
                  onClick={handleGoogleLogin}
                >
                  <svg width="18" height="18" viewBox="0 0 24 24">
                    <path fill="#4285F4" d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v4.51h6.6c-.29 1.52-1.14 2.8-2.4 3.66v3.05h3.88c2.27-2.09 3.66-5.17 3.66-9.15z"/>
                    <path fill="#34A853" d="M12 24c3.24 0 5.95-1.08 7.93-2.91l-3.88-3.05c-1.08.72-2.45 1.16-4.05 1.16-3.12 0-5.77-2.1-6.72-4.93H1.26v3.15C3.25 21.37 7.31 24 12 24z"/>
                    <path fill="#FBBC05" d="M5.28 14.27c-.25-.72-.38-1.49-.38-2.27s.13-1.55.38-2.27V6.58H1.26C.46 8.17 0 9.97 0 12s.46 3.83 1.26 5.42l4.02-3.15z"/>
                    <path fill="#EA4335" d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.95 1.19 15.24 0 12 0 7.31 0 3.25 2.63 1.26 6.58l4.02 3.15c.95-2.83 3.6-4.98 6.72-4.98z"/>
                  </svg>
                  Đăng nhập bằng Email @fpt.edu.vn
                </button>
              </div>
            )}
          </form>
        )}
      </div>
    </div>
  );
};
