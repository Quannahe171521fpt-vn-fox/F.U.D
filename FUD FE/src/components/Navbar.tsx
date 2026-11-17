import React, { useState } from 'react';
import { Logo } from './Icons';
import { NotificationBell } from './NotificationBell';
import { useAuth } from '../contexts/AuthContext';
import type { NotificationItem } from '../services/api';
import type { AppView } from '../utils/notificationRouting';

interface NavbarProps {
  onOpenLogin: () => void;
  onNavigate: (sectionId: string) => void;
  currentRole?: 'guest' | 'student' | 'manager';
  currentView: AppView;
  onChangeView: (view: AppView) => void;
  onLogout: () => void;
  onNavigateNotification?: (item: NotificationItem) => void;
  onOpenAllNotifications?: () => void;
  notificationSyncKey?: number;
  onNotificationsChanged?: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({
  onOpenLogin,
  onNavigate,
  currentView,
  onChangeView,
  onLogout,
  onNavigateNotification,
  onOpenAllNotifications,
  notificationSyncKey,
  onNotificationsChanged,
}) => {
  const { currentUser } = useAuth();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const getRoleDisplayName = () => {
    if (!currentUser) return '';
    const roleMap: Record<string, string> = {
      Admin: 'Quản trị viên',
      Manager: 'Ban Quản lý',
      Guard: 'Đội Bảo vệ',
      Student: 'Sinh viên',
    };
    return `${currentUser.fullName} (${roleMap[currentUser.primaryRole] || currentUser.primaryRole})`;
  };

  // Khi đã đăng nhập vào các Portal (Manager, Admin, Guard, Student):
  // Xóa nút "Xem Trang chủ" / navigation trang chủ ở header theo yêu cầu:
  // "khi đăng nhập vào từng role xóa nút xem trang chủ ở header"
  if (currentUser) {
    return (
      <header className="navbar-container">
        <div className="navbar-inner navbar-minimal">
          <div className="navbar-logo-link" style={{ cursor: 'default' }}>
            <Logo />
          </div>
          <div className="navbar-right-minimal">
            <div className="user-action-group">
              <span className="user-role-label">{getRoleDisplayName()}</span>
              {currentUser.primaryRole !== 'Admin' && (
                <NotificationBell
                  key={currentUser.id}
                  onNavigateTarget={onNavigateNotification}
                  onOpenAllNotifications={onOpenAllNotifications}
                  syncKey={notificationSyncKey}
                  onNotificationsChanged={onNotificationsChanged}
                />
              )}
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

  // Khách (Guest) chưa đăng nhập: Hiển thị đầy đủ thanh điều hướng Trang chủ KTX
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
        <div className="navbar-left">
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
        </div>

        {/* Desktop Navigation Links */}
        <nav className="navbar-nav navbar-nav-center">
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
        </nav>

        {/* Right Action: Login Button */}
        <div className="navbar-right">
          <button
            type="button"
            onClick={onOpenLogin}
            className="navbar-login-btn"
            id="navbar-login-button"
          >
            Đăng nhập
          </button>
        </div>

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
        </div>
      )}
    </header>
  );
};
