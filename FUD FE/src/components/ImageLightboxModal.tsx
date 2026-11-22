import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';

export interface ImageLightboxModalProps {
  imageUrl: string | null;
  title?: string;
  subtitle?: string;
  onClose: () => void;
}

export const ImageLightboxModal: React.FC<ImageLightboxModalProps> = ({
  imageUrl,
  title,
  subtitle,
  onClose,
}) => {
  useEffect(() => {
    if (!imageUrl) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        e.stopImmediatePropagation();
        onClose();
      }
    };

    window.addEventListener('keydown', handleKeyDown, true);
    return () => window.removeEventListener('keydown', handleKeyDown, true);
  }, [imageUrl, onClose]);

  if (!imageUrl || typeof document === 'undefined') {
    return null;
  }

  return createPortal(
    <div
      onClick={onClose}
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 10060,
        backgroundColor: 'rgba(15, 23, 42, 0.85)',
        backdropFilter: 'blur(4px)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: 20,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          backgroundColor: '#0F172A',
          borderRadius: 14,
          border: '1px solid rgba(255, 255, 255, 0.14)',
          padding: 16,
          maxWidth: '94vw',
          maxHeight: '92vh',
          display: 'flex',
          flexDirection: 'column',
          gap: 12,
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.6)',
        }}
      >
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 16,
          }}
        >
          <div>
            <div style={{ fontSize: 15, fontWeight: 700, color: '#F8FAFC' }}>
              {title || 'Ảnh bằng chứng công tơ'}
            </div>
            {subtitle && (
              <div style={{ fontSize: 12, color: '#94A3B8', marginTop: 2 }}>
                {subtitle}
              </div>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <a
              href={imageUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                padding: '6px 12px',
                borderRadius: 6,
                border: '1px solid rgba(255,255,255,0.2)',
                background: 'rgba(255,255,255,0.08)',
                color: '#E2E8F0',
                fontSize: 12,
                fontWeight: 600,
                textDecoration: 'none',
              }}
            >
              Mở tab mới
            </a>
            <button
              type="button"
              onClick={onClose}
              aria-label="Đóng ảnh"
              style={{
                width: 32,
                height: 32,
                borderRadius: '50%',
                border: 'none',
                background: 'rgba(255,255,255,0.14)',
                color: '#FFFFFF',
                fontSize: 16,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              ✕
            </button>
          </div>
        </div>

        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            overflow: 'auto',
          }}
        >
          <img
            src={imageUrl}
            alt={title || 'Ảnh công tơ điện nước'}
            style={{
              maxWidth: '92vw',
              maxHeight: '80vh',
              objectFit: 'contain',
              borderRadius: 8,
              backgroundColor: '#020617',
            }}
          />
        </div>
      </div>
    </div>,
    document.body
  );
};
