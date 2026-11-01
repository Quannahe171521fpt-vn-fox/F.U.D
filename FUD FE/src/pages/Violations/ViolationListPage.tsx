import React, { useState, useEffect } from 'react';
import { violationApi, type ViolationItem } from '../../services/api';
import '../../styles/violation.css';

export const ViolationListPage: React.FC = () => {
  const [violations, setViolations] = useState<ViolationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filterCode, setFilterCode] = useState('');
  const [selectedViolation, setSelectedViolation] = useState<ViolationItem | null>(null);

  useEffect(() => {
    loadViolations();
  }, []);

  const loadViolations = async () => {
    setLoading(true);
    try {
      const data = await violationApi.getViolations();
      setViolations(data);
    } catch (err) {
      console.error('Không thể tải danh sách vi phạm:', err);
    } finally {
      setLoading(false);
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
          type="button"
          className="btn-secondary"
          onClick={loadViolations}
          style={{ display: 'flex', alignItems: 'center', gap: 6 }}
        >
          🔄 Làm mới dữ liệu
        </button>
      </div>

      {/* Filter bar */}
      <div style={{ marginBottom: 16, maxWidth: 400 }}>
        <input
          type="text"
          className="violation-input"
          placeholder="Tìm theo Mã biên bản, MSSV, Sinh viên, Phòng..."
          value={filterCode}
          onChange={e => setFilterCode(e.target.value)}
        />
      </div>

      {loading ? (
        <div style={{ padding: 40, textAlign: 'center', color: '#64748b' }}>Đang nạp danh sách hồ sơ...</div>
      ) : (
        <div style={{ backgroundColor: '#ffffff', borderRadius: 8, border: '1px solid #e2e8f0', overflow: 'hidden' }}>
          <table className="violation-table" style={{ margin: 0 }}>
            <thead>
              <tr>
                <th>Mã biên bản</th>
                <th>Sinh viên</th>
                <th>MSSV</th>
                <th>Phòng</th>
                <th>Loại vi phạm</th>
                <th>Thời điểm</th>
                <th>Người ghi nhận</th>
                <th>Trạng thái</th>
                <th>Minh chứng</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredViolations.length === 0 ? (
                <tr>
                  <td colSpan={10} style={{ textAlign: 'center', padding: 32, color: '#64748b' }}>
                    Chưa có hồ sơ vi phạm nào được ghi nhận.
                  </td>
                </tr>
              ) : (
                filteredViolations.map(v => (
                  <tr key={v.id}>
                    <td style={{ fontWeight: 700, color: '#2563eb' }}>{v.code}</td>
                    <td style={{ fontWeight: 600 }}>{v.studentName}</td>
                    <td>{v.studentCode}</td>
                    <td>{v.roomNumber} ({v.blockName || 'Tòa A'})</td>
                    <td>{v.violationTypeName}</td>
                    <td style={{ fontSize: 12 }}>{new Date(v.occurrenceTime).toLocaleString('vi-VN')}</td>
                    <td style={{ fontSize: 12 }}>{v.recordedByName || 'Bảo vệ'}</td>
                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '3px 8px',
                          borderRadius: 9999,
                          fontSize: 11,
                          fontWeight: 600,
                          backgroundColor: v.status === 'Recorded' ? '#fef3c7' : '#ecfdf5',
                          color: v.status === 'Recorded' ? '#92400e' : '#047857',
                        }}
                      >
                        {v.status === 'Recorded' ? 'Đã ghi nhận' : v.status}
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
                              title="Bấm để xem ảnh trên Cloudinary"
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
                        className="btn-secondary"
                        style={{ padding: '4px 10px', fontSize: 12 }}
                        onClick={() => setSelectedViolation(v)}
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
      )}

      {/* Modal chi tiết hồ sơ */}
      {selectedViolation && (
        <div
          style={{
            position: 'fixed',
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
          }}
          onClick={() => setSelectedViolation(null)}
        >
          <div
            style={{
              backgroundColor: '#ffffff',
              borderRadius: 12,
              padding: 28,
              maxWidth: 600,
              width: '90%',
              maxHeight: '85vh',
              overflowY: 'auto',
            }}
            onClick={e => e.stopPropagation()}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <h2 style={{ fontSize: 18, fontWeight: 700, margin: 0 }}>
                Chi tiết hồ sơ: {selectedViolation.code}
              </h2>
              <button
                type="button"
                style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer' }}
                onClick={() => setSelectedViolation(null)}
              >
                ✕
              </button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, fontSize: 13, marginBottom: 16 }}>
              <div><strong>Sinh viên:</strong> {selectedViolation.studentName} ({selectedViolation.studentCode})</div>
              <div><strong>Phòng:</strong> {selectedViolation.roomNumber}</div>
              <div><strong>Loại vi phạm:</strong> {selectedViolation.violationTypeName}</div>
              <div><strong>Thời điểm:</strong> {new Date(selectedViolation.occurrenceTime).toLocaleString('vi-VN')}</div>
              <div><strong>Người ghi nhận:</strong> {selectedViolation.recordedByName || 'Bảo vệ'}</div>
              <div><strong>Trạng thái:</strong> {selectedViolation.status}</div>
            </div>

            <div style={{ marginBottom: 16 }}>
              <strong style={{ fontSize: 13 }}>Mô tả sự việc:</strong>
              <div style={{ padding: 10, background: '#f8fafc', borderRadius: 6, fontSize: 13, marginTop: 4 }}>
                {selectedViolation.description || 'Không có mô tả chi tiết'}
              </div>
            </div>

            <div>
              <strong style={{ fontSize: 13 }}>Ảnh minh chứng Cloudinary ({selectedViolation.evidences.length}):</strong>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, marginTop: 8 }}>
                {selectedViolation.evidences.map((e, idx) => (
                  <a key={idx} href={e.fileUrl} target="_blank" rel="noreferrer">
                    <img
                      src={e.fileUrl}
                      alt="Minh chứng"
                      style={{ width: 120, height: 120, objectFit: 'cover', borderRadius: 6, border: '1px solid #cbd5e1' }}
                    />
                  </a>
                ))}
              </div>
            </div>

            <div style={{ textAlign: 'right', marginTop: 24 }}>
              <button
                type="button"
                className="btn-secondary"
                onClick={() => setSelectedViolation(null)}
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

