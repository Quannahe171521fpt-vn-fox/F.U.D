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

interface StudentViolationNoticesPageProps {
  onGoToAppeal?: (violation: ViolationItem) => void;
}

export const StudentViolationNoticesPage: React.FC<StudentViolationNoticesPageProps> = ({
  onGoToAppeal,
}) => {
  const [notices, setNotices] = useState<ViolationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [selectedNotice, setSelectedNotice] = useState<ViolationItem | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  useEffect(() => {
    loadNotices(true);

    // Tự động polling cập nhật thông báo vi phạm theo thời gian thực (mỗi 5 giây)
    const intervalId = setInterval(() => {
      loadNotices(false);
    }, 5000);

    return () => clearInterval(intervalId);
  }, []);

  const loadNotices = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await violationApi.getStudentNotices();
      setNotices(data);
      if (selectedNotice) {
        const updated = data.find(n => n.id === selectedNotice.id);
        if (updated) setSelectedNotice(updated);
      }
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải danh sách thông báo vi phạm');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  const handleOpenNotice = (notice: ViolationItem) => {
    setSelectedNotice(notice);
  };

  const paginatedNotices = notices.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  // Kiểm tra thời hạn 24h
  const isExpired = (deadline?: string) => {
    if (!deadline) return true;
    return new Date() > new Date(deadline);
  };

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Thông báo vi phạm của bạn
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Theo dõi các biên bản vi phạm đã được Ban Quản lý xử lý, thông báo và quyền gửi đơn khiếu nại (Request)
        </p>
      </div>

      {errorMsg && (
        <div className="violation-alert violation-alert-error" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{errorMsg}</span>
          <button
            type="button"
            onClick={() => loadNotices(true)}
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

      <div style={{ display: 'grid', gridTemplateColumns: selectedNotice ? '1.2fr 1fr' : '1fr', gap: '20px' }}>
        {/* Cột danh sách các thông báo */}
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #E2E8F0', padding: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
            <h2 style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: '#1E293B' }}>
              Danh sách biên bản thông báo ({notices.length})
            </h2>
            <button
              type="button"
              onClick={() => loadNotices(true)}
              style={{
                background: 'none',
                border: 'none',
                color: '#EA580C',
                fontSize: '12px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Tải lại
            </button>
          </div>

          {loading ? (
            <div style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>Đang nạp dữ liệu thông báo...</div>
          ) : notices.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>
              <strong>Không có thông báo vi phạm nào</strong>
              <p style={{ fontSize: '13px', margin: '4px 0 0' }}>Bạn luôn tuân thủ rất tốt nội quy ký túc xá.</p>
            </div>
          ) : (
            <>
              {/* Bảng thông báo: Chuẩn không co, không trượt ngang */}
              <div className="fud-table-wrapper" style={{ border: 'none', borderRadius: 0 }}>
                <table className="fud-table">
                  <colgroup>
                    <col style={{ width: '22%' }} />
                    <col style={{ width: '40%' }} />
                    <col style={{ width: '18%' }} />
                    <col style={{ width: '20%' }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th>Mã biên bản</th>
                      <th>Lỗi vi phạm</th>
                      <th>Mức độ</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paginatedNotices.map(n => {
                      const isSelected = selectedNotice?.id === n.id;
                      return (
                        <tr
                          key={n.id}
                          onClick={() => handleOpenNotice(n)}
                          style={{
                            cursor: 'pointer',
                            backgroundColor: isSelected ? '#FFF7ED' : 'transparent',
                            transition: 'all 0.15s ease',
                          }}
                        >
                          {/* Mã biên bản (1 dòng, hover xem full) */}
                          <td>
                            <span className="cell-text-1line" title={n.code} style={{ fontWeight: 700, color: '#C2410C' }}>
                              {n.code}
                            </span>
                          </td>

                          {/* Hành vi vi phạm (3 dòng, hover xem full) */}
                          <td>
                            <div className="cell-text-3line" title={`${n.violationTypeName} (${formatDate(n.occurrenceTime)})`}>
                              <span style={{ fontWeight: 600 }}>{n.violationTypeName}</span>
                              <div style={{ fontSize: '11px', color: '#64748B' }}>
                                {formatDate(n.occurrenceTime)}
                              </div>
                            </div>
                          </td>

                          {/* Mức độ (1 dòng, hover xem full) */}
                          <td>
                            <span className="cell-text-1line" title={n.finalLevel || n.initialLevel || 'Chờ xử lý'} style={{ fontWeight: 600, color: n.finalLevel ? '#DC2626' : '#EA580C' }}>
                              {n.finalLevel || n.initialLevel || 'Đang xử lý'}
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
                                backgroundColor:
                                  n.status === 'Recorded' ? '#FEF3C7' :
                                  n.status === 'Finalized' ? '#FEE2E2' : '#F1F5F9',
                                color:
                                  n.status === 'Recorded' ? '#92400E' :
                                  n.status === 'Finalized' ? '#B91C1C' : '#475569',
                              }}
                            >
                              {VIOLATION_STATUS_VI[n.status] || n.status}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {/* Phân trang danh sách thông báo */}
              <Pagination
                currentPage={currentPage}
                totalItems={notices.length}
                pageSize={PAGE_SIZE}
                onPageChange={page => setCurrentPage(page)}
                itemName="thông báo"
              />
            </>
          )}
        </div>

        {/* Cột Chi tiết biên bản & Phản hồi */}
        {selectedNotice && (
          <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #E2E8F0', padding: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 700, color: '#C2410C', textTransform: 'uppercase' }}>
                  Chi tiết hồ sơ
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>
                  {selectedNotice.code}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedNotice(null)}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#94A3B8' }}
              >
                Đóng
              </button>
            </div>

            {/* Thông tin biên bản */}
            <div style={{ backgroundColor: '#F8FAFC', borderRadius: '8px', padding: '14px', fontSize: '13px', lineHeight: 1.8, marginBottom: 16 }}>
              <div><strong>Hành vi vi phạm:</strong> <span style={{ color: '#B91C1C', fontWeight: 600 }}>{selectedNotice.violationTypeName}</span></div>
              <div><strong>Thời điểm vi phạm:</strong> {formatDateTime(selectedNotice.occurrenceTime)}</div>
              <div><strong>Vị trí:</strong> Phòng {selectedNotice.roomNumber} ({selectedNotice.blockName})</div>
              <div><strong>Mức độ ban đầu:</strong> <span style={{ color: '#EA580C', fontWeight: 700 }}>{selectedNotice.initialLevel || 'Chưa định mức'}</span></div>
              {selectedNotice.finalLevel && (
                <div><strong>Mức độ chính thức:</strong> <span style={{ color: '#DC2626', fontWeight: 700 }}>{selectedNotice.finalLevel}</span></div>
              )}
              {selectedNotice.requestDeadline && selectedNotice.status === 'Recorded' && (
                <div><strong>Hạn gửi khiếu nại (24h):</strong> <span style={{ color: '#B45309', fontWeight: 600 }}>{formatDateTime(selectedNotice.requestDeadline)}</span></div>
              )}
            </div>

            <div style={{ marginBottom: 14 }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>Mô tả từ Bảo vệ:</div>
              <div style={{ backgroundColor: '#F1F5F9', padding: '10px', borderRadius: '6px', fontSize: '13px', wordBreak: 'break-word' }}>
                {selectedNotice.description || 'Không có mô tả chi tiết'}
              </div>
            </div>

            {/* Ảnh minh chứng từ bảo vệ */}
            {selectedNotice.evidences.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 6 }}>Ảnh minh chứng từ bảo vệ:</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(80px, 1fr))', gap: '8px' }}>
                  {selectedNotice.evidences.map((ev, i) => (
                    <a key={i} href={ev.fileUrl} target="_blank" rel="noreferrer">
                      <img src={ev.fileUrl} alt="evidence" style={{ width: '100%', height: '65px', objectFit: 'cover', borderRadius: '6px' }} />
                    </a>
                  ))}
                </div>
              </div>
            )}

            {/* Khối phản hồi chính thức từ Ban Quản lý (Sinh viên xem được phản hồi) */}
            {selectedNotice.managerNote && (
              <div style={{ backgroundColor: '#F0FDF4', border: '1px solid #86EFAC', borderRadius: '8px', padding: '14px', marginBottom: 16 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginBottom: 6 }}>
                  <strong style={{ color: '#166534', fontSize: '13px' }}>Phản hồi & Kết luận từ Ban Quản lý:</strong>
                </div>
                <div style={{ fontSize: '13px', color: '#14532D', whiteSpace: 'pre-line', lineHeight: 1.6, wordBreak: 'break-word', backgroundColor: '#FFFFFF', padding: '10px', borderRadius: '6px', border: '1px solid #DCFCE7' }}>
                  {selectedNotice.managerNote}
                </div>
                {selectedNotice.finalizedTime && (
                  <div style={{ fontSize: '11px', color: '#15803D', marginTop: 6, textAlign: 'right' }}>
                    Thời gian kết luận: {formatDateTime(selectedNotice.finalizedTime)}
                  </div>
                )}
              </div>
            )}

            {/* Quyền khiếu nại qua trang Tạo đơn (chỉ hiển thị khi còn hạn 24h và chưa có đơn khiếu nại liên kết) */}
            {selectedNotice.status === 'Recorded' && !selectedNotice.linkedRequestId && !isExpired(selectedNotice.requestDeadline) && (
              <div style={{ textAlign: 'center', padding: '16px', backgroundColor: '#FFF7ED', borderRadius: '8px', border: '1px solid #FFEDD5' }}>
                <p style={{ fontSize: '13px', color: '#9A3412', margin: '0 0 10px', lineHeight: 1.5 }}>
                  Nếu bạn nhận thấy nội dung biên bản vi phạm này chưa chính xác, bạn có quyền gửi khiếu nại trong vòng 24h qua trang <strong>Tạo đơn yêu cầu</strong> (Mục: Khiếu nại vi phạm).
                </p>
                {onGoToAppeal && (
                  <button
                    type="button"
                    onClick={() => onGoToAppeal(selectedNotice)}
                    style={{
                      padding: '9px 18px',
                      backgroundColor: '#EA580C',
                      color: '#FFFFFF',
                      border: 'none',
                      borderRadius: '6px',
                      fontWeight: 600,
                      fontSize: '13px',
                      cursor: 'pointer',
                      boxShadow: '0 1px 2px rgba(0,0,0,0.05)',
                    }}
                  >
                    Tới trang Tạo đơn / Khiếu nại →
                  </button>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
