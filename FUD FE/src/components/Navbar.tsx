import React, { useState } from 'react';
import { Logo } from './Icons';

interface NavbarProps {
  onOpenLogin: () => void;
  onNavigate: (sectionId: string) => void;
}

export const Navbar: React.FC<NavbarProps> = ({ onOpenLogin, onNavigate }) => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const handleNavClick = (id: string) => {
    onNavigate(id);
    setMobileMenuOpen(false);
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
          {/* About link as shown in the screenshot */}
          <button type="button" onClick={() => handleNavClick('about-section')} className="nav-link about-link">
            About
          </button>

          {/* Orange Login Button matching sample */}
          <button
            type="button"
            onClick={onOpenLogin}
            className="navbar-login-btn"
            id="navbar-login-button"
          >
            Login
          </button>
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
