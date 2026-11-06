import React, { useState, useEffect } from 'react';
import { requestApi, type RequestItemDto } from '../../services/api';
import { REQUEST_STATUS_LABELS, REQUEST_STATUS_COLORS, REQUEST_TYPE_LABELS, type DBRequestStatus } from './SubmitRequestPage';
import { Pagination } from '../../components/Pagination';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

interface StudentRequestListPageProps {
  onGoToSubmit?: () => void;
}

export const StudentRequestListPage: React.FC<StudentRequestListPageProps> = ({ onGoToSubmit }) => {
  const [requests, setRequests] = useState<RequestItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [filterType, setFilterType] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedRequest, setSelectedRequest] = useState<RequestItemDto | null>(null);

  useEffect(() => {
    loadMyRequests(true);

    // Tự động polling cập nhật danh sách đơn theo thời gian thực (mỗi 5 giây)
    const intervalId = setInterval(() => {
      loadMyRequests(false);
    }, 5000);

    return () => clearInterval(intervalId);
  }, []);

  const loadMyRequests = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await requestApi.getMyRequests();
      setRequests(data);
      if (selectedRequest) {
        const updated = data.find(r => r.id === selectedRequest.id);
        if (updated) setSelectedRequest(updated);
      }
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải danh sách đơn từ của bạn');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  const filteredRequests = requests.filter(r => {
    if (filterType && r.requestType !== filterType) return false;
    if (filterStatus && r.status !== filterStatus) return false;
    return true;
  });

  const paginatedRequests = filteredRequests.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE
  );

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* Tiêu đề & Nút gửi đơn mới */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
            Đơn từ & Yêu cầu của bạn
          </h1>
          <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
            Theo dõi tiến độ xử lý và phản hồi các đơn yêu cầu chuyển phòng, trả phòng, khiếu nại và kiến nghị
          </p>
        </div>
        {onGoToSubmit && (
          <button
            type="button"
            onClick={onGoToSubmit}
            style={{
              padding: '9px 18px',
              backgroundColor: '#EA580C',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: '8px',
              fontWeight: 600,
              fontSize: '13px',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              boxShadow: '0 1px 3px rgba(0,0,0,0.1)',
            }}
          >
            <span>+</span> Gửi đơn yêu cầu mới
          </button>
        )}
      </div>

      {errorMsg && (
        <div className="violation-alert violation-alert-error" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{errorMsg}</span>
          <button
            type="button"
            onClick={() => loadMyRequests(true)}
            style={{
              background: '#DC2626',
              color: '#FFF',
              border: 'none',
              padding: '4px 12px',
              borderRadius: '6px',
              cursor: 'pointer',
              fontSize: '12px',
            }}
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Bộ lọc đơn từ */}
      <div
        style={{
          display: 'flex',
          gap: '12px',
          alignItems: 'center',
          backgroundColor: '#FFFFFF',
          padding: '14px 18px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
          marginBottom: 16,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span style={{ fontSize: '12px', fontWeight: 700, color: '#475569' }}>Loại đơn:</span>
          <select
            value={filterType}
            onChange={e => {
              setFilterType(e.target.value);
              setCurrentPage(1);
            }}
            style={{ padding: '6px 10px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '12px' }}
          >
            <option value="">Tất cả loại đơn</option>
            {Object.keys(REQUEST_TYPE_LABELS).map(key => (
              <option key={key} value={key}>
                {REQUEST_TYPE_LABELS[key as keyof typeof REQUEST_TYPE_LABELS]}
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span style={{ fontSize: '12px', fontWeight: 700, color: '#475569' }}>Trạng thái:</span>
          <select
            value={filterStatus}
            onChange={e => {
              setFilterStatus(e.target.value);
              setCurrentPage(1);
            }}
            style={{ padding: '6px 10px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '12px' }}
          >
            <option value="">Tất cả trạng thái</option>
            {Object.keys(REQUEST_STATUS_LABELS).map(st => (
              <option key={st} value={st}>
                {REQUEST_STATUS_LABELS[st as DBRequestStatus]}
              </option>
            ))}
          </select>
        </div>

        <button
          type="button"
          onClick={() => loadMyRequests(true)}
          style={{
            marginLeft: 'auto',
            padding: '6px 12px',
            background: '#F1F5F9',
            border: '1px solid #CBD5E1',
            borderRadius: '6px',
            fontSize: '12px',
            cursor: 'pointer',
          }}
        >
          Làm mới
        </button>
      </div>

      {/* Nội dung danh sách & chi tiết */}
      <div style={{ display: 'grid', gridTemplateColumns: selectedRequest ? '1.1fr 0.9fr' : '1fr', gap: 16 }}>
        {/* Bảng danh sách đơn */}
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #E2E8F0', padding: '16px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
            <span style={{ fontSize: '13px', fontWeight: 700, color: '#0F172A' }}>
              Danh sách đơn đã nộp ({filteredRequests.length})
            </span>
          </div>

          {loading ? (
            <div style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>Đang nạp danh sách đơn từ...</div>
          ) : filteredRequests.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '40px', color: '#64748B' }}>
              <strong>Chưa có đơn từ / yêu cầu nào</strong>
              <p style={{ fontSize: '13px', margin: '4px 0 0' }}>Khi bạn gửi đơn, thông tin tiến độ xét duyệt sẽ hiển thị tại đây.</p>
            </div>
          ) : (
            <>
              <div className="fud-table-wrapper" style={{ border: 'none', borderRadius: 0 }}>
                <table className="fud-table">
                  <colgroup>
                    <col style={{ width: '22%' }} />
                    <col style={{ width: '28%' }} />
                    <col style={{ width: '25%' }} />
                    <col style={{ width: '25%' }} />
                  </colgroup>
                  <thead>
                    <tr>
                      <th>Mã đơn</th>
                      <th>Loại yêu cầu</th>
                      <th>Thời gian gửi</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paginatedRequests.map(r => {
                      const isSelected = selectedRequest?.id === r.id;
                      const statusColor = REQUEST_STATUS_COLORS[r.status as DBRequestStatus] || { bg: '#F1F5F9', color: '#475569' };
                      return (
                        <tr
                          key={r.id}
                          onClick={() => setSelectedRequest(r)}
                          style={{
                            cursor: 'pointer',
                            backgroundColor: isSelected ? '#FFF7ED' : 'transparent',
                            transition: 'all 0.15s ease',
                          }}
                        >
                          <td>
                            <span className="cell-text-1line" title={r.requestCode} style={{ fontWeight: 700, color: '#C2410C' }}>
                              {r.requestCode}
                            </span>
                          </td>
                          <td>
                            <span className="cell-text-1line" title={REQUEST_TYPE_LABELS[r.requestType as keyof typeof REQUEST_TYPE_LABELS] || r.requestType}>
                              {REQUEST_TYPE_LABELS[r.requestType as keyof typeof REQUEST_TYPE_LABELS] || r.requestType}
                            </span>
                          </td>
                          <td>
                            <span className="cell-text-1line">
                              {new Date(r.createdTime).toLocaleDateString('vi-VN')}
                            </span>
                          </td>
                          <td>
                            <span
                              style={{
                                display: 'inline-block',
                                padding: '3px 8px',
                                borderRadius: '9999px',
                                fontSize: '11px',
                                fontWeight: 700,
                                backgroundColor: statusColor.bg,
                                color: statusColor.color,
                              }}
                            >
                              {REQUEST_STATUS_LABELS[r.status as DBRequestStatus] || r.status}
                            </span>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              <Pagination
                currentPage={currentPage}
                totalItems={filteredRequests.length}
                pageSize={PAGE_SIZE}
                onPageChange={page => setCurrentPage(page)}
                itemName="đơn yêu cầu"
              />
            </>
          )}
        </div>

        {/* Khối chi tiết đơn yêu cầu */}
        {selectedRequest && (
          <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #E2E8F0', padding: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 700, color: '#C2410C', textTransform: 'uppercase' }}>
                  Chi tiết đơn yêu cầu
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>
                  {selectedRequest.requestCode}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedRequest(null)}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#94A3B8' }}
              >
                ✕
              </button>
            </div>

            <div style={{ backgroundColor: '#F8FAFC', borderRadius: '8px', padding: '14px', fontSize: '13px', lineHeight: 1.8, marginBottom: 16 }}>
              <div><strong>Loại đơn:</strong> {REQUEST_TYPE_LABELS[selectedRequest.requestType as keyof typeof REQUEST_TYPE_LABELS] || selectedRequest.requestType}</div>
              <div><strong>Ngày gửi đơn:</strong> {new Date(selectedRequest.createdTime).toLocaleString('vi-VN')}</div>
              <div>
                <strong>Trạng thái: </strong>
                <span
                  style={{
                    display: 'inline-block',
                    padding: '2px 8px',
                    borderRadius: '9999px',
                    fontSize: '11px',
                    fontWeight: 700,
                    backgroundColor: (REQUEST_STATUS_COLORS[selectedRequest.status as DBRequestStatus] || { bg: '#F1F5F9', color: '#475569' }).bg,
                    color: (REQUEST_STATUS_COLORS[selectedRequest.status as DBRequestStatus] || { bg: '#F1F5F9', color: '#475569' }).color,
                  }}
                >
                  {REQUEST_STATUS_LABELS[selectedRequest.status as DBRequestStatus] || selectedRequest.status}
                </span>
              </div>
              {selectedRequest.processedByName && (
                <div><strong>Người thụ lý:</strong> {selectedRequest.processedByName}</div>
              )}
            </div>

            {/* Thông tin gắn kèm biên bản vi phạm nếu là khiếu nại vi phạm */}
            {selectedRequest.complaint && (
              <div style={{ backgroundColor: '#FFF7ED', border: '1px solid #FED7AA', borderRadius: '8px', padding: '12px', marginBottom: 16, fontSize: '12px', lineHeight: 1.6 }}>
                <div style={{ fontWeight: 700, color: '#C2410C', marginBottom: 4 }}>Biên bản vi phạm khiếu nại liên quan:</div>
                <div><strong>Mã biên bản:</strong> {selectedRequest.complaint.violationCode}</div>
                <div><strong>Lỗi vi phạm:</strong> {selectedRequest.complaint.violationTypeName}</div>
              </div>
            )}

            {/* Nội dung đơn sinh viên viết */}
            <div style={{ marginBottom: 14 }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>Lý do / Nội dung bạn đã gửi:</div>
              <div style={{ backgroundColor: '#F1F5F9', padding: '12px', borderRadius: '8px', fontSize: '13px', lineHeight: 1.6, wordBreak: 'break-word', whiteSpace: 'pre-line' }}>
                {selectedRequest.reason || selectedRequest.complaint?.content || 'Không có mô tả chi tiết'}
              </div>
            </div>

            {/* Ảnh minh chứng sinh viên tải lên */}
            {selectedRequest.complaint?.evidences && selectedRequest.complaint.evidences.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 6 }}>Minh chứng đính kèm:</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(80px, 1fr))', gap: '8px' }}>
                  {selectedRequest.complaint.evidences.map((url: string, i: number) => (
                    <a key={i} href={url} target="_blank" rel="noreferrer">
                      <img src={url} alt="evidence" style={{ width: '100%', height: '65px', objectFit: 'cover', borderRadius: '6px' }} />
                    </a>
                  ))}
                </div>
              </div>
            )}

            {/* Phản hồi & Kết quả từ Quản lý */}
            {(selectedRequest.rejectReason || selectedRequest.complaint?.resolutionNote) && (
              <div style={{ backgroundColor: '#F0FDF4', border: '1px solid #86EFAC', borderRadius: '8px', padding: '14px' }}>
                <strong style={{ color: '#166534', fontSize: '13px', display: 'block', marginBottom: 6 }}>
                  Phản hồi từ Ban Quản lý KTX:
                </strong>
                <div style={{ fontSize: '13px', color: '#14532D', whiteSpace: 'pre-line', lineHeight: 1.6, backgroundColor: '#FFFFFF', padding: '10px', borderRadius: '6px', border: '1px solid #DCFCE7' }}>
                  {selectedRequest.rejectReason || selectedRequest.complaint?.resolutionNote}
                </div>
                {selectedRequest.processedTime && (
                  <div style={{ fontSize: '11px', color: '#15803D', marginTop: 6, textAlign: 'right' }}>
                    Thời gian duyệt: {new Date(selectedRequest.processedTime).toLocaleString('vi-VN')}
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
