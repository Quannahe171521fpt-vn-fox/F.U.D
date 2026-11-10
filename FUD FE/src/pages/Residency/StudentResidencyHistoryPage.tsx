import React, { useState, useEffect, useCallback } from 'react';
import {
  residencyApi,
  type ResidencyHistoryItem,
  type ResidencyHistoryDetail,
} from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDate, formatDateTime } from '../../utils/dateUtils';
import '../../styles/violation.css';

const PAGE_SIZE = 8;

const RESIDENCY_STATUS_LABELS: Record<string, string> = {
  Active: 'Đang lưu trú',
  Reserved: 'Đã giữ chỗ / Chờ nhận phòng',
  Ended: 'Đã kết thúc',
  Cancelled: 'Đã hủy',
};

const RESIDENCY_STATUS_STYLES: Record<string, { bg: string; color: string; border: string }> = {
  Active: { bg: '#DCFCE7', color: '#15803D', border: '#86EFAC' },
  Reserved: { bg: '#DBEAFE', color: '#1D4ED8', border: '#93C5FD' },
  Ended: { bg: '#F1F5F9', color: '#334155', border: '#CBD5E1' },
  Cancelled: { bg: '#FEE2E2', color: '#B91C1C', border: '#FCA5A5' },
};

const REGISTRATION_TYPE_LABELS: Record<string, string> = {
  Booking: 'Đặt phòng mới (Booking)',
  Keeping: 'Giữ chỗ kỳ tiếp (Keeping)',
  Transfer: 'Chuyển giường (Transfer)',
};

const POSITION_LABELS: Record<string, string> = {
  'Bottom Left': 'Tầng dưới - Trái',
  'Bottom Right': 'Tầng dưới - Phải',
  'Top Left': 'Tầng trên - Trái',
  'Top Right': 'Tầng trên - Phải',
  'Bottom Center': 'Tầng dưới - Giữa',
  'Top Center': 'Tầng trên - Giữa',
};

const INVOICE_TYPE_LABELS: Record<string, string> = {
  RoomFee: 'Tiền phòng nội trú',
  BedTransfer: 'Chênh lệch chuyển giường',
  Compensation: 'Bồi thường tài sản',
  Utility: 'Tiền điện / nước',
};

const INVOICE_STATUS_LABELS: Record<string, { label: string; bg: string; color: string }> = {
  Paid: { label: 'Đã thanh toán', bg: '#DCFCE7', color: '#15803D' },
  Unpaid: { label: 'Chưa thanh toán', bg: '#FEF3C7', color: '#92400E' },
  Processing: { label: 'Đang xử lý', bg: '#DBEAFE', color: '#1D4ED8' },
  Overdue: { label: 'Quá hạn', bg: '#FEE2E2', color: '#B91C1C' },
  Cancelled: { label: 'Đã hủy', bg: '#F1F5F9', color: '#475569' },
};

const REQUEST_STATUS_VI: Record<string, string> = {
  Pending: 'Chờ duyệt',
  'Pending Payment': 'Chờ thanh toán',
  'Pending Inspection': 'Chờ kiểm tra',
  Approved: 'Đã duyệt',
  Completed: 'Đã hoàn tất',
  Rejected: 'Đã từ chối',
  Cancelled: 'Đã hủy',
  Expired: 'Hết hạn',
};

interface StudentResidencyHistoryPageProps {
  onOpenCheckinRecords?: () => void;
  onBackToProfile?: () => void;
}

