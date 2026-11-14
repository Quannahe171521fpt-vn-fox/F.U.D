import React, { useState, useEffect } from 'react';
import { requestApi, violationApi, type RequestItemDto } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime } from '../../utils/dateUtils';
import { removeVietnameseTones } from '../../utils/textUtils';
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
  const [filterSuspended, setFilterSuspended] = useState<'all' | 'suspended' | 'normal'>('all');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
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
    // Tìm mức độ nhẹ hơn mức ban đầu nếu là complaint
    const initial = req.complaint?.initialLevel;
    const allLevels = ['Nhắc nhở', 'Khiển trách', 'Cảnh cáo', 'Đình chỉ nội trú'];
    const initialIndex = initial ? allLevels.findIndex(l => l.toLowerCase() === initial.toLowerCase() || initial.toLowerCase().includes(l.toLowerCase())) : -1;
    const lighterLevels = initialIndex > 0 ? allLevels.slice(0, initialIndex) : allLevels.filter(l => !initial || !l.toLowerCase().includes(initial.toLowerCase()));
    setAdjustedLevel(lighterLevels.length > 0 ? lighterLevels[0] : 'Nhắc nhở');
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

  // Lọc tìm kiếm client-side (hỗ trợ có dấu và không dấu, lọc đình chỉ, sort thời gian)
  const filteredRequests = requests
    .filter(r => {
      if (filterSuspended === 'suspended') {
        const isSuspended = r.studentAccountStatus === 'Suspended' || (r.suspensionReason && r.suspensionReason.trim() !== '');
        if (!isSuspended) return false;
      } else if (filterSuspended === 'normal') {
        const isSuspended = r.studentAccountStatus === 'Suspended' || (r.suspensionReason && r.suspensionReason.trim() !== '');
        if (isSuspended) return false;
      }

      if (!searchTerm.trim()) return true;
      const term = removeVietnameseTones(searchTerm);
      const codeNorm = removeVietnameseTones(r.requestCode || '');
      const nameNorm = removeVietnameseTones(r.studentName || '');
      const studentCodeNorm = removeVietnameseTones(r.studentCode || '');
      const roomNorm = removeVietnameseTones(r.roomInfo || '');
      const reasonNorm = removeVietnameseTones(r.reason || '');

      return (
        codeNorm.includes(term) ||
        nameNorm.includes(term) ||
        studentCodeNorm.includes(term) ||
        roomNorm.includes(term) ||
        reasonNorm.includes(term)
      );
    })
    .sort((a, b) => {
      const timeA = new Date(a.createdTime).getTime();
      const timeB = new Date(b.createdTime).getTime();
      return sortOrder === 'desc' ? timeB - timeA : timeA - timeB;
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
        <p style={{ color: '#000000ff', fontSize: '13px', margin: 0 }}>
          Theo dõi và thẩm định các đơn từ.
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

        {/* Lọc tình trạng đình chỉ */}
        <div style={{ minWidth: '180px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Tài khoản sinh viên
          </label>
          <select
            value={filterSuspended}
            onChange={e => {
              setFilterSuspended(e.target.value as any);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
              fontWeight: filterSuspended === 'suspended' ? 700 : 400,
              color: filterSuspended === 'suspended' ? '#DC2626' : 'inherit',
            }}
          >
            <option value="all">Tất cả sinh viên</option>
            <option value="suspended">Đang bị đình chỉ</option>
            <option value="normal">Bình thường</option>
          </select>
        </div>

        {/* Sắp xếp thời gian */}
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
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
              fontWeight: 600,
            }}
          >
            <option value="desc">Mới nhất trước ↓</option>
            <option value="asc">Cũ nhất trước ↑</option>
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

                    {/* Sinh viên (1 dòng, hover xem full) kèm cảnh báo nếu đang bị đình chỉ */}
                    <td>
                      <div className="cell-text-1line" title={`${item.studentName} (${item.studentCode})`}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span style={{ fontWeight: 600 }}>{item.studentName}</span>
                          {(item.studentAccountStatus === 'Suspended' || item.suspensionReason) && (
                            <span
                              title={`Tài khoản bị đình chỉ: ${item.suspensionReason || 'Vi phạm quy chế KTX'}`}
                              style={{
                                display: 'inline-block',
                                padding: '1px 5px',
                                fontSize: '10px',
                                fontWeight: 700,
                                backgroundColor: '#FEE2E2',
                                color: '#DC2626',
                                borderRadius: '4px',
                                border: '1px solid #FCA5A5',
                              }}
                            >
                              Đình chỉ
                            </span>
                          )}
                        </div>
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
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#000000ff' }}
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
                    <div style={{ fontWeight: 700 }}>Tài khoản sinh viên đang bị ĐÌNH CHỈ (Khóa Đặt & Giữ phòng và một vài chức năng liên quan đến KTX):</div>
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
                      {unsuspending ? 'Đang mở khóa...' : 'Mở khóa lại quyền Đặt & Giữ phòng cho sinh viên'}
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
                {selectedRequest.complaint.initialLevel && (
                  <div>
                    <strong>Mức độ ban đầu:</strong>{' '}
                    <span style={{ color: '#EA580C', fontWeight: 700, padding: '2px 8px', backgroundColor: '#FFEDD5', borderRadius: '4px' }}>
                      {selectedRequest.complaint.initialLevel}
                    </span>
                  </div>
                )}
                {selectedRequest.complaint.finalLevel && (
                  <div style={{ marginTop: 4 }}>
                    <strong>Mức độ sau khi xử lý (Chính thức):</strong>{' '}
                    <span style={{ color: selectedRequest.status === 'Approved' ? '#047857' : '#DC2626', fontWeight: 700, padding: '2px 8px', backgroundColor: selectedRequest.status === 'Approved' ? '#D1FAE5' : '#FEE2E2', borderRadius: '4px' }}>
                      {selectedRequest.complaint.finalLevel}
                    </span>
                  </div>
                )}
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
                     <strong>Lưu ý:</strong> Khi từ chối đơn khiếu nại (Reject), mức kết luận chính thức sẽ giữ nguyên mức ban đầu.
                    Nếu mức ban đầu là <strong>Đình chỉ nội trú</strong>, hệ thống sẽ <strong>ngay lập tức Khóa quyền Book phòng & Giữ phòng</strong> của sinh viên (hoặc tự động khóa sau 24h nếu không có khiếu nại khác).
                  </div>
                )}

                {/* Nếu chấp thuận khiếu nại vi phạm: Chọn mức phạt mới điều chỉnh (loại bỏ mức ban đầu và chỉ cho chọn mức nhẹ hơn) */}
                {selectedRequest.requestType === 'Complaint' && decision === 'Approved' && (() => {
                  const initial = selectedRequest.complaint?.initialLevel;
                  const allLevels = ['Nhắc nhở', 'Khiển trách', 'Cảnh cáo', 'Đình chỉ nội trú'];
                  const initialIndex = initial ? allLevels.findIndex(l => l.toLowerCase() === initial.toLowerCase() || initial.toLowerCase().includes(l.toLowerCase())) : -1;
                  // Lấy các mức nhẹ hơn mức ban đầu
                  const availableLevels = initialIndex > 0 ? allLevels.slice(0, initialIndex) : allLevels.filter(l => !initial || !l.toLowerCase().includes(initial.toLowerCase()));

                  return (
                    <div style={{ marginBottom: 14 }}>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                        Mức độ xử lý sau khi chấp thuận khiếu nại (Chọn mức nhẹ hơn mức ban đầu):
                      </label>
                      <select
                        value={adjustedLevel}
                        onChange={e => setAdjustedLevel(e.target.value)}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                      >
                        {availableLevels.map(lvl => (
                          <option key={lvl} value={lvl}>{lvl}</option>
                        ))}
                      </select>
                      {initial && (
                        <div style={{ fontSize: '11px', color: '#64748B', marginTop: 4 }}>
                          * Mức ban đầu là <strong>{initial}</strong> đã bị ẩn theo quy định giảm nhẹ hình thức xử lý.
                        </div>
                      )}
                    </div>
                  );
                })()}

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
                {/* Hiển thị chi tiết kết quả sau khi xử lý đơn */}
                <div style={{ backgroundColor: selectedRequest.status === 'Approved' ? '#ECFDF5' : '#FEF2F2', border: `1px solid ${selectedRequest.status === 'Approved' ? '#A7F3D0' : '#FECACA'}`, borderRadius: '8px', padding: '14px', marginBottom: 14 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: 8 }}>
                    <span style={{ fontSize: '13px', fontWeight: 700, color: selectedRequest.status === 'Approved' ? '#047857' : '#991B1B' }}>
                      {selectedRequest.status === 'Approved' ? 'Kết quả: Đơn khiếu nại ĐÃ ĐƯỢC CHẤP THUẬN' : 'Kết quả: Đơn khiếu nại ĐÃ BỊ TỪ CHỐI'}
                    </span>
                  </div>

                  {selectedRequest.complaint?.finalLevel && (
                    <div style={{ fontSize: '13px', marginBottom: 6, color: '#0F172A' }}>
                      <strong>Mức độ kỷ luật áp dụng chính thức:</strong>{' '}
                      <span style={{ fontWeight: 700, color: selectedRequest.status === 'Approved' ? '#047857' : '#DC2626' }}>
                        {selectedRequest.complaint.finalLevel}
                      </span>
                      {selectedRequest.complaint.initialLevel && selectedRequest.status === 'Approved' && (
                        <span style={{ fontSize: '12px', color: '#64748B', marginLeft: 6 }}>
                          (Đã giảm nhẹ từ mức ban đầu: <s>{selectedRequest.complaint.initialLevel}</s>)
                        </span>
                      )}
                      {selectedRequest.complaint.initialLevel && selectedRequest.status === 'Rejected' && (
                        <span style={{ fontSize: '12px', color: '#64748B', marginLeft: 6 }}>
                          (Giữ nguyên mức độ kỷ luật ban đầu)
                        </span>
                      )}
                    </div>
                  )}

                  {(selectedRequest.rejectReason || selectedRequest.complaint?.resolutionNote) && (
                    <div style={{ marginTop: 8, paddingTop: 8, borderTop: `1px dashed ${selectedRequest.status === 'Approved' ? '#A7F3D0' : '#FECACA'}` }}>
                      <strong style={{ fontSize: '12px', color: selectedRequest.status === 'Approved' ? '#065F46' : '#991B1B' }}>
                        Ghi chú / Phản hồi của Ban Quản lý:
                      </strong>
                      <div style={{ fontSize: '13px', color: selectedRequest.status === 'Approved' ? '#064E3B' : '#7F1D1D', marginTop: 4, whiteSpace: 'pre-line' }}>
                        {selectedRequest.rejectReason || selectedRequest.complaint?.resolutionNote}
                      </div>
                    </div>
                  )}

                  {selectedRequest.processedByName && (
                    <div style={{ fontSize: '11px', color: '#64748B', marginTop: 8 }}>
                      Xử lý bởi: <strong>{selectedRequest.processedByName}</strong> {selectedRequest.processedTime && `vào lúc ${formatDateTime(selectedRequest.processedTime)}`}
                    </div>
                  )}
                </div>

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
