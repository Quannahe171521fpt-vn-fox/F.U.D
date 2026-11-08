import React, { useState, useEffect } from 'react';
import { CloseIcon, UserIcon, ShieldIcon } from './Icons';
import { useAuth } from '../contexts/AuthContext';
import type { UserInfo } from '../contexts/AuthContext';
import { loginWithCredentials, loginWithGoogle } from '../services/authApi';

declare global {
  interface Window {
    google?: any;
  }
}

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
  onLoginSuccess?: (user: UserInfo) => void;
}

export const LoginModal: React.FC<LoginModalProps> = ({ isOpen, onClose, onLoginSuccess }) => {
  const [role, setRole] = useState<'student' | 'manager'>('student');
  const [account, setAccount] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(true);
  const [loginSuccess, setLoginSuccess] = useState(false);
  const [loggedInUser, setLoggedInUser] = useState<UserInfo | null>(null);
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
              setLoggedInUser(data.user);
              setLoginSuccess(true);
              authLoginSuccess(data.accessToken, data.user);
              setTimeout(() => {
                setLoginSuccess(false);
                setLoggedInUser(null);
                onLoginSuccess?.(data.user);
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
    const cleanAccount = account.trim();
    if (!cleanAccount || !password) return;
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      const data = await loginWithCredentials(cleanAccount, password);
      setLoggedInUser(data.user);
      setLoginSuccess(true);
      authLoginSuccess(data.accessToken, data.user);
      setTimeout(() => {
        setLoginSuccess(false);
        setLoggedInUser(null);
        onLoginSuccess?.(data.user);
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
            <p>
              Chào mừng {loggedInUser?.fullName || 'bạn'} (
              {loggedInUser?.primaryRole === 'Admin'
                ? 'Quản trị viên'
                : loggedInUser?.primaryRole === 'Guard'
                ? 'Đội Bảo vệ'
                : loggedInUser?.primaryRole === 'Manager'
                ? 'Ban Quản lý'
                : 'Sinh viên'}
              ) đã quay trở lại hệ thống KTX.
            </p>
          </div>
        ) : role === 'student' ? (
          <div className="student-login-view" style={{ textAlign: 'center', padding: '16px 8px' }}>
            {errorMessage && (
              <div style={{ color: '#ef4444', backgroundColor: '#fee2e2', padding: '8px 12px', borderRadius: '6px', fontSize: '13px', marginBottom: '16px', textAlign: 'left' }}>
                {errorMessage}
              </div>
            )}
            <p style={{ color: '#4b5563', fontSize: '14px', lineHeight: '1.6', marginBottom: '20px' }}>
              Sinh viên đăng nhập trực tiếp bằng tài khoản Google đã được kích hoạt trong hệ thống.
            </p>
            <div id="google-btn-container" style={{ display: 'flex', justifyContent: 'center', minHeight: '44px', marginBottom: '16px' }}>
              {/* Google official button will be rendered here */}
            </div>
            {isSubmitting && <div style={{ fontSize: '13px', color: '#f97316', marginTop: '8px' }}>Đang xác thực tài khoản Google...</div>}
            <div style={{ fontSize: '12px', color: '#9ca3af', marginTop: '16px' }}>
              * Lưu ý: Chỉ tài khoản sinh viên đã được kích hoạt trong hệ thống KTX mới có thể truy cập.
            </div>
          </div>
        ) : (
          <form onSubmit={handleLoginSubmit} className="login-form">
            {errorMessage && (
              <div style={{ color: '#ef4444', backgroundColor: '#fee2e2', padding: '8px 12px', borderRadius: '6px', fontSize: '13px', marginBottom: '12px' }}>
                {errorMessage}
              </div>
            )}

            <div className="form-field-group">
              <label htmlFor="login-account" className="form-label">Tài khoản / Email cán bộ</label>
              <input
                id="login-account"
                type="text"
                required
                placeholder="mng01 hoặc mng01@fpt.edu.vn"
                value={account}
                onChange={(e) => setAccount(e.target.value)}
                className="form-input"
              />
            </div>

            <div className="form-field-group">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                <label htmlFor="login-password" className="form-label" style={{ marginBottom: 0 }}>Mật khẩu</label>
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  style={{
                    background: 'none',
                    border: 'none',
                    color: '#f97316',
                    cursor: 'pointer',
                    fontSize: '12px',
                    padding: 0
                  }}
                >
                  {showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                </button>
              </div>
              <input
                id="login-password"
                type={showPassword ? 'text' : 'password'}
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
              <a href="#forgot" onClick={(e) => { e.preventDefault(); alert('Vui lòng liên hệ Quản trị viên IT để cấp lại mật khẩu.'); }} className="forgot-link">
                Quên mật khẩu?
              </a>
            </div>

            <button type="submit" className="login-submit-btn" disabled={isSubmitting}>
              {isSubmitting ? 'Đang xác thực...' : 'Đăng nhập hệ thống'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
