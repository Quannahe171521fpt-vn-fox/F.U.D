import React, { useState, useEffect } from 'react';
import { bookingApi, type BookingRequestManagerDto } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { useIdleAwarePolling } from '../../utils/useIdleAwarePolling';
import '../../styles/booking.css';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

export const ManagerBookingManagement: React.FC = () => {
  const [requests, setRequests] = useState<BookingRequestManagerDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState<string>('');

  // Filters
  const [filterStatus, setFilterStatus] = useState<string>('Pending');
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [currentPage, setCurrentPage] = useState<number>(1);

  // Modal actions
  const [processingId, setProcessingId] = useState<number | null>(null);
  const [rejectModalTarget, setRejectModalTarget] = useState<BookingRequestManagerDto | null>(null);
  const [rejectReason, setRejectReason] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  const loadRequests = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await bookingApi.getManagerRequests(filterStatus);
      setRequests(data);
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải danh sách đơn đặt phòng');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  useEffect(() => {
    loadRequests(true);
  }, [filterStatus]);

  useIdleAwarePolling(() => {
    void loadRequests(false);
  }, 5000);

  const handleApprove = async (id: number) => {
    if (!window.confirm('Bạn có chắc chắn muốn PHÊ DUYỆT đơn đặt phòng này? Hệ thống sẽ tạo hồ sơ lưu trú cho sinh viên.')) {
      return;
    }

    setProcessingId(id);
    setErrorMsg('');
    setSuccessMsg('');
    try {
      await bookingApi.processBooking(id, 'Approved');
      setSuccessMsg('Đã phê duyệt đơn đặt phòng thành công!');
      await loadRequests(false);
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi phê duyệt đơn đặt phòng');
    } finally {
      setProcessingId(null);
    }
  };

  const handleOpenRejectModal = (item: BookingRequestManagerDto) => {
    setRejectModalTarget(item);
    setRejectReason('');
  };

  const handleConfirmReject = async () => {
    if (!rejectModalTarget) return;

    if (!rejectReason.trim()) {
      alert('Vui lòng nhập lý do từ chối đơn đặt phòng.');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');
    try {
      await bookingApi.processBooking(rejectModalTarget.id, 'Rejected', rejectReason.trim());
      setSuccessMsg(`Đã từ chối đơn đặt phòng #${rejectModalTarget.id}!`);
      setRejectModalTarget(null);
      await loadRequests(false);
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi từ chối đơn đặt phòng');
    } finally {
      setIsSubmitting(false);
    }
  };

  // Filter & Search
  const filtered = requests.filter((r) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      r.studentCode.toLowerCase().includes(term) ||
      r.studentName.toLowerCase().includes(term) ||
      r.roomNumber.toLowerCase().includes(term) ||
      r.bedCode.toLowerCase().includes(term) ||
      r.dormName.toLowerCase().includes(term) ||
      r.blockName.toLowerCase().includes(term) ||
      r.id.toString().includes(term)
    );
  });

  const paginated = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Xét duyệt Đặt phòng KTX (Booking Approval)
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Tiếp nhận, thẩm định và phê duyệt các đơn đăng ký lưu trú mới của sinh viên
        </p>
      </div>

      {successMsg && (
        <div style={{ background: '#DCFCE7', border: '1px solid #86EFAC', borderRadius: 8, padding: '12px 16px', color: '#166534', marginBottom: 16, fontSize: 13, fontWeight: 600 }}>
          {successMsg}
        </div>
      )}

      {errorMsg && (
        <div style={{ background: '#FEF2F2', border: '1px solid #F87171', borderRadius: 8, padding: '12px 16px', color: '#991B1B', marginBottom: 16, fontSize: 13 }}>
          {errorMsg}
        </div>
      )}

      {/* Filter Bar */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <select
            value={filterStatus}
            onChange={(e) => {
              setFilterStatus(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              padding: '8px 14px',
              borderRadius: 8,
              border: '1px solid #CBD5E1',
              fontSize: 13,
              fontWeight: 600,
              color: '#1E293B',
              backgroundColor: '#FFFFFF',
            }}
          >
            <option value="All">Tất cả trạng thái</option>
            <option value="Pending">Đang chờ duyệt (Pending)</option>
            <option value="Approved">Đã phê duyệt (Approved)</option>
            <option value="Rejected">Đã từ chối (Rejected)</option>
          </select>

          <input
            type="text"
            placeholder="Tìm theo MSSV, Tên sinh viên, Phòng, Giường..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              padding: '8px 14px',
              borderRadius: 8,
              border: '1px solid #CBD5E1',
              fontSize: 13,
              width: 320,
              backgroundColor: '#FFFFFF',
            }}
          />
        </div>

        <button
          type="button"
          onClick={() => loadRequests(true)}
          style={{
            padding: '8px 14px',
            borderRadius: 8,
            border: '1px solid #CBD5E1',
            background: '#FFFFFF',
            fontSize: 13,
            fontWeight: 600,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 6,
          }}
        >
          <span>🔄</span> Làm mới
        </button>
      </div>

      {/* Main Table */}
      <div style={{ background: '#FFFFFF', borderRadius: 12, border: '1px solid #E2E8F0', overflow: 'hidden', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
        {loading && requests.length === 0 ? (
          <div style={{ padding: '40px', textAlign: 'center', color: '#64748B' }}>Đang nạp dữ liệu...</div>
        ) : filtered.length === 0 ? (
          <div style={{ padding: '40px', textAlign: 'center', color: '#64748B' }}>Không có đơn đặt phòng nào phù hợp bộ lọc.</div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="manager-booking-table">
              <thead>
                <tr>
                  <th style={{ width: 60 }}>Mã đơn</th>
                  <th>Sinh viên</th>
                  <th>Vị trí đăng ký</th>
                  <th>Học kỳ</th>
                  <th>Đơn giá</th>
                  <th>Thời gian gửi</th>
                  <th>Trạng thái</th>
                  <th style={{ textAlign: 'center', width: 170 }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {paginated.map((item) => (
                  <tr key={item.id}>
                    <td style={{ fontWeight: 700, color: '#0F172A' }}>#{item.id}</td>
                    <td>
                      <div style={{ fontWeight: 700, color: '#0F172A' }}>{item.studentName}</div>
                      <div style={{ fontSize: 11, color: '#64748B' }}>
                        MSSV: {item.studentCode} · Giới tính: {item.studentGender === 'Female' ? 'Nữ' : 'Nam'}
                      </div>
                    </td>
                    <td>
                      <div style={{ fontWeight: 600 }}>{item.dormName} — {item.blockName}</div>
                      <div style={{ fontSize: 12, color: '#EA580C', fontWeight: 600 }}>
                        Phòng {item.roomNumber} · Giường {item.bedCode}
                      </div>
                    </td>
                    <td>{item.termName}</td>
                    <td style={{ fontWeight: 700, color: '#0F172A' }}>
                      {item.termPrice ? `${item.termPrice.toLocaleString('vi-VN')} đ` : '—'}
                    </td>
                    <td style={{ fontSize: 12, color: '#64748B' }}>
                      {new Date(item.createdTime).toLocaleString('vi-VN')}
                    </td>
                    <td>
                      <span
                        className={`booking-status-badge ${
                          item.status === 'Approved' ? 'approved' : item.status === 'Rejected' ? 'rejected' : 'pending'
                        }`}
                      >
                        {item.status === 'Approved' ? 'Đã duyệt' : item.status === 'Rejected' ? 'Từ chối' : 'Chờ duyệt'}
                      </span>
                      {item.processedByName && (
                        <div style={{ fontSize: 11, color: '#64748B', marginTop: 3 }}>
                          Bởi: {item.processedByName}
                        </div>
                      )}
                      {item.rejectReason && (
                        <div style={{ fontSize: 11, color: '#DC2626', marginTop: 2, maxWidth: 180 }}>
                          Lý do: {item.rejectReason}
                        </div>
                      )}
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      {item.status === 'Pending' ? (
                        <div style={{ display: 'flex', gap: 6, justifyContent: 'center' }}>
                          <button
                            type="button"
                            disabled={processingId === item.id}
                            onClick={() => handleApprove(item.id)}
                            style={{
                              padding: '6px 12px',
                              background: '#16A34A',
                              color: '#FFFFFF',
                              border: 'none',
                              borderRadius: 6,
                              fontSize: 12,
                              fontWeight: 700,
                              cursor: 'pointer',
                            }}
                          >
                            Duyệt
                          </button>
                          <button
                            type="button"
                            disabled={processingId === item.id}
                            onClick={() => handleOpenRejectModal(item)}
                            style={{
                              padding: '6px 12px',
                              background: '#EF4444',
                              color: '#FFFFFF',
                              border: 'none',
                              borderRadius: 6,
                              fontSize: 12,
                              fontWeight: 700,
                              cursor: 'pointer',
                            }}
                          >
                            Từ chối
                          </button>
                        </div>
                      ) : (
                        <span style={{ fontSize: 12, color: '#94A3B8' }}>Đã hoàn tất</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {filtered.length > PAGE_SIZE && (
          <div style={{ padding: '16px 20px', borderTop: '1px solid #E2E8F0', display: 'flex', justifyContent: 'center' }}>
            <Pagination
              currentPage={currentPage}
              totalItems={filtered.length}
              pageSize={PAGE_SIZE}
              onPageChange={(page) => setCurrentPage(page)}
              itemName="đơn đặt phòng"
            />
          </div>
        )}
      </div>

      {/* Reject Modal */}
      {rejectModalTarget && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(0, 0, 0, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: 20,
          }}
        >
          <div
            style={{
              background: '#FFFFFF',
              borderRadius: 12,
              padding: 24,
              width: '100%',
              maxWidth: 480,
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <h3 style={{ margin: '0 0 12px', fontSize: 18, fontWeight: 700, color: '#0F172A' }}>
              Từ chối đơn đặt phòng #{rejectModalTarget.id}
            </h3>
            <p style={{ fontSize: 13, color: '#64748B', margin: '0 0 16px', lineHeight: 1.5 }}>
              Sinh viên: <strong>{rejectModalTarget.studentName}</strong> ({rejectModalTarget.studentCode})
              <br />
              Phòng {rejectModalTarget.roomNumber} · Giường {rejectModalTarget.bedCode} ({rejectModalTarget.dormName})
            </p>

            <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#334155', marginBottom: 6 }}>
              Lý do từ chối: <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <textarea
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder="Nhập lý do không chấp thuận (vd: Chưa đủ điều kiện, thông tin không hợp lệ...)"
              style={{
                width: '100%',
                padding: '10px 12px',
                borderRadius: 8,
                border: '1px solid #CBD5E1',
                fontSize: 13,
                boxSizing: 'border-box',
                resize: 'vertical',
                marginBottom: 20,
              }}
            />

            <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
              <button
                type="button"
                disabled={isSubmitting}
                onClick={() => setRejectModalTarget(null)}
                style={{
                  padding: '8px 16px',
                  background: '#F1F5F9',
                  border: '1px solid #CBD5E1',
                  borderRadius: 6,
                  fontSize: 13,
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isSubmitting || !rejectReason.trim()}
                onClick={handleConfirmReject}
                style={{
                  padding: '8px 16px',
                  background: '#EF4444',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: 6,
                  fontSize: 13,
                  fontWeight: 700,
                  cursor: 'pointer',
                }}
              >
                {isSubmitting ? 'Đang gửi...' : 'Xác nhận từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
