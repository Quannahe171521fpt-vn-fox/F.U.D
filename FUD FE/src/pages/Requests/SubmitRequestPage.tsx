import React, { useState, useEffect, useMemo } from 'react';
import {
  violationApi,
  requestApi,
  type ViolationItem,
  type BedTransferContextDto,
} from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { formatDateTime, formatDate } from '../../utils/dateUtils';
import '../../styles/violation.css';

// ── Mapping chuẩn CHECK CONSTRAINT DB CSDL FUD ──────────────────
// CONSTRAINT CHK_Request_Type CHECK (RequestType IN ('BedTransfer','Checkout','Complaint','Others'))
export type DBRequestType = 'BedTransfer' | 'Checkout' | 'Complaint' | 'Others';

export const REQUEST_TYPE_LABELS: Record<DBRequestType, string> = {
  BedTransfer: 'Yêu cầu chuyển giường / đổi phòng (Bed Transfer)',
  Checkout: 'Yêu cầu trả phòng / rời KTX (Checkout)',
  Complaint: 'Khiếu nại / Kháng cáo vi phạm (Complaint)',
  Others: 'Yêu cầu & Kiến nghị khác (Others)',
};

// CONSTRAINT CHK_Request_Status CHECK (Status IN ('Pending','Approved','Completed','Rejected','Expired'))
export type DBRequestStatus =
  | 'Pending'
  | 'Approved'
  | 'Completed'
  | 'Rejected'
  | 'Expired';

export const REQUEST_STATUS_LABELS: Record<string, string> = {
  'Pending': 'Đang chờ xét duyệt',
  'Approved': 'Đã phê duyệt chấp thuận',
  'Completed': 'Đã hoàn tất xử lý',
  'Rejected': 'Đã từ chối đơn',
  'Expired': 'Đã hết hạn xử lý',
};

export const REQUEST_STATUS_COLORS: Record<string, { bg: string; color: string }> = {
  'Pending': { bg: '#EFF6FF', color: '#1D4ED8' },
  'Approved': { bg: '#ECFDF5', color: '#047857' },
  'Completed': { bg: '#DCFCE7', color: '#15803D' },
  'Rejected': { bg: '#FEE2E2', color: '#B91C1C' },
  'Expired': { bg: '#F8FAFC', color: '#64748B' },
};

interface SubmitRequestPageProps {
  onSubmitted?: () => void;
}

