import React, { useState, useEffect, useCallback } from 'react';
import { keepingManagerApi, type KeepingRequestManagerDto } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { useIdleAwarePolling } from '../../utils/useIdleAwarePolling';
import { formatDate } from '../../utils/dateUtils';
import '../../styles/booking.css';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

const formatVnd = (amount: number) => `${Number(amount || 0).toLocaleString('vi-VN')} đ`;

export const ManagerKeepingManagement: React.FC = () => {
  const [requests, setRequests] = useState<KeepingRequestManagerDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState<string>('');

  const [searchTerm, setSearchTerm] = useState<string>('');
  const [currentPage, setCurrentPage] = useState<number>(1);

  const [processingId, setProcessingId] = useState<number | null>(null);
  const [rejectModalTarget, setRejectModalTarget] = useState<KeepingRequestManagerDto | null>(null);
  const [rejectReason, setRejectReason] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);

  const loadRequests = useCallback(async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await keepingManagerApi.getPending();
      setRequests(data || []);
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải danh sách yêu cầu giữ phòng');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadRequests(true);
  }, [loadRequests]);

  useIdleAwarePolling(() => {
    void loadRequests(false);
  }, 5000);

  const handleApprove = async (item: KeepingRequestManagerDto) => {
    if (
      !window.confirm(
        `Bạn có chắc chắn muốn PHÊ DUYỆT yêu cầu giữ phòng #${item.requestId}? ` +
          'Hệ thống sẽ tạo hồ sơ cư trú cho học kỳ kế tiếp của sinh viên.'
      )
    ) {
      return;
    }

    setProcessingId(item.requestId);
    setErrorMsg('');
    setSuccessMsg('');
    try {
      await keepingManagerApi.approve(item.requestId);
      setSuccessMsg(`Đã phê duyệt yêu cầu giữ phòng #${item.requestId} thành công!`);
      await loadRequests(false);
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi phê duyệt yêu cầu giữ phòng');
    } finally {
      setProcessingId(null);
    }
  };

  const handleOpenRejectModal = (item: KeepingRequestManagerDto) => {
    setRejectModalTarget(item);
    setRejectReason('');
  };

  const handleConfirmReject = async () => {
    if (!rejectModalTarget) return;

    if (!rejectReason.trim()) {
      alert('Vui lòng nhập lý do từ chối yêu cầu giữ phòng.');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');
    try {
      await keepingManagerApi.reject(rejectModalTarget.requestId, rejectReason.trim());
      setSuccessMsg(`Đã từ chối yêu cầu giữ phòng #${rejectModalTarget.requestId}!`);
      setRejectModalTarget(null);
      await loadRequests(false);
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi từ chối yêu cầu giữ phòng');
    } finally {
      setIsSubmitting(false);
    }
  };

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
      r.requestId.toString().includes(term)
    );
  });

  const paginated = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: 24, fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Duyệt yêu cầu giữ phòng
        </h1>
        <p style={{ color: '#64748B', fontSize: 13, margin: 0 }}>
          Danh sách yêu cầu giữ phòng đã được sinh viên thanh toán phí và đang chờ Ban Quản lý xét duyệt
        </p>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

      {/* Thanh tìm kiếm */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 20, flexWrap: 'wrap', alignItems: 'center', justifyContent: 'space-between' }}>
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
            minWidth: 320,
          }}
        />
        <button
          type="button"
          onClick={() => void loadRequests(true)}
          disabled={loading}
          style={{
            padding: '8px 16px',
            borderRadius: 8,
            border: '1px solid #CBD5E1',
            background: '#FFFFFF',
            fontSize: 13,
            fontWeight: 600,
            cursor: loading ? 'not-allowed' : 'pointer',
          }}
        >
          {loading ? 'Đang tải...' : '🔄 Làm mới'}
        </button>
      </div>

      {/* Bảng danh sách */}
      <div className="fud-table-wrapper" style={{ marginBottom: 20 }}>
        <table className="fud-table">
          <thead>
            <tr>
              <th>Mã yêu cầu</th>
              <th>Sinh viên</th>
              <th>Chỗ ỉ</th>
              <th>Học kỳ</th>
              <th style={{ textAlign: 'right' }}>Phí giữ phòng</th>
              <th>Hóa đơn</th>
              <th style={{ textAlign: 'center' }}>Thời điểm</th>
              <th style={{ textAlign: 'center' }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: 36, color: '#64748B' }}>
                  Đang tải danh sách yêu cầu giữ phòng...
                </td>
              </tr>
            ) : paginated.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: 42, color: '#64748B' }}>
                  <div style={{ fontWeight: 700, fontSize: 15, color: '#0F172A', marginBottom: 4 }}>
                    Không có yêu cầu giữ phòng nào chờ duyệt
                  </div>
                  <div style={{ fontSize: 13 }}>
                    Các yêu cầu sẽ xuất hiện ở đây sau khi sinh viên hoàn tất thanh toán phí giữ phòng.
                  </div>
                </td>
              </tr>
            ) : (
              paginated.map((item) => (
                <tr key={item.requestId}>
                  <td style={{ fontWeight: 700, color: '#0F172A' }}>#{item.requestId}</td>
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
                  <td style={{ textAlign: 'right', fontWeight: 700, color: '#0F172A' }}>
                    {formatVnd(item.feeAmount)}
                  </td>
                  <td>
                    {item.invoiceNumber ? (
                      <>
                        <div style={{ fontWeight: 700, color: '#C2410C' }}>{item.invoiceNumber}</div>
                        <span
                          style={{
                            display: 'inline-block',
                            marginTop: 3,
                            padding: '2px 8px',
                            borderRadius: 9999,
                            fontSize: 11,
                            fontWeight: 700,
                            background: item.isPaid ? '#DCFCE7' : '#FEF3C7',
                            color: item.isPaid ? '#15803D' : '#92400E',
                          }}
                        >
                          {item.isPaid ? 'Đã thanh toán' : item.invoiceStatus || 'Chưa thanh toán'}
                        </span>
                      </>
                    ) : (
                      <span style={{ fontSize: 12, color: '#94A3B8' }}>—</span>
                    )}
                  </td>
                  <td style={{ fontSize: 12, color: '#64748B', textAlign: 'center' }}>
                    {formatDate(item.createdTime)}
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <div style={{ display: 'flex', gap: 6, justifyContent: 'center' }}>
                      <button
                        type="button"
                        disabled={processingId === item.requestId || !item.isPaid}
                        onClick={() => void handleApprove(item)}
                        title={!item.isPaid ? 'Sinh viên chưa thanh toán phí giữ phòng' : 'Phê duyệt'}
                        style={{
                          padding: '6px 12px',
                          background: !item.isPaid ? '#CBD5E1' : '#16A34A',
                          color: '#FFFFFF',
                          border: 'none',
                          borderRadius: 6,
                          fontSize: 12,
                          fontWeight: 700,
                          cursor: !item.isPaid ? 'not-allowed' : 'pointer',
                        }}
                      >
                        ✓ Duyệt
                      </button>
                      <button
                        type="button"
                        disabled={processingId === item.requestId}
                        onClick={() => handleOpenRejectModal(item)}
                        style={{
                          padding: '6px 12px',
                          background: '#DC2626',
                          color: '#FFFFFF',
                          border: 'none',
                          borderRadius: 6,
                          fontSize: 12,
                          fontWeight: 700,
                          cursor: 'pointer',
                        }}
                      >
                        ✕ Từ chối
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {filtered.length > 0 && (
        <Pagination
          currentPage={currentPage}
          totalItems={filtered.length}
          pageSize={PAGE_SIZE}
          onPageChange={setCurrentPage}
          itemName="yêu cầu giữ phòng"
        />
      )}

      {/* Modal từ chối */}
      {rejectModalTarget && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.55)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: 20,
          }}
          onClick={() => !isSubmitting && setRejectModalTarget(null)}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: 14,
              maxWidth: 520,
              width: '100%',
              padding: 24,
              boxShadow: '0 20px 30px -10px rgba(0,0,0,0.2)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <h3 style={{ fontSize: 18, fontWeight: 700, margin: 0, color: '#0F172A' }}>
                Từ chối yêu cầu giữ phòng #{rejectModalTarget.requestId}
              </h3>
              <button
                type="button"
                disabled={isSubmitting}
                onClick={() => setRejectModalTarget(null)}
                style={{ background: 'none', border: 'none', fontSize: 18, cursor: 'pointer', color: '#94A3B8' }}
              >
                ✕
              </button>
            </div>

            <div
              style={{
                backgroundColor: '#F8FAFC',
                border: '1px solid #E2E8F0',
                borderRadius: 10,
                padding: '12px 16px',
                fontSize: 13,
                lineHeight: 1.8,
                marginBottom: 16,
              }}
            >
              <div>
                <strong>Sinh viên:</strong> {rejectModalTarget.studentName} ({rejectModalTarget.studentCode})
              </div>
              <div>
                <strong>Giường:</strong> {rejectModalTarget.bedCode} — P.{rejectModalTarget.roomNumber},{' '}
                {rejectModalTarget.blockName}, {rejectModalTarget.dormName}
              </div>
              <div>
                <strong>Học kỳ:</strong> {rejectModalTarget.termName}
              </div>
              <div>
                <strong>Phí giữ phòng:</strong> {formatVnd(rejectModalTarget.feeAmount)}
              </div>
            </div>

            <label style={{ display: 'block', fontSize: 13, fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
              Lý do từ chối <span style={{ color: '#DC2626' }}>*</span>
            </label>
            <textarea
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              rows={4}
              placeholder="Nhập lý do từ chối để thông báo cho sinh viên..."
              style={{
                width: '100%',
                padding: '10px 12px',
                borderRadius: 8,
                border: '1px solid #CBD5E1',
                fontSize: 13,
                resize: 'vertical',
                marginBottom: 20,
              }}
            />

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button
                type="button"
                disabled={isSubmitting}
                onClick={() => setRejectModalTarget(null)}
                style={{
                  padding: '9px 16px',
                  background: '#F1F5F9',
                  color: '#334155',
                  border: '1px solid #CBD5E1',
                  borderRadius: 8,
                  fontWeight: 600,
                  fontSize: 13,
                  cursor: 'pointer',
                }}
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={isSubmitting || !rejectReason.trim()}
                onClick={() => void handleConfirmReject()}
                style={{
                  padding: '9px 20px',
                  background: isSubmitting || !rejectReason.trim() ? '#CBD5E1' : '#DC2626',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: 8,
                  fontWeight: 700,
                  fontSize: 13,
                  cursor: isSubmitting || !rejectReason.trim() ? 'not-allowed' : 'pointer',
                }}
              >
                {isSubmitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
