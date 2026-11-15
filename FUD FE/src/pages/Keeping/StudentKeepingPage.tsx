import React, { useState, useEffect, useCallback } from 'react';
import {
  keepingApi,
  type KeepingEligibility,
  type KeepingRequestResult,
} from '../../services/api';
import { formatDate, formatDateTime } from '../../utils/dateUtils';
import '../../styles/violation.css';

const REASON_HINTS: Record<string, string> = {
  NOT_RESIDENT: 'Bạn hiện không có lưu trú đang hoạt động ở học kỳ hiện tại nên không thể giữ phòng.',
  PERIOD_NOT_OPEN: 'Hiện chưa có kỳ giữ phòng nào đang mở cho học kỳ kế tiếp.',
  PRICE_NOT_PUBLISHED: 'Học kỳ kế tiếp chưa công bố bảng giá. Vui lòng quay lại sau.',
  HAS_UNPAID_INVOICE: 'Bạn còn hóa đơn chưa thanh toán. Hãy thanh toán trước khi giữ phòng.',
  HAS_OVERDUE_INVOICE: 'Bạn còn hóa đơn quá hạn. Hãy thanh toán trước khi giữ phòng.',
  ALREADY_KEPT: 'Bạn đã giữ phòng cho học kỳ kế tiếp.',
};

const formatMoney = (amount: number, currency = 'VND') =>
  `${amount.toLocaleString('vi-VN')} ${currency === 'VND' ? 'đ' : currency}`;

const card: React.CSSProperties = {
  backgroundColor: '#FFFFFF',
  borderRadius: '12px',
  border: '1px solid #E2E8F0',
  padding: '20px 24px',
  marginBottom: 16,
  maxWidth: '760px',
};

const cardTitle: React.CSSProperties = {
  fontSize: '12px',
  fontWeight: 700,
  color: '#C2410C',
  textTransform: 'uppercase',
  marginBottom: 10,
};

const infoGrid: React.CSSProperties = {
  display: 'grid',
  gridTemplateColumns: '1fr 1fr',
  gap: '6px 16px',
  fontSize: '13px',
  lineHeight: 1.8,
};

