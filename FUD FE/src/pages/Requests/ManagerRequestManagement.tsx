import React, { useState, useEffect } from 'react';
import { requestApi, violationApi, type RequestItemDto } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime } from '../../utils/dateUtils';
import {
  type DBRequestType,
  type DBRequestStatus,
  REQUEST_TYPE_LABELS,
  REQUEST_STATUS_LABELS,
  REQUEST_STATUS_COLORS,
} from './SubmitRequestPage';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

export const ManagerRequestManagement: React.FC = () => {
  const [requests, setRequests] = useState<RequestItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Lọc
  const [filterType, setFilterType] = useState<string>('');
  const [filterStatus, setFilterStatus] = useState<string>('');
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [currentPage, setCurrentPage] = useState(1);

  // Modal xử lý duyệt / từ chối
  const [selectedRequest, setSelectedRequest] = useState<RequestItemDto | null>(null);
  const [decision, setDecision] = useState<'Approved' | 'Rejected'>('Approved');
  const [resolutionNote, setResolutionNote] = useState('');
  const [adjustedLevel, setAdjustedLevel] = useState<string>('Nhắc nhở');
  const [submitting, setSubmitting] = useState(false);
  const [unsuspending, setUnsuspending] = useState(false);

  useEffect(() => {
    loadRequests(true);

    // Tự động polling cập nhật danh sách yêu cầu/khiếu nại theo thời gian thực (mỗi 5 giây)
    const intervalId = setInterval(() => {
      loadRequests(false);
    }, 5000);

    return () => clearInterval(intervalId);
  }, [filterStatus, filterType]);

  const loadRequests = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await requestApi.getAll({
        requestType: filterType || undefined,
        status: filterStatus || undefined,
      });
      setRequests(data);
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải danh sách đơn yêu cầu');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  const handleOpenProcess = (req: RequestItemDto) => {
    setSelectedRequest(req);
    setDecision('Approved');
    setResolutionNote('');
    setAdjustedLevel('Nhắc nhở');
    setErrorMsg('');
    setSuccessMsg('');
  };

  const handleCloseModal = () => {
    setSelectedRequest(null);
  };

  const handleUnsuspendStudentFromRequest = async (studentId: number) => {
    if (!window.confirm('Bạn có chắc chắn muốn mở khóa quyền đăng ký book phòng và giữ phòng cho sinh viên này không?')) return;
    setUnsuspending(true);
    try {
      await violationApi.unsuspendStudent(studentId, `Mở khóa qua đơn yêu cầu ${selectedRequest?.requestCode}`);
      setSuccessMsg('Đã mở khóa quyền book phòng và giữ phòng cho sinh viên thành công!');
      await loadRequests(false);
      if (selectedRequest) {
        setSelectedRequest(prev => prev ? { ...prev, studentAccountStatus: 'Active', suspensionReason: undefined } : null);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi mở khóa tài khoản');
    } finally {
      setUnsuspending(false);
    }
  };

  // Manager xử lý đơn: Duyệt (chấp thuận) hoặc Từ chối (bác bỏ)
  const handleSubmitProcess = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRequest) return;

    setSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');

    try {
      await requestApi.process(selectedRequest.id, {
        decision,
        note: resolutionNote.trim() || undefined,
        adjustedLevel: (selectedRequest.requestType === 'Complaint' && decision === 'Approved') ? adjustedLevel : undefined,
      });

      setSuccessMsg(`Đã cập nhật trạng thái đơn ${selectedRequest.requestCode} thành "${REQUEST_STATUS_LABELS[decision as DBRequestStatus]}" thành công.`);
      handleCloseModal();
      await loadRequests();
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi cập nhật trạng thái đơn');
    } finally {
      setSubmitting(false);
    }
  };

  // Lọc tìm kiếm client-side
  const filteredRequests = requests.filter(r => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      r.requestCode.toLowerCase().includes(term) ||
      r.studentName.toLowerCase().includes(term) ||
      r.studentCode.toLowerCase().includes(term) ||
      r.roomInfo.toLowerCase().includes(term) ||
      (r.reason && r.reason.toLowerCase().includes(term))
    );
  });

  const paginatedRequests = filteredRequests.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE
  );

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* Tiêu đề */}
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Quản lý & Xét duyệt Đơn yêu cầu
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Theo dõi và thẩm định các đơn khiếu nại vi phạm (Complaint), đổi phòng, trả phòng, đăng ký giữ chỗ và kiến nghị
        </p>
      </div>

      {/* Thông báo Alert */}
      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

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
        {/* Lọc loại đơn */}
        <div style={{ minWidth: '220px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Loại đơn yêu cầu
          </label>
          <select
            value={filterType}
            onChange={e => {
              setFilterType(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả loại đơn</option>
            {(Object.keys(REQUEST_TYPE_LABELS) as DBRequestType[]).map(typeKey => (
              <option key={typeKey} value={typeKey}>{REQUEST_TYPE_LABELS[typeKey]}</option>
            ))}
          </select>
        </div>

        {/* Lọc trạng thái */}
        <div style={{ minWidth: '200px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Trạng thái đơn
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
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả trạng thái</option>
            {(Object.keys(REQUEST_STATUS_LABELS) as DBRequestStatus[]).map(st => (
              <option key={st} value={st}>{REQUEST_STATUS_LABELS[st]}</option>
            ))}
          </select>
        </div>

        {/* Ô tìm kiếm */}
        <div style={{ flex: 1, minWidth: '240px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Tìm theo mã đơn, MSSV, tên, phòng
          </label>
          <input
            type="text"
            placeholder="Nhập thông tin tìm kiếm..."
            value={searchTerm}
            onChange={e => {
              setSearchTerm(e.target.value);
              setCurrentPage(1);
            }}
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
            onClick={() => loadRequests(true)}
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

      {/* Bảng danh sách đơn yêu cầu: Không trượt ngang, không co bảng */}
      <div className="fud-table-wrapper">
        <table className="fud-table">
          <colgroup>
            <col style={{ width: '9%' }} />
            <col style={{ width: '14%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '15%' }} />
            <col style={{ width: '17%' }} />
            <col style={{ width: '20%' }} />
            <col style={{ width: '12%' }} />
          </colgroup>
          <thead>
            <tr>
              <th>Mã đơn</th>
              <th>Sinh viên</th>
              <th>Phòng</th>
              <th>Loại yêu cầu</th>
              <th>Nội dung / Căn cứ</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Đang nạp dữ liệu đơn từ...
                </td>
              </tr>
            ) : paginatedRequests.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Không có đơn yêu cầu nào phù hợp.
                </td>
              </tr>
            ) : (
              paginatedRequests.map(item => {
                const statusMeta = REQUEST_STATUS_COLORS[item.status as DBRequestStatus] || { bg: '#F1F5F9', color: '#475569' };
                return (
                  <tr key={item.id}>
                    {/* Mã đơn (1 dòng, hover xem full) */}
                    <td>
                      <span className="cell-text-1line" title={item.requestCode} style={{ fontWeight: 700, color: '#C2410C' }}>
                        {item.requestCode}
                      </span>
                    </td>

                    {/* Sinh viên (1 dòng, hover xem full) */}
                    <td>
                      <div className="cell-text-1line" title={`${item.studentName} (${item.studentCode})`}>
                        <span style={{ fontWeight: 600 }}>{item.studentName}</span>
                        <div style={{ fontSize: '11px', color: '#64748B' }}>{item.studentCode}</div>
                      </div>
                    </td>

                    {/* Phòng (1 dòng, hover xem full) */}
                    <td>
                      <span className="cell-text-1line" title={item.roomInfo}>
                        {item.roomInfo}
                      </span>
                    </td>

                    {/* Loại đơn (1 dòng, hover xem full) */}
                    <td>
                      <span className="cell-text-1line" title={REQUEST_TYPE_LABELS[item.requestType as DBRequestType] || item.requestType} style={{ fontWeight: 600, color: '#0F172A' }}>
                        {REQUEST_TYPE_LABELS[item.requestType as DBRequestType] || item.requestType}
                      </span>
                    </td>

                    {/* Nội dung / Căn cứ (rút gọn 1-2 dòng kèm dấu ..., hover xem full) */}
                    <td>
                      <div className="cell-text-1line" title={item.reason || 'Không có mô tả'}>
                        {item.complaint?.violationCode ? (
                          <span style={{ fontWeight: 600, color: '#EA580C' }}>
                            [{item.complaint.violationCode}] {item.reason || ''}
                          </span>
                        ) : (
                          <span>{item.reason || '—'}</span>
                        )}
                      </div>
                    </td>

                    {/* Trạng thái (đủ chỗ rộng, hiển thị trọn vẹn badge không bị cắt) */}
                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '4px 10px',
                          borderRadius: '9999px',
                          fontSize: '11px',
                          fontWeight: 600,
                          backgroundColor: statusMeta.bg,
                          color: statusMeta.color,
                          whiteSpace: 'nowrap',
                          maxWidth: '100%',
                          textAlign: 'center',
                        }}
                      >
                        {REQUEST_STATUS_LABELS[item.status as DBRequestStatus] || item.status}
                      </span>
                    </td>

                    {/* Nút Thao tác: Hiển thị đầy đủ chữ */}
                    <td style={{ textAlign: 'right' }}>
                      <button
                        type="button"
                        onClick={() => handleOpenProcess(item)}
                        style={{
                          padding: '6px 10px',
                          backgroundColor: item.status === 'Pending' ? '#EA580C' : '#F1F5F9',
                          color: item.status === 'Pending' ? '#FFFFFF' : '#334155',
                          border: item.status === 'Pending' ? 'none' : '1px solid #CBD5E1',
                          borderRadius: '6px',
                          fontSize: '12px',
                          fontWeight: 600,
                          cursor: 'pointer',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {item.status === 'Pending' ? 'Xử lý đơn' : 'Xem chi tiết'}
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
        totalItems={filteredRequests.length}
        pageSize={PAGE_SIZE}
        onPageChange={page => setCurrentPage(page)}
        itemName="đơn yêu cầu"
      />

      {/* Modal Xử lý / Phê duyệt đơn */}
      {selectedRequest && (
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
              maxWidth: '650px',
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
                  Thẩm định & Phê duyệt đơn
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>
                  {selectedRequest.requestCode} ({REQUEST_TYPE_LABELS[selectedRequest.requestType as DBRequestType] || selectedRequest.requestType})
                </h3>
              </div>
              <button
                type="button"
                onClick={handleCloseModal}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#94A3B8' }}
              >
                Đóng
              </button>
            </div>

            {/* Thông tin đơn */}
            <div style={{ backgroundColor: '#F8FAFC', borderRadius: '8px', padding: '14px', fontSize: '13px', lineHeight: 1.8, marginBottom: 16 }}>
              <div><strong>Sinh viên gửi đơn:</strong> {selectedRequest.studentName} ({selectedRequest.studentCode})</div>
              <div><strong>Vị trí:</strong> {selectedRequest.roomInfo}</div>
              <div><strong>Thời điểm gửi:</strong> {formatDateTime(selectedRequest.createdTime)}</div>
              <div><strong>Trạng thái hiện tại:</strong> {REQUEST_STATUS_LABELS[selectedRequest.status as DBRequestStatus] || selectedRequest.status}</div>
              {selectedRequest.processedByName && (
                <div><strong>Người xử lý:</strong> {selectedRequest.processedByName}</div>
              )}
              {/* Chỉ hiển thị nút mở khóa nếu sinh viên bị đình chỉ VÀ lý do đình chỉ liên quan đến biên bản trong đơn này */}
              {selectedRequest.studentAccountStatus === 'Suspended' &&
                selectedRequest.complaint?.violationCode &&
                selectedRequest.suspensionReason?.includes(selectedRequest.complaint.violationCode) && (
                  <div style={{ marginTop: 8, padding: '8px 12px', backgroundColor: '#FEF2F2', border: '1px solid #FCA5A5', borderRadius: '6px', color: '#991B1B' }}>
                    <div style={{ fontWeight: 700 }}>⚠️ Tài khoản sinh viên đang bị ĐÌNH CHỈ (Khóa Book & Giữ phòng):</div>
                    <div style={{ fontSize: '12px', marginTop: 2 }}>{selectedRequest.suspensionReason || 'Đình chỉ nội trú do vi phạm quy chế.'}</div>
                    <button
                      type="button"
                      disabled={unsuspending}
                      onClick={() => handleUnsuspendStudentFromRequest(selectedRequest.studentId)}
                      style={{
                        marginTop: 6,
                        padding: '5px 12px',
                        backgroundColor: '#15803D',
                        color: '#FFF',
                        border: 'none',
                        borderRadius: '5px',
                        fontSize: '12px',
                        fontWeight: 600,
                        cursor: 'pointer',
                      }}
                    >
                      {unsuspending ? 'Đang mở khóa...' : '🔓 Mở khóa lại quyền Book & Giữ phòng cho sinh viên'}
                    </button>
                  </div>
                )}
            </div>

            {/* Nếu là khiếu nại vi phạm kỷ luật */}
            {selectedRequest.complaint?.violationCode && (
              <div style={{ backgroundColor: '#FFF7ED', border: '1px solid #FED7AA', borderRadius: '8px', padding: '12px', marginBottom: 16, fontSize: '13px', lineHeight: 1.6 }}>
                <div style={{ fontWeight: 700, color: '#C2410C', marginBottom: 4 }}>
                  Chi tiết biên bản bị khiếu nại: {selectedRequest.complaint.violationCode}
                </div>
                <div><strong>Lỗi vi phạm:</strong> {selectedRequest.complaint.violationTypeName}</div>
                {selectedRequest.complaint.evidences.length > 0 && (
                  <div style={{ marginTop: 8 }}>
                    <div style={{ fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: 4 }}>Minh chứng sinh viên đính kèm:</div>
                    <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                      {selectedRequest.complaint.evidences.map((url, idx) => (
                        <a key={idx} href={url} target="_blank" rel="noreferrer">
                          <img src={url} alt="complaint-evidence" style={{ width: '60px', height: '60px', objectFit: 'cover', borderRadius: '6px', border: '1px solid #CBD5E1' }} />
                        </a>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Lý do / Căn cứ sinh viên trình bày */}
            <div style={{ marginBottom: 16 }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
                Nội dung đề xuất / Căn cứ của sinh viên:
              </div>
              <div style={{ backgroundColor: '#F1F5F9', padding: '12px', borderRadius: '6px', fontSize: '13px', wordBreak: 'break-word', whiteSpace: 'pre-line' }}>
                {selectedRequest.reason || 'Không có mô tả chi tiết'}
              </div>
            </div>

            {/* Form xét duyệt đơn nếu đang ở Pending */}
            {selectedRequest.status === 'Pending' ? (
              <form onSubmit={handleSubmitProcess} style={{ borderTop: '1px solid #E2E8F0', paddingTop: 16 }}>
                <div style={{ marginBottom: 14 }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, color: '#0F172A', marginBottom: 6 }}>
                    Quyết định xét duyệt <span style={{ color: '#DC2626' }}>*</span>
                  </label>
                  <div style={{ display: 'flex', gap: '16px' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer', fontWeight: 600, color: '#047857' }}>
                      <input
                        type="radio"
                        name="decision"
                        value="Approved"
                        checked={decision === 'Approved'}
                        onChange={() => setDecision('Approved')}
                      />
                      Chấp thuận đơn khiếu nại (Approved)
                    </label>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer', fontWeight: 600, color: '#DC2626' }}>
                      <input
                        type="radio"
                        name="decision"
                        value="Rejected"
                        checked={decision === 'Rejected'}
                        onChange={() => setDecision('Rejected')}
                      />
                      Từ chối đơn khiếu nại (Rejected)
                    </label>
                  </div>
                </div>

                {/* Cảnh báo rõ ràng cho Manager khi Reject đơn khiếu nại mà mức phạt là Đình chỉ */}
                {selectedRequest.requestType === 'Complaint' && decision === 'Rejected' && (
                  <div style={{ marginBottom: 14, padding: '10px 14px', backgroundColor: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '13px' }}>
                    ⚠️ <strong>Lưu ý:</strong> Khi từ chối đơn khiếu nại (Reject), mức kết luận chính thức sẽ giữ nguyên mức ban đầu.
                    Nếu mức ban đầu là <strong>Đình chỉ nội trú</strong>, hệ thống sẽ <strong>ngay lập tức Khóa quyền Book phòng & Giữ phòng</strong> của sinh viên (hoặc tự động khóa sau 24h nếu không có khiếu nại khác).
                  </div>
                )}

                {/* Nếu chấp thuận khiếu nại vi phạm: Chọn mức phạt mới điều chỉnh */}
                {selectedRequest.requestType === 'Complaint' && decision === 'Approved' && (
                  <div style={{ marginBottom: 14 }}>
                    <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                      Mức độ xử lý sau khi chấp thuận khiếu nại:
                    </label>
                    <select
                      value={adjustedLevel}
                      onChange={e => setAdjustedLevel(e.target.value)}
                      style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                    >
                      <option value="Nhắc nhở">Nhắc nhở</option>
                      <option value="Khiển trách">Khiển trách</option>
                      <option value="Cảnh cáo">Cảnh cáo</option>
                    </select>
                  </div>
                )}

                <div style={{ marginBottom: 16 }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                    Ghi chú / Lý do phản hồi tới sinh viên <span style={{ color: '#DC2626' }}>*</span>
                  </label>
                  <textarea
                    rows={3}
                    value={resolutionNote}
                    onChange={e => setResolutionNote(e.target.value)}
                    placeholder="Ghi rõ lý do chấp thuận hoặc căn cứ từ chối để gửi email và hiển thị trên giao diện của sinh viên..."
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', boxSizing: 'border-box' }}
                    required
                  />
                  <div style={{ fontSize: '12px', color: '#64748B', marginTop: 4 }}>
                    Hệ thống sẽ tự động cập nhật biên bản vi phạm và gửi email thông báo kết quả tới sinh viên.
                  </div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                  <button
                    type="button"
                    onClick={handleCloseModal}
                    style={{ padding: '8px 16px', background: '#F1F5F9', border: '1px solid #CBD5E1', borderRadius: '6px', fontSize: '13px', cursor: 'pointer' }}
                  >
                    Hủy bỏ
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    style={{
                      padding: '8px 18px',
                      background: decision === 'Approved' ? '#047857' : '#DC2626',
                      color: '#FFF',
                      border: 'none',
                      borderRadius: '6px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    {submitting ? 'Đang lưu...' : decision === 'Approved' ? 'Xác nhận chấp thuận' : 'Xác nhận từ chối'}
                  </button>
                </div>
              </form>
            ) : (
              <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: 14 }}>
                {selectedRequest.rejectReason && (
                  <div style={{ backgroundColor: '#FEE2E2', border: '1px solid #FCA5A5', borderRadius: '8px', padding: '12px', marginBottom: 14 }}>
                    <strong style={{ color: '#991B1B', fontSize: '12px' }}>Ghi chú / Lý do xử lý của BQL:</strong>
                    <div style={{ fontSize: '13px', color: '#7F1D1D', marginTop: 4, whiteSpace: 'pre-line' }}>
                      {selectedRequest.rejectReason}
                    </div>
                  </div>
                )}
                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                  <button
                    type="button"
                    onClick={handleCloseModal}
                    style={{ padding: '8px 16px', background: '#EA580C', color: '#FFF', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Đóng
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
