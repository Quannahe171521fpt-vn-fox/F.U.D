import React, { useState, useEffect } from 'react';
import { violationApi, requestApi, type ViolationItem } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { formatDateTime, formatDate } from '../../utils/dateUtils';
import '../../styles/violation.css';

// ── Mapping chuẩn CHECK CONSTRAINT DB CSDL FUD ──────────────────
// CONSTRAINT CHK_Request_Type CHECK (RequestType IN ('Booking','BedTransfer','Checkout','Complaint','Others'))
export type DBRequestType = 'Booking' | 'BedTransfer' | 'Checkout' | 'Complaint' | 'Others';

export const REQUEST_TYPE_LABELS: Record<DBRequestType, string> = {
  Booking: 'Đăng ký phòng ở (Booking)',
  BedTransfer: 'Yêu cầu chuyển giường / đổi phòng (Bed Transfer)',
  Checkout: 'Yêu cầu trả phòng / rời KTX (Checkout)',
  Complaint: 'Khiếu nại / Kháng cáo vi phạm (Complaint)',
  Others: 'Yêu cầu & Kiến nghị khác (Others)',
};

// CONSTRAINT CHK_Request_Status CHECK (Status IN ('Pending Payment','Pending','Approved','Completed','Rejected','Cancelled','Expired'))
export type DBRequestStatus =
  | 'Pending Payment'
  | 'Pending'
  | 'Approved'
  | 'Completed'
  | 'Rejected'
  | 'Cancelled'
  | 'Expired';

export const REQUEST_STATUS_LABELS: Record<string, string> = {
  'Pending Payment': 'Chờ thanh toán phí',
  'Pending': 'Đang chờ xét duyệt',
  'Approved': 'Đã phê duyệt chấp thuận',
  'Completed': 'Đã hoàn tất xử lý',
  'Rejected': 'Đã từ chối đơn',
  'Cancelled': 'Đã hủy bỏ',
  'Expired': 'Đã hết hạn xử lý',
};

export const REQUEST_STATUS_COLORS: Record<string, { bg: string; color: string }> = {
  'Pending Payment': { bg: '#FEF3C7', color: '#92400E' },
  'Pending': { bg: '#EFF6FF', color: '#1D4ED8' },
  'Approved': { bg: '#ECFDF5', color: '#047857' },
  'Completed': { bg: '#DCFCE7', color: '#15803D' },
  'Rejected': { bg: '#FEE2E2', color: '#B91C1C' },
  'Cancelled': { bg: '#F1F5F9', color: '#475569' },
  'Expired': { bg: '#F8FAFC', color: '#64748B' },
};

interface SubmitRequestPageProps {
  onSubmitted?: () => void;
}

