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

export const ViolationListPage: React.FC = () => {
  const [violations, setViolations] = useState<ViolationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterCode, setFilterCode] = useState('');
  const [selectedViolation, setSelectedViolation] = useState<ViolationItem | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  useEffect(() => {
    loadViolations(true);

    // Tự động polling cập nhật danh sách vi phạm theo thời gian thực (mỗi 5 giây)
    const intervalId = setInterval(() => {
      loadViolations(false);
    }, 5000);

    return () => clearInterval(intervalId);
  }, []);

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

  const filteredViolations = violations.filter(v => {
    if (!filterCode.trim()) return true;
    const term = filterCode.toLowerCase();
    return (
      v.code.toLowerCase().includes(term) ||
      v.studentName.toLowerCase().includes(term) ||
      v.studentCode.toLowerCase().includes(term) ||
      v.roomNumber.toLowerCase().includes(term) ||
      v.violationTypeName.toLowerCase().includes(term)
    );
  });

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

      <div style={{ marginBottom: 16 }}>
        <input
          type="text"
          placeholder="Tìm theo mã biên bản, MSSV, tên, phòng, loại vi phạm..."
          value={filterCode}
          onChange={e => {
            setFilterCode(e.target.value);
            setCurrentPage(1);
          }}
          style={{
            width: '100%',
            maxWidth: 400,
            padding: '8px 12px',
            borderRadius: 6,
            border: '1px solid #cbd5e1',
            fontSize: 13,
          }}
        />
      </div>

      <div className="violation-table-container">
        <table className="violation-table">
          <thead>
            <tr>
              <th>Mã biên bản</th>
              <th>Sinh viên</th>
              <th>Phòng</th>
              <th>Hành vi vi phạm</th>
              <th>Thời điểm</th>
              <th>Mức độ</th>
              <th>Trạng thái</th>
              <th>Minh chứng</th>
              <th>Chi tiết</th>
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
                <tr key={v.id}>
                  <td style={{ fontWeight: 600, color: '#c2410c' }}>{v.code}</td>
                  <td>
                    <div><strong>{v.studentName}</strong></div>
                    <div style={{ fontSize: 11, color: '#64748b' }}>{v.studentCode}</div>
                  </td>
                  <td>P.{v.roomNumber} ({v.blockName})</td>
                  <td>{v.violationTypeName}</td>
                  <td>{formatDate(v.occurrenceTime)}</td>
                  <td>
                    <span style={{ fontWeight: 600, color: v.finalLevel ? '#dc2626' : '#ea580c' }}>
                      {v.finalLevel || v.initialLevel || '—'}
                    </span>
                  </td>
                  <td>
                    <span
                      style={{
                        display: 'inline-block',
                        padding: '3px 8px',
                        borderRadius: 9999,
                        fontSize: 11,
                        fontWeight: 600,
                        backgroundColor:
                          v.status === 'Recorded' ? '#fef3c7' :
                          v.status === 'Finalized' ? '#fee2e2' : '#ecfdf5',
                        color:
                          v.status === 'Recorded' ? '#92400e' :
                          v.status === 'Finalized' ? '#b91c1c' : '#047857',
                      }}
                    >
                      {VIOLATION_STATUS_VI[v.status] || v.status}
                    </span>
                  </td>
                  <td>
                    {v.evidences && v.evidences.length > 0 ? (
                      <div style={{ display: 'flex', gap: 4 }}>
                        {v.evidences.map((e, idx) => (
                          <a
                            key={e.id || idx}
                            href={e.fileUrl}
                            target="_blank"
                            rel="noreferrer"
                            title="Bấm để xem ảnh"
                          >
                            <img
                              src={e.fileUrl}
                              alt="Minh chứng"
                              style={{ width: 32, height: 32, objectFit: 'cover', borderRadius: 4, border: '1px solid #cbd5e1' }}
                            />
                          </a>
                        ))}
                      </div>
                    ) : (
                      <span style={{ color: '#94a3b8', fontSize: 12 }}>Không có</span>
                    )}
                  </td>
                  <td>
                    <button
                      type="button"
                      onClick={() => setSelectedViolation(v)}
                      style={{
                        padding: '4px 10px',
                        background: '#f1f5f9',
                        border: '1px solid #cbd5e1',
                        borderRadius: 4,
                        fontSize: 12,
                        fontWeight: 600,
                        cursor: 'pointer',
                        color: '#334155',
                      }}
                    >
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
                  {selectedViolation.evidences.map((ev, i) => (
                    <a key={i} href={ev.fileUrl} target="_blank" rel="noreferrer">
                      <img src={ev.fileUrl} alt="evidence" style={{ width: '100%', height: '70px', objectFit: 'cover', borderRadius: '6px', border: '1px solid #cbd5e1' }} />
                    </a>
                  ))}
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
                style={{ padding: '8px 18px', background: '#cbd5e1', border: 'none', borderRadius: 6, cursor: 'pointer', fontSize: 13, fontWeight: 600, color: '#1e293b' }}
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