export const StudentResidencyHistoryPage: React.FC<StudentResidencyHistoryPageProps> = ({
  onOpenCheckinRecords,
  onBackToProfile,
}) => {
  const [history, setHistory] = useState<ResidencyHistoryItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [errorMsg, setErrorMsg] = useState<string>('');

  const [filterStatus, setFilterStatus] = useState<string>('');
  const [filterRegType, setFilterRegType] = useState<string>('');
  const [filterTerm, setFilterTerm] = useState<string>('');
  const [searchKeyword, setSearchKeyword] = useState<string>('');
  const [currentPage, setCurrentPage] = useState<number>(1);

  const [detailLoading, setDetailLoading] = useState<boolean>(false);
  const [detailError, setDetailError] = useState<string>('');
  const [selectedDetail, setSelectedDetail] = useState<ResidencyHistoryDetail | null>(null);

  const loadHistory = useCallback(async (showSpinner = true) => {
    if (showSpinner) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await residencyApi.getMyHistory({
        status: filterStatus || undefined,
        registrationType: filterRegType || undefined,
        term: filterTerm || undefined,
        search: searchKeyword || undefined,
      });
      setHistory(data);
    } catch (err: unknown) {
      if (showSpinner) {
        const message = err instanceof Error ? err.message : 'Không thể tải lịch sử lưu trú.';
        setErrorMsg(message);
      }
    } finally {
      if (showSpinner) {
        setLoading(false);
      }
    }
  }, [filterStatus, filterRegType, filterTerm, searchKeyword]);

  useEffect(() => {
    setCurrentPage(1);
    loadHistory(true);
  }, [loadHistory]);

  const handleOpenDetail = async (id: number) => {
    try {
      setDetailLoading(true);
      setDetailError('');
      const detail = await residencyApi.getMyHistoryById(id);
      setSelectedDetail(detail);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Không thể tải chi tiết đợt lưu trú.';
      setDetailError(message);
    } finally {
      setDetailLoading(false);
    }
  };

  const activeResidency = history.find(r => r.status === 'Active');
  const reservedCount = history.filter(r => r.status === 'Reserved').length;
  const endedCount = history.filter(r => r.status === 'Ended').length;

  const paginatedHistory = history.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE
  );

  const formatCurrency = (amount?: number) => {
    if (amount === undefined || amount === null) return '—';
    return `${amount.toLocaleString('vi-VN')} đ`;
  };

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* Header */}
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: 12,
          marginBottom: 20,
        }}
      >
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
            Lịch sử lưu trú ký túc xá
          </h1>
          <p style={{ color: '#64748B', fontSize: '13.5px', margin: 0 }}>
            Tra cứu toàn bộ các đợt nội trú, vị trí phòng/giường, lịch sử chuyển giường, trả phòng và hóa đơn qua từng học kỳ
          </p>
        </div>

        <div style={{ display: 'flex', gap: 10 }}>
          {onOpenCheckinRecords && (
            <button
              type="button"
              onClick={onOpenCheckinRecords}
              style={{
                padding: '8px 14px',
                borderRadius: '8px',
                border: '1px solid #FDBA74',
                backgroundColor: '#FFF7ED',
                color: '#C2410C',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              📋 Biên bản nhận phòng
            </button>
          )}
          {onBackToProfile && (
            <button
              type="button"
              onClick={onBackToProfile}
              style={{
                padding: '8px 14px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                backgroundColor: '#FFFFFF',
                color: '#334155',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              ← Về hồ sơ sinh viên
            </button>
          )}
        </div>
      </div>

      {/* Thẻ tóm tắt KPI & Chỗ ở hiện tại */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))',
          gap: 14,
          marginBottom: 18,
        }}
      >
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '16px 18px',
          }}
        >
          <div style={{ fontSize: '12px', color: '#64748B', fontWeight: 600, marginBottom: 4 }}>
            Tổng số đợt đăng ký lưu trú
          </div>
          <div style={{ fontSize: '24px', fontWeight: 700, color: '#0F172A' }}>
            {history.length} <span style={{ fontSize: '13px', fontWeight: 500, color: '#64748B' }}>đợt</span>
          </div>
        </div>

        <div
          style={{
            backgroundColor: activeResidency ? '#F0FDF4' : '#FFFFFF',
            borderRadius: '12px',
            border: activeResidency ? '1px solid #86EFAC' : '1px solid #E2E8F0',
            padding: '16px 18px',
          }}
        >
          <div style={{ fontSize: '12px', color: activeResidency ? '#166534' : '#64748B', fontWeight: 600, marginBottom: 4 }}>
            Chỗ ở hiện tại (Active)
          </div>
          {activeResidency ? (
            <div>
              <div style={{ fontSize: '16px', fontWeight: 700, color: '#15803D' }}>
                Phòng {activeResidency.roomNumber} · Giường {activeResidency.bedCode}
              </div>
              <div style={{ fontSize: '12px', color: '#166534', marginTop: 2 }}>
                {activeResidency.blockName || `Tòa ${activeResidency.blockCode}`} — {activeResidency.termName} ({activeResidency.academicYear})
              </div>
            </div>
          ) : (
            <div style={{ fontSize: '14px', fontWeight: 600, color: '#64748B' }}>
              Chưa có phòng đang ở
            </div>
          )}
        </div>

        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '16px 18px',
          }}
        >
          <div style={{ fontSize: '12px', color: '#64748B', fontWeight: 600, marginBottom: 4 }}>
            Giữ chỗ / Chờ nhận phòng
          </div>
          <div style={{ fontSize: '24px', fontWeight: 700, color: '#1D4ED8' }}>
            {reservedCount} <span style={{ fontSize: '13px', fontWeight: 500, color: '#64748B' }}>kỳ</span>
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '16px 18px',
          }}
        >
          <div style={{ fontSize: '12px', color: '#64748B', fontWeight: 600, marginBottom: 4 }}>
            Kỳ lưu trú đã hoàn thành
          </div>
          <div style={{ fontSize: '24px', fontWeight: 700, color: '#334155' }}>
            {endedCount} <span style={{ fontSize: '13px', fontWeight: 500, color: '#64748B' }}>kỳ</span>
          </div>
        </div>
      </div>

      {/* Thông báo lỗi nếu có */}
      {(errorMsg || detailError) && (
        <div
          className="violation-alert violation-alert-error"
          style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}
        >
          <span>{errorMsg || detailError}</span>
          <button
            type="button"
            onClick={() => {
              setDetailError('');
              loadHistory(true);
            }}
            style={{
              background: '#DC2626',
              color: '#FFF',
              border: 'none',
              padding: '5px 12px',
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

      {/* Bộ lọc tra cứu */}
      <div
        style={{
          display: 'flex',
          gap: '12px',
          alignItems: 'flex-end',
          backgroundColor: '#FFFFFF',
          padding: '16px 20px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
          marginBottom: 16,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ minWidth: '190px', flex: '1 1 180px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Trạng thái lưu trú
          </label>
          <select
            value={filterStatus}
            onChange={e => setFilterStatus(e.target.value)}
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
            <option value="Active">Đang lưu trú (Active)</option>
            <option value="Reserved">Đã giữ chỗ / Chờ nhận phòng (Reserved)</option>
            <option value="Ended">Đã kết thúc (Ended)</option>
            <option value="Cancelled">Đã hủy (Cancelled)</option>
          </select>
        </div>

        <div style={{ minWidth: '190px', flex: '1 1 180px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Hình thức đăng ký
          </label>
          <select
            value={filterRegType}
            onChange={e => setFilterRegType(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả hình thức</option>
            <option value="Booking">Đặt phòng mới (Booking)</option>
            <option value="Keeping">Giữ phòng cũ (Keeping)</option>
            <option value="Transfer">Chuyển giường (Transfer)</option>
          </select>
        </div>

        <div style={{ minWidth: '180px', flex: '1 1 160px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Lọc theo Học kỳ
          </label>
          <input
            type="text"
            placeholder="VD: Fall, Spring, Summer..."
            value={filterTerm}
            onChange={e => setFilterTerm(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
            }}
          />
        </div>

        <div style={{ minWidth: '210px', flex: '1.2 1 200px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Tìm theo Phòng / Tòa / Giường
          </label>
          <input
            type="text"
            placeholder="Nhập số phòng, mã giường, tòa..."
            value={searchKeyword}
            onChange={e => setSearchKeyword(e.target.value)}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
            }}
          />
        </div>

        <div style={{ display: 'flex', gap: 8 }}>
          {(filterStatus || filterRegType || filterTerm || searchKeyword) && (
            <button
              type="button"
              onClick={() => {
                setFilterStatus('');
                setFilterRegType('');
                setFilterTerm('');
                setSearchKeyword('');
              }}
              style={{
                padding: '8px 12px',
                backgroundColor: '#F1F5F9',
                color: '#475569',
                border: '1px solid #CBD5E1',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '13px',
                cursor: 'pointer',
              }}
            >
              Xóa lọc
            </button>
          )}
          <button
            type="button"
            onClick={() => loadHistory(true)}
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

      {/* Bảng danh sách lịch sử lưu trú */}
      <div className="fud-table-wrapper">
        <table className="fud-table">
          <colgroup>
            <col style={{ width: '10%' }} />
            <col style={{ width: '15%' }} />
            <col style={{ width: '21%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '18%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '10%' }} />
          </colgroup>
          <thead>
            <tr>
              <th>Mã hồ sơ</th>
              <th>Học kỳ</th>
              <th>Khu / Tòa / Phòng / Giường</th>
              <th>Hình thức</th>
              <th>Thời gian lưu trú</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '36px', color: '#64748B' }}>
                  Đang tải dữ liệu lịch sử lưu trú...
                </td>
              </tr>
            ) : paginatedHistory.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '44px', color: '#64748B' }}>
                  <div style={{ fontSize: '26px', marginBottom: 6 }}>🏠</div>
                  <div style={{ fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                    Không tìm thấy hồ sơ lưu trú nào
                  </div>
                  <div style={{ fontSize: '13px' }}>
                    Chưa có đợt lưu trú nào khớp với bộ lọc hiện tại của bạn.
                  </div>
                </td>
              </tr>
            ) : (
              paginatedHistory.map(item => {
                const stStyle = RESIDENCY_STATUS_STYLES[item.status] || {
                  bg: '#F1F5F9',
                  color: '#475569',
                  border: '#CBD5E1',
                };
                const checkOutDisplay = item.actualCheckOutDate
                  ? `${formatDate(item.actualCheckOutDate)} (Thực tế)`
                  : item.plannedCheckOutDate
                    ? `${formatDate(item.plannedCheckOutDate)} (Dự kiến)`
                    : '—';

                return (
                  <tr key={item.id}>
                    <td>
                      <span
                        className="cell-text-1line"
                        title={item.residencyCode}
                        style={{ fontWeight: 700, color: '#C2410C' }}
                      >
                        {item.residencyCode}
                      </span>
                    </td>

                    <td>
                      <div style={{ fontWeight: 700, color: '#0F172A' }}>
                        {item.termName}
                      </div>
                      <div style={{ fontSize: '11.5px', color: '#64748B' }}>
                        Năm học {item.academicYear}
                      </div>
                    </td>

                    <td>
                      <div style={{ fontWeight: 700, color: '#0F172A' }}>
                        Phòng {item.roomNumber} · Giường {item.bedCode}
                      </div>
                      <div style={{ fontSize: '11.5px', color: '#64748B' }}>
                        {item.blockName || `Tòa ${item.blockCode}`} ({item.dormName || item.dormCode}) · {item.roomTypeName || `${item.bedCapacity} giường`}
                      </div>
                    </td>

                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '2px 8px',
                          borderRadius: '6px',
                          backgroundColor: '#FFF7ED',
                          color: '#9A3412',
                          border: '1px solid #FED7AA',
                          fontSize: '11.5px',
                          fontWeight: 600,
                        }}
                      >
                        {item.registrationType === 'Keeping'
                          ? 'Giữ phòng (Keeping)'
                          : item.registrationType === 'Transfer'
                            ? 'Chuyển giường'
                            : 'Đặt mới (Booking)'}
                      </span>
                    </td>

                    <td>
                      <div style={{ fontSize: '12.5px', color: '#0F172A' }}>
                        <strong>Nhận:</strong> {item.checkInDate ? formatDate(item.checkInDate) : 'Chưa check-in'}
                      </div>
                      <div style={{ fontSize: '12px', color: '#64748B' }}>
                        <strong>Trả:</strong> {checkOutDisplay}
                      </div>
                    </td>

                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '3px 9px',
                          borderRadius: '9999px',
                          fontSize: '11px',
                          fontWeight: 700,
                          whiteSpace: 'nowrap',
                          backgroundColor: stStyle.bg,
                          color: stStyle.color,
                          border: `1px solid ${stStyle.border}`,
                        }}
                      >
                        {RESIDENCY_STATUS_LABELS[item.status] || item.status}
                      </span>
                    </td>

                    <td>
                      <button
                        type="button"
                        disabled={detailLoading}
                        onClick={() => handleOpenDetail(item.id)}
                        style={{
                          padding: '6px 12px',
                          backgroundColor: '#F8FAFC',
                          border: '1px solid #CBD5E1',
                          borderRadius: '6px',
                          fontSize: '12px',
                          cursor: 'pointer',
                          fontWeight: 600,
                          color: '#0F172A',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        Chi tiết
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
        totalItems={history.length}
        pageSize={PAGE_SIZE}
        onPageChange={page => setCurrentPage(page)}
        itemName="đợt lưu trú"
      />

      {/* Modal Chi tiết đợt lưu trú */}
      {selectedDetail && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.55)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: '20px',
          }}
          onClick={() => setSelectedDetail(null)}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '14px',
              maxWidth: '780px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              padding: '24px 28px',
              boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)',
            }}
            onClick={e => e.stopPropagation()}
          >
            {/* Header Modal */}
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'flex-start',
                borderBottom: '1px solid #E2E8F0',
                paddingBottom: 14,
                marginBottom: 18,
              }}
            >
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                  <span
                    style={{
                      fontSize: '11px',
                      fontWeight: 700,
                      color: '#C2410C',
                      textTransform: 'uppercase',
                      letterSpacing: '0.04em',
                    }}
                  >
                    Hồ sơ lưu trú #{selectedDetail.residencyCode}
                  </span>
                  <span
                    style={{
                      padding: '2px 9px',
                      borderRadius: '9999px',
                      fontSize: '11px',
                      fontWeight: 700,
                      backgroundColor: (RESIDENCY_STATUS_STYLES[selectedDetail.status] || { bg: '#F1F5F9' }).bg,
                      color: (RESIDENCY_STATUS_STYLES[selectedDetail.status] || { color: '#334155' }).color,
                    }}
                  >
                    {RESIDENCY_STATUS_LABELS[selectedDetail.status] || selectedDetail.status}
                  </span>
                </div>
                <h3 style={{ fontSize: '20px', fontWeight: 700, margin: 0, color: '#0F172A' }}>
                  Phòng {selectedDetail.roomNumber} — Giường {selectedDetail.bedCode} ({selectedDetail.blockName || `Tòa ${selectedDetail.blockCode}`})
                </h3>
              </div>

              <button
                type="button"
                onClick={() => setSelectedDetail(null)}
                style={{
                  background: '#F1F5F9',
                  border: 'none',
                  borderRadius: '6px',
                  padding: '6px 10px',
                  fontSize: '14px',
                  cursor: 'pointer',
                  color: '#475569',
                  fontWeight: 700,
                }}
              >
                ✕
              </button>
            </div>

            {/* 1. Thông tin Phòng, Giường & Học kỳ */}
            <div
              style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
                gap: 14,
                marginBottom: 18,
              }}
            >
              <div
                style={{
                  backgroundColor: '#F8FAFC',
                  borderRadius: '10px',
                  border: '1px solid #E2E8F0',
                  padding: '14px 16px',
                  fontSize: '13px',
                  lineHeight: 1.8,
                }}
              >
                <div style={{ fontWeight: 700, color: '#0F172A', marginBottom: 6, fontSize: '13.5px' }}>
                  📍 Thông tin Vị trí & Phòng ở
                </div>
                <div><strong>Cơ sở / Khu KTX:</strong> {selectedDetail.campusName} — {selectedDetail.dormName} ({selectedDetail.dormCode})</div>
                <div><strong>Tòa nhà (Block):</strong> {selectedDetail.blockName} ({selectedDetail.blockCode})</div>
                <div><strong>Phòng & Tầng:</strong> Phòng {selectedDetail.roomNumber}{selectedDetail.floorLevel ? ` (Tầng ${selectedDetail.floorLevel})` : ''}</div>
                <div><strong>Loại phòng:</strong> {selectedDetail.roomTypeName} ({selectedDetail.bedCapacity} giường)</div>
                <div>
                  <strong>Mã giường & Vị trí:</strong> Giường <span style={{ color: '#C2410C', fontWeight: 700 }}>{selectedDetail.bedCode}</span>
                  {selectedDetail.positionInRoom ? ` (${POSITION_LABELS[selectedDetail.positionInRoom] || selectedDetail.positionInRoom})` : ''}
                </div>
              </div>

              <div
                style={{
                  backgroundColor: '#F8FAFC',
                  borderRadius: '10px',
                  border: '1px solid #E2E8F0',
                  padding: '14px 16px',
                  fontSize: '13px',
                  lineHeight: 1.8,
                }}
              >
                <div style={{ fontWeight: 700, color: '#0F172A', marginBottom: 6, fontSize: '13.5px' }}>
                  🗓️ Học kỳ & Mốc thời gian
                </div>
                <div><strong>Học kỳ:</strong> {selectedDetail.termName} ({selectedDetail.academicYear})</div>
                <div><strong>Thời gian học kỳ:</strong> {formatDate(selectedDetail.termStartDate)} → {formatDate(selectedDetail.termEndDate)}</div>
                <div>
                  <strong>Hình thức đăng ký:</strong>{' '}
                  {REGISTRATION_TYPE_LABELS[selectedDetail.registrationType || 'Booking'] || selectedDetail.registrationType}
                </div>
                <div><strong>Ngày nhận phòng (Check-in):</strong> {selectedDetail.checkInDate ? formatDate(selectedDetail.checkInDate) : 'Chưa nhận phòng'}</div>
                <div><strong>Ngày trả phòng dự kiến:</strong> {selectedDetail.plannedCheckOutDate ? formatDate(selectedDetail.plannedCheckOutDate) : '—'}</div>
                <div>
                  <strong>Ngày trả phòng thực tế:</strong>{' '}
                  <span style={{ fontWeight: 600, color: selectedDetail.actualCheckOutDate ? '#15803D' : '#64748B' }}>
                    {selectedDetail.actualCheckOutDate ? formatDate(selectedDetail.actualCheckOutDate) : 'Chưa trả phòng'}
                  </span>
                </div>
              </div>
            </div>

            {/* 2. Biên bản bàn giao nhận phòng (Check-in Handover) */}
            <div
              style={{
                backgroundColor: selectedDetail.hasCheckinHandover ? '#F0FDF4' : '#FFFBEB',
                border: selectedDetail.hasCheckinHandover ? '1px solid #86EFAC' : '1px solid #FDE68A',
                borderRadius: '10px',
                padding: '14px 16px',
                marginBottom: 18,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: 12,
              }}
            >
              <div style={{ fontSize: '13px', lineHeight: 1.6 }}>
                <div style={{ fontWeight: 700, color: selectedDetail.hasCheckinHandover ? '#166534' : '#92400E', fontSize: '13.5px' }}>
                  📋 Biên bản bàn giao nhận phòng (Check-in Handover)
                </div>
                {selectedDetail.hasCheckinHandover ? (
                  <div style={{ color: '#14532D' }}>
                    Đã lập biên bản <strong>#{selectedDetail.checkinHandoverId}</strong> · Cán bộ bàn giao:{' '}
                    <strong>{selectedDetail.checkinGuardName || 'Bảo vệ KTX'}</strong> · Thời gian:{' '}
                    {formatDateTime(selectedDetail.checkinCompletedTime)}
                    {selectedDetail.hasDamageOrMissingAtCheckin && (
                      <span style={{ marginLeft: 8, color: '#B45309', fontWeight: 600 }}>
                        (⚠️ Có ghi nhận thiết bị hỏng/thiếu)
                      </span>
                    )}
                  </div>
                ) : (
                  <div style={{ color: '#92400E' }}>
                    Đợt lưu trú này chưa có biên bản bàn giao nhận phòng từ cán bộ Bảo vệ.
                  </div>
                )}
              </div>

              {selectedDetail.hasCheckinHandover && onOpenCheckinRecords && (
                <button
                  type="button"
                  onClick={() => {
                    setSelectedDetail(null);
                    onOpenCheckinRecords();
                  }}
                  style={{
                    padding: '7px 14px',
                    backgroundColor: '#16A34A',
                    color: '#FFFFFF',
                    border: 'none',
                    borderRadius: '6px',
                    fontSize: '12.5px',
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  Xem biên bản nhận phòng →
                </button>
              )}
            </div>

            {/* 3. Tóm tắt Hóa đơn tiền phòng / phí liên quan */}
            <div style={{ marginBottom: 18 }}>
              <div style={{ fontWeight: 700, color: '#0F172A', fontSize: '14px', marginBottom: 8 }}>
                💳 Hóa đơn liên quan ({selectedDetail.invoices.length})
              </div>
              {selectedDetail.invoices.length === 0 ? (
                <div
                  style={{
                    backgroundColor: '#F8FAFC',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '12px 14px',
                    fontSize: '13px',
                    color: '#64748B',
                  }}
                >
                  Chưa có hóa đơn nào gắn trực tiếp với đợt lưu trú này.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {selectedDetail.invoices.map(inv => {
                    const invBadge = INVOICE_STATUS_LABELS[inv.status] || {
                      label: inv.status,
                      bg: '#F1F5F9',
                      color: '#475569',
                    };
                    return (
                      <div
                        key={inv.id}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          flexWrap: 'wrap',
                          gap: 10,
                          backgroundColor: '#F8FAFC',
                          border: '1px solid #E2E8F0',
                          borderRadius: '8px',
                          padding: '10px 14px',
                          fontSize: '13px',
                        }}
                      >
                        <div>
                          <span style={{ fontWeight: 700, color: '#C2410C' }}>{inv.invoiceNumber}</span>
                          <span style={{ margin: '0 8px', color: '#CBD5E1' }}>|</span>
                          <span style={{ fontWeight: 600, color: '#0F172A' }}>
                            {INVOICE_TYPE_LABELS[inv.invoiceType] || inv.invoiceType}
                          </span>
                          <div style={{ fontSize: '12px', color: '#64748B', marginTop: 2 }}>
                            Ngày phát hành: {formatDateTime(inv.issuedTime)} · Hạn thanh toán: {formatDate(inv.dueDate)}
                            {inv.paidTime ? ` · Thanh toán lúc: ${formatDateTime(inv.paidTime)}` : ''}
                          </div>
                        </div>

                        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                          <span style={{ fontWeight: 700, fontSize: '14px', color: '#0F172A' }}>
                            {formatCurrency(inv.amount)}
                          </span>
                          <span
                            style={{
                              padding: '3px 9px',
                              borderRadius: '9999px',
                              fontSize: '11px',
                              fontWeight: 700,
                              backgroundColor: invBadge.bg,
                              color: invBadge.color,
                            }}
                          >
                            {invBadge.label}
                          </span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>

            {/* 4. Lịch sử Chuyển giường trong kỳ (Bed_Transfer_Requests) */}
            <div style={{ marginBottom: 18 }}>
              <div style={{ fontWeight: 700, color: '#0F172A', fontSize: '14px', marginBottom: 8 }}>
                🔄 Lịch sử chuyển giường trong kỳ ({selectedDetail.transfers.length})
              </div>
              {selectedDetail.transfers.length === 0 ? (
                <div
                  style={{
                    backgroundColor: '#F8FAFC',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '12px 14px',
                    fontSize: '13px',
                    color: '#64748B',
                  }}
                >
                  Không phát sinh yêu cầu chuyển giường trong đợt lưu trú này.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {selectedDetail.transfers.map(tr => (
                    <div
                      key={tr.requestId}
                      style={{
                        backgroundColor: '#F8FAFC',
                        border: '1px solid #E2E8F0',
                        borderRadius: '8px',
                        padding: '12px 14px',
                        fontSize: '13px',
                        lineHeight: 1.6,
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                        <div>
                          <span style={{ fontWeight: 700, color: '#0F172A' }}>
                            Đơn chuyển giường #{tr.requestId}
                          </span>{' '}
                          <span style={{ color: '#475569' }}>
                            ({tr.fromBlockCode ? `Tòa ${tr.fromBlockCode} - ` : ''}Phòng {tr.fromRoomNumber} / Giường {tr.fromBedCode} →{' '}
                            {tr.toBlockCode ? `Tòa ${tr.toBlockCode} - ` : ''}Phòng {tr.toRoomNumber} / Giường {tr.toBedCode})
                          </span>
                        </div>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '9999px',
                            fontSize: '11px',
                            fontWeight: 700,
                            backgroundColor: '#FFF7ED',
                            color: '#C2410C',
                            border: '1px solid #FED7AA',
                          }}
                        >
                          {REQUEST_STATUS_VI[tr.status] || tr.status}
                        </span>
                      </div>
                      <div style={{ fontSize: '12.5px', color: '#475569', marginTop: 4 }}>
                        <strong>Lý do:</strong> {tr.transferReason}
                      </div>
                      <div style={{ fontSize: '12px', color: '#64748B', marginTop: 2 }}>
                        Ngày gửi: {formatDateTime(tr.createdTime)}
                        {tr.transferDate ? ` · Ngày chuyển: ${formatDate(tr.transferDate)}` : ''}
                        {tr.priceDifference !== undefined && tr.priceDifference !== null
                          ? ` · Chênh lệch phí: ${formatCurrency(tr.priceDifference)}`
                          : ''}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* 5. Thông tin Đơn trả phòng (Checkout_Requests) */}
            <div style={{ marginBottom: 18 }}>
              <div style={{ fontWeight: 700, color: '#0F172A', fontSize: '14px', marginBottom: 8 }}>
                🚪 Thông tin đơn trả phòng ({selectedDetail.checkouts.length})
              </div>
              {selectedDetail.checkouts.length === 0 ? (
                <div
                  style={{
                    backgroundColor: '#F8FAFC',
                    border: '1px solid #E2E8F0',
                    borderRadius: '8px',
                    padding: '12px 14px',
                    fontSize: '13px',
                    color: '#64748B',
                  }}
                >
                  Chưa có đơn yêu cầu trả phòng cho đợt lưu trú này.
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  {selectedDetail.checkouts.map(co => (
                    <div
                      key={co.requestId}
                      style={{
                        backgroundColor: '#F8FAFC',
                        border: '1px solid #E2E8F0',
                        borderRadius: '8px',
                        padding: '12px 14px',
                        fontSize: '13px',
                        lineHeight: 1.6,
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                        <div style={{ fontWeight: 700, color: '#0F172A' }}>
                          Đơn trả phòng #{co.requestId} · Ngày trả dự kiến: {formatDate(co.expectedCheckOutDate)}
                        </div>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '9999px',
                            fontSize: '11px',
                            fontWeight: 700,
                            backgroundColor: '#EFF6FF',
                            color: '#1D4ED8',
                            border: '1px solid #BFDBFE',
                          }}
                        >
                          {REQUEST_STATUS_VI[co.status] || co.status}
                        </span>
                      </div>
                      {co.reason && (
                        <div style={{ fontSize: '12.5px', color: '#475569', marginTop: 4 }}>
                          <strong>Lý do trả phòng:</strong> {co.reason}
                        </div>
                      )}
                      {co.isExceptionalCase && co.exceptionNote && (
                        <div style={{ fontSize: '12.5px', color: '#B45309', marginTop: 2 }}>
                          <strong>Trường hợp ngoại lệ:</strong> {co.exceptionNote}
                        </div>
                      )}
                      <div style={{ fontSize: '12px', color: '#64748B', marginTop: 2 }}>
                        Ngày gửi đơn: {formatDateTime(co.createdTime)}
                        {co.reviewedTime ? ` · Duyệt lúc: ${formatDateTime(co.reviewedTime)}` : ''}
                        {co.checkoutHandoverId
                          ? ` · Biên bản trả phòng #${co.checkoutHandoverId} (${co.checkoutHandoverStatus})`
                          : ''}
                        {co.totalCompensation && co.totalCompensation > 0
                          ? ` · Bồi thường thiết bị: ${formatCurrency(co.totalCompensation)}`
                          : ''}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Footer Modal */}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 18 }}>
              <button
                type="button"
                onClick={() => setSelectedDetail(null)}
                style={{
                  padding: '8px 20px',
                  backgroundColor: '#EA580C',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: '8px',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
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
