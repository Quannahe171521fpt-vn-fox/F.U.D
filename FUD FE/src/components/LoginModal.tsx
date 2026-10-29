import React, { useState, useEffect } from 'react';
import { CloseIcon, UserIcon, ShieldIcon } from './Icons';
import { useAuth } from '../contexts/AuthContext';
import { loginWithCredentials, loginWithGoogle } from '../services/authApi';

declare global {
  interface Window {
    google?: any;
  }
}

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
  onLoginSuccess?: (role: 'student' | 'manager', username: string) => void;
}

export const LoginModal: React.FC<LoginModalProps> = ({ isOpen, onClose, onLoginSuccess }) => {
  const [role, setRole] = useState<'student' | 'manager'>('student');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);
  const [loginSuccess, setLoginSuccess] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { loginSuccess: authLoginSuccess } = useAuth();

  // Khởi tạo Google Identity Services
  useEffect(() => {
    if (!isOpen || role !== 'student') return;

    const clientId = "465802234542-gktp27sds4gc8l6snvrmt8e1168gif37.apps.googleusercontent.com";

    const initGoogle = () => {
      if (window.google?.accounts?.id) {
        window.google.accounts.id.initialize({
          client_id: clientId,
          callback: async (response: { credential: string }) => {
            setIsSubmitting(true);
            setErrorMessage(null);
            try {
              const data = await loginWithGoogle(response.credential);
              setLoginSuccess(true);
              authLoginSuccess(data.accessToken, data.user);
              setTimeout(() => {
                setLoginSuccess(false);
                const mappedRole = (data.user.primaryRole === 'Admin' || data.user.primaryRole === 'Manager' || data.user.primaryRole === 'Guard') ? 'manager' : 'student';
                onLoginSuccess?.(mappedRole, data.user.code);
                onClose();
              }, 800);
            } catch (err: any) {
              setErrorMessage(err.message || 'Đăng nhập Google thất bại');
            } finally {
              setIsSubmitting(false);
            }
          },
        });

        const btnContainer = document.getElementById('google-btn-container');
        if (btnContainer) {
          btnContainer.innerHTML = '';
          window.google.accounts.id.renderButton(btnContainer, {
            theme: 'outline',
            size: 'large',
            width: 320,
            text: 'signin_with',
            locale: 'vi',
          });
        }
      }
    };

    if (window.google?.accounts?.id) {
      initGoogle();
    } else {
      const timer = setInterval(() => {
        if (window.google?.accounts?.id) {
          clearInterval(timer);
          initGoogle();
        }
      }, 200);
      return () => clearInterval(timer);
    }
  }, [isOpen, role]);

  if (!isOpen) return null;

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username || !password) return;
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const data = await loginWithCredentials(username, password);
      setLoginSuccess(true);
      authLoginSuccess(data.accessToken, data.user);
      setTimeout(() => {
        setLoginSuccess(false);
        const mappedRole = (data.user.primaryRole === 'Admin' || data.user.primaryRole === 'Manager' || data.user.primaryRole === 'Guard') ? 'manager' : 'student';
        onLoginSuccess?.(mappedRole, data.user.code);
        onClose();
      }, 800);
    } catch (err: any) {
      setErrorMessage(err.message || 'Đăng nhập thất bại');
    } finally {
      setIsSubmitting(false);
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
            className={`role-tab-btn ${role === 'student' ? 'active' : ''}`}
            onClick={() => { setRole('student'); setErrorMessage(null); }}
          >
            <UserIcon size={16} />
            <span>Sinh viên</span>
          </button>
          <button
            type="button"
            className={`role-tab-btn ${role === 'manager' ? 'active' : ''}`}
            onClick={() => { setRole('manager'); setErrorMessage(null); }}
          >
            <ShieldIcon size={16} />
            <span>Cán bộ Quản lý</span>
          </button>
        </div>

        {loginSuccess ? (
          <div className="login-success-view">
            <div className="success-bubble">✓</div>
            <h4>Đăng nhập thành công!</h4>
            <p>Chào mừng {role === 'student' ? 'sinh viên' : 'thầy cô'} quay trở lại hệ thống KTX.</p>
          </div>
        ) : (
          <form onSubmit={handleLoginSubmit} className="login-form">
            {errorMessage && (
              <div style={{ color: '#ef4444', backgroundColor: '#fee2e2', padding: '8px 12px', borderRadius: '6px', fontSize: '13px', marginBottom: '12px' }}>
                {errorMessage}
              </div>
            )}

            <div className="form-field-group">
              <label htmlFor="login-username" className="form-label">
                {role === 'student' ? 'Email FPT (@fpt.edu.vn) hoặc MSSV' : 'Tên tài khoản / Email cán bộ'}
              </label>
              <input
                id="login-username"
                type="text"
                required
                placeholder={role === 'student' ? 'SE17001 hoặc nampsse17001@fpt.edu.vn' : 'ADMIN01 hoặc admin01@fpt.edu.vn'}
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

            <button type="submit" className="login-submit-btn" disabled={isSubmitting}>
              {isSubmitting ? 'Đang xác thực...' : 'Đăng nhập hệ thống'}
            </button>

            {role === 'student' && (
              <div className="oauth-login-divider">
                <span>Hoặc đăng nhập nhanh bằng</span>
                <div id="google-btn-container" style={{ display: 'flex', justifyContent: 'center', marginTop: '12px', minHeight: '44px' }}>
                  {/* Google official button will be rendered here */}
                </div>
              </div>
            )}
          </form>
        )}
      </div>
    </div>
  );
};
