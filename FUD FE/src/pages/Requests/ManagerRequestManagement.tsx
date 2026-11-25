import React, { useState, useEffect } from 'react';
import { requestApi, violationApi, type GuardLookupDto, type RequestItemDto } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { TransferDatePicker } from '../../components/TransferDatePicker';
import { ImageLightboxModal } from '../../components/ImageLightboxModal';
import { formatDateTime } from '../../utils/dateUtils';
import { removeVietnameseTones } from '../../utils/textUtils';
import { toSafeHref } from '../../utils/urlUtils';
import { useIdleAwarePolling } from '../../utils/useIdleAwarePolling';
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
  const [meterLightbox, setMeterLightbox] = useState<{ url: string; title: string; subtitle?: string } | null>(null);

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
  const [guards, setGuards] = useState<GuardLookupDto[]>([]);
  const [loadingGuards, setLoadingGuards] = useState(false);
  const [assignedGuardId, setAssignedGuardId] = useState<string>('');
  const [transferDateInput, setTransferDateInput] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [unsuspending, setUnsuspending] = useState(false);

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
      setSelectedRequest(prev => (prev ? data.find(item => item.id === prev.id) || prev : null));
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

  useEffect(() => {
    loadRequests(true);
  }, [filterStatus, filterType]);

  useIdleAwarePolling(() => {
    void loadRequests(false);
  }, 5000);

  const handleOpenProcess = async (req: RequestItemDto) => {
    setSelectedRequest(req);
    setDecision('Approved');
    setResolutionNote('');
    setAssignedGuardId(
      req.bedTransfer?.assignedGuardId
        ? String(req.bedTransfer.assignedGuardId)
        : req.checkout?.assignedGuardId
          ? String(req.checkout.assignedGuardId)
          : ''
    );
    setTransferDateInput(
      req.bedTransfer?.transferDate
        ? req.bedTransfer.transferDate.slice(0, 10)
        : req.checkout?.expectedCheckOutDate
          ? req.checkout.expectedCheckOutDate.slice(0, 10)
          : ''
    );

    // Tìm mức độ nhẹ hơn mức ban đầu nếu là complaint
    const initial = req.complaint?.initialLevel;
    const allLevels = ['Nhắc nhở', 'Khiển trách', 'Cảnh cáo', 'Đình chỉ nội trú'];
    const initialIndex = initial ? allLevels.findIndex(l => l.toLowerCase() === initial.toLowerCase() || initial.toLowerCase().includes(l.toLowerCase())) : -1;
    const lighterLevels = initialIndex > 0 ? allLevels.slice(0, initialIndex) : allLevels.filter(l => !initial || !l.toLowerCase().includes(initial.toLowerCase()));
    setAdjustedLevel(lighterLevels.length > 0 ? lighterLevels[0] : 'Nhắc nhở');
    setErrorMsg('');
    setSuccessMsg('');

    if ((req.requestType === 'BedTransfer' || req.requestType === 'Checkout') && req.status === 'Pending') {
      const targetDormId = req.requestType === 'BedTransfer' ? req.bedTransfer?.fromDormId : req.checkout?.dormId;
      setLoadingGuards(true);
      try {
        const list = await requestApi.getGuards(targetDormId);
        const sameDormGuards = targetDormId
          ? list.filter(g => g.assignedDormId === targetDormId)
          : list;
        setGuards(sameDormGuards);
        setAssignedGuardId(sameDormGuards.length > 0 ? String(sameDormGuards[0].userId) : '');
      } catch (err: any) {
        setGuards([]);
        setAssignedGuardId('');
        setErrorMsg(err.message || 'Không thể tải thông tin Bảo vệ phụ trách tòa KTX.');
      } finally {
        setLoadingGuards(false);
      }
    }
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

  // Manager xử lý đơn: Duyệt Bước 1 (Approved) hoặc Từ chối (Rejected)
  const handleSubmitProcess = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedRequest) return;

    if ((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') && decision === 'Approved' && !assignedGuardId) {
      setErrorMsg('Vui lòng phân công Bảo vệ kiểm tra phòng trước khi duyệt Bước 1.');
      return;
    }

    if ((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') && decision === 'Rejected' && !resolutionNote.trim()) {
      setErrorMsg(
        selectedRequest.requestType === 'Checkout'
          ? 'Vui lòng nhập lý do từ chối đơn trả phòng.'
          : 'Vui lòng nhập lý do từ chối đơn chuyển giường.'
      );
      return;
    }

    setSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');

    try {
      await requestApi.process(selectedRequest.id, {
        decision,
        note: resolutionNote.trim() || undefined,
        adjustedLevel: (selectedRequest.requestType === 'Complaint' && decision === 'Approved') ? adjustedLevel : undefined,
        assignedGuardId: ((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') && decision === 'Approved') ? Number(assignedGuardId) : undefined,
        transferDate: (selectedRequest.requestType === 'BedTransfer' && decision === 'Approved' && transferDateInput) ? transferDateInput : undefined,
        expectedCheckOutDate: (selectedRequest.requestType === 'Checkout' && decision === 'Approved' && transferDateInput) ? transferDateInput : undefined,
      });

      setSuccessMsg(`Đã cập nhật trạng thái đơn ${selectedRequest.requestCode} thành "${REQUEST_STATUS_LABELS[decision as DBRequestStatus] || decision}" thành công.`);
      handleCloseModal();
      await loadRequests();
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi cập nhật trạng thái đơn');
    } finally {
      setSubmitting(false);
    }
  };

  // Manager Bước 2: Hoàn tất trả phòng & phát hành hóa đơn (Completed)
  const handleCompleteCheckout = async () => {
    if (!selectedRequest) return;
    setSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');

    try {
      const updated = await requestApi.process(selectedRequest.id, {
        decision: 'Completed',
        note: resolutionNote.trim() || undefined,
      });

      const issuedCount = updated.checkout?.invoices?.length ?? 0;
      setSuccessMsg(
        issuedCount > 0
          ? `Đã hoàn tất trả phòng (Bed -> Free), phát hành ${issuedCount} hóa đơn công nợ sang mục Thanh toán của sinh viên cho đơn ${selectedRequest.requestCode}.`
          : `Đã hoàn tất trả phòng (Bed -> Free) cho đơn ${selectedRequest.requestCode} (không phát sinh công nợ).`
      );
      handleCloseModal();
      await loadRequests();
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi hoàn tất trả phòng');
    } finally {
      setSubmitting(false);
    }
  };

  // Manager Bước 2: Phát hành hóa đơn & cập nhật Residency/Bed (Completed)
  const handleCompleteBedTransfer = async () => {
    if (!selectedRequest) return;
    setSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');

    try {
      const updated = await requestApi.process(selectedRequest.id, {
        decision: 'Completed',
        note: resolutionNote.trim() || undefined,
      });

      const issuedCount = updated.bedTransfer?.invoices?.length ?? 0;
      setSuccessMsg(
        issuedCount > 0
          ? `Đã phát hành ${issuedCount} hóa đơn và đẩy sang mục Thanh toán hóa đơn của sinh viên; hoàn tất chuyển giường cho đơn ${selectedRequest.requestCode}.`
          : `Đã hoàn tất chuyển giường cho đơn ${selectedRequest.requestCode} (không phát sinh hóa đơn công nợ).`
      );
      handleCloseModal();
      await loadRequests();
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi hoàn tất chuyển giường');
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

                    {/* Nút Thao tác */}
                    <td style={{ textAlign: 'right' }}>
                      {(() => {
                        const isBedTransferStep2Ready =
                          item.requestType === 'BedTransfer' &&
                          item.status === 'Approved' &&
                          Boolean(item.bedTransfer?.handover);
                        const isCheckoutStep2Ready =
                          item.requestType === 'Checkout' &&
                          item.status === 'Approved' &&
                          Boolean(item.checkout?.handover);
                        const isStep2Ready = isBedTransferStep2Ready || isCheckoutStep2Ready;
                        const isWaitingGuard =
                          ((item.requestType === 'BedTransfer' && !item.bedTransfer?.handover) ||
                            (item.requestType === 'Checkout' && !item.checkout?.handover)) &&
                          item.status === 'Approved';
                        const isPrimary = item.status === 'Pending' || isStep2Ready;

                        return (
                          <button
                            type="button"
                            onClick={() => handleOpenProcess(item)}
                            style={{
                              padding: '6px 10px',
                              backgroundColor: isStep2Ready
                                ? '#047857'
                                : isPrimary
                                  ? '#EA580C'
                                  : isWaitingGuard
                                    ? '#EFF6FF'
                                    : '#F1F5F9',
                              color: isPrimary ? '#FFFFFF' : isWaitingGuard ? '#1D4ED8' : '#334155',
                              border: isPrimary
                                ? 'none'
                                : isWaitingGuard
                                  ? '1px solid #BFDBFE'
                                  : '1px solid #CBD5E1',
                              borderRadius: '6px',
                              fontSize: '12px',
                              fontWeight: 600,
                              cursor: 'pointer',
                              whiteSpace: 'nowrap',
                            }}
                          >
                            {item.status === 'Pending'
                              ? 'Xử lý đơn'
                              : isBedTransferStep2Ready
                                ? 'Xuất HĐ & Chuyển giường'
                                : isCheckoutStep2Ready
                                  ? 'Hoàn tất & Xuất HĐ'
                                  : isWaitingGuard
                                    ? 'Chờ Bảo vệ kiểm tra'
                                    : 'Xem chi tiết'}
                          </button>
                        );
                      })()}
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
                      {selectedRequest.complaint.evidences.map((url, idx) => {
                        const safeUrl = toSafeHref(url);
                        if (!safeUrl) return null;
                        return (
                          <a key={idx} href={safeUrl} target="_blank" rel="noopener noreferrer">
                            <img src={safeUrl} alt="complaint-evidence" style={{ width: '60px', height: '60px', objectFit: 'cover', borderRadius: '6px', border: '1px solid #CBD5E1' }} />
                          </a>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Nếu là đơn chuyển giường */}
            {selectedRequest.bedTransfer && (() => {
              const bt = selectedRequest.bedTransfer;
              const isApprovedWaitOrStep2 = selectedRequest.status === 'Approved';
              const isCompleted = selectedRequest.status === 'Completed';
              return (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginBottom: 16 }}>
                  <div style={{ backgroundColor: '#EFF6FF', border: '1px solid #BFDBFE', borderRadius: '8px', padding: '12px', fontSize: '13px', lineHeight: 1.7 }}>
                    <div style={{ fontWeight: 700, color: '#1D4ED8', marginBottom: 4 }}>
                      Chi tiết yêu cầu chuyển giường:
                    </div>
                    <div>
                      <strong>Từ giường:</strong> {bt.fromDormName} — {bt.fromBlockName} — P.{bt.fromRoomNumber} (Giường <strong>{bt.fromBedCode}</strong>)
                    </div>
                    <div>
                      <strong>Chuyển đến:</strong> {bt.toDormName} — {bt.toBlockName} — P.{bt.toRoomNumber} (Giường <strong>{bt.toBedCode}</strong> — {bt.toRoomTypeName})
                    </div>
                    {bt.transferDate && (
                      <div>
                        <strong>Ngày chuyển áp dụng:</strong> {new Date(bt.transferDate).toLocaleDateString('vi-VN')}
                      </div>
                    )}
                    {bt.assignedGuardName && (
                      <div>
                        <strong>Bảo vệ phụ trách kiểm tra:</strong> {bt.assignedGuardName} ({bt.assignedGuardCode})
                      </div>
                    )}
                    {(bt.daysStayed != null || bt.daysRemaining != null) && (
                      <div style={{ marginTop: 6, paddingTop: 6, borderTop: '1px dashed #93C5FD', display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 6, fontSize: 12 }}>
                        <div>Số ngày đã ở giường cũ: <strong>{bt.daysStayed ?? 0} ngày</strong> ({(bt.oldBedDailyRate ?? 0).toLocaleString('vi-VN')} đ/ngày)</div>
                        <div>Số ngày còn lại giường mới: <strong>{bt.daysRemaining ?? 0} ngày</strong> ({(bt.newBedDailyRate ?? 0).toLocaleString('vi-VN')} đ/ngày)</div>
                        <div>Tiền giường cũ: <strong>{(bt.oldBedAmount ?? 0).toLocaleString('vi-VN')} đ</strong></div>
                        <div>Tiền giường mới: <strong>{(bt.newBedAmount ?? 0).toLocaleString('vi-VN')} đ</strong></div>
                        <div>Đã thanh toán trước đó: <strong>{(bt.amountAlreadyPaid ?? 0).toLocaleString('vi-VN')} đ</strong></div>
                        <div>
                          Chênh lệch Prorate:{' '}
                          <strong style={{ color: (bt.priceDifference ?? 0) > 0 ? '#B91C1C' : '#047857' }}>
                            {(bt.priceDifference ?? 0).toLocaleString('vi-VN')} đ
                          </strong>
                        </div>
                      </div>
                    )}
                    {bt.priceDifference != null && bt.daysStayed == null && (
                      <div>
                        <strong>Chênh lệch chi phí (Prorate):</strong>{' '}
                        <span style={{ fontWeight: 700, color: bt.priceDifference > 0 ? '#B91C1C' : '#047857' }}>
                          {bt.priceDifference.toLocaleString('vi-VN')} đ
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Báo cáo kiểm tra phòng cũ của Bảo vệ (nếu có) */}
                  {bt.handover && (
                    <div style={{ backgroundColor: '#ECFDF5', border: '1px solid #A7F3D0', borderRadius: '8px', padding: '12px', fontSize: '12px', lineHeight: 1.6 }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                        <strong style={{ color: '#047857', fontSize: 13 }}>
                          Biên bản Kiểm tra & Chốt điện nước ({bt.handover.handoverCode})
                        </strong>
                        <span style={{ fontSize: 11, fontWeight: 700, color: '#065F46', background: '#D1FAE5', padding: '2px 8px', borderRadius: 999 }}>
                          Chứng kiến 3 bên: {bt.handover.studentAcceptedFlag ? 'Đã đồng thuận' : 'Chờ xác nhận'}
                        </span>
                      </div>
                      <div><strong>Bảo vệ kiểm tra:</strong> {bt.handover.inspectorName} · <strong>Thời gian:</strong> {bt.handover.completedTime ? formatDateTime(bt.handover.completedTime) : '—'}</div>
                      {bt.handover.inspectionNote && (
                        <div><strong>Ghi chú bàn giao:</strong> {bt.handover.inspectionNote}</div>
                      )}

                      {bt.meterReadings && bt.meterReadings.length > 0 && (
                        <div style={{ marginTop: 8 }}>
                          <strong>Chỉ số Điện/Nước chốt phòng cũ:</strong>
                          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 8, marginTop: 4 }}>
                            {bt.meterReadings.map((mr) => (
                              <div key={mr.id} style={{ background: '#FFFFFF', border: '1px solid #A7F3D0', borderRadius: 6, padding: 8 }}>
                                <div style={{ fontWeight: 700, color: '#0F172A' }}>
                                  {mr.meterType === 'Electricity' ? ' Điện (kWh)' : 'Nước (m³)'}
                                </div>
                                <div>Chỉ số: {mr.previousValue} → <strong>{mr.currentValue}</strong> (Dùng: {mr.consumption})</div>
                                <div>Phí chia {mr.residentCount} SV: <strong style={{ color: '#0369A1' }}>{mr.studentShareCost.toLocaleString('vi-VN')} đ</strong></div>
                                {mr.evidenceImageUrl && (
                                  <div style={{ marginTop: 6 }}>
                                    <button
                                      type="button"
                                      onClick={() =>
                                        setMeterLightbox({
                                          url: mr.evidenceImageUrl!,
                                          title: `Ảnh công tơ ${mr.meterType === 'Electricity' ? 'Điện' : 'Nước'} — P.${bt.fromRoomNumber}`,
                                          subtitle: `Chỉ số: ${mr.previousValue} → ${mr.currentValue}`,
                                        })
                                      }
                                      style={{
                                        padding: '3px 9px',
                                        borderRadius: 6,
                                        border: '1px solid #BFDBFE',
                                        background: '#EFF6FF',
                                        color: '#1D4ED8',
                                        fontSize: 11,
                                        fontWeight: 600,
                                        cursor: 'pointer',
                                      }}
                                    >
                                      Xem ảnh công tơ
                                    </button>
                                  </div>
                                )}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      {bt.handover.items && bt.handover.items.length > 0 && (
                        <div style={{ marginTop: 8 }}>
                          <strong>Thiết bị kiểm tra ({bt.handover.items.length} mục):</strong>
                          <div style={{ marginTop: 4, display: 'flex', flexDirection: 'column', gap: 4 }}>
                            {bt.handover.items.map((item) => (
                              <div key={item.id} style={{ display: 'flex', justifyContent: 'space-between', background: '#FFFFFF', padding: '4px 8px', borderRadius: 4, border: '1px solid #E2E8F0' }}>
                                <span>
                                  <strong>{item.equipmentCode}</strong> - {item.equipmentName}{' '}
                                  <span style={{ color: item.itemCondition === 'Good' ? '#047857' : '#DC2626', fontWeight: 600 }}>
                                    ({item.itemCondition === 'Good' ? 'Bình thường' : item.itemCondition === 'Damaged' ? 'Hư hỏng' : 'Thiếu/Mất'})
                                  </span>
                                  {item.description ? ` · ${item.description}` : ''}
                                </span>
                                <strong style={{ color: item.compensationAmount > 0 ? '#DC2626' : '#64748B' }}>
                                  {item.compensationAmount > 0 ? `${item.compensationAmount.toLocaleString('vi-VN')} đ` : '0 đ'}
                                </strong>
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      <div style={{ marginTop: 10, paddingTop: 8, borderTop: '1px dashed #6EE7B7', fontSize: 13 }}>
                        <div><strong>Chênh lệch phòng (Prorate dương):</strong> {(Math.max(0, bt.priceDifference ?? 0)).toLocaleString('vi-VN')} đ</div>
                        <div><strong>Tiền Điện/Nước chốt phòng cũ:</strong> {(bt.utilityShareAmount ?? 0).toLocaleString('vi-VN')} đ</div>
                        <div><strong>Tổng bồi thường thiết bị:</strong> {(bt.equipmentCompensationAmount ?? bt.handover.totalCompensation).toLocaleString('vi-VN')} đ</div>
                        <div style={{ marginTop: 4, fontSize: 14, color: '#991B1B' }}>
                          <strong>Tổng công nợ cần phát hành hóa đơn: {(bt.totalOutstandingAmount ?? 0).toLocaleString('vi-VN')} đ</strong>
                        </div>
                        {bt.refundDueAmount != null && bt.refundDueAmount > 0 && (
                          <div style={{ color: '#047857', fontWeight: 600 }}>
                            Chênh lệch giảm phí phòng (ghi nhận hoàn/cấn trừ): {bt.refundDueAmount.toLocaleString('vi-VN')} đ
                          </div>
                        )}
                      </div>
                    </div>
                  )}

                  {/* Hóa đơn đã phát hành (sau khi hoàn tất Bước 2) */}
                  {(isCompleted || (bt.invoices && bt.invoices.length > 0)) && bt.invoices && bt.invoices.length > 0 && (
                    <div style={{ backgroundColor: '#F8FAFC', border: '1px solid #CBD5E1', borderRadius: '8px', padding: '10px 12px', fontSize: 12 }}>
                      <strong style={{ display: 'block', marginBottom: 4, color: '#0F172A' }}>Hóa đơn đã phát hành:</strong>
                      {bt.invoices.map((inv) => (
                        <div key={inv.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '3px 0' }}>
                          <span><strong>{inv.invoiceNumber}</strong> ({inv.invoiceType}) — Hạn: {inv.dueDate || '—'}</span>
                          <span><strong>{inv.totalAmount.toLocaleString('vi-VN')} đ</strong> ({inv.status})</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {isApprovedWaitOrStep2 && !bt.handover && (
                    <div style={{ backgroundColor: '#FEF3C7', border: '1px solid #FDE68A', borderRadius: '8px', padding: '10px 12px', fontSize: 13, color: '#92400E' }}>
                      Đơn đã qua Bước 1. Đang chờ Bảo vệ <strong>{bt.assignedGuardName || 'được phân công'}</strong> kiểm tra thiết bị phòng cũ và chốt chỉ số điện nước trước khi sang Bước 2.
                    </div>
                  )}
                </div>
              );
            })()}

            {/* Chi tiết đơn Trả phòng (Checkout) */}
            {selectedRequest.checkout && (() => {
              const co = selectedRequest.checkout;
              const isApprovedWaitOrStep2 = selectedRequest.status === 'Approved';
              return (
                <div style={{ marginBottom: 16, display: 'flex', flexDirection: 'column', gap: 10 }}>
                  <div style={{ backgroundColor: '#FFF7ED', border: '1px solid #FED7AA', borderRadius: '8px', padding: '12px', fontSize: 13, lineHeight: 1.6 }}>
                    <div style={{ fontWeight: 700, color: '#9A3412', marginBottom: 4 }}>
                      Thông tin Trả phòng KTX (Check-out)
                    </div>
                    <div>
                      <strong>Phòng / Giường trả:</strong> {co.dormName} · {co.blockName} · P.{co.roomNumber} · Giường <strong>{co.bedCode}</strong>
                    </div>
                    <div>
                      <strong>Ngày dự kiến trả phòng:</strong> {co.expectedCheckOutDate}
                      {co.actualCheckOutDate && <> · <strong>Ngày trả thực tế:</strong> {co.actualCheckOutDate}</>}
                    </div>
                    {co.assignedGuardName && (
                      <div>
                        <strong>Bảo vệ phụ trách:</strong> {co.assignedGuardName} ({co.assignedGuardCode || 'Guard'})
                      </div>
                    )}
                  </div>

                  {co.handover && (
                    <div style={{ backgroundColor: '#ECFDF5', border: '1px solid #A7F3D0', borderRadius: '8px', padding: '12px', fontSize: 12.5, lineHeight: 1.6 }}>
                      <div style={{ fontWeight: 700, color: '#047857', marginBottom: 6 }}>
                        Biên bản Kiểm tra trả phòng của Bảo vệ ({co.handover.handoverCode})
                      </div>
                      <div>
                        <strong>Người lập:</strong> {co.handover.inspectorName} · <strong>Đồ cá nhân:</strong> Đã xác nhận dọn sạch ·{' '}
                        <strong>Thời gian:</strong> {formatDateTime(co.handover.completedTime || '')}
                      </div>
                      {co.meterReadings && co.meterReadings.length > 0 && (
                        <div style={{ marginTop: 6 }}>
                          <strong>Chỉ số Điện &amp; Nước chốt trả phòng:</strong>
                          {co.meterReadings.map((mr) => (
                            <div key={mr.id} style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', marginTop: 3 }}>
                              <span>
                                • {mr.meterType === 'Electricity' ? 'Điện' : 'Nước'}: {mr.previousValue} → <strong>{mr.currentValue}</strong> (Dùng {mr.consumption}, chia {mr.residentCount} SV:{' '}
                                <strong>{mr.studentShareCost.toLocaleString('vi-VN')} đ</strong>)
                              </span>
                              {mr.evidenceImageUrl && (
                                <button
                                  type="button"
                                  onClick={() =>
                                    setMeterLightbox({
                                      url: mr.evidenceImageUrl!,
                                      title: `Ảnh công tơ ${mr.meterType === 'Electricity' ? 'Điện' : 'Nước'} — P.${co.roomNumber}`,
                                      subtitle: `Chỉ số: ${mr.previousValue} → ${mr.currentValue}`,
                                    })
                                  }
                                  style={{
                                    padding: '2px 8px',
                                    borderRadius: 6,
                                    border: '1px solid #BFDBFE',
                                    background: '#EFF6FF',
                                    color: '#1D4ED8',
                                    fontSize: 11,
                                    fontWeight: 600,
                                    cursor: 'pointer',
                                  }}
                                >
                                  Xem ảnh công tơ
                                </button>
                              )}
                            </div>
                          ))}
                        </div>
                      )}
                      <div style={{ marginTop: 6 }}>
                        <strong>Bồi thường thiết bị:</strong> {(co.equipmentCompensationAmount ?? co.handover.totalCompensation).toLocaleString('vi-VN')} đ
                      </div>
                      {co.handover.items.filter(i => i.itemCondition !== 'Good').length > 0 && (
                        <div style={{ marginTop: 4, paddingLeft: 10 }}>
                          {co.handover.items
                            .filter(i => i.itemCondition !== 'Good')
                            .map(i => (
                              <div key={i.id}>
                                • {i.equipmentName} ({i.equipmentCode}): <strong>{i.itemCondition}</strong> — {i.compensationAmount.toLocaleString('vi-VN')} đ {i.description ? `(${i.description})` : ''}
                              </div>
                            ))}
                        </div>
                      )}
                      {co.totalOutstandingAmount != null && (
                        <div style={{ marginTop: 6, color: '#991B1B', fontWeight: 700 }}>
                          Tổng công nợ phát sinh trả phòng (điện/nước + bồi thường): {co.totalOutstandingAmount.toLocaleString('vi-VN')} đ
                        </div>
                      )}
                    </div>
                  )}

                  {co.invoices && co.invoices.length > 0 && (
                    <div style={{ backgroundColor: '#F8FAFC', border: '1px solid #CBD5E1', borderRadius: '8px', padding: '10px 12px', fontSize: 12.5 }}>
                      <div style={{ fontWeight: 700, color: '#0F172A', marginBottom: 4 }}>
                        Hóa đơn công nợ đã phát hành ({co.invoices.length}):
                      </div>
                      {co.invoices.map(inv => (
                        <div key={inv.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '4px 0', borderBottom: '1px dashed #E2E8F0' }}>
                          <span><strong>{inv.invoiceNumber}</strong> ({inv.invoiceType}) — Hạn: {inv.dueDate || '—'}</span>
                          <span><strong>{inv.totalAmount.toLocaleString('vi-VN')} đ</strong> ({inv.status})</span>
                        </div>
                      ))}
                    </div>
                  )}

                  {isApprovedWaitOrStep2 && !co.handover && (
                    <div style={{ backgroundColor: '#FEF3C7', border: '1px solid #FDE68A', borderRadius: '8px', padding: '10px 12px', fontSize: 13, color: '#92400E' }}>
                      Đơn đã qua Bước 1. Đang chờ Bảo vệ <strong>{co.assignedGuardName || 'được phân công'}</strong> kiểm tra dọn đồ cá nhân, thiết bị phòng và chốt chỉ số điện nước trước khi sang Bước 2.
                    </div>
                  )}
                </div>
              );
            })()}

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
                  <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer', fontWeight: 600, color: '#047857' }}>
                      <input
                        type="radio"
                        name="decision"
                        value="Approved"
                        checked={decision === 'Approved'}
                        onChange={() => setDecision('Approved')}
                      />
                      {selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout'
                        ? 'Chấp thuận Bước 1 & Phân công Bảo vệ (Approved)'
                        : 'Chấp thuận đơn khiếu nại (Approved)'}
                    </label>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer', fontWeight: 600, color: '#DC2626' }}>
                      <input
                        type="radio"
                        name="decision"
                        value="Rejected"
                        checked={decision === 'Rejected'}
                        onChange={() => setDecision('Rejected')}
                      />
                      {selectedRequest.requestType === 'BedTransfer'
                        ? 'Từ chối yêu cầu chuyển giường (Rejected)'
                        : selectedRequest.requestType === 'Checkout'
                          ? 'Từ chối yêu cầu trả phòng (Rejected)'
                          : 'Từ chối đơn khiếu nại (Rejected)'}
                    </label>
                  </div>
                </div>

                {/* Form Bước 1 dành riêng cho BedTransfer & Checkout */}
                {(selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') && decision === 'Approved' && (
                  <div style={{ backgroundColor: '#F8FAFC', border: '1px solid #CBD5E1', borderRadius: 8, padding: 12, marginBottom: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                        {selectedRequest.requestType === 'Checkout'
                          ? 'Bảo vệ kiểm tra trả phòng (Cố định theo tòa KTX hiện tại)'
                          : 'Bảo vệ kiểm tra phòng cũ (Cố định theo tòa KTX cũ)'}
                      </label>
                      {loadingGuards ? (
                        <div style={{ fontSize: 12, color: '#64748B' }}>Đang tải thông tin Bảo vệ phụ trách tòa KTX...</div>
                      ) : guards.length > 0 ? (
                        <div
                          style={{
                            width: '100%',
                            padding: '9px 12px',
                            borderRadius: '6px',
                            border: '1px solid #CBD5E1',
                            fontSize: '13px',
                            backgroundColor: '#F1F5F9',
                            color: '#0F172A',
                            boxSizing: 'border-box',
                          }}
                        >
                          <div style={{ fontWeight: 700 }}>
                            {guards[0].fullName} ({guards[0].guardCode}) — {guards[0].assignedDormName}
                          </div>
                          <div style={{ fontSize: 11.5, color: '#475569', marginTop: 2 }}>
                            Tự động phân công Bảo vệ phụ trách tòa KTX phòng hiện tại của sinh viên (không thay đổi sang tòa khác).
                          </div>
                        </div>
                      ) : (
                        <div
                          style={{
                            padding: '9px 12px',
                            borderRadius: '6px',
                            border: '1px solid #FCA5A5',
                            backgroundColor: '#FEF2F2',
                            color: '#B91C1C',
                            fontSize: 12.5,
                            fontWeight: 600,
                          }}
                        >
                          Tòa KTX ({selectedRequest.bedTransfer?.fromDormName || selectedRequest.checkout?.dormName || 'chưa rõ'}) hiện chưa có Bảo vệ đang hoạt động để phân công.
                        </div>
                      )}
                    </div>

                    <div>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                        {selectedRequest.requestType === 'Checkout'
                          ? 'Ngày dự kiến trả phòng (Xác nhận khi duyệt Bước 1)'
                          : 'Ngày chuyển giường dự kiến (Chốt để tính Prorate)'}
                      </label>
                      <TransferDatePicker
                        value={transferDateInput}
                        fullWidth
                        openDirection="up"
                        onChange={setTransferDateInput}
                      />
                    </div>
                  </div>
                )}

                {selectedRequest.requestType === 'BedTransfer' && decision === 'Rejected' && (
                  <div style={{ marginBottom: 14, padding: '10px 14px', backgroundColor: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '13px' }}>
                    <strong>Lưu ý:</strong> Khi từ chối đơn chuyển giường, giường đích đang tạm giữ (<em>Pending Transfer</em>) sẽ tự động được giải phóng về trạng thái trống (<strong>Free</strong>).
                  </div>
                )}

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
                    {(selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') && decision === 'Approved'
                      ? 'Ghi chú dặn dò Bảo vệ / Sinh viên (tùy chọn)'
                      : <>Ghi chú / Lý do phản hồi tới sinh viên <span style={{ color: '#DC2626' }}>*</span></>}
                  </label>
                  <textarea
                    rows={3}
                    value={resolutionNote}
                    onChange={e => setResolutionNote(e.target.value)}
                    placeholder="Ghi rõ lý do chấp thuận hoặc căn cứ từ chối để gửi email và hiển thị trên giao diện của sinh viên..."
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', boxSizing: 'border-box' }}
                    required={!((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') && decision === 'Approved')}
                  />
                  <div style={{ fontSize: '12px', color: '#64748B', marginTop: 4 }}>
                    {selectedRequest.requestType === 'BedTransfer'
                      ? 'Hệ thống sẽ gửi thông báo & email cập nhật tiến trình xử lý chuyển giường tới sinh viên.'
                      : selectedRequest.requestType === 'Checkout'
                        ? 'Hệ thống sẽ gửi thông báo & email cập nhật tiến trình xử lý trả phòng tới sinh viên.'
                        : 'Hệ thống sẽ tự động cập nhật biên bản vi phạm và gửi email thông báo kết quả tới sinh viên.'}
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
                    disabled={
                      submitting ||
                      ((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') &&
                        decision === 'Approved' &&
                        (loadingGuards || !assignedGuardId))
                    }
                    style={{
                      padding: '8px 18px',
                      background: decision === 'Approved' ? '#047857' : '#DC2626',
                      color: '#FFF',
                      border: 'none',
                      borderRadius: '6px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor:
                        submitting ||
                        ((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') &&
                          decision === 'Approved' &&
                          (loadingGuards || !assignedGuardId))
                          ? 'not-allowed'
                          : 'pointer',
                      opacity:
                        submitting ||
                        ((selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout') &&
                          decision === 'Approved' &&
                          (loadingGuards || !assignedGuardId))
                          ? 0.6
                          : 1,
                    }}
                  >
                    {submitting
                      ? 'Đang lưu...'
                      : decision === 'Approved'
                        ? (selectedRequest.requestType === 'BedTransfer' || selectedRequest.requestType === 'Checkout')
                          ? 'Duyệt Bước 1 & Phân công Bảo vệ'
                          : 'Xác nhận chấp thuận'
                        : 'Xác nhận từ chối'}
                  </button>
                </div>
              </form>
            ) : selectedRequest.requestType === 'Checkout' && selectedRequest.status === 'Approved' ? (
              <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: 16 }}>
                {selectedRequest.checkout?.handover ? (
                  <>
                    <div style={{ marginBottom: 14 }}>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                        Ghi chú hoàn tất trả phòng &amp; phát hành hóa đơn (tùy chọn)
                      </label>
                      <textarea
                        rows={2}
                        value={resolutionNote}
                        onChange={(e) => setResolutionNote(e.target.value)}
                        placeholder="Ghi chú xác nhận trả phòng hoặc nhắc thanh toán hóa đơn công nợ..."
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', boxSizing: 'border-box' }}
                      />
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
                      <button
                        type="button"
                        onClick={handleCloseModal}
                        style={{ padding: '8px 16px', background: '#F1F5F9', border: '1px solid #CBD5E1', borderRadius: '6px', fontSize: '13px', cursor: 'pointer' }}
                      >
                        Đóng
                      </button>
                      <button
                        type="button"
                        disabled={submitting}
                        onClick={handleCompleteCheckout}
                        style={{
                          padding: '9px 18px',
                          background: '#047857',
                          color: '#FFF',
                          border: 'none',
                          borderRadius: '6px',
                          fontSize: '13px',
                          fontWeight: 700,
                          cursor: submitting ? 'not-allowed' : 'pointer',
                        }}
                      >
                        {submitting ? 'Đang hoàn tất trả phòng...' : 'Hoàn tất Trả phòng & Xuất hóa đơn (Complete)'}
                      </button>
                    </div>
                  </>
                ) : (
                  <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <button
                      type="button"
                      onClick={handleCloseModal}
                      style={{ padding: '8px 16px', background: '#EA580C', color: '#FFF', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}
                    >
                      Đóng
                    </button>
                  </div>
                )}
              </div>
            ) : selectedRequest.requestType === 'BedTransfer' && selectedRequest.status === 'Approved' ? (
              <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: 16 }}>
                {selectedRequest.bedTransfer?.handover ? (
                  <>
                    <div style={{ marginBottom: 14 }}>
                      <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                        Ghi chú hoàn tất chuyển giường & phát hành hóa đơn (tùy chọn)
                      </label>
                      <textarea
                        rows={2}
                        value={resolutionNote}
                        onChange={(e) => setResolutionNote(e.target.value)}
                        placeholder="Ghi chú hướng dẫn nhận giường mới hoặc thanh toán hóa đơn công nợ..."
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', boxSizing: 'border-box' }}
                      />
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
                      <button
                        type="button"
                        onClick={handleCloseModal}
                        style={{ padding: '8px 16px', background: '#F1F5F9', border: '1px solid #CBD5E1', borderRadius: '6px', fontSize: '13px', cursor: 'pointer' }}
                      >
                        Đóng
                      </button>
                      <button
                        type="button"
                        disabled={submitting}
                        onClick={handleCompleteBedTransfer}
                        style={{
                          padding: '9px 18px',
                          background: '#047857',
                          color: '#FFF',
                          border: 'none',
                          borderRadius: '6px',
                          fontSize: '13px',
                          fontWeight: 700,
                          cursor: submitting ? 'not-allowed' : 'pointer',
                        }}
                      >
                        {submitting ? 'Đang phát hành & chuyển giường...' : 'Phát hành Hóa đơn & Xác nhận chuyển giường (Complete)'}
                      </button>
                    </div>
                  </>
                ) : (
                  <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <button
                      type="button"
                      onClick={handleCloseModal}
                      style={{ padding: '8px 16px', background: '#EA580C', color: '#FFF', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}
                    >
                      Đóng
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: 14 }}>
                {/* Hiển thị chi tiết kết quả sau khi xử lý đơn */}
                {(() => {
                  const isSuccessState = selectedRequest.status === 'Approved' || selectedRequest.status === 'Completed';
                  const docNoun = selectedRequest.requestType === 'BedTransfer'
                    ? 'Đơn chuyển giường'
                    : selectedRequest.requestType === 'Checkout'
                      ? 'Đơn trả phòng'
                      : 'Đơn khiếu nại';
                  return (
                    <div style={{ backgroundColor: isSuccessState ? '#ECFDF5' : '#FEF2F2', border: `1px solid ${isSuccessState ? '#A7F3D0' : '#FECACA'}`, borderRadius: '8px', padding: '14px', marginBottom: 14 }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: 8 }}>
                        <span style={{ fontSize: '13px', fontWeight: 700, color: isSuccessState ? '#047857' : '#991B1B' }}>
                          {selectedRequest.status === 'Completed'
                            ? `Kết quả: ${docNoun} ĐÃ HOÀN TẤT XỬ LÝ`
                            : selectedRequest.status === 'Approved'
                              ? `Kết quả: ${docNoun} ĐÃ ĐƯỢC CHẤP THUẬN`
                              : `Kết quả: ${docNoun} ĐÃ BỊ TỪ CHỐI`}
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
                        <div style={{ marginTop: 8, paddingTop: 8, borderTop: `1px dashed ${isSuccessState ? '#A7F3D0' : '#FECACA'}` }}>
                          <strong style={{ fontSize: '12px', color: isSuccessState ? '#065F46' : '#991B1B' }}>
                            Ghi chú / Phản hồi của Ban Quản lý:
                          </strong>
                          <div style={{ fontSize: '13px', color: isSuccessState ? '#064E3B' : '#7F1D1D', marginTop: 4, whiteSpace: 'pre-line' }}>
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
                  );
                })()}

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

      <ImageLightboxModal
        imageUrl={meterLightbox?.url ?? null}
        title={meterLightbox?.title}
        subtitle={meterLightbox?.subtitle}
        onClose={() => setMeterLightbox(null)}
      />
    </div>
  );
};
