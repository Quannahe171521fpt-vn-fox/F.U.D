import React, { useState } from 'react';
import { Logo } from './Icons';
import { useAuth } from '../contexts/AuthContext';

interface NavbarProps {
  onOpenLogin: () => void;
  onNavigate: (sectionId: string) => void;
  currentRole: 'guest' | 'student' | 'manager';
  currentView: 'home' | 'announcements' | 'management' | 'admin' | 'guard';
  onChangeView: (view: 'home' | 'announcements' | 'management' | 'admin' | 'guard') => void;
  onLogout: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  onOpenLogin,
  onNavigate,
  currentRole,
  currentView,
  onChangeView,
  onLogout,
}) => {
  const { currentUser } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const getRoleDisplayName = () => {
    if (!currentUser) return currentRole === 'manager' ? 'Cán bộ KTX' : 'Sinh viên';
    const roleMap: Record<string, string> = {
      Admin: 'Quản trị viên',
      Manager: 'Ban Quản lý',
      Guard: 'Đội Bảo vệ',
      Student: 'Sinh viên',
    };
    return `${currentUser.fullName} (${roleMap[currentUser.primaryRole] || currentUser.primaryRole})`;
  };

  // If in Management Portal, Admin Portal, or Guard Portal: keep minimal header with only Logo, Name and Logout
  if (currentView === 'management' || currentView === 'admin' || currentView === 'guard') {
    return (
      <header className="navbar-container">
        <div className="navbar-inner navbar-minimal">
          <div className="navbar-logo-link" style={{ cursor: 'default' }}>
            <Logo />
          </div>
          <div className="navbar-right-minimal">
            <div className="user-action-group">
              <span className="user-role-label">{getRoleDisplayName()}</span>
              <button
                type="button"
                onClick={onLogout}
                className="btn-logout"
                title="Đăng xuất khỏi hệ thống"
              >
                Đăng xuất
              </button>
            </div>
          </div>
        </div>
      </header>
    );
  }

  // If in Homepage or Announcements: Keep FULL original Homepage Header
  const handleNavClick = (id: string) => {
    if (currentView !== 'home') {
      onChangeView('home');
      setTimeout(() => onNavigate(id), 100);
    } else {
      onNavigate(id);
    }
    setMobileMenuOpen(false);
  };

  return (
    <header className="navbar-container">
      <div className="navbar-inner">
        {/* Brand Logo - FUD Dormitory with Golden Frog Mascot */}
        <a
          href="#hero"
          onClick={(e) => {
            e.preventDefault();
            onChangeView('home');
            onNavigate('hero');
          }}
          className="navbar-logo-link"
        >
          <Logo />
        </a>

        {/* Desktop Navigation Links (Full homepage links kept as original) */}
        <nav className="navbar-nav">
          <button
            type="button"
            onClick={() => {
              onChangeView('home');
              onNavigate('hero');
            }}
            className={`nav-link ${currentView === 'home' ? 'active-nav' : ''}`}
          >
            Trang chủ
          </button>

          <button
            type="button"
            onClick={() => onChangeView('announcements')}
            className={`nav-link ${currentView === 'announcements' ? 'active-nav font-bold' : ''}`}
          >
            Thông báo
          </button>

          <button
            type="button"
            onClick={() => handleNavClick('dorm-info')}
            className="nav-link"
          >
            Thông tin KTX
          </button>

          <button
            type="button"
            onClick={() => handleNavClick('room-types')}
            className="nav-link"
          >
            Loại phòng
          </button>

          <button
            type="button"
            onClick={() => handleNavClick('registration-guide')}
            className="nav-link"
          >
            Đăng ký KTX
          </button>

          <button
            type="button"
            onClick={() => handleNavClick('faq-section')}
            className="nav-link"
          >
            Hỏi đáp
          </button>

          <button
            type="button"
            onClick={() => handleNavClick('support-section')}
            className="nav-link"
          >
            Hỗ trợ & Liên hệ
          </button>

          <button
            type="button"
            onClick={() => handleNavClick('about-section')}
            className="nav-link about-link"
          >
            Giới thiệu
          </button>

          {/* Login or User Status */}
          {currentRole !== 'guest' ? (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <span className="user-role-label">
                {getRoleDisplayName()}
              </span>
              <button
                type="button"
                onClick={onLogout}
                className="btn-logout"
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
              Đăng nhập
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
          <button
            type="button"
            onClick={() => {
              onChangeView('home');
              setMobileMenuOpen(false);
            }}
            className="mobile-nav-link"
          >
            Trang chủ
          </button>
          <button
            type="button"
            onClick={() => {
              onChangeView('announcements');
              setMobileMenuOpen(false);
            }}
            className="mobile-nav-link"
          >
            Thông báo
          </button>
          <button
            type="button"
            onClick={() => handleNavClick('dorm-info')}
            className="mobile-nav-link"
          >
            Thông tin KTX
          </button>
          <button
            type="button"
            onClick={() => handleNavClick('room-types')}
            className="mobile-nav-link"
          >
            Loại phòng & Tiện ích
          </button>
          <button
            type="button"
            onClick={() => handleNavClick('registration-guide')}
            className="mobile-nav-link"
          >
            Hướng dẫn & Đăng ký
          </button>
          <button
            type="button"
            onClick={() => handleNavClick('faq-section')}
            className="mobile-nav-link"
          >
            Câu hỏi thường gặp (FAQ)
          </button>
          <button
            type="button"
            onClick={() => handleNavClick('support-section')}
            className="mobile-nav-link"
          >
            Hỗ trợ & Báo sự cố
          </button>
          <button
            type="button"
            onClick={() => handleNavClick('about-section')}
            className="mobile-nav-link about-mobile"
          >
            About
          </button>
          {currentRole !== 'guest' ? (
            <button
              type="button"
              onClick={() => {
                setMobileMenuOpen(false);
                onLogout();
              }}
              className="navbar-login-btn mobile-login-btn"
            >
              Đăng xuất
            </button>
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