export const SubmitRequestPage: React.FC<SubmitRequestPageProps> = ({ onSubmitted }) => {
  const { currentUser } = useAuth();
  const isSuspended = currentUser?.accountStatus === 'Suspended';
  const [requestType, setRequestType] = useState<DBRequestType>(() => isSuspended ? 'Complaint' : 'BedTransfer');
  const [violations, setViolations] = useState<ViolationItem[]>([]);
  const [selectedViolationId, setSelectedViolationId] = useState<number | ''>('');
  const [transferContext, setTransferContext] = useState<BedTransferContextDto | null>(null);
  const [loadingTransferContext, setLoadingTransferContext] = useState(false);
  const [selectedDormFilter, setSelectedDormFilter] = useState<string>('');
  const [selectedBlockFilter, setSelectedBlockFilter] = useState<string>('');
  const [selectedToBedId, setSelectedToBedId] = useState<number | ''>('');
  const [transferDate, setTransferDate] = useState<string>('');
  const [content, setContent] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  useEffect(() => {
    if (requestType === 'BedTransfer' && !isSuspended) {
      loadBedTransferContext();
    }
  }, [requestType, isSuspended]);

  const loadBedTransferContext = async () => {
    setLoadingTransferContext(true);
    try {
      const ctx = await requestApi.getBedTransferContext();
      setTransferContext(ctx);
      setSelectedDormFilter('');
      setSelectedBlockFilter('');
      if (ctx.eligible && ctx.availableBeds.length > 0) {
        setSelectedToBedId(ctx.availableBeds[0].bedId);
      } else {
        setSelectedToBedId('');
      }
    } catch (err: any) {
      setTransferContext({
        eligible: false,
        ineligibleReason: err.message || 'Không thể tải thông tin chuyển giường.',
        availableBeds: [],
      });
      setSelectedToBedId('');
    } finally {
      setLoadingTransferContext(false);
    }
  };

  const dormOptions = useMemo(() => {
    if (!transferContext?.availableBeds) return [];
    const map = new Map<string, string>();
    transferContext.availableBeds.forEach(b => map.set(b.dormCode, b.dormName));
    return Array.from(map.entries()).map(([code, name]) => ({ code, name }));
  }, [transferContext]);

  const blockOptions = useMemo(() => {
    if (!transferContext?.availableBeds) return [];
    const map = new Map<string, string>();
    transferContext.availableBeds
      .filter(b => !selectedDormFilter || b.dormCode === selectedDormFilter)
      .forEach(b => map.set(b.blockCode, b.blockName));
    return Array.from(map.entries()).map(([code, name]) => ({ code, name }));
  }, [transferContext, selectedDormFilter]);

  const filteredAvailableBeds = useMemo(() => {
    if (!transferContext?.availableBeds) return [];
    return transferContext.availableBeds.filter(b => {
      if (selectedDormFilter && b.dormCode !== selectedDormFilter) return false;
      if (selectedBlockFilter && b.blockCode !== selectedBlockFilter) return false;
      return true;
    });
  }, [transferContext, selectedDormFilter, selectedBlockFilter]);

  useEffect(() => {
    if (requestType !== 'BedTransfer') return;
    if (filteredAvailableBeds.length === 0) {
      setSelectedToBedId('');
      return;
    }
    if (!filteredAvailableBeds.some(b => b.bedId === selectedToBedId)) {
      setSelectedToBedId(filteredAvailableBeds[0].bedId);
    }
  }, [filteredAvailableBeds, requestType, selectedToBedId]);

  const selectedTargetBed = useMemo(
    () => transferContext?.availableBeds.find(b => b.bedId === selectedToBedId),
    [transferContext, selectedToBedId]
  );

  // Cập nhật tự động loại bỏ biên bản hết hạn mỗi giây
  useEffect(() => {
    const tick = setInterval(() => {
      const currentNow = new Date();
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

    // Đếm số từ trong nội dung đơn
    const wordCount = content.trim() ? content.trim().split(/\s+/).length : 0;
    if (wordCount > 200) {
      setErrorMsg(`Nội dung đơn không được vượt quá 200 từ (hiện tại: ${wordCount} từ).`);
      return;
    }

    if (requestType === 'Complaint') {
      if (!content.trim()) {
        setErrorMsg('Vui lòng nhập lý do và căn cứ khiếu nại.');
        return;
      }

      setSubmitting(true);
      try {
        const formData = new FormData();
        formData.append('requestType', 'Complaint');
        formData.append('targetType', selectedViolationId ? 'Violation' : 'General');
        if (selectedViolationId) {
          formData.append('targetViolationId', selectedViolationId.toString());
        }
        formData.append('content', content.trim());
        formData.append('reason', content.trim());
        files.forEach(f => formData.append('evidences', f));

        await requestApi.create(formData);
        setSuccessMsg('Đã tạo đơn khiếu nại (Type: Complaint) thành công!');
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
    } else if (requestType === 'BedTransfer') {
      if (!transferContext?.eligible) {
        setErrorMsg(transferContext?.ineligibleReason || 'Bạn chưa đủ điều kiện gửi đơn chuyển giường.');
        return;
      }
      if (!selectedToBedId) {
        setErrorMsg('Vui lòng chọn giường đích muốn chuyển đến.');
        return;
      }
      if (!content.trim()) {
        setErrorMsg('Vui lòng nhập lý do chuyển giường.');
        return;
      }

      setSubmitting(true);
      try {
        const formData = new FormData();
        formData.append('requestType', 'BedTransfer');
        formData.append('toBedId', selectedToBedId.toString());
        if (transferDate) {
          formData.append('transferDate', transferDate);
        }
        formData.append('reason', content.trim());

        await requestApi.create(formData);
        setSuccessMsg(`Đã gửi đơn chuyển giường thành công với trạng thái: ${REQUEST_STATUS_LABELS['Pending']}!`);
        setContent('');
        setTransferDate('');
        setFiles([]);
        loadBedTransferContext();
        if (onSubmitted) {
          setTimeout(() => onSubmitted(), 1200);
        }
      } catch (err: any) {
        setErrorMsg(err.message || 'Lỗi khi gửi đơn chuyển giường');
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

  return (
    <div style={{ padding: '24px 32px', display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
      <div style={{ marginBottom: 20, width: '100%', maxWidth: '750px', textAlign: 'left' }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Tạo đơn / Gửi yêu cầu
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Hệ thống tiếp nhận yêu cầu chuyển giường, trả phòng, khiếu nại vi phạm (Complaint) và kiến nghị
        </p>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error" style={{ width: '100%', maxWidth: '750px', boxSizing: 'border-box' }}>{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success" style={{ width: '100%', maxWidth: '750px', boxSizing: 'border-box' }}>{successMsg}</div>}

      {isSuspended ? (
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #FCA5A5', padding: '32px', width: '100%', maxWidth: '750px', textAlign: 'center', boxSizing: 'border-box' }}>
          <div style={{ fontSize: '48px', marginBottom: 12 }}>🚫</div>
          <h2 style={{ fontSize: '18px', fontWeight: 700, color: '#991B1B', margin: '0 0 8px' }}>
            Quyền gửi đơn đã bị khóa
          </h2>
          <p style={{ color: '#475569', fontSize: '14px', lineHeight: 1.6, maxWidth: '580px', margin: '0 auto 16px' }}>
            Tài khoản của bạn đã có kết luận chính thức <strong>Đình chỉ nội trú / Buộc rời ký túc xá</strong>. Toàn bộ tính năng đặt phòng, giữ phòng và gửi đơn yêu cầu (Request) đã bị tạm khóa.
          </p>
          <div style={{ display: 'inline-block', backgroundColor: '#FEF2F2', padding: '10px 18px', borderRadius: '8px', border: '1px solid #FECACA', fontSize: '13px', color: '#B91C1C' }}>
            Lý do: {currentUser?.suspensionReason || 'Vi phạm quy chế kỷ luật nội trú KTX.'}
          </div>
          <div style={{ marginTop: 20, fontSize: '13px', color: '#64748B' }}>
            Vui lòng liên hệ trực tiếp Ban Quản lý ký túc xá để được xem xét mở lại tài khoản.
          </div>
        </div>
      ) : (
        <div style={{ backgroundColor: '#FFFFFF', borderRadius: '12px', border: '1px solid #E2E8F0', padding: '24px', width: '100%', maxWidth: '750px', boxSizing: 'border-box', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <form onSubmit={handleSubmit}>
            {/* Loại đơn: Chuẩn theo CHECK CONSTRAINT CSDL */}
            <div style={{ marginBottom: 18 }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
                Chọn loại đơn yêu cầu (Request Type) <span style={{ color: '#DC2626' }}>*</span>
              </label>
              {isSuspended && (
                <div style={{ marginBottom: 14, padding: '10px 14px', backgroundColor: '#FEF2F2', border: '1px solid #FECACA', borderRadius: '8px', color: '#991B1B', fontSize: '13px' }}>
                  <strong>Tài khoản đang bị đình chỉ:</strong> Chức năng đăng ký giữ phòng, đặt phòng và chuyển phòng đã bị khóa cho đến khi được Ban Quản lý mở lại.
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
                  .filter(typeKey => !isSuspended || typeKey !== 'BedTransfer')
                  .map(typeKey => (
                    <option key={typeKey} value={typeKey}>
                      {REQUEST_TYPE_LABELS[typeKey]}
                    </option>
                  ))}
              </select>
            </div>

            {/* Nếu chọn loại BedTransfer: Hiển thị chỗ ở hiện tại & chọn giường trống đích */}
            {requestType === 'BedTransfer' && (
              <div style={{ marginBottom: 18 }}>
                {loadingTransferContext ? (
                  <div style={{ padding: '14px', backgroundColor: '#F8FAFC', borderRadius: '8px', border: '1px solid #E2E8F0', fontSize: '13px', color: '#64748B' }}>
                    Đang kiểm tra điều kiện cư trú và tải danh sách giường trống...
                  </div>
                ) : (
                  <>
                    {transferContext?.currentResidency && (
                      <div style={{ padding: '14px', backgroundColor: '#F8FAFC', borderRadius: '8px', border: '1px solid #CBD5E1', marginBottom: 14, fontSize: '13px', lineHeight: 1.7 }}>
                        <div style={{ fontWeight: 700, color: '#0F172A', marginBottom: 4 }}>
                          Chỗ ở hiện tại ({transferContext.currentResidency.termName} {transferContext.currentResidency.academicYear})
                        </div>
                        <div>
                          <strong>Vị trí:</strong> {transferContext.currentResidency.dormName} — {transferContext.currentResidency.blockName} — P.{transferContext.currentResidency.roomNumber} — Giường <strong>{transferContext.currentResidency.bedCode}</strong>
                          {transferContext.currentResidency.positionInRoom ? ` (${transferContext.currentResidency.positionInRoom})` : ''}
                        </div>
                        <div>
                          <strong>Loại phòng:</strong> {transferContext.currentResidency.roomTypeName}
                          {transferContext.currentResidency.currentTermPrice != null && (
                            <span style={{ marginLeft: 12 }}>
                              <strong>Đơn giá kỳ:</strong> {transferContext.currentResidency.currentTermPrice.toLocaleString('vi-VN')} đ
                            </span>
                          )}
                        </div>
                      </div>
                    )}

                    {!transferContext?.eligible ? (
                      <div style={{ padding: '14px', backgroundColor: '#FEF2F2', borderRadius: '8px', border: '1px solid #FECACA', fontSize: '13px', color: '#991B1B' }}>
                        <strong>Không thể gửi đơn chuyển giường:</strong> {transferContext?.ineligibleReason || 'Chưa đủ điều kiện chuyển giường.'}
                      </div>
                    ) : transferContext.availableBeds.length === 0 ? (
                      <div style={{ padding: '14px', backgroundColor: '#FFFBEB', borderRadius: '8px', border: '1px solid #FDE68A', fontSize: '13px', color: '#92400E' }}>
                        Hiện không còn giường trống phù hợp giới tính trong ký túc xá để chuyển.
                      </div>
                    ) : (
                      <>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: 12 }}>
                          <div>
                            <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, marginBottom: 4, color: '#475569' }}>
                              Lọc theo Tòa (Dorm)
                            </label>
                            <select
                              value={selectedDormFilter}
                              onChange={e => {
                                setSelectedDormFilter(e.target.value);
                                setSelectedBlockFilter('');
                              }}
                              style={{ width: '100%', padding: '8px 12px', borderRadius: '8px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                            >
                              <option value="">Tất cả tòa nhà</option>
                              {dormOptions.map(d => (
                                <option key={d.code} value={d.code}>{d.name}</option>
                              ))}
                            </select>
                          </div>

                          <div>
                            <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, marginBottom: 4, color: '#475569' }}>
                              Lọc theo Khu (Block)
                            </label>
                            <select
                              value={selectedBlockFilter}
                              onChange={e => setSelectedBlockFilter(e.target.value)}
                              style={{ width: '100%', padding: '8px 12px', borderRadius: '8px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                            >
                              <option value="">Tất cả khu</option>
                              {blockOptions.map(bl => (
                                <option key={bl.code} value={bl.code}>{bl.name}</option>
                              ))}
                            </select>
                          </div>
                        </div>

                        <div style={{ marginBottom: 12 }}>
                          <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
                            Chọn giường trống muốn chuyển đến <span style={{ color: '#DC2626' }}>*</span>
                          </label>
                          <select
                            value={selectedToBedId}
                            onChange={e => setSelectedToBedId(Number(e.target.value))}
                            style={{ width: '100%', padding: '10px 14px', borderRadius: '8px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                            required
                          >
                            {filteredAvailableBeds.map(b => (
                              <option key={b.bedId} value={b.bedId}>
                                {b.dormName} - {b.blockCode} - P.{b.roomNumber} - Giường {b.bedCode}
                                {b.positionInRoom ? ` (${b.positionInRoom})` : ''} — {b.roomTypeName}
                                {b.termPrice != null ? ` (${b.termPrice.toLocaleString('vi-VN')} đ/kỳ)` : ''}
                              </option>
                            ))}
                          </select>
                        </div>

                        {selectedTargetBed && (
                          <div style={{ padding: '12px', backgroundColor: '#ECFDF5', borderRadius: '8px', border: '1px solid #A7F3D0', fontSize: '12px', lineHeight: 1.6, marginBottom: 12, color: '#065F46' }}>
                            <div><strong>Giường đích đã chọn:</strong> {selectedTargetBed.dormName} — {selectedTargetBed.blockName} — P.{selectedTargetBed.roomNumber} — Giường <strong>{selectedTargetBed.bedCode}</strong></div>
                            <div>
                              <strong>Loại phòng:</strong> {selectedTargetBed.roomTypeName}
                              {selectedTargetBed.termPrice != null && (
                                <span style={{ marginLeft: 12 }}>
                                  <strong>Đơn giá kỳ:</strong> {selectedTargetBed.termPrice.toLocaleString('vi-VN')} đ
                                </span>
                              )}
                            </div>
                            {selectedTargetBed.termPrice != null && transferContext.currentResidency?.currentTermPrice != null && (
                              <div>
                                <strong>Chênh lệch đơn giá kỳ tham khảo:</strong>{' '}
                                {(selectedTargetBed.termPrice - transferContext.currentResidency.currentTermPrice).toLocaleString('vi-VN')} đ
                                {' '}(Mức chênh lệch thực tế sẽ được Ban Quản lý tính theo số ngày ở thực tế khi duyệt đơn)
                              </div>
                            )}
                          </div>
                        )}

                        <div>
                          <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
                            Ngày dự kiến chuyển (tùy chọn)
                          </label>
                          <input
                            type="date"
                            value={transferDate}
                            min={new Date().toISOString().split('T')[0]}
                            max={transferContext.currentResidency?.termEndDate}
                            onChange={e => setTransferDate(e.target.value)}
                            style={{ padding: '8px 12px', borderRadius: '8px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                          />
                        </div>
                      </>
                    )}
                  </>
                )}
              </div>
            )}

            {/* Nếu chọn loại Complaint: Chọn biên bản vi phạm hoặc khiếu nại riêng */}
            {requestType === 'Complaint' && (
              <div style={{ marginBottom: 18 }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 700, marginBottom: 6, color: '#1E293B' }}>
                  Liên kết biên bản vi phạm cần khiếu nại (Không bắt buộc)
                </label>

                {violations.length === 0 ? (
                  <div style={{ padding: '14px', backgroundColor: '#F8FAFC', borderRadius: '8px', border: '1px dashed #CBD5E1', fontSize: '13px', color: '#64748B' }}>
                    Hiện tại bạn không có biên bản vi phạm nào cần liên kết. Đơn khiếu nại sẽ được gửi dưới dạng <strong>Yêu cầu & Kiến nghị khác (Others)</strong>.
                  </div>
                ) : (
                  <>
                    <select
                      value={selectedViolationId}
                      onChange={e => setSelectedViolationId(e.target.value === '' ? '' : Number(e.target.value))}
                      style={{
                        width: '100%',
                        padding: '10px 14px',
                        borderRadius: '8px',
                        border: '1px solid #CBD5E1',
                        fontSize: '13px',
                        backgroundColor: '#FFF',
                      }}
                    >
                      <option value="">-- Không liên kết biên bản (Gửi khiếu nại riêng) --</option>
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
                        </div>
                      </div>
                    )}
                  </>
                )}
              </div>
            )}

            {/* Lý do / Nội dung đơn */}
            <div style={{ marginBottom: 18 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                <label style={{ fontSize: '13px', fontWeight: 700, color: '#1E293B' }}>
                  {requestType === 'Complaint' ? 'Lý do & Căn cứ khiếu nại vi phạm' : 'Lý do & Nội dung yêu cầu'} <span style={{ color: '#DC2626' }}>*</span>
                </label>
                <span
                  style={{
                    fontSize: '12px',
                    fontWeight: 600,
                    color: (content.trim() ? content.trim().split(/\s+/).length : 0) > 200 ? '#DC2626' : '#64748B',
                  }}
                >
                  {content.trim() ? content.trim().split(/\s+/).length : 0} / 200 từ
                </span>
              </div>
              <textarea
                rows={5}
                placeholder={
                  requestType === 'Complaint'
                    ? 'Trình bày chi tiết lý do bạn cho rằng biên bản vi phạm chưa chính xác, chứng minh không tham gia hoặc xin giảm nhẹ (tối đa 200 chữ)...'
                    : 'Trình bày chi tiết nguyện vọng và thông tin cần thiết (tối đa 200 chữ)...'
                }
                style={{
                  width: '100%',
                  padding: '10px 14px',
                  borderRadius: '8px',
                  border: `1px solid ${(content.trim() ? content.trim().split(/\s+/).length : 0) > 200 ? '#DC2626' : '#CBD5E1'}`,
                  fontSize: '13px',
                  boxSizing: 'border-box',
                  lineHeight: 1.6,
                }}
                value={content}
                onChange={e => setContent(e.target.value)}
                required
              />
              {(content.trim() ? content.trim().split(/\s+/).length : 0) > 200 && (
                <div style={{ color: '#DC2626', fontSize: '12px', marginTop: 4 }}>
                  Nội dung đã vượt quá giới hạn 200 từ. Vui lòng rút gọn lại để gửi đơn.
                </div>
              )}
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
                disabled={
                  submitting
                  || (requestType === 'Complaint' && violations.length === 0)
                  || (requestType === 'BedTransfer' && (loadingTransferContext || !transferContext?.eligible || !selectedToBedId))
                }
                style={{
                  padding: '10px 24px',
                  backgroundColor: '#EA580C',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: (
                    submitting
                    || (requestType === 'Complaint' && violations.length === 0)
                    || (requestType === 'BedTransfer' && (loadingTransferContext || !transferContext?.eligible || !selectedToBedId))
                  ) ? 'not-allowed' : 'pointer',
                  opacity: (
                    submitting
                    || (requestType === 'Complaint' && violations.length === 0)
                    || (requestType === 'BedTransfer' && (loadingTransferContext || !transferContext?.eligible || !selectedToBedId))
                  ) ? 0.6 : 1,
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
