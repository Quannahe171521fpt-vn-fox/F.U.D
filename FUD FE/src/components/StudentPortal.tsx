import React from 'react';
import { useAuth } from '../contexts/AuthContext';

interface StudentPortalProps {
  onLogout: () => void;
  onBackToHome: () => void;
  onOpenAnnouncements: () => void;
  onNavigateToSection: (sectionId: string) => void;
}

export const StudentPortal: React.FC<StudentPortalProps> = ({
  onLogout,
  onBackToHome,
  onOpenAnnouncements,
  onNavigateToSection,
}) => {
  const { currentUser } = useAuth();

  return (
    <div
      style={{
        minHeight: 'calc(100vh - 72px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: '#F8FAFC',
        padding: '32px 20px',
      }}
    >
      <div
        style={{
          maxWidth: '560px',
          width: '100%',
          backgroundColor: '#FFFFFF',
          borderRadius: '16px',
          border: '1px solid #E2E8F0',
          boxShadow: '0 10px 25px -5px rgba(15, 23, 42, 0.08), 0 8px 10px -6px rgba(15, 23, 42, 0.04)',
          padding: '36px 32px',
          textAlign: 'center',
        }}
      >
        {/* Student Graduation Cap Badge */}
        <div
          style={{
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            backgroundColor: '#FFF7ED',
            color: '#EA580C',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 20px',
            boxShadow: 'inset 0 0 0 1px #FDBA74',
          }}
        >
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
            <path d="M6 12v5c3 3 9 3 12 0v-5" />
          </svg>
        </div>

        <span
          style={{
            display: 'inline-block',
            padding: '4px 12px',
            borderRadius: '9999px',
            backgroundColor: '#FFF7ED',
            color: '#C2410C',
            fontSize: '12px',
            fontWeight: 700,
            letterSpacing: '0.05em',
            textTransform: 'uppercase',
            marginBottom: '12px',
          }}
        >
          Cổng Sinh Viên Nội Trú (Student Portal)
        </span>

        <h1
          style={{
            fontSize: '22px',
            fontWeight: 700,
            color: '#0F172A',
            margin: '0 0 8px',
          }}
        >
          Ký Túc Xá Đại Học FPT Đà Nẵng
        </h1>

        <p
          style={{
            fontSize: '14px',
            color: '#64748B',
            margin: '0 0 24px',
            lineHeight: 1.5,
          }}
        >
          Chào mừng sinh viên <strong>{currentUser?.fullName || 'Sinh viên'}</strong> đã đăng nhập thành công.
        </p>

        {/* Account Info Box */}
        <div
          style={{
            backgroundColor: '#F8FAFC',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
            textAlign: 'left',
            marginBottom: '24px',
            fontSize: '13px',
            color: '#334155',
            lineHeight: 1.9,
          }}
        >
          <div><strong>Mã sinh viên (MSSV):</strong> {currentUser?.code || 'SE...'}</div>
          <div><strong>Họ và tên:</strong> {currentUser?.fullName || '—'}</div>
          <div><strong>Email FPT:</strong> {currentUser?.email || '—'}</div>
          <div><strong>Vai trò:</strong> Sinh viên (Student)</div>
        </div>

        {/* Quick Action Navigation Grid */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: '1fr 1fr',
            gap: '12px',
            marginBottom: '24px',
          }}
        >
          <button
            type="button"
            onClick={() => {
              onBackToHome();
              setTimeout(() => onNavigateToSection('registration-guide'), 150);
            }}
            style={{
              padding: '12px 14px',
              backgroundColor: '#EA580C',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: '10px',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '6px',
            }}
          >
            <span>📝</span> Đăng ký KTX
          </button>

          <button
            type="button"
            onClick={onOpenAnnouncements}
            style={{
              padding: '12px 14px',
              backgroundColor: '#F1F5F9',
              color: '#0F172A',
              border: '1px solid #CBD5E1',
              borderRadius: '10px',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '6px',
            }}
          >
            <span>📢</span> Xem Thông báo KTX
          </button>

          <button
            type="button"
            onClick={() => {
              onBackToHome();
              setTimeout(() => onNavigateToSection('room-types'), 150);
            }}
            style={{
              padding: '12px 14px',
              backgroundColor: '#F1F5F9',
              color: '#0F172A',
              border: '1px solid #CBD5E1',
              borderRadius: '10px',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '6px',
            }}
          >
            <span>🛏️</span> Tra cứu Loại phòng
          </button>

          <button
            type="button"
            onClick={onBackToHome}
            style={{
              padding: '12px 14px',
              backgroundColor: '#F1F5F9',
              color: '#0F172A',
              border: '1px solid #CBD5E1',
              borderRadius: '10px',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: '6px',
            }}
          >
            <span>🏠</span> Về Trang chủ
          </button>
        </div>

        {/* Action Button: Logout */}
        <button
          type="button"
          onClick={onLogout}
          style={{
            width: '100%',
            padding: '12px 20px',
            backgroundColor: '#EF4444',
            color: '#FFFFFF',
            border: 'none',
            borderRadius: '10px',
            fontSize: '14px',
            fontWeight: 600,
            cursor: 'pointer',
            transition: 'background-color 0.15s ease',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '8px',
          }}
          onMouseOver={(e) => (e.currentTarget.style.backgroundColor = '#DC2626')}
          onMouseOut={(e) => (e.currentTarget.style.backgroundColor = '#EF4444')}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <polyline points="16 17 21 12 16 7" />
            <line x1="21" y1="12" x2="9" y2="12" />
          </svg>
          Đăng xuất tài khoản
        </button>
      </div>
    </div>
  );
};
