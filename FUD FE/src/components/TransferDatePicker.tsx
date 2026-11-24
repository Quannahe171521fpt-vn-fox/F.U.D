import React, { useEffect, useRef, useState } from 'react';
import { formatDate } from '../utils/dateUtils';

function formatYmd(year: number, monthIndex: number, day: number): string {
  return `${year}-${String(monthIndex + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

const WEEKDAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

export interface TransferDatePickerProps {
  value: string;
  minDate?: string;
  maxDate?: string;
  fullWidth?: boolean;
  openDirection?: 'down' | 'up';
  onChange: (value: string) => void;
}

export const TransferDatePicker: React.FC<TransferDatePickerProps> = ({
  value,
  minDate,
  maxDate,
  fullWidth = false,
  openDirection = 'down',
  onChange,
}) => {
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const resolveInitialDate = () => {
    const refDate = value || minDate;
    if (refDate) {
      const [y, m] = refDate.split('-').map(Number);
      if (y && m) return { year: y, month: m - 1 };
    }
    const now = new Date();
    return { year: now.getFullYear(), month: now.getMonth() };
  };

  const initial = resolveInitialDate();
  const [viewYear, setViewYear] = useState<number>(initial.year);
  const [viewMonth, setViewMonth] = useState<number>(initial.month);

  useEffect(() => {
    if (!open) return;
    const next = resolveInitialDate();
    setViewYear(next.year);
    setViewMonth(next.month);
  }, [open, value, minDate]);

  useEffect(() => {
    if (!open) return;
    const onDocMouseDown = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', onDocMouseDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onDocMouseDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  const firstDayOfMonth = new Date(viewYear, viewMonth, 1);
  // Monday = 0 ... Sunday = 6
  const startOffset = (firstDayOfMonth.getDay() + 6) % 7;
  const daysInMonth = new Date(viewYear, viewMonth + 1, 0).getDate();

  const cells: Array<{ day: number; ymd: string; disabled: boolean } | null> = [];
  for (let i = 0; i < startOffset; i++) cells.push(null);
  for (let day = 1; day <= daysInMonth; day++) {
    const ymd = formatYmd(viewYear, viewMonth, day);
    const disabled = Boolean((minDate && ymd < minDate) || (maxDate && ymd > maxDate));
    cells.push({ day, ymd, disabled });
  }

  const goPrevMonth = () => {
    if (viewMonth === 0) {
      setViewYear(y => y - 1);
      setViewMonth(11);
    } else {
      setViewMonth(m => m - 1);
    }
  };

  const goNextMonth = () => {
    if (viewMonth === 11) {
      setViewYear(y => y + 1);
      setViewMonth(0);
    } else {
      setViewMonth(m => m + 1);
    }
  };

  return (
    <div
      ref={containerRef}
      style={{
        position: 'relative',
        display: fullWidth ? 'block' : 'inline-block',
        width: fullWidth ? '100%' : undefined,
      }}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 6, width: fullWidth ? '100%' : undefined }}>
        <button
          type="button"
          onClick={() => setOpen(prev => !prev)}
          style={{
            flex: fullWidth ? 1 : undefined,
            minWidth: fullWidth ? undefined : 185,
            width: fullWidth ? '100%' : undefined,
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 10,
            padding: '8px 12px',
            borderRadius: '8px',
            border: '1px solid #CBD5E1',
            fontSize: '13px',
            backgroundColor: '#FFF',
            color: value ? '#0F172A' : '#94A3B8',
            cursor: 'pointer',
            boxSizing: 'border-box',
          }}
        >
          <span>{value ? formatDate(value) : 'dd/mm/yyyy'}</span>
          <span aria-hidden="true" style={{ fontSize: 14, color: '#475569' }}>📅</span>
        </button>

        {value && (
          <button
            type="button"
            onClick={() => onChange('')}
            title="Xóa ngày dự kiến"
            style={{
              padding: '7px 10px',
              borderRadius: '8px',
              border: '1px solid #E2E8F0',
              backgroundColor: '#F8FAFC',
              color: '#475569',
              fontSize: '12px',
              cursor: 'pointer',
              whiteSpace: 'nowrap',
            }}
          >
            Xóa
          </button>
        )}
      </div>

      {open && (
        <div
          style={{
            position: 'absolute',
            ...(openDirection === 'up'
              ? { bottom: 'calc(100% + 6px)' }
              : { top: 'calc(100% + 6px)' }),
            left: 0,
            zIndex: 60,
            width: 276,
            backgroundColor: '#FFFFFF',
            border: '1px solid #CBD5E1',
            borderRadius: '10px',
            boxShadow: '0 10px 25px rgba(15, 23, 42, 0.14)',
            padding: '12px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 10 }}>
            <button
              type="button"
              onClick={goPrevMonth}
              aria-label="Tháng trước"
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                border: '1px solid #E2E8F0',
                background: '#F8FAFC',
                color: '#334155',
                cursor: 'pointer',
                fontWeight: 700,
              }}
            >
              ‹
            </button>
            <span style={{ fontSize: 13, fontWeight: 700, color: '#0F172A' }}>
              Tháng {viewMonth + 1}, {viewYear}
            </span>
            <button
              type="button"
              onClick={goNextMonth}
              aria-label="Tháng sau"
              style={{
                width: 28,
                height: 28,
                borderRadius: 6,
                border: '1px solid #E2E8F0',
                background: '#F8FAFC',
                color: '#334155',
                cursor: 'pointer',
                fontWeight: 700,
              }}
            >
              ›
            </button>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 2, marginBottom: 4 }}>
            {WEEKDAY_LABELS.map(w => (
              <div key={w} style={{ textAlign: 'center', fontSize: 11, fontWeight: 700, color: '#64748B', padding: '4px 0' }}>
                {w}
              </div>
            ))}
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 2 }}>
            {cells.map((cell, idx) => {
              if (!cell) return <div key={`empty-${idx}`} />;
              const isSelected = cell.ymd === value;
              return (
                <button
                  key={cell.ymd}
                  type="button"
                  disabled={cell.disabled}
                  onClick={() => {
                    onChange(cell.ymd);
                    setOpen(false);
                  }}
                  style={{
                    height: 32,
                    borderRadius: 6,
                    border: 'none',
                    fontSize: 12.5,
                    fontWeight: isSelected ? 700 : 500,
                    backgroundColor: isSelected ? '#EA580C' : 'transparent',
                    color: isSelected ? '#FFFFFF' : cell.disabled ? '#CBD5E1' : '#0F172A',
                    cursor: cell.disabled ? 'not-allowed' : 'pointer',
                  }}
                >
                  {cell.day}
                </button>
              );
            })}
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 10, paddingTop: 8, borderTop: '1px solid #F1F5F9' }}>
            {value ? (
              <button
                type="button"
                onClick={() => {
                  onChange('');
                  setOpen(false);
                }}
                style={{
                  border: 'none',
                  background: 'transparent',
                  color: '#DC2626',
                  fontSize: 12,
                  fontWeight: 600,
                  cursor: 'pointer',
                  padding: '2px 4px',
                }}
              >
                Xóa ngày
              </button>
            ) : (
              <span />
            )}
            <button
              type="button"
              onClick={() => setOpen(false)}
              style={{
                border: 'none',
                background: 'transparent',
                color: '#475569',
                fontSize: 12,
                fontWeight: 600,
                cursor: 'pointer',
                padding: '2px 4px',
              }}
            >
              Đóng
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
