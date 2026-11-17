import React, { useState, useEffect } from 'react';
import { useAuth } from '../contexts/AuthContext';

export const ImpersonationBanner: React.FC = () => {
  const { impersonation, stopImpersonation } = useAuth();
  const [minutesLeft, setMinutesLeft] = useState<number>(0);

  useEffect(() => {
    if (!impersonation) return;

    const updateRemaining = () => {
      const remaining = Math.max(0, Math.ceil((impersonation.expiresAt - Date.now()) / (1000 * 60)));
      setMinutesLeft(remaining);
    };

    updateRemaining();
    const interval = setInterval(updateRemaining, 30000);
    return () => clearInterval(interval);
  }, [impersonation]);

  if (!impersonation) return null;

  const { student } = impersonation;

  return (
    <aside
      role="status"
      aria-live="polite"
      style={{
        position: 'sticky',
        top: 0,
        zIndex: 9999,
        backgroundColor: '#C2410C',
        backgroundImage: 'linear-gradient(90deg, #9A3412 0%, #EA580C 50%, #C2410C 100%)',
        color: '#FFFFFF',
        padding: '10px 24px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '16px',
        boxShadow: '0 4px 14px rgba(194, 65, 12, 0.45)',
        borderBottom: '2px solid #FDBA74',
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
        <span style={{ fontSize: '20px', display: 'flex', alignItems: 'center' }}>
          🛡️
        </span>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexWrap: 'wrap', fontSize: '14px' }}>
          <strong style={{ letterSpacing: '0.3px', textTransform: 'uppercase', fontSize: '13px' }}>
            Đang đăng nhập thay sinh viên
          </strong>
          <span style={{ opacity: 0.85 }}>|</span>
          <span>
            Họ tên: <strong>{student.fullName}</strong>
          </span>
          <span style={{ opacity: 0.85 }}>•</span>
          <span>
            MSSV:{' '}
            <code
              style={{
                backgroundColor: 'rgba(255, 255, 255, 0.2)',
                padding: '2px 8px',
                borderRadius: '4px',
                fontWeight: 700,
                color: '#FFF7ED',
              }}
            >
              {student.code}
            </code>
          </span>
          {student.email && (
            <>
              <span style={{ opacity: 0.85 }}>•</span>
              <span style={{ fontSize: '13px', opacity: 0.95 }}>({student.email})</span>
            </>
          )}
          {minutesLeft > 0 && (
            <span
              style={{
                fontSize: '12px',
                backgroundColor: 'rgba(0, 0, 0, 0.25)',
                padding: '2px 8px',
                borderRadius: '12px',
                marginLeft: '4px',
              }}
            >
              ⏱️ Phiên còn ~{minutesLeft} phút
            </span>
          )}
        </div>
      </div>

      <button
        type="button"
        onClick={stopImpersonation}
        style={{
          padding: '8px 18px',
          backgroundColor: '#FFFFFF',
          color: '#9A3412',
          border: 'none',
          borderRadius: '8px',
          fontWeight: 700,
          fontSize: '13px',
          cursor: 'pointer',
          whiteSpace: 'nowrap',
          display: 'flex',
          alignItems: 'center',
          gap: '6px',
          boxShadow: '0 2px 6px rgba(0, 0, 0, 0.15)',
          transition: 'all 0.15s ease-in-out',
        }}
        onMouseEnter={(e) => {
          e.currentTarget.style.backgroundColor = '#FFF7ED';
          e.currentTarget.style.transform = 'translateY(-1px)';
        }}
        onMouseLeave={(e) => {
          e.currentTarget.style.backgroundColor = '#FFFFFF';
          e.currentTarget.style.transform = 'none';
        }}
      >
        <span style={{ fontSize: '14px' }}>✕</span>
        <span>Thoát và về Dashboard Quản lý</span>
      </button>
    </aside>
  );
};

