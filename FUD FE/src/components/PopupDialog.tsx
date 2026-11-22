import React, { useCallback, useEffect, useRef, useState } from 'react';

type PopupVariant = 'info' | 'success' | 'warning' | 'danger';

interface PopupOptions {
  title?: string;
  variant?: PopupVariant;
  confirmText?: string;
  cancelText?: string;
}

interface PopupState extends PopupOptions {
  message: string;
  isConfirm: boolean;
}

const VARIANT_STYLE: Record<PopupVariant, { bg: string; color: string; icon: string; btn: string }> = {
  info: { bg: '#EFF6FF', color: '#1D4ED8', icon: 'i', btn: '#EA580C' },
  success: { bg: '#DCFCE7', color: '#15803D', icon: '✓', btn: '#16A34A' },
  warning: { bg: '#FEF3C7', color: '#B45309', icon: '!', btn: '#EA580C' },
  danger: { bg: '#FEE2E2', color: '#B91C1C', icon: '!', btn: '#DC2626' },
};

/**
 * Popup dùng chung thay cho window.alert / window.confirm.
 * - showAlert(message, options): hiển thị thông báo, đóng bằng nút "Đã hiểu".
 * - showConfirm(message, options): trả về Promise<boolean> (true nếu bấm xác nhận).
 * Nhớ render `{popup}` trong JSX của component sử dụng hook.
 */
export function usePopup() {
  const [state, setState] = useState<PopupState | null>(null);
  const resolverRef = useRef<((value: boolean) => void) | null>(null);

  const close = useCallback((result: boolean) => {
    resolverRef.current?.(result);
    resolverRef.current = null;
    setState(null);
  }, []);

  const showAlert = useCallback((message: string, options?: PopupOptions): Promise<void> => {
    return new Promise<void>((resolve) => {
      resolverRef.current = () => resolve();
      setState({ message, isConfirm: false, variant: 'warning', ...options });
    });
  }, []);

  const showConfirm = useCallback((message: string, options?: PopupOptions): Promise<boolean> => {
    return new Promise<boolean>((resolve) => {
      resolverRef.current = resolve;
      setState({ message, isConfirm: true, variant: 'info', ...options });
    });
  }, []);

  useEffect(() => {
    if (!state) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') close(false);
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [state, close]);

  const popup: React.ReactNode = state ? (
    <PopupView state={state} onClose={close} />
  ) : null;

  return { showAlert, showConfirm, popup };
}

const PopupView: React.FC<{ state: PopupState; onClose: (result: boolean) => void }> = ({ state, onClose }) => {
  const v = VARIANT_STYLE[state.variant || 'info'];
  const title =
    state.title ||
    (state.isConfirm ? 'Xác nhận thao tác' : state.variant === 'success' ? 'Thành công' : 'Thông báo');

  return (
    <div
      role="dialog"
      aria-modal="true"
      onClick={() => onClose(false)}
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(15, 23, 42, 0.55)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 10000,
        padding: 20,
      }}
    >
      <div
        onClick={(e) => e.stopPropagation()}
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: 14,
          maxWidth: 440,
          width: '100%',
          padding: '24px',
          boxShadow: '0 20px 30px -10px rgba(0,0,0,0.25)',
          textAlign: 'center',
        }}
      >
        <div
          style={{
            width: 52,
            height: 52,
            borderRadius: '50%',
            backgroundColor: v.bg,
            color: v.color,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 26,
            fontWeight: 800,
            margin: '0 auto 12px',
          }}
        >
          {v.icon}
        </div>
        <h3 style={{ fontSize: 18, fontWeight: 700, color: '#0F172A', margin: '0 0 8px' }}>{title}</h3>
        <p style={{ fontSize: 14, color: '#475569', lineHeight: 1.6, margin: '0 0 20px', whiteSpace: 'pre-line' }}>
          {state.message}
        </p>
        <div style={{ display: 'flex', justifyContent: 'center', gap: 10 }}>
          {state.isConfirm && (
            <button
              type="button"
              onClick={() => onClose(false)}
              style={{
                padding: '9px 20px',
                backgroundColor: '#F1F5F9',
                color: '#334155',
                border: '1px solid #CBD5E1',
                borderRadius: 8,
                fontWeight: 600,
                fontSize: 13,
                cursor: 'pointer',
              }}
            >
              {state.cancelText || 'Hủy'}
            </button>
          )}
          <button
            type="button"
            autoFocus
            onClick={() => onClose(true)}
            style={{
              padding: '9px 22px',
              backgroundColor: v.btn,
              color: '#FFFFFF',
              border: 'none',
              borderRadius: 8,
              fontWeight: 700,
              fontSize: 13,
              cursor: 'pointer',
            }}
          >
            {state.confirmText || (state.isConfirm ? 'Xác nhận' : 'Đã hiểu')}
          </button>
        </div>
      </div>
    </div>
  );
};