export const StudentKeepingPage: React.FC = () => {
  const [eligibility, setEligibility] = useState<KeepingEligibility | null>(null);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<KeepingRequestResult | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      setEligibility(await keepingApi.getEligibility());
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể kiểm tra điều kiện giữ phòng');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const handleSubmit = async () => {
    if (!agreed) {
      setErrorMsg('Bạn cần đồng ý với nội quy ký túc xá trước khi giữ phòng.');
      return;
    }
    if (!window.confirm('Xác nhận giữ phòng cho học kỳ kế tiếp?')) return;
    setSubmitting(true);
    setErrorMsg('');
    try {
      setResult(await keepingApi.create(true));
      await load();
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi giữ phòng');
      await load();
    } finally {
      setSubmitting(false);
    }
  };

  const stay = eligibility?.currentStay;
  const next = eligibility?.nextTerm;
  const fee = eligibility?.fee;
  const period = eligibility?.period;

  return (
    <div style={{ padding: '24px 32px' }}>
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Giữ phòng học kỳ kế tiếp
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Giữ lại giường đang ở cho học kỳ tiếp theo trong thời gian kỳ giữ phòng mở
        </p>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}

      {result && (
        <div className="violation-alert violation-alert-success" style={{ maxWidth: 760 }}>
          <div style={{ fontWeight: 700, marginBottom: 4 }}>Giữ phòng thành công!</div>
          <div>Mã yêu cầu: #{result.requestId} · Trạng thái: {result.status}</div>
          {result.bed && (
            <div>
              Giường: {result.bed.bedCode} – Phòng {result.bed.roomNumber}, {result.bed.blockName}, {result.bed.dormName}
            </div>
          )}
          {result.nextTerm && <div>Học kỳ: {result.nextTerm.name} ({result.nextTerm.academicYear})</div>}
          {result.feeAmount != null && <div>Phí giữ phòng: {formatMoney(result.feeAmount, result.currency)}</div>}
          <div>Thời điểm: {formatDateTime(result.createdAt)}</div>
        </div>
      )}

      {loading ? (
        <div style={{ padding: 30, color: '#64748B' }}>Đang kiểm tra điều kiện giữ phòng...</div>
      ) : eligibility ? (
        <>
          {/* Trạng thái đủ điều kiện */}
          <div
            style={{
              ...card,
              backgroundColor: eligibility.eligible ? '#F0FDF4' : '#FEF2F2',
              border: `1px solid ${eligibility.eligible ? '#86EFAC' : '#FCA5A5'}`,
              color: eligibility.eligible ? '#166534' : '#991B1B',
            }}
          >
            <div style={{ fontWeight: 700, fontSize: '15px', marginBottom: 4 }}>
              {eligibility.eligible ? 'Bạn đủ điều kiện giữ phòng' : 'Bạn chưa thể giữ phòng'}
            </div>
            {!eligibility.eligible && (
              <div style={{ fontSize: '13px', lineHeight: 1.6 }}>
                {(eligibility.reasonCode && REASON_HINTS[eligibility.reasonCode]) ||
                  eligibility.message ||
                  'Không đủ điều kiện giữ phòng.'}
                {eligibility.message &&
                  eligibility.reasonCode &&
                  REASON_HINTS[eligibility.reasonCode] &&
                  eligibility.message !== REASON_HINTS[eligibility.reasonCode] && (
                    <div style={{ marginTop: 4, opacity: 0.85 }}>{eligibility.message}</div>
                  )}
              </div>
            )}
          </div>

          {stay && (
            <div style={card}>
              <div style={cardTitle}>Chỗ ở hiện tại</div>
              <div style={infoGrid}>
                <div><strong>Ký túc xá:</strong> {stay.dormName}</div>
                <div><strong>Block:</strong> {stay.blockName}</div>
                <div><strong>Phòng:</strong> {stay.roomNumber}</div>
                <div><strong>Giường:</strong> {stay.bedCode}</div>
                <div><strong>Học kỳ hiện tại:</strong> {stay.currentTerm}</div>
                <div><strong>Trạng thái:</strong> {stay.status}</div>
              </div>
            </div>
          )}

          {(next || fee || period) && (
            <div style={card}>
              <div style={cardTitle}>Thông tin kỳ giữ phòng</div>
              <div style={infoGrid}>
                {next && (
                  <>
                    <div><strong>Học kỳ kế tiếp:</strong> {next.name} ({next.academicYear})</div>
                    <div><strong>Thời gian học kỳ:</strong> {formatDate(next.startDate)} – {formatDate(next.endDate)}</div>
                  </>
                )}
                {period && (
                  <div style={{ gridColumn: '1 / -1' }}>
                    <strong>Kỳ giữ phòng mở:</strong> {formatDate(period.start)} – {formatDate(period.end)}
                  </div>
                )}
                {fee && (
                  <div style={{ gridColumn: '1 / -1' }}>
                    <strong>Phí giữ phòng:</strong>{' '}
                    <span style={{ color: '#C2410C', fontWeight: 700 }}>{formatMoney(fee.amount, fee.currency)}</span>
                    {fee.months > 0 && ` (${fee.months} tháng)`}
                    {fee.description && <div style={{ color: '#64748B', fontSize: '12px' }}>{fee.description}</div>}
                  </div>
                )}
              </div>
            </div>
          )}

          {eligibility.eligible && (
            <div style={card}>
              <div style={cardTitle}>Xác nhận giữ phòng</div>
              <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8, fontSize: '13px', cursor: 'pointer', marginBottom: 14 }}>
                <input type="checkbox" checked={agreed} onChange={e => setAgreed(e.target.checked)} style={{ marginTop: 3 }} />
                Tôi đã đọc và đồng ý tuân thủ nội quy ký túc xá trong học kỳ kế tiếp.
              </label>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  type="button"
                  onClick={handleSubmit}
                  disabled={submitting || !agreed}
                  style={{
                    padding: '10px 20px',
                    backgroundColor: '#EA580C',
                    color: '#FFF',
                    border: 'none',
                    borderRadius: '6px',
                    fontWeight: 600,
                    fontSize: '13px',
                    cursor: submitting || !agreed ? 'not-allowed' : 'pointer',
                    opacity: submitting || !agreed ? 0.6 : 1,
                  }}
                >
                  {submitting ? 'Đang xử lý...' : 'Giữ phòng'}
                </button>
                <button
                  type="button"
                  onClick={load}
                  disabled={submitting}
                  style={{
                    padding: '10px 20px',
                    backgroundColor: '#F1F5F9',
                    color: '#334155',
                    border: '1px solid #CBD5E1',
                    borderRadius: '6px',
                    fontWeight: 600,
                    fontSize: '13px',
                    cursor: 'pointer',
                  }}
                >
                  Kiểm tra lại
                </button>
              </div>
            </div>
          )}
        </>
      ) : null}
    </div>
  );
};

