import React from 'react';

export interface PaginationProps {
  currentPage: number;
  totalItems: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  itemName?: string;
}

export const Pagination: React.FC<PaginationProps> = ({
  currentPage,
  totalItems,
  pageSize,
  onPageChange,
  itemName = 'mục',
}) => {
  const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
  if (totalItems <= 0) return null;

  const safePage = Math.min(Math.max(1, currentPage), totalPages);
  const startItem = (safePage - 1) * pageSize + 1;
  const endItem = Math.min(safePage * pageSize, totalItems);

  // Xây dựng danh sách trang có dấu ...
  const pages: (number | '...')[] = [];
  for (let i = 1; i <= totalPages; i++) {
    if (i === 1 || i === totalPages || Math.abs(i - safePage) <= 1) {
      pages.push(i);
    } else if (pages[pages.length - 1] !== '...') {
      pages.push('...');
    }
  }

  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginTop: '16px',
        flexWrap: 'wrap',
        gap: '10px',
        fontSize: '13px',
      }}
    >
      <div style={{ color: '#64748B' }}>
        Hiển thị <strong>{startItem}</strong>–<strong>{endItem}</strong> trong tổng số{' '}
        <strong>{totalItems}</strong> {itemName}
      </div>

      <div style={{ display: 'flex', gap: '4px', alignItems: 'center' }}>
        <button
          type="button"
          disabled={safePage <= 1}
          onClick={() => onPageChange(safePage - 1)}
          style={{
            padding: '5px 12px',
            borderRadius: '6px',
            border: '1px solid #CBD5E1',
            backgroundColor: safePage <= 1 ? '#F8FAFC' : '#FFFFFF',
            color: safePage <= 1 ? '#94A3B8' : '#334155',
            cursor: safePage <= 1 ? 'not-allowed' : 'pointer',
            fontSize: '12px',
            fontWeight: 500,
            transition: 'all 0.15s ease',
          }}
        >
          ‹ Trước
        </button>

        {pages.map((p, idx) =>
          p === '...' ? (
            <span key={`dots-${idx}`} style={{ padding: '0 6px', color: '#94A3B8' }}>
              …
            </span>
          ) : (
            <button
              key={`page-${p}`}
              type="button"
              onClick={() => onPageChange(p as number)}
              style={{
                minWidth: '32px',
                height: '30px',
                padding: '0 6px',
                borderRadius: '6px',
                border: '1px solid',
                borderColor: p === safePage ? '#EA580C' : '#CBD5E1',
                backgroundColor: p === safePage ? '#EA580C' : '#FFFFFF',
                color: p === safePage ? '#FFFFFF' : '#334155',
                cursor: 'pointer',
                fontSize: '12px',
                fontWeight: p === safePage ? 700 : 500,
                transition: 'all 0.15s ease',
              }}
            >
              {p}
            </button>
          )
        )}

        <button
          type="button"
          disabled={safePage >= totalPages}
          onClick={() => onPageChange(safePage + 1)}
          style={{
            padding: '5px 12px',
            borderRadius: '6px',
            border: '1px solid #CBD5E1',
            backgroundColor: safePage >= totalPages ? '#F8FAFC' : '#FFFFFF',
            color: safePage >= totalPages ? '#94A3B8' : '#334155',
            cursor: safePage >= totalPages ? 'not-allowed' : 'pointer',
            fontSize: '12px',
            fontWeight: 500,
            transition: 'all 0.15s ease',
          }}
        >
          Sau ›
        </button>
      </div>
    </div>
  );
};

