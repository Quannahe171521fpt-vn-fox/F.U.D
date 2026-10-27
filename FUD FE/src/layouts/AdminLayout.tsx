import { useState, type ReactNode } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import '../styles/admin.css';

interface AdminLayoutProps {
  children: ReactNode;
}

const NAV_ITEMS = [
  {
    to: '/admin/accounts',
    label: 'Tài khoản',
    icon: (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
      </svg>
    ),
  },
  {
    to: '/admin/accounts/new',
    label: 'Tạo tài khoản',
    icon: (
      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
        <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <line x1="19" y1="8" x2="19" y2="14" />
        <line x1="16" y1="11" x2="22" y2="11" />
      </svg>
    ),
  },
];

const BrandMark = () => (
  <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6" />
  </svg>
);

export function AdminLayout({ children }: AdminLayoutProps) {
  const navigate = useNavigate();
  const session = useAuthStore((s) => s.session);
  const logout = useAuthStore((s) => s.logout);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const handleLogout = () => {
    logout();
    navigate('/admin/login', { replace: true });
  };

  return (
    <div className="admin-shell">
      {/* Desktop sidebar */}
      <aside className="admin-sidebar">
        {/* Brand */}
        <div className="admin-brand">
          <div className="admin-brand-mark">
            <BrandMark />
          </div>
          <div>
            <p className="admin-brand-name">F.U.D Admin</p>
            <p className="admin-brand-subtitle">Ký túc xá</p>
          </div>
        </div>

        {/* Nav */}
        <div className="admin-nav-section">Quản lý</div>
        <nav aria-label="Admin navigation">
          {NAV_ITEMS.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/admin/accounts'}
              className={({ isActive }) =>
                `admin-nav-link${isActive ? ' active' : ''}`
              }
            >
              {item.icon}
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>

        {/* User box */}
        <div className="admin-user-box">
          <p className="admin-user-name">{session?.fullName || 'Quản trị viên'}</p>
          <p className="admin-user-sub">{session?.username}</p>
          <button
            type="button"
            onClick={handleLogout}
            className="admin-logout-btn"
          >
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
              <polyline points="16 17 21 12 16 7" />
              <line x1="21" y1="12" x2="9" y2="12" />
            </svg>
            Đăng xuất
          </button>
        </div>
      </aside>

      {/* Mobile top bar */}
      <header className="admin-mobile-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div className="admin-brand-mark" style={{ width: 30, height: 30 }}>
            <BrandMark />
          </div>
          <span className="admin-brand-name">F.U.D Admin</span>
        </div>
        <button
          type="button"
          aria-label={isMenuOpen ? 'Đóng menu' : 'Mở menu'}
          onClick={() => setIsMenuOpen((v) => !v)}
          style={{
            padding: '6px 12px',
            borderRadius: 7,
            border: '1px solid var(--a-rule-strong)',
            background: 'transparent',
            fontSize: 12,
            fontWeight: 600,
            color: 'var(--a-ink-2)',
            cursor: 'pointer',
          }}
        >
          {isMenuOpen ? 'Đóng' : 'Menu'}
        </button>
      </header>

      {/* Mobile dropdown nav */}
      {isMenuOpen && (
        <aside className="admin-sidebar open" aria-label="Mobile admin navigation">
          <nav style={{ padding: '8px 10px' }}>
            {NAV_ITEMS.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.to === '/admin/accounts'}
                onClick={() => setIsMenuOpen(false)}
                className={({ isActive }) =>
                  `admin-nav-link${isActive ? ' active' : ''}`
                }
              >
                {item.icon}
                <span>{item.label}</span>
              </NavLink>
            ))}
          </nav>
          <div className="admin-user-box">
            <button type="button" onClick={handleLogout} className="admin-logout-btn">
              Đăng xuất
            </button>
          </div>
        </aside>
      )}

      {/* Main content — white sheet canvas */}
      <main className="admin-main">
        <div className="admin-canvas">
          {children}
        </div>
      </main>
    </div>
  );
}

export default AdminLayout;
