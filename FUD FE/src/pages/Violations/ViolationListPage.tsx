import React, { useState, useEffect } from 'react';
import { violationApi, type ViolationItem } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime, formatDate } from '../../utils/dateUtils';
import { removeVietnameseTones } from '../../utils/textUtils';
import { toSafeHref } from '../../utils/urlUtils';
import { useIdleAwarePolling } from '../../utils/useIdleAwarePolling';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

// Mapping trạng thái theo CONSTRAINT CHK_Violation_Status CHECK (Status IN ('Recorded','Finalized','Dismissed'))
export const VIOLATION_STATUS_VI: Record<string, string> = {
  'Recorded': 'Đã ghi nhận',
  'Finalized': 'Đã có kết luận',
  'Dismissed': 'Đã bác bỏ',
};

export const ViolationListPage: React.FC = () => {
  const [violations, setViolations] = useState<ViolationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterCode, setFilterCode] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [filterLevel, setFilterLevel] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
  const [selectedViolation, setSelectedViolation] = useState<ViolationItem | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  const loadViolations = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
    }
    try {
      const data = await violationApi.getViolations();
      setViolations(data);
    } catch (err) {
      console.error('Không thể tải danh sách vi phạm:', err);
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    loadViolations(true);
  }, []);

  // Tự động polling cập nhật danh sách vi phạm theo thời gian thực (tạm dừng khi ẩn tab hoặc idle)
  useIdleAwarePolling(() => {
    void loadViolations(false);
  }, 5000, { immediate: false });

  const filteredViolations = violations
    .filter(v => {
      if (filterStatus && v.status !== filterStatus) return false;

      const currentLevel = v.finalLevel || v.initialLevel || '';
      if (filterLevel && !currentLevel.toLowerCase().includes(filterLevel.toLowerCase())) {
        return false;
      }

      if (dateFrom) {
        const from = new Date(dateFrom);
        from.setHours(0, 0, 0, 0);
        if (new Date(v.occurrenceTime) < from) return false;
      }

      if (dateTo) {
        const to = new Date(dateTo);
        to.setHours(23, 59, 59, 999);
        if (new Date(v.occurrenceTime) > to) return false;
      }

      if (filterCode.trim()) {
        const term = removeVietnameseTones(filterCode);
        const codeNorm = removeVietnameseTones(v.code || '');
        const nameNorm = removeVietnameseTones(v.studentName || '');
        const studentCodeNorm = removeVietnameseTones(v.studentCode || '');
        const roomNorm = removeVietnameseTones(v.roomNumber || '');
        const blockNorm = removeVietnameseTones(v.blockName || '');
        const typeNorm = removeVietnameseTones(v.violationTypeName || '');

        const matches =
          codeNorm.includes(term) ||
          nameNorm.includes(term) ||
          studentCodeNorm.includes(term) ||
          roomNorm.includes(term) ||
          blockNorm.includes(term) ||
          typeNorm.includes(term);

        if (!matches) return false;
      }

      return true;
    })
    .sort((a, b) => {
      const timeA = new Date(a.occurrenceTime).getTime();
      const timeB = new Date(b.occurrenceTime).getTime();
      return sortOrder === 'desc' ? timeB - timeA : timeA - timeB;
    });

  const resetFilters = () => {
    setFilterCode('');
    setFilterStatus('');
    setFilterLevel('');
    setDateFrom('');
    setDateTo('');
    setSortOrder('desc');
    setCurrentPage(1);
  };

  const paginatedViolations = filteredViolations.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE
  );

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0f172a' }}>
            Danh sách hồ sơ vi phạm
          </h1>
          <p style={{ color: '#64748b', fontSize: '13px', margin: 0 }}>
            Tổng hợp toàn bộ các biên bản vi phạm đã ghi nhận trong ký túc xá
          </p>
        </div>
        <button
          onClick={() => loadViolations(true)}
          style={{
            padding: '8px 16px',
            backgroundColor: '#ea580c',
            color: '#fff',
            border: 'none',
            borderRadius: '6px',
            cursor: 'pointer',
            fontSize: '13px',
            fontWeight: 600,
          }}
        >
          Làm mới dữ liệu
        </button>
      </div>

      {/* Advance Search & Sort */}
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
        <div style={{ flex: '1 1 240px', minWidth: '220px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Tìm kiếm đa nhiệm (có dấu & không dấu)
          </label>
          <input
            type="text"
            placeholder="Mã biên bản, MSSV, tên, phòng, loại vi phạm..."
            value={filterCode}
            onChange={e => {
              setFilterCode(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              fontSize: 13,
            }}
          />
        </div>

        <div style={{ minWidth: '150px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Trạng thái biên bản
          </label>
          <select
            value={filterStatus}
            onChange={e => {
              setFilterStatus(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              fontSize: 13,
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả trạng thái</option>
            {Object.keys(VIOLATION_STATUS_VI).map(st => (
              <option key={st} value={st}>{VIOLATION_STATUS_VI[st]}</option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: '150px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Mức độ xử lý
          </label>
          <select
            value={filterLevel}
            onChange={e => {
              setFilterLevel(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              fontSize: 13,
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả mức độ</option>
            <option value="Nhắc nhở">Nhắc nhở</option>
            <option value="Khiển trách">Khiển trách</option>
            <option value="Cảnh cáo">Cảnh cáo</option>
            <option value="Đình chỉ">Đình chỉ nội trú</option>
          </select>
        </div>

        <div style={{ minWidth: '130px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Từ ngày
          </label>
          <input
            type="date"
            value={dateFrom}
            onChange={e => {
              setDateFrom(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '6px 10px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              fontSize: 12,
            }}
          />
        </div>

        <div style={{ minWidth: '130px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Đến ngày
          </label>
          <input
            type="date"
            value={dateTo}
            onChange={e => {
              setDateTo(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '6px 10px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              fontSize: 12,
            }}
          />
        </div>

        <div style={{ minWidth: '150px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Sắp xếp thời gian
          </label>
          <select
            value={sortOrder}
            onChange={e => setSortOrder(e.target.value as 'desc' | 'asc')}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: 6,
              border: '1px solid #cbd5e1',
              fontSize: 13,
              backgroundColor: '#FFF',
              fontWeight: 600,
            }}
          >
            <option value="desc">Mới nhất trước ↓</option>
            <option value="asc">Cũ nhất trước ↑</option>
          </select>
        </div>

        {(filterCode || filterStatus || filterLevel || dateFrom || dateTo || sortOrder !== 'desc') && (
          <div style={{ alignSelf: 'flex-end' }}>
            <button
              type="button"
              onClick={resetFilters}
              style={{
                padding: '8px 14px',
                borderRadius: 6,
                border: '1px solid #cbd5e1',
                backgroundColor: '#F8FAFC',
                color: '#64748B',
                fontSize: 12,
                cursor: 'pointer',
                fontWeight: 600,
              }}
            >
              Đặt lại lọc
            </button>
          </div>
        )}
      </div>

      <div className="fud-table-wrapper" style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13, minWidth: 860 }}>
          <thead>
            <tr>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Mã biên bản</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Sinh viên</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Phòng</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left' }}>Hành vi vi phạm</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Thời điểm</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Mức độ</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Trạng thái</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Minh chứng</th>
              <th style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', padding: '10px 10px', fontSize: 11, fontWeight: 700, color: '#475569', textTransform: 'uppercase', textAlign: 'left', whiteSpace: 'nowrap' }}>Chi tiết</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={9} style={{ textAlign: 'center', padding: '24px', color: '#64748b' }}>
                  Đang tải danh sách biên bản...
                </td>
              </tr>
            ) : paginatedViolations.length === 0 ? (
              <tr>
                <td colSpan={9} style={{ textAlign: 'center', padding: '24px', color: '#64748b' }}>
                  Không tìm thấy hồ sơ vi phạm nào
                </td>
              </tr>
            ) : (
              paginatedViolations.map(v => (
                <tr key={v.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                  <td style={{ padding: '10px', fontWeight: 700, color: '#c2410c', whiteSpace: 'nowrap' }}>{v.code}</td>
                  <td style={{ padding: '10px', minWidth: 130 }}>
                    <div style={{ fontWeight: 600 }}>{v.studentName}</div>
                    <div style={{ fontSize: 11, color: '#64748b' }}>{v.studentCode}</div>
                  </td>
                  <td style={{ padding: '10px', whiteSpace: 'nowrap' }}>P.{v.roomNumber} ({v.blockName})</td>
                  <td style={{ padding: '10px', minWidth: 160 }}>
                    <div style={{ fontWeight: 600 }}>{v.violationTypeName}</div>
                  </td>
                  <td style={{ padding: '10px', whiteSpace: 'nowrap' }}>{formatDate(v.occurrenceTime)}</td>
                  <td style={{ padding: '10px', whiteSpace: 'nowrap', fontWeight: 600, color: v.finalLevel ? '#dc2626' : v.initialLevel ? '#ea580c' : '#94a3b8' }}>
                    {v.finalLevel || v.initialLevel || '—'}
                  </td>
                  <td style={{ padding: '10px' }}>
                    <span style={{
                      display: 'inline-block', padding: '3px 8px', borderRadius: 9999, fontSize: 11, fontWeight: 600, whiteSpace: 'nowrap',
                      backgroundColor: v.status === 'Recorded' ? '#fef3c7' : v.status === 'Finalized' ? '#fee2e2' : '#ecfdf5',
                      color: v.status === 'Recorded' ? '#92400e' : v.status === 'Finalized' ? '#b91c1c' : '#047857'
                    }}>
                      {VIOLATION_STATUS_VI[v.status] || v.status}
                    </span>
                  </td>
                  <td style={{ padding: '10px' }}>
                    {v.evidences && v.evidences.length > 0 ? (
                      <div style={{ display: 'flex', gap: 4 }}>
                        {v.evidences.slice(0, 2).map((e, idx) => {
                          const safeUrl = toSafeHref(e.fileUrl);
                          if (!safeUrl) return null;
                          return (
                            <a key={e.id || idx} href={safeUrl} target="_blank" rel="noopener noreferrer" title="Xem ảnh">
                              <img src={safeUrl} alt="MC" style={{ width: 28, height: 28, objectFit: 'cover', borderRadius: 4, border: '1px solid #cbd5e1' }} />
                            </a>
                          );
                        })}
                      </div>
                    ) : <span style={{ color: '#94a3b8', fontSize: 11 }}>—</span>}
                  </td>
                  <td style={{ padding: '10px' }}>
                    <button type="button" onClick={() => setSelectedViolation(v)}
                      style={{ padding: '4px 10px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: 4, fontSize: 12, fontWeight: 600, cursor: 'pointer', color: '#334155', whiteSpace: 'nowrap' }}>
                      Chi tiết
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Phân trang cho bảo vệ */}
      <Pagination
        currentPage={currentPage}
        totalItems={filteredViolations.length}
        pageSize={PAGE_SIZE}
        onPageChange={page => setCurrentPage(page)}
        itemName="hồ sơ"
      />

      {/* Bảng Pop-up chi tiết Violation */}
      {selectedViolation && (
        <div className="violation-modal-overlay" onClick={() => setSelectedViolation(null)}>
          <div className="violation-modal-content" onClick={e => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, borderBottom: '1px solid #e2e8f0', paddingBottom: 12 }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 700, color: '#c2410c', textTransform: 'uppercase' }}>
                  Hồ sơ biên bản an ninh
                </span>
                <h3 style={{ fontSize: 18, fontWeight: 700, margin: '2px 0 0', color: '#0f172a' }}>
                  Biên bản {selectedViolation.code}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedViolation(null)}
                style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer', color: '#94a3b8' }}
              >
                ✕
              </button>
            </div>

            <div style={{ lineHeight: 1.8, fontSize: 13, marginBottom: 16, backgroundColor: '#f8fafc', padding: 16, borderRadius: 8 }}>
              <div><strong>Sinh viên:</strong> {selectedViolation.studentName} ({selectedViolation.studentCode})</div>
              <div><strong>Vị trí:</strong> Phòng {selectedViolation.roomNumber} ({selectedViolation.blockName})</div>
              <div><strong>Loại vi phạm:</strong> <span style={{ color: '#b91c1c', fontWeight: 600 }}>{selectedViolation.violationTypeName}</span></div>
              <div><strong>Thời gian xảy ra:</strong> {formatDateTime(selectedViolation.occurrenceTime)}</div>
              <div><strong>Mức độ ban đầu:</strong> {selectedViolation.initialLevel || 'Chưa định mức'}</div>
              <div><strong>Mức độ chính thức:</strong> <span style={{ color: '#dc2626', fontWeight: 700 }}>{selectedViolation.finalLevel || 'Chưa có kết luận'}</span></div>
              <div><strong>Trạng thái:</strong> {VIOLATION_STATUS_VI[selectedViolation.status] || selectedViolation.status}</div>
              <div><strong>Người lập biên bản:</strong> {selectedViolation.recordedByName || 'Bảo vệ ca trực'}</div>
              {selectedViolation.evaluatedByName && (
                <div><strong>Người duyệt:</strong> {selectedViolation.evaluatedByName}</div>
              )}
            </div>

            <div style={{ marginBottom: 14 }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>Mô tả sự việc:</div>
              <div style={{ backgroundColor: '#f1f5f9', padding: '10px 14px', borderRadius: '6px', fontSize: '13px', wordBreak: 'break-word', color: '#1e293b' }}>
                {selectedViolation.description || 'Không có mô tả chi tiết'}
              </div>
            </div>

            {selectedViolation.evidences && selectedViolation.evidences.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 6 }}>Ảnh minh chứng đã tải lên:</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(90px, 1fr))', gap: '8px' }}>
                  {selectedViolation.evidences.map((ev, i) => {
                    const safeUrl = toSafeHref(ev.fileUrl);
                    if (!safeUrl) return null;
                    return (
                      <a key={i} href={safeUrl} target="_blank" rel="noopener noreferrer">
                        <img src={safeUrl} alt="evidence" style={{ width: '100%', height: '70px', objectFit: 'cover', borderRadius: '6px', border: '1px solid #cbd5e1' }} />
                      </a>
                    );
                  })}
                </div>
              </div>
            )}

            {selectedViolation.managerNote && (
              <div style={{ marginBottom: 16, padding: '10px 14px', background: '#fffbeb', borderRadius: 6, border: '1px solid #fef3c7', fontSize: 13, color: '#92400e' }}>
                <strong>Ghi chú / Kết luận BQL:</strong> {selectedViolation.managerNote}
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end', borderTop: '1px solid #e2e8f0', paddingTop: 12 }}>
              <button
                type="button"
                onClick={() => setSelectedViolation(null)}
                style={{ padding: '8px 18px', background: '#000000ff', border: 'none', borderRadius: 6, cursor: 'pointer', fontSize: 13, fontWeight: 600, color: '#1e293b' }}
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
