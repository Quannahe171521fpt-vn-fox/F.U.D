import React from 'react';
import { useAuth } from '../contexts/AuthContext';

interface GuardPortalProps {
  onLogout: () => void;
}

export const GuardPortal: React.FC<GuardPortalProps> = ({ onLogout }) => {
  const { currentUser } = useAuth();

  return (
    <div
      style={{
        minHeight: 'calc(100vh - 72px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        backgroundColor: '#F8FAFC',
        padding: '24px',
      }}
    >
      <div
        style={{
          maxWidth: '520px',
          width: '100%',
          backgroundColor: '#FFFFFF',
          borderRadius: '16px',
          border: '1px solid #E2E8F0',
          boxShadow: '0 10px 25px -5px rgba(15, 23, 42, 0.08), 0 8px 10px -6px rgba(15, 23, 42, 0.04)',
          padding: '36px 32px',
          textAlign: 'center',
        }}
      >
        {/* Guard Shield Icon Badge */}
        <div
          style={{
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            backgroundColor: '#ECFDF5',
            color: '#059669',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 20px',
            boxShadow: 'inset 0 0 0 1px #A7F3D0',
          }}
        >
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
        </div>

        <span
          style={{
            display: 'inline-block',
            padding: '4px 12px',
            borderRadius: '9999px',
            backgroundColor: '#ECFDF5',
            color: '#047857',
            fontSize: '12px',
            fontWeight: 700,
            letterSpacing: '0.05em',
            textTransform: 'uppercase',
            marginBottom: '12px',
          }}
        >
          Cán bộ Đội Bảo vệ (Guard)
        </span>

        <h1
          style={{
            fontSize: '22px',
            fontWeight: 700,
            color: '#0F172A',
            margin: '0 0 8px',
          }}
        >
          Cổng Nghiệp Vụ Bảo Vệ KTX
        </h1>

        <p
          style={{
            fontSize: '14px',
            color: '#64748B',
            margin: '0 0 24px',
            lineHeight: 1.5,
          }}
        >
          Chào mừng <strong>{currentUser?.fullName || 'Cán bộ Bảo vệ'}</strong> đã đăng nhập.
        </p>

        {/* Account Info Box */}
        <div
          style={{
            backgroundColor: '#F1F5F9',
            borderRadius: '12px',
            padding: '16px',
            textAlign: 'left',
            marginBottom: '24px',
            fontSize: '13px',
            color: '#334155',
            lineHeight: 1.8,
          }}
        >
          <div><strong>Mã cán bộ:</strong> {currentUser?.code || 'GRD01'}</div>
          <div><strong>Email:</strong> {currentUser?.email || 'guard01@fpt.edu.vn'}</div>
          <div><strong>Vai trò:</strong> Đội Bảo vệ KTX (Guard)</div>
        </div>

        {/* Placeholder Notice */}
        <div
          style={{
            backgroundColor: '#FEF3C7',
            border: '1px solid #FCD34D',
            borderRadius: '10px',
            padding: '12px 16px',
            fontSize: '13px',
            color: '#92400E',
            marginBottom: '28px',
            lineHeight: 1.5,
          }}
        >
          🛡️ <em>Chức năng bảo vệ (Ghi chỉ số điện nước, Quản lý an ninh, Kiểm tra ra vào) đang được triển khai.</em>
        </div>

        {/* Action Button: Logout */}
        <button
          type="button"
          onClick={onLogout}
          style={{
            width: '100%',
            padding: '12px 20px',
            backgroundColor: '#DC2626',
            color: '#FFFFFF',
            border: 'none',
            borderRadius: '10px',
            fontSize: '15px',
            fontWeight: 600,
            cursor: 'pointer',
            transition: 'background-color 0.15s ease',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '8px',
          }}
          onMouseOver={(e) => (e.currentTarget.style.backgroundColor = '#B91C1C')}
          onMouseOut={(e) => (e.currentTarget.style.backgroundColor = '#DC2626')}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <polyline points="16 17 21 12 16 7" />
            <line x1="21" y1="12" x2="9" y2="12" />
          </svg>
          Đăng xuất khỏi tài khoản Bảo vệ
        </button>
      </div>
    </div>
  );
};
