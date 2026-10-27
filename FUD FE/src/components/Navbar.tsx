import React, { useState } from 'react';
import { Logo, UserIcon } from './Icons';
import { useAuth } from '../context/AuthContext';

interface NavbarProps {
  onOpenLogin: () => void;
  onNavigate: (sectionId: string) => void;
  onGoToDashboard?: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({ onOpenLogin, onNavigate, onGoToDashboard }) => {
  const { user, isAuthenticated, logout } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const handleNavClick = (id: string) => {
    onNavigate(id);
    setMobileMenuOpen(false);
  };

  const getPrimaryRole = () => {
    if (!user || !user.roles.length) return 'Thành viên';
    if (user.roles.includes('Admin')) return 'Admin';
    if (user.roles.includes('Manager')) return 'Cán bộ QL';
    if (user.roles.includes('Guard')) return 'Bảo vệ';
    if (user.roles.includes('Student')) return 'Sinh viên';
    return user.roles[0];
  };

  return (
    <header className="navbar-container">
      <div className="navbar-inner">
        {/* Brand Logo - On campus Dormitory */}
        <a href="#hero" onClick={(e) => { e.preventDefault(); handleNavClick('hero'); }} className="navbar-logo-link">
          <Logo />
        </a>

        {/* Desktop Navigation Links */}
        <nav className="navbar-nav">
          <button type="button" onClick={() => handleNavClick('dorm-info')} className="nav-link">
            Thông tin KTX
          </button>
          <button type="button" onClick={() => handleNavClick('room-types')} className="nav-link">
            Loại phòng
          </button>
          <button type="button" onClick={() => handleNavClick('registration-guide')} className="nav-link">
            Đăng ký KTX
          </button>
          <button type="button" onClick={() => handleNavClick('faq-section')} className="nav-link">
            FAQ
          </button>
          <button type="button" onClick={() => handleNavClick('support-section')} className="nav-link">
            Hỗ trợ & Liên hệ
          </button>
          <button type="button" onClick={() => handleNavClick('about-section')} className="nav-link about-link">
            About
          </button>

          {/* User Profile / Login State */}
          {isAuthenticated && user ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginLeft: '8px' }}>
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  backgroundColor: '#f8fafc',
                  border: '1px solid #e2e8f0',
                  padding: '4px 10px',
                  borderRadius: '20px',
                  fontSize: '0.85rem',
                }}
              >
                <UserIcon size={14} color="#D96528" />
                <span style={{ fontWeight: 600, color: '#1e293b' }}>{user.fullName}</span>
                <span
                  style={{
                    backgroundColor: '#ea580c',
                    color: '#fff',
                    padding: '1px 6px',
                    borderRadius: '10px',
                    fontSize: '0.72rem',
                    fontWeight: 700,
                  }}
                >
                  {getPrimaryRole()}
                </span>
                <span
                  style={{
                    backgroundColor: user.gender === 'Male' ? '#dbeafe' : '#fce7f3',
                    color: user.gender === 'Male' ? '#1d4ed8' : '#be185d',
                    padding: '1px 6px',
                    borderRadius: '10px',
                    fontSize: '0.72rem',
                    fontWeight: 700,
                  }}
                >
                  {user.gender === 'Male' ? 'Nam' : 'Nữ'}
                </span>
              </div>

              {onGoToDashboard && (
                <button
                  type="button"
                  onClick={onGoToDashboard}
                  style={{
                    backgroundColor: '#ea580c',
                    color: '#fff',
                    border: 'none',
                    padding: '5px 12px',
                    borderRadius: '6px',
                    fontSize: '0.82rem',
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  Vào Cổng {getPrimaryRole()}
                </button>
              )}

              <button
                type="button"
                onClick={logout}
                style={{
                  backgroundColor: '#f1f5f9',
                  border: '1px solid #cbd5e1',
                  color: '#475569',
                  padding: '5px 12px',
                  borderRadius: '6px',
                  fontSize: '0.82rem',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Đăng xuất
              </button>
            </div>
          ) : (
            <button
              type="button"
              onClick={onOpenLogin}
              className="navbar-login-btn"
              id="navbar-login-button"
            >
              Login
            </button>
          )}
        </nav>

        {/* Mobile Hamburger Button */}
        <button
          type="button"
          className="mobile-menu-toggle"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
          aria-label="Toggle navigation menu"
        >
          <span className={`bar ${mobileMenuOpen ? 'open' : ''}`}></span>
          <span className={`bar ${mobileMenuOpen ? 'open' : ''}`}></span>
          <span className={`bar ${mobileMenuOpen ? 'open' : ''}`}></span>
        </button>
      </div>

      {/* Mobile Dropdown Menu */}
      {mobileMenuOpen && (
        <div className="mobile-dropdown">
          <button type="button" onClick={() => handleNavClick('dorm-info')} className="mobile-nav-link">
            Thông tin KTX
          </button>
          <button type="button" onClick={() => handleNavClick('room-types')} className="mobile-nav-link">
            Loại phòng & Tiện ích
          </button>
          <button type="button" onClick={() => handleNavClick('registration-guide')} className="mobile-nav-link">
            Hướng dẫn & Đăng ký
          </button>
          <button type="button" onClick={() => handleNavClick('faq-section')} className="mobile-nav-link">
            Câu hỏi thường gặp (FAQ)
          </button>
          <button type="button" onClick={() => handleNavClick('support-section')} className="mobile-nav-link">
            Hỗ trợ & Báo sự cố
          </button>
          <button type="button" onClick={() => handleNavClick('about-section')} className="mobile-nav-link about-mobile">
            About
          </button>
          {isAuthenticated && user ? (
            <div style={{ padding: '8px 16px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
              <div style={{ fontSize: '0.9rem', color: '#1e293b' }}>
                👤 {user.fullName} ({getPrimaryRole()} - {user.gender === 'Male' ? 'Nam' : 'Nữ'})
              </div>
              {onGoToDashboard && (
                <button
                  type="button"
                  onClick={() => {
                    setMobileMenuOpen(false);
                    onGoToDashboard();
                  }}
                  className="navbar-login-btn mobile-login-btn"
                >
                  Vào Cổng {getPrimaryRole()}
                </button>
              )}
              <button
                type="button"
                onClick={() => {
                  setMobileMenuOpen(false);
                  logout();
                }}
                className="navbar-login-btn mobile-login-btn"
                style={{ backgroundColor: '#dc2626' }}
              >
                Đăng xuất
              </button>
            </div>
          ) : (
            <button
              type="button"
              onClick={() => {
                setMobileMenuOpen(false);
                onOpenLogin();
              }}
              className="navbar-login-btn mobile-login-btn"
            >
              Login
            </button>
          )}
        </div>
      )}
    </header>
  );
};