export const SubmitRequestPage: React.FC<SubmitRequestPageProps> = ({ onSubmitted }) => {
  const { currentUser } = useAuth();
  const isSuspended = currentUser?.accountStatus === 'Suspended';
  const [requestType, setRequestType] = useState<DBRequestType>(() => isSuspended ? 'Complaint' : 'Booking');
  const [violations, setViolations] = useState<ViolationItem[]>([]);
  const [selectedViolationId, setSelectedViolationId] = useState<number | ''>('');
  const [content, setContent] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Đồng hồ đếm ngược thời gian thực
  const [now, setNow] = useState(new Date());

  // Nạp danh sách biên bản vi phạm có thể khiếu nại (Chưa quá hạn 24h và chưa gửi đơn)
  useEffect(() => {
    if (requestType === 'Complaint') {
      loadViolationsForAppeal();
    }
  }, [requestType]);

  // Cập nhật đồng hồ thực mỗi giây và tự động loại bỏ biên bản hết hạn
  useEffect(() => {
    const tick = setInterval(() => {
      const currentNow = new Date();
      setNow(currentNow);
      // Tự động lọc bỏ các biên bản đã hết hạn trong danh sách hiện tại
      setViolations(prev => {
        const stillValid = prev.filter(v => !v.requestDeadline || new Date(v.requestDeadline) > currentNow);
        // Nếu biên bản đang chọn bị hết hạn, reset selection
        if (stillValid.length !== prev.length) {
          setSelectedViolationId(stillValid.length > 0 ? stillValid[0].id : '');
        }
        return stillValid;
      });
    }, 1000);
    return () => clearInterval(tick);
  }, []);

  // Polling mỗi 5 giây để đồng bộ danh sách biên bản có thể khiếu nại
  useEffect(() => {
    if (requestType !== 'Complaint') return;
    const interval = setInterval(() => {
      loadViolationsForAppeal();
    }, 5000);
    return () => clearInterval(interval);
  }, [requestType]);

  const loadViolationsForAppeal = async () => {
    try {
      const data = await violationApi.getStudentNotices();
      // Chỉ lấy các biên bản đang ở trạng thái 'Recorded', chưa gửi đơn khiếu nại và chưa quá hạn 24h
      const now = new Date();
      const validNotices = data.filter(v => {
        if (v.status !== 'Recorded') return false;
        if (v.linkedRequestId) return false;
        if (v.requestDeadline && new Date(v.requestDeadline) < now) return false;
        return true;
      });
      setViolations(validNotices);
      if (validNotices.length > 0) {
        setSelectedViolationId(validNotices[0].id);
      } else {
        setSelectedViolationId('');
      }
    } catch {
      // Ignore
    }
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const selected = Array.from(e.target.files);
      if (files.length + selected.length > 5) {
        setErrorMsg('Chỉ được đính kèm tối đa 5 ảnh minh chứng.');
        return;
      }
      setFiles(prev => [...prev, ...selected]);
    }
  };

  const handleRemoveFile = (index: number) => {
    setFiles(prev => prev.filter((_, i) => i !== index));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg('');
    setSuccessMsg('');

    if (requestType === 'Complaint') {
      if (!selectedViolationId) {
        setErrorMsg('Vui lòng chọn biên bản vi phạm cần khiếu nại / kháng cáo.');
        return;
      }
      if (!content.trim()) {
        setErrorMsg('Vui lòng nhập lý do và căn cứ kháng cáo.');
        return;
      }

      setSubmitting(true);
      try {
        const formData = new FormData();
        formData.append('requestType', 'Complaint');
        formData.append('targetType', 'Violation');
        formData.append('targetViolationId', selectedViolationId.toString());
        formData.append('content', content.trim());
        formData.append('reason', content.trim());
        files.forEach(f => formData.append('evidences', f));

        await requestApi.create(formData);
        setSuccessMsg('Đã tạo đơn khiếu nại vi phạm kỷ luật (Type: Complaint) thành công!');
        setContent('');
        setFiles([]);
        loadViolationsForAppeal();
        if (onSubmitted) {
          setTimeout(() => onSubmitted(), 1200);
        }
      } catch (err: any) {
        setErrorMsg(err.message || 'Lỗi khi gửi đơn khiếu nại');
      } finally {
        setSubmitting(false);
      }
    } else {
      setSubmitting(true);
      try {
        const formData = new FormData();
        formData.append('requestType', requestType);
        formData.append('reason', content.trim());
        files.forEach(f => formData.append('evidences', f));

        await requestApi.create(formData);
        setSuccessMsg(`Đã gửi đơn ${REQUEST_TYPE_LABELS[requestType]} thành công với trạng thái: ${REQUEST_STATUS_LABELS['Pending']}!`);
        setContent('');
        setFiles([]);
        if (onSubmitted) {
          setTimeout(() => onSubmitted(), 1200);
        }
      } catch (err: any) {
        setErrorMsg(err.message || 'Lỗi khi gửi đơn yêu cầu');
      } finally {
        setSubmitting(false);
      }
    }
  };

  const selectedViolation = violations.find(v => v.id === selectedViolationId);

  // Tính thời gian còn lại đến hạn nộp khiếu nại
  const getTimeRemaining = (deadline: string | undefined): string => {
    if (!deadline) return 'Trong vòng 24h';
    const diff = new Date(deadline).getTime() - now.getTime();
    if (diff <= 0) return 'Đã hết hạn';
    const hours = Math.floor(diff / 3600000);
    const minutes = Math.floor((diff % 3600000) / 60000);
    const seconds = Math.floor((diff % 60000) / 1000);
    if (hours > 0) return `Còn ${hours}h ${minutes}m ${seconds}s`;
    if (minutes > 0) return `Còn ${minutes}m ${seconds}s`;
    return `Còn ${seconds}s`;
  };

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Tạo đơn / Gửi yêu cầu (Submit Request)
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Hệ thống tiếp nhận đơn đăng ký phòng, chuyển giường, trả phòng, khiếu nại vi phạm (Complaint) và kiến nghị
        </p>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

      {isSuspended ? (
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #FCA5A5', padding: '32px', maxWidth: '750px', textAlign: 'center' }}>
          <div style={{ fontSize: '48px', marginBottom: 12 }}>🚫</div>
          <h2 style={{ fontSize: '18px', fontWeight: 700, color: '#991B1B', margin: '0 0 8px' }}>
            Quyền gửi đơn & Booking đã bị khóa
          </h2>
          <p style={{ color: '#475569', fontSize: '14px', lineHeight: 1.6, maxWidth: '580px', margin: '0 auto 16px' }}>
            Tài khoản của bạn đã có kết luận chính thức <strong>Đình chỉ nội trú / Buộc rời ký túc xá</strong>. Toàn bộ tính năng Booking, giữ phòng và gửi đơn yêu cầu (Request) đã bị tạm khóa.
          </p>
          <div style={{ display: 'inline-block', backgroundColor: '#FEF2F2', padding: '10px 18px', borderRadius: '8px', border: '1px solid #FECACA', fontSize: '13px', color: '#B91C1C' }}>
            Lý do: {currentUser?.suspensionReason || 'Vi phạm quy chế kỷ luật nội trú KTX.'}
          </div>
          <div style={{ marginTop: 20, fontSize: '13px', color: '#64748B' }}>
            Vui lòng liên hệ trực tiếp Ban Quản lý ký túc xá để được xem xét mở lại tài khoản.
          </div>
        </div>
      ) : (
      <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #E2E8F0', padding: '24px', maxWidth: '750px' }}>
        <form onSubmit={handleSubmit}>
          {/* Loại đơn: Chuẩn theo CHECK CONSTRAINT CSDL */}
          <div style={{ marginBottom: 18 }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
              Chọn loại đơn yêu cầu (Request Type) <span style={{ color: '#DC2626' }}>*</span>
            </label>
            {isSuspended && (
              <div style={{ marginBottom: 14, padding: '10px 14px', backgroundColor: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '13px' }}>
                ⚠️ <strong>Tài khoản đang bị đình chỉ:</strong> Chức năng đăng ký giữ chỗ (Booking) và chuyển/giữ phòng (BedTransfer) đã bị khóa cho đến khi được Ban Quản lý mở lại.
              </div>
            )}
            <select
              value={requestType}
              onChange={e => {
                setRequestType(e.target.value as DBRequestType);
                setErrorMsg('');
                setSuccessMsg('');
              }}
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '14px',
                backgroundColor: '#FFF',
                fontWeight: 600,
              }}
            >
              {(Object.keys(REQUEST_TYPE_LABELS) as DBRequestType[])
                .filter(typeKey => !isSuspended || (typeKey !== 'Booking' && typeKey !== 'BedTransfer'))
                .map(typeKey => (
                  <option key={typeKey} value={typeKey}>
                    {REQUEST_TYPE_LABELS[typeKey]}
                  </option>
                ))}
            </select>
          </div>

          {/* Nếu chọn loại Complaint: Chọn biên bản vi phạm */}
          {requestType === 'Complaint' && (
            <div style={{ marginBottom: 18 }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
                Chọn biên bản vi phạm cần khiếu nại <span style={{ color: '#DC2626' }}>*</span>
              </label>

              {violations.length === 0 ? (
                <div style={{ padding: '14px', backgroundColor: '#F8FAFC', borderRadius: '8px', border: '1px dashed #CBD5E1', fontSize: '13px', color: '#64748B' }}>
                  Hiện tại bạn không có biên bản vi phạm nào cần khiếu nại trong hạn 24 giờ (hoặc biên bản đã được gửi đơn khiếu nại).
                </div>
              ) : (
                <>
                  <select
                    value={selectedViolationId}
                    onChange={e => setSelectedViolationId(Number(e.target.value))}
                    style={{
                      width: '100%',
                      padding: '10px 14px',
                      borderRadius: '8px',
                      border: '1px solid #CBD5E1',
                      fontSize: '13px',
                      backgroundColor: '#FFF',
                    }}
                    required
                  >
                    {violations.map(v => (
                      <option key={v.id} value={v.id}>
                        {v.code} - {v.violationTypeName} ({formatDate(v.occurrenceTime)})
                      </option>
                    ))}
                  </select>

                    {selectedViolation && (
                    <div style={{ marginTop: 10, padding: '12px', backgroundColor: '#FFF7ED', borderRadius: '8px', border: '1px solid #FED7AA', fontSize: '12px', lineHeight: 1.6 }}>
                      <div><strong>Hành vi vi phạm:</strong> {selectedViolation.violationTypeName}</div>
                      <div><strong>Phòng:</strong> P.{selectedViolation.roomNumber} ({selectedViolation.blockName})</div>
                      <div><strong>Mức độ đề xuất:</strong> <span style={{ color: '#EA580C', fontWeight: 700 }}>{selectedViolation.initialLevel || 'Chưa định mức'}</span></div>
                      <div>
                        <strong>Hạn nộp khiếu nại (24h):</strong>{' '}
                        <span style={{ color: '#C2410C' }}>{selectedViolation.requestDeadline ? formatDateTime(selectedViolation.requestDeadline) : 'Trong vòng 24h'}</span>
                        {selectedViolation.requestDeadline && (
                          <span style={{
                            marginLeft: 8,
                            fontWeight: 700,
                            color: (() => {
                              const diff = new Date(selectedViolation.requestDeadline).getTime() - now.getTime();
                              return diff < 3600000 ? '#DC2626' : '#92400E';
                            })(),
                          }}>
                            ({getTimeRemaining(selectedViolation.requestDeadline)})
                          </span>
                        )}
                      </div>
                    </div>
                  )}
                </>
              )}
            </div>
          )}

          {/* Lý do / Nội dung đơn */}
          <div style={{ marginBottom: 18 }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
              {requestType === 'Complaint' ? 'Lý do & Căn cứ khiếu nại vi phạm' : 'Lý do & Nội dung yêu cầu'} <span style={{ color: '#DC2626' }}>*</span>
            </label>
            <textarea
              rows={5}
              placeholder={
                requestType === 'Complaint'
                  ? 'Trình bày chi tiết lý do bạn cho rằng biên bản vi phạm chưa chính xác, chứng minh không tham gia hoặc xin giảm nhẹ...'
                  : 'Trình bày chi tiết nguyện vọng và thông tin cần thiết...'
              }
              style={{
                width: '100%',
                padding: '10px 14px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                fontSize: '13px',
                boxSizing: 'border-box',
                lineHeight: 1.6,
              }}
              value={content}
              onChange={e => setContent(e.target.value)}
              required
            />
          </div>

          {/* Đính kèm ảnh minh chứng */}
          <div style={{ marginBottom: 20 }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
              Ảnh minh chứng đính kèm (tối đa 5 ảnh)
            </label>
            <input
              type="file"
              accept="image/*"
              multiple
              onChange={handleFileChange}
              style={{ fontSize: '13px' }}
            />
            {files.length > 0 && (
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: 10 }}>
                {files.map((f, idx) => (
                  <span key={idx} style={{ padding: '4px 10px', backgroundColor: '#F1F5F9', borderRadius: '6px', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                    {f.name}
                    <button type="button" onClick={() => handleRemoveFile(idx)} style={{ border: 'none', background: 'none', cursor: 'pointer', color: '#EF4444', fontWeight: 'bold' }}>✕</button>
                  </span>
                ))}
              </div>
            )}
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
            <button
              type="submit"
              disabled={submitting || (requestType === 'Complaint' && violations.length === 0)}
              style={{
                padding: '10px 24px',
                backgroundColor: '#EA580C',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '13px',
                cursor: (submitting || (requestType === 'Complaint' && violations.length === 0)) ? 'not-allowed' : 'pointer',
                opacity: (submitting || (requestType === 'Complaint' && violations.length === 0)) ? 0.6 : 1,
              }}
            >
              {submitting ? 'Đang gửi đơn...' : 'Gửi đơn yêu cầu'}
            </button>
          </div>
        </form>
      </div>
      )}
    </div>
  );
};
