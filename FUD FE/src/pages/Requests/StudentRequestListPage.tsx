import React, { useState, useEffect } from 'react';
import { requestApi, type RequestItemDto } from '../../services/api';
import { REQUEST_STATUS_LABELS, REQUEST_STATUS_COLORS, REQUEST_TYPE_LABELS, type DBRequestStatus } from './SubmitRequestPage';
import { Pagination } from '../../components/Pagination';
import { removeVietnameseTones } from '../../utils/textUtils';
import { useIdleAwarePolling } from '../../utils/useIdleAwarePolling';
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
  const [searchTerm, setSearchTerm] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
  const [currentPage, setCurrentPage] = useState(1);
  const [selectedRequest, setSelectedRequest] = useState<RequestItemDto | null>(null);

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

  useEffect(() => {
    loadMyRequests(true);
  }, []);

  useIdleAwarePolling(() => {
    void loadMyRequests(false);
  }, 5000);

  const filteredRequests = requests.filter(r => {
    if (filterType && r.requestType !== filterType) return false;
    if (filterStatus && r.status !== filterStatus) return false;

    if (searchTerm.trim()) {
      const term = removeVietnameseTones(searchTerm);
      const codeNorm = removeVietnameseTones(r.requestCode || '');
      const typeLabelNorm = removeVietnameseTones(REQUEST_TYPE_LABELS[r.requestType as keyof typeof REQUEST_TYPE_LABELS] || r.requestType || '');
      const reasonNorm = removeVietnameseTones(r.reason || '');
      const roomNorm = removeVietnameseTones(r.roomInfo || '');
      const statusLabelNorm = removeVietnameseTones(REQUEST_STATUS_LABELS[r.status as DBRequestStatus] || r.status || '');

      const matches =
        codeNorm.includes(term) ||
        typeLabelNorm.includes(term) ||
        reasonNorm.includes(term) ||
        roomNorm.includes(term) ||
        statusLabelNorm.includes(term);

      if (!matches) return false;
    }

    return true;
  }).sort((a, b) => {
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

        <div style={{ flex: '1 1 200px', minWidth: '180px' }}>
          <input
            type="text"
            placeholder="Tìm theo mã đơn, lý do, phòng, trạng thái..."
            value={searchTerm}
            onChange={e => {
              setSearchTerm(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '6px 10px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '12px',
            }}
          />
        </div>

        <div style={{ minWidth: '145px' }}>
          <select
            value={sortOrder}
            onChange={e => setSortOrder(e.target.value as 'desc' | 'asc')}
            style={{
              padding: '6px 10px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '12px',
              fontWeight: 600,
              backgroundColor: '#FFF',
            }}
          >
            <option value="desc">Mới nhất trước ↓</option>
            <option value="asc">Cũ nhất trước ↑</option>
          </select>
        </div>

        {(filterType || filterStatus || searchTerm || sortOrder !== 'desc') && (
          <button
            type="button"
            onClick={() => {
              setFilterType('');
              setFilterStatus('');
              setSearchTerm('');
              setSortOrder('desc');
              setCurrentPage(1);
            }}
            style={{
              padding: '6px 12px',
              background: '#F1F5F9',
              border: '1px solid #CBD5E1',
              borderRadius: '6px',
              fontSize: '12px',
              cursor: 'pointer',
              color: '#64748B',
            }}
          >
            Đặt lại lọc
          </button>
        )}

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

            {/* Thông tin chuyển giường nếu là đơn BedTransfer */}
            {selectedRequest.bedTransfer && (() => {
              const bt = selectedRequest.bedTransfer;
              return (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 16 }}>
                  <div style={{ backgroundColor: '#EFF6FF', border: '1px solid #BFDBFE', borderRadius: '8px', padding: '12px', fontSize: '12px', lineHeight: 1.7 }}>
                    <div style={{ fontWeight: 700, color: '#1D4ED8', marginBottom: 4 }}>Thông tin yêu cầu chuyển giường:</div>
                    <div>
                      <strong>Từ giường:</strong> {bt.fromDormName} — {bt.fromBlockName} — P.{bt.fromRoomNumber} (Giường <strong>{bt.fromBedCode}</strong>)
                    </div>
                    <div>
                      <strong>Chuyển đến:</strong> {bt.toDormName} — {bt.toBlockName} — P.{bt.toRoomNumber} (Giường <strong>{bt.toBedCode}</strong> — {bt.toRoomTypeName})
                    </div>
                    {bt.transferDate && (
                      <div>
                        <strong>Ngày chuyển dự kiến:</strong> {new Date(bt.transferDate).toLocaleDateString('vi-VN')}
                      </div>
                    )}
                    {bt.assignedGuardName && (
                      <div>
                        <strong>Bảo vệ phụ trách kiểm tra:</strong> {bt.assignedGuardName} ({bt.assignedGuardCode})
                      </div>
                    )}
                    {bt.priceDifference != null && (
                      <div>
                        <strong>Chênh lệch phí giường (Prorate):</strong>{' '}
                        <span style={{ fontWeight: 700, color: bt.priceDifference > 0 ? '#B91C1C' : '#047857' }}>
                          {bt.priceDifference.toLocaleString('vi-VN')} đ
                        </span>
                      </div>
                    )}
                  </div>

                  {selectedRequest.status === 'Approved' && !bt.handover && (
                    <div style={{ backgroundColor: '#FEF3C7', border: '1px solid #FDE68A', borderRadius: '8px', padding: '10px 12px', fontSize: 12, color: '#92400E' }}>
                      Đơn đã được duyệt Bước 1. Vui lòng phối hợp cùng Bảo vệ <strong>{bt.assignedGuardName || ''}</strong> và bạn cùng phòng kiểm tra phòng cũ &amp; chốt chỉ số điện nước.
                    </div>
                  )}

                  {bt.handover && (
                    <div style={{ backgroundColor: '#ECFDF5', border: '1px solid #A7F3D0', borderRadius: '8px', padding: '12px', fontSize: 12, lineHeight: 1.6 }}>
                      <div style={{ fontWeight: 700, color: '#047857', marginBottom: 4 }}>
                        Biên bản Kiểm tra phòng cũ ({bt.handover.handoverCode})
                      </div>
                      <div><strong>Bảo vệ lập biên bản:</strong> {bt.handover.inspectorName}</div>
                      {bt.meterReadings && bt.meterReadings.length > 0 && (
                        <div style={{ marginTop: 6 }}>
                          <strong>Điện &amp; Nước chốt phòng cũ:</strong>
                          {bt.meterReadings.map((mr) => (
                            <div key={mr.id} style={{ marginTop: 2 }}>
                              • {mr.meterType === 'Electricity' ? 'Điện' : 'Nước'}: {mr.previousValue} → <strong>{mr.currentValue}</strong> (Dùng {mr.consumption}, chia {mr.residentCount} SV:{' '}
                              <strong>{mr.studentShareCost.toLocaleString('vi-VN')} đ</strong>)
                            </div>
                          ))}
                        </div>
                      )}
                      <div><strong>Bồi thường thiết bị:</strong> {(bt.equipmentCompensationAmount ?? bt.handover.totalCompensation).toLocaleString('vi-VN')} đ</div>
                      {bt.totalOutstandingAmount != null && (
                        <div style={{ marginTop: 4, color: '#991B1B', fontWeight: 700 }}>
                          Tổng chi phí phát sinh chuyển giường: {bt.totalOutstandingAmount.toLocaleString('vi-VN')} đ
                        </div>
                      )}
                      {bt.refundDueAmount != null && bt.refundDueAmount > 0 && (
                        <div style={{ color: '#047857', fontWeight: 600 }}>
                          Chênh lệch giảm phí phòng (đối soát hoàn/cấn trừ): {bt.refundDueAmount.toLocaleString('vi-VN')} đ
                        </div>
                      )}

                      {onGoToSubmit && (
                        <div style={{ marginTop: 8 }}>
                          <button
                            type="button"
                            onClick={onGoToSubmit}
                            style={{
                              padding: '5px 10px',
                              borderRadius: 6,
                              border: '1px solid #F59E0B',
                              background: '#FFFBEB',
                              color: '#B45309',
                              fontSize: 11,
                              fontWeight: 600,
                              cursor: 'pointer',
                            }}
                          >
                            Gửi đơn Khiếu nại nếu có thắc mắc biên bản
                          </button>
                        </div>
                      )}
                    </div>
                  )}

                  {bt.invoices && bt.invoices.length > 0 && (
                    <div style={{ backgroundColor: '#F8FAFC', border: '1px solid #CBD5E1', borderRadius: '8px', padding: '10px 12px', fontSize: 12 }}>
                      <strong style={{ display: 'block', marginBottom: 4, color: '#0F172A' }}>Hóa đơn phát sinh:</strong>
                      {bt.invoices.map((inv) => (
                        <div key={inv.id} style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 0' }}>
                          <span><strong>{inv.invoiceNumber}</strong> ({inv.invoiceType}) — Hạn: {inv.dueDate || '—'}</span>
                          <span><strong>{inv.totalAmount.toLocaleString('vi-VN')} đ</strong> ({inv.status})</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              );
            })()}

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
