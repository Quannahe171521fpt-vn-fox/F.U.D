import React, { useState, useEffect } from 'react';
import { violationApi, type ViolationItem } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime, formatDate } from '../../utils/dateUtils';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

// Mapping trạng thái theo CONSTRAINT CHK_Violation_Status CHECK (Status IN ('Recorded','Finalized','Dismissed'))
export const VIOLATION_STATUS_VI: Record<string, string> = {
  'Recorded': 'Đã ghi nhận',
  'Finalized': 'Đã có kết luận',
  'Dismissed': 'Đã bác bỏ',
};

const STATUS_COLOR: Record<string, { bg: string; color: string }> = {
  'Recorded': { bg: '#FEF3C7', color: '#92400E' },
  'Finalized': { bg: '#FEE2E2', color: '#B91C1C' },
  'Dismissed': { bg: '#ECFDF5', color: '#047857' },
};

export const StudentViolationHistoryPage: React.FC = () => {
  const [history, setHistory] = useState<ViolationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [filterTerm, setFilterTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedRecord, setSelectedRecord] = useState<ViolationItem | null>(null);

  useEffect(() => {
    setCurrentPage(1);
    loadHistory(true);

    // Tự động polling cập nhật lịch sử vi phạm theo thời gian thực (mỗi 5 giây)
    const intervalId = setInterval(() => {
      loadHistory(false);
    }, 5000);

    return () => clearInterval(intervalId);
  }, [filterStatus, filterTerm]);

  const loadHistory = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await violationApi.getStudentHistory({
        status: filterStatus || undefined,
        term: filterTerm || undefined,
      });
      setHistory(data);
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải lịch sử vi phạm');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  const paginatedHistory = history.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Lịch sử vi phạm kỷ luật của bạn
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Tra cứu toàn bộ các vi phạm kỷ luật đã phát sinh qua các học kỳ tại ký túc xá
        </p>
      </div>

      {errorMsg && (
        <div className="violation-alert violation-alert-error" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{errorMsg}</span>
          <button
            type="button"
            onClick={() => loadHistory(true)}
            style={{
              background: '#DC2626',
              color: '#FFF',
              border: 'none',
              padding: '4px 12px',
              borderRadius: '6px',
              cursor: 'pointer',
              fontSize: '12px',
              fontWeight: 600,
            }}
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Bộ lọc */}
      <div
        style={{
          display: 'flex',
          gap: '12px',
          alignItems: 'center',
          backgroundColor: '#FFFFFF',
          padding: '16px 20px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
          marginBottom: 16,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ minWidth: '220px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Lọc theo Trạng thái
          </label>
          <select
            value={filterStatus}
            onChange={e => setFilterStatus(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả trạng thái</option>
            <option value="Recorded">Đã ghi nhận</option>
            <option value="Finalized">Đã có kết luận</option>
            <option value="Dismissed">Đã bác bỏ</option>
          </select>
        </div>

        <div style={{ minWidth: '200px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Tìm theo Học kỳ
          </label>
          <input
            type="text"
            placeholder="Ví dụ: Fall 2026, Spring 2026..."
            value={filterTerm}
            onChange={e => setFilterTerm(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
            }}
          />
        </div>

        <div style={{ alignSelf: 'flex-end' }}>
          <button
            type="button"
            onClick={() => loadHistory(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#EA580C',
              color: '#FFF',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              fontSize: '13px',
              cursor: 'pointer',
            }}
          >
            Làm mới
          </button>
        </div>
      </div>

      {/* Bảng lịch sử vi phạm */}
      <div className="fud-table-wrapper">
        <table className="fud-table">
          <colgroup>
            <col style={{ width: '13%' }} />
            <col style={{ width: '28%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '12%' }} />
            <col style={{ width: '8%' }} />
          </colgroup>
          <thead>
            <tr>
              <th>Mã biên bản</th>
              <th>Hành vi vi phạm</th>
              <th>Học kỳ</th>
              <th>Thời điểm</th>
              <th>Mức độ</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Đang tải dữ liệu lịch sử...
                </td>
              </tr>
            ) : paginatedHistory.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>
                  Không có biên bản vi phạm nào được ghi nhận.
                </td>
              </tr>
            ) : (
              paginatedHistory.map(item => {
                const meta = STATUS_COLOR[item.status] || { bg: '#F1F5F9', color: '#475569' };
                return (
                  <tr key={item.id}>
                    {/* Mã biên bản (1 dòng, hover xem full) */}
                    <td>
                      <span className="cell-text-1line" title={item.code} style={{ fontWeight: 700, color: '#C2410C' }}>
                        {item.code}
                      </span>
                    </td>

                    {/* Hành vi vi phạm (3 dòng, hover xem full) */}
                    <td>
                      <div className="cell-text-3line" title={`${item.violationTypeName} - ${item.description || ''}`}>
                        <span style={{ fontWeight: 600 }}>{item.violationTypeName}</span>
                        {item.description && (
                          <div style={{ fontSize: '11px', color: '#64748B' }}>- {item.description}</div>
                        )}
                      </div>
                    </td>

                    {/* Học kỳ (1 dòng, hover xem full) */}
                    <td>
                      <span className="cell-text-1line" title={item.termName || 'Học kỳ hiện tại'}>
                        {item.termName || 'Hiện tại'}
                      </span>
                    </td>

                    {/* Thời điểm (1 dòng, hover xem full) */}
                    <td>
                      <span className="cell-text-1line" title={formatDateTime(item.occurrenceTime)}>
                        {formatDate(item.occurrenceTime)}
                      </span>
                    </td>

                    {/* Mức độ (1 dòng, hover xem full) */}
                    <td>
                      <span
                        className="cell-text-1line"
                        title={item.finalLevel || item.initialLevel || 'Chưa định mức'}
                        style={{
                          fontWeight: 600,
                          color: item.finalLevel ? '#DC2626' : item.initialLevel ? '#EA580C' : '#94A3B8',
                        }}
                      >
                        {item.finalLevel || item.initialLevel || '—'}
                      </span>
                    </td>

                    {/* Trạng thái */}
                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '2px 8px',
                          borderRadius: '9999px',
                          fontSize: '11px',
                          fontWeight: 600,
                          whiteSpace: 'nowrap',
                          backgroundColor: meta.bg,
                          color: meta.color,
                        }}
                      >
                        {VIOLATION_STATUS_VI[item.status] || item.status}
                      </span>
                    </td>

                    {/* Thao tác */}
                    <td>
                      <button
                        type="button"
                        onClick={() => setSelectedRecord(item)}
                        style={{
                          padding: '5px 10px',
                          backgroundColor: '#F1F5F9',
                          border: '1px solid #CBD5E1',
                          borderRadius: '6px',
                          fontSize: '12px',
                          cursor: 'pointer',
                          fontWeight: 600,
                          whiteSpace: 'nowrap',
                        }}
                      >
                        Xem
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Phân trang */}
      <Pagination
        currentPage={currentPage}
        totalItems={history.length}
        pageSize={PAGE_SIZE}
        onPageChange={page => setCurrentPage(page)}
        itemName="biên bản"
      />

      {/* Modal chi tiết biên bản */}
      {selectedRecord && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: '20px',
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              maxWidth: '600px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              padding: '24px',
              boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 700, color: '#C2410C', textTransform: 'uppercase' }}>
                  Chi tiết hồ sơ vi phạm
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>
                  Biên bản {selectedRecord.code}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedRecord(null)}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#94A3B8' }}
              >
                Đóng
              </button>
            </div>

            <div style={{ backgroundColor: '#F8FAFC', borderRadius: '8px', padding: '14px', fontSize: '13px', lineHeight: 1.8, marginBottom: 16 }}>
              <div><strong>Hành vi vi phạm:</strong> <span style={{ color: '#B91C1C', fontWeight: 600 }}>{selectedRecord.violationTypeName}</span></div>
              <div><strong>Thời điểm vi phạm:</strong> {formatDateTime(selectedRecord.occurrenceTime)}</div>
              <div><strong>Học kỳ:</strong> {selectedRecord.termName || 'Học kỳ hiện tại'}</div>
              <div><strong>Vị trí:</strong> Phòng {selectedRecord.roomNumber} ({selectedRecord.blockName})</div>
              <div><strong>Mức độ ban đầu:</strong> {selectedRecord.initialLevel || 'Chưa định mức'}</div>
              <div><strong>Mức độ chính thức:</strong> <span style={{ color: '#DC2626', fontWeight: 700 }}>{selectedRecord.finalLevel || 'Đang cập nhật'}</span></div>
              <div><strong>Trạng thái:</strong> {VIOLATION_STATUS_VI[selectedRecord.status] || selectedRecord.status}</div>
            </div>

            <div style={{ marginBottom: 14 }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>Mô tả chi tiết từ bảo vệ:</div>
              <div style={{ backgroundColor: '#F1F5F9', padding: '10px', borderRadius: '6px', fontSize: '13px', wordBreak: 'break-word' }}>
                {selectedRecord.description || 'Không có mô tả chi tiết'}
              </div>
            </div>

            {selectedRecord.managerNote && (
              <div style={{ backgroundColor: '#F0FDF4', border: '1px solid #86EFAC', borderRadius: '8px', padding: '12px', marginBottom: 14 }}>
                <strong style={{ color: '#166534', fontSize: '12px' }}>Phản hồi & Kết luận từ Ban Quản lý:</strong>
                <div style={{ fontSize: '13px', color: '#14532D', marginTop: 4, whiteSpace: 'pre-line' }}>
                  {selectedRecord.managerNote}
                </div>
                {selectedRecord.finalizedTime && (
                  <div style={{ fontSize: '11px', color: '#15803D', marginTop: 6, textAlign: 'right' }}>
                    Thời gian kết luận: {formatDateTime(selectedRecord.finalizedTime)}
                  </div>
                )}
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
              <button
                type="button"
                onClick={() => setSelectedRecord(null)}
                style={{ padding: '8px 16px', background: '#EA580C', color: '#FFF', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
