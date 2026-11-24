import React, { useState, useEffect, useMemo } from 'react';
import {
  bookingApi,
  navigateToStudentPayment,
  type BookingEligibilityDto,
  type BookingAvailableDormDto,
  type BookingAvailableBlockDto,
  type BookingAvailableRoomDto,
  type BookingAvailableBedDto,
  type BookingRequestItemDto,
} from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import { usePopup } from '../../components/PopupDialog';
import '../../styles/booking.css';

interface BookingPageProps {
  onGoToRequests?: () => void;
}

export const BookingPage: React.FC<BookingPageProps> = ({ onGoToRequests }) => {
  const { currentUser, impersonation } = useAuth();
  const activeUser = impersonation?.student || currentUser;
  const { showAlert, popup } = usePopup();

  const [loading, setLoading] = useState<boolean>(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [eligibility, setEligibility] = useState<BookingEligibilityDto | null>(null);

  // Cascading selections
  const [dorms, setDorms] = useState<BookingAvailableDormDto[]>([]);
  const [selectedDormId, setSelectedDormId] = useState<number | null>(null);

  const [blocks, setBlocks] = useState<BookingAvailableBlockDto[]>([]);
  const [selectedBlockId, setSelectedBlockId] = useState<number | null>(null);

  const [rooms, setRooms] = useState<BookingAvailableRoomDto[]>([]);
  const [selectedRoomId, setSelectedRoomId] = useState<number | null>(null);

  const [beds, setBeds] = useState<BookingAvailableBedDto[]>([]);
  const [selectedBedId, setSelectedBedId] = useState<number | null>(null);

  // Regulation agreement & submission
  const [agreed, setAgreed] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [submitSuccess, setSubmitSuccess] = useState<BookingRequestItemDto | null>(null);

  // 1. Check eligibility on mount
  useEffect(() => {
    loadEligibility();
  }, []);

  const loadEligibility = async () => {
    setLoading(true);
    setErrorMsg(null);
    try {
      const data = await bookingApi.getEligibility();
      setEligibility(data);

      if (data.eligible) {
        // Load dorms
        const dormList = await bookingApi.getAvailableDorms();
        setDorms(dormList);
        if (dormList.length > 0) {
          setSelectedDormId(dormList[0].dormId);
        }
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể kiểm tra điều kiện đặt phòng.');
    } finally {
      setLoading(false);
    }
  };

  // 2. Load blocks when selectedDormId changes
  useEffect(() => {
    if (!selectedDormId) {
      setBlocks([]);
      setSelectedBlockId(null);
      return;
    }

    const loadBlocks = async () => {
      try {
        const blockList = await bookingApi.getAvailableBlocks(selectedDormId);
        setBlocks(blockList);
        if (blockList.length > 0) {
          setSelectedBlockId(blockList[0].blockId);
        } else {
          setSelectedBlockId(null);
        }
      } catch (err: any) {
        console.error('Lỗi tải danh sách block:', err);
        setBlocks([]);
        setSelectedBlockId(null);
      }
    };

    loadBlocks();
  }, [selectedDormId]);

  // 3. Load rooms when selectedBlockId changes
  useEffect(() => {
    if (!selectedBlockId) {
      setRooms([]);
      setSelectedRoomId(null);
      return;
    }

    const loadRooms = async () => {
      try {
        const roomList = await bookingApi.getAvailableRooms(selectedBlockId);
        setRooms(roomList);
        if (roomList.length > 0) {
          setSelectedRoomId(roomList[0].roomId);
        } else {
          setSelectedRoomId(null);
        }
      } catch (err: any) {
        console.error('Lỗi tải danh sách phòng:', err);
        setRooms([]);
        setSelectedRoomId(null);
      }
    };

    loadRooms();
  }, [selectedBlockId]);

  // 4. Load beds when selectedRoomId changes
  useEffect(() => {
    if (!selectedRoomId) {
      setBeds([]);
      setSelectedBedId(null);
      return;
    }

    const loadBeds = async () => {
      try {
        const bedList = await bookingApi.getAvailableBeds(selectedRoomId);
        setBeds(bedList);
        if (bedList.length > 0) {
          setSelectedBedId(bedList[0].bedId);
        } else {
          setSelectedBedId(null);
        }
      } catch (err: any) {
        console.error('Lỗi tải danh sách giường:', err);
        setBeds([]);
        setSelectedBedId(null);
      }
    };

    loadBeds();
  }, [selectedRoomId]);

  // Find currently selected objects
  const selectedDorm = useMemo(() => dorms.find((d) => d.dormId === selectedDormId), [dorms, selectedDormId]);
  const selectedBlock = useMemo(() => blocks.find((b) => b.blockId === selectedBlockId), [blocks, selectedBlockId]);
  const selectedRoom = useMemo(() => rooms.find((r) => r.roomId === selectedRoomId), [rooms, selectedRoomId]);
  const selectedBed = useMemo(() => beds.find((b) => b.bedId === selectedBedId), [beds, selectedBedId]);

  // 5. Handle submission
  const handleSubmitBooking = async () => {
    if (!selectedBedId) {
      void showAlert('Vui lòng chọn giường bạn muốn đăng ký.', { title: 'Chưa chọn giường' });
      return;
    }

    if (!agreed) {
      void showAlert('Vui lòng đọc và tích đồng ý với Quy chế & Nội quy Ký túc xá.', { title: 'Chưa đồng ý nội quy' });
      return;
    }

    setSubmitting(true);
    setErrorMsg(null);
    try {
      const res = await bookingApi.createBooking({
        bedId: selectedBedId,
        agreementToRegulations: true,
      });
      setSubmitSuccess(res);
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi gửi yêu cầu đặt phòng.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="booking-page-container" style={{ textAlign: 'center', padding: '60px 20px' }}>
        <p style={{ color: '#64748B', fontSize: '15px' }}>Đang nạp thông tin kỳ đặt phòng...</p>
      </div>
    );
  }

  // Nếu không mở kỳ đặt phòng
  if (!eligibility?.isBookingPeriodOpen) {
    return (
      <div className="booking-page-container">
        <div className="booking-header">
          <h1 className="booking-title"><span>🛏️</span> Đặt phòng Ký túc xá</h1>
          <p className="booking-subtitle">Cổng đăng ký trực tuyến chỗ ở nội trú Đại học FPT</p>
        </div>

        <div className="booking-closed-banner">
          <h3>Cổng đặt phòng hiện đang đóng</h3>
          <p>
            Hiện tại không nằm trong thời gian mở đăng ký Ký túc xá.
            Vui lòng theo dõi các bản tin thông báo và email sinh viên để cập nhật lịch mở cổng cho học kỳ tiếp theo.
          </p>
        </div>
      </div>
    );
  }

  // Nếu sinh viên đã có booking hoặc không đủ điều kiện (nhưng kỳ đang mở)
  if (!eligibility.eligible && eligibility.currentBooking) {
    const booking = eligibility.currentBooking;
    return (
      <div className="booking-page-container">
        <div className="booking-header">
          <h1 className="booking-title"><span>🛏️</span> Trạng thái đặt phòng — {eligibility.nextTermName}</h1>
          <p className="booking-subtitle">Thông tin đơn đăng ký lưu trú của bạn</p>
        </div>

        <div className="booking-status-card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: '#0F172A' }}>
              Mã đơn: #{booking.requestId}
            </h2>
            <span
              className={`booking-status-badge ${
                booking.status === 'Approved' ? 'approved' : booking.status === 'Rejected' ? 'rejected' : 'pending'
              }`}
            >
              {booking.status === 'Approved'
                ? 'Đã phê duyệt'
                : booking.status === 'Rejected'
                ? 'Đã từ chối'
                : 'Đang chờ Ban Quản lý duyệt'}
            </span>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', fontSize: '14px', lineHeight: 1.6 }}>
            <div><strong>Học kỳ:</strong> {booking.termName}</div>
            <div><strong>Thời gian gửi:</strong> {new Date(booking.createdTime).toLocaleString('vi-VN')}</div>
            <div><strong>Vị trí:</strong> {booking.dormName} — {booking.blockName}</div>
            <div><strong>Phòng / Giường:</strong> Phòng {booking.roomNumber} — Giường {booking.bedCode}</div>
            {booking.termPrice && (
              <div><strong>Đơn giá học kỳ:</strong> {booking.termPrice.toLocaleString('vi-VN')} VNĐ</div>
            )}
            {booking.rejectReason && (
              <div style={{ gridColumn: 'span 2', color: '#DC2626' }}>
                <strong>Lý do từ chối:</strong> {booking.rejectReason}
              </div>
            )}
          </div>

          <div style={{ marginTop: 24, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            {(booking.status === 'Pending Payment' || booking.status === 'Pending') && (
              <button
                type="button"
                onClick={() => navigateToStudentPayment({ sourceType: 'Booking', sourceId: booking.requestId })}
                style={{
                  padding: '10px 18px',
                  backgroundColor: '#16A34A',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: '8px',
                  fontWeight: 700,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                💳 Thanh toán ngay
              </button>
            )}
            {onGoToRequests && (
              <button
                type="button"
                onClick={onGoToRequests}
                style={{
                  padding: '10px 18px',
                  backgroundColor: '#EA580C',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: '8px',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Xem tại Danh sách đơn từ
              </button>
            )}
          </div>
        </div>
      </div>
    );
  }

  // Nếu bị chặn vì lý do khác (vd: vi phạm, hoặc đã có residency)
  if (!eligibility.eligible) {
    return (
      <div className="booking-page-container">
        <div className="booking-header">
          <h1 className="booking-title"><span>🛏️</span> Đặt phòng Ký túc xá</h1>
          <p className="booking-subtitle">Học kỳ: {eligibility.nextTermName || 'Kế tiếp'}</p>
        </div>

        <div style={{ background: '#FEF2F2', border: '1px solid #F87171', borderRadius: 12, padding: 24, color: '#991B1B' }}>
          <h3 style={{ margin: '0 0 8px', fontSize: '17px', fontWeight: 700 }}>Không đủ điều kiện đặt phòng</h3>
          <p style={{ margin: 0, fontSize: '14px', lineHeight: 1.6 }}>
            {eligibility.ineligibleReason || 'Bạn hiện không thể thực hiện đặt phòng trong kỳ này.'}
          </p>
        </div>
      </div>
    );
  }

  // Màn hình hoàn tất vừa submit thành công
  if (submitSuccess) {
    const needsPayment = submitSuccess.requiresPayment && submitSuccess.invoiceId;
    return (
      <div className="booking-page-container">
        <div style={{ background: '#FFFFFF', border: '1px solid #E2E8F0', borderRadius: 14, padding: 36, textAlign: 'center', maxWidth: 600, margin: '20px auto' }}>
          <div style={{ width: 64, height: 64, borderRadius: '50%', background: needsPayment ? '#FFF7ED' : '#DCFCE7', color: needsPayment ? '#EA580C' : '#16A34A', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 32, margin: '0 auto 16px' }}>
            {needsPayment ? '💳' : '✓'}
          </div>
          <h2 style={{ fontSize: 22, fontWeight: 700, color: '#0F172A', margin: '0 0 8px' }}>
            {needsPayment ? 'Đặt phòng thành công - Vui lòng thanh toán!' : 'Đặt phòng thành công!'}
          </h2>
          <p style={{ color: '#64748B', fontSize: 14, lineHeight: 1.6, margin: '0 0 24px' }}>
            {needsPayment ? (
              <>
                Yêu cầu đặt phòng của bạn đã được tạo. Vui lòng <strong>thanh toán phí phòng</strong> để
                đơn được chuyển đến <strong>Ban Quản lý KTX</strong> xét duyệt.
                Hạn thanh toán: <strong>{new Date(submitSuccess.paymentDeadline).toLocaleString('vi-VN')}</strong>.
              </>
            ) : (
              <>
                Yêu cầu đặt phòng của bạn đã được chuyển đến <strong>Ban Quản lý KTX</strong> để thẩm định và phê duyệt.
                Bạn sẽ nhận được thông báo ngay khi hồ sơ được duyệt.
              </>
            )}
          </p>

          <div style={{ background: '#F8FAFC', borderRadius: 10, padding: 16, textAlign: 'left', fontSize: 13, lineHeight: 1.8, marginBottom: 24 }}>
            <div><strong>Mã yêu cầu:</strong> #{submitSuccess.requestId}</div>
            <div><strong>Ký túc xá:</strong> {submitSuccess.dormName} — {submitSuccess.blockName}</div>
            <div><strong>Phòng & Giường:</strong> Phòng {submitSuccess.roomNumber} — Giường {submitSuccess.bedCode}</div>
            <div><strong>Học kỳ:</strong> {submitSuccess.termName}</div>
            {submitSuccess.termPrice && (
              <div><strong>Đơn giá:</strong> {submitSuccess.termPrice.toLocaleString('vi-VN')} VNĐ / kỳ</div>
            )}
            <div>
              <strong>Trạng thái:</strong>{' '}
              <span style={{ color: '#D97706', fontWeight: 700 }}>
                {needsPayment ? 'Chờ thanh toán (Pending Payment)' : 'Đang chờ duyệt (Pending)'}
              </span>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 12, justifyContent: 'center', flexWrap: 'wrap' }}>
            {needsPayment && (
              <button
                type="button"
                onClick={() =>
                  navigateToStudentPayment({
                    invoiceId: submitSuccess.invoiceId,
                    tabFilter: 'RoomFee',
                  })
                }
                style={{
                  padding: '10px 20px',
                  background: '#EA580C',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: 8,
                  fontWeight: 600,
                  fontSize: 14,
                  cursor: 'pointer',
                }}
              >
                💳 Thanh toán ngay
              </button>
            )}
            <button
              type="button"
              onClick={() => {
                setSubmitSuccess(null);
                loadEligibility();
              }}
              style={{
                padding: '10px 20px',
                background: '#F1F5F9',
                color: '#0F172A',
                border: '1px solid #CBD5E1',
                borderRadius: 8,
                fontWeight: 600,
                fontSize: 14,
                cursor: 'pointer',
              }}
            >
              Xem trạng thái
            </button>
            {onGoToRequests && (
              <button
                type="button"
                onClick={onGoToRequests}
                style={{
                  padding: '10px 20px',
                  background: '#EA580C',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: 8,
                  fontWeight: 600,
                  fontSize: 14,
                  cursor: 'pointer',
                }}
              >
                Đến danh sách đơn từ
              </button>
            )}
          </div>
        </div>
      </div>
    );
  }

  // MÀN HÌNH CHỌN PHÒNG (Wizard: Dorm -> Block -> Room -> Bed)
  return (
    <div className="booking-page-container">
      {popup}
      <div className="booking-header">
        <h1 className="booking-title"><span>🛏️</span> Đăng ký chỗ ở KTX — {eligibility.nextTermName}</h1>
        <p className="booking-subtitle">
          Giới tính tài khoản: <strong style={{ color: '#EA580C' }}>{eligibility.studentGender === 'Female' ? 'Nữ' : 'Nam'}</strong>
          {' '}(Hệ thống chỉ hiển thị các Block và Phòng phù hợp theo giới tính của bạn)
        </p>
      </div>

      {errorMsg && (
        <div style={{ background: '#FEF2F2', border: '1px solid #F87171', borderRadius: 8, padding: '12px 16px', color: '#991B1B', marginBottom: 20, fontSize: 14 }}>
          {errorMsg}
        </div>
      )}

      <div className="booking-wizard-grid">
        {/* Main Wizard Form */}
        <div className="booking-wizard-main">
          {/* BƯỚC 1: CHỌN TÒA (DORM) */}
          <div className="wizard-step-section">
            <div className="wizard-step-header">
              <span className="wizard-step-num">1</span>
              <h3 className="wizard-step-title">Chọn Ký túc xá (Dorm)</h3>
            </div>
            {dorms.length === 0 ? (
              <p style={{ color: '#64748B', fontSize: 13 }}>Hiện không có tòa nào còn chỗ trống phù hợp.</p>
            ) : (
              <div className="booking-select-grid">
                {dorms.map((dorm) => (
                  <button
                    key={dorm.dormId}
                    type="button"
                    className={`booking-pill-btn ${selectedDormId === dorm.dormId ? 'active' : ''}`}
                    onClick={() => setSelectedDormId(dorm.dormId)}
                  >
                    <span className="booking-pill-name">{dorm.dormName}</span>
                    <span className="booking-pill-sub">{dorm.totalAvailableBeds} giường trống</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* BƯỚC 2: CHỌN BLOCK */}
          <div className="wizard-step-section">
            <div className="wizard-step-header">
              <span className="wizard-step-num">2</span>
              <h3 className="wizard-step-title">
                Chọn Block ({eligibility.studentGender === 'Female' ? 'Khu Nữ' : 'Khu Nam'})
              </h3>
            </div>
            {blocks.length === 0 ? (
              <p style={{ color: '#64748B', fontSize: 13 }}>Không có Block nào còn giường trống trong tòa này.</p>
            ) : (
              <div className="booking-select-grid">
                {blocks.map((block) => (
                  <button
                    key={block.blockId}
                    type="button"
                    className={`booking-pill-btn ${selectedBlockId === block.blockId ? 'active' : ''}`}
                    onClick={() => setSelectedBlockId(block.blockId)}
                  >
                    <span className="booking-pill-name">{block.blockName}</span>
                    <span className="booking-pill-sub">
                      {block.genderType === 'Female' ? 'Dành cho Nữ' : 'Dành cho Nam'} · {block.totalAvailableBeds} chỗ
                    </span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* BƯỚC 3: CHỌN PHÒNG */}
          <div className="wizard-step-section">
            <div className="wizard-step-header">
              <span className="wizard-step-num">3</span>
              <h3 className="wizard-step-title">Chọn Phòng</h3>
            </div>
            {rooms.length === 0 ? (
              <p style={{ color: '#64748B', fontSize: 13 }}>Chưa có phòng khả dụng trong block này.</p>
            ) : (
              <div className="booking-select-grid">
                {rooms.map((room) => (
                  <button
                    key={room.roomId}
                    type="button"
                    className={`booking-pill-btn ${selectedRoomId === room.roomId ? 'active' : ''}`}
                    onClick={() => setSelectedRoomId(room.roomId)}
                  >
                    <span className="booking-pill-name">Phòng {room.roomNumber}</span>
                    <span className="booking-pill-sub">
                      {room.roomTypeName || `${room.bedCapacity} giường`} · Tầng {room.floorLevel || 1}
                    </span>
                    <span style={{ fontSize: 11, color: '#16A34A', fontWeight: 600 }}>
                      Còn {room.freeBeds} giường
                    </span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* BƯỚC 4: CHỌN GIƯỜNG */}
          <div className="wizard-step-section">
            <div className="wizard-step-header">
              <span className="wizard-step-num">4</span>
              <h3 className="wizard-step-title">Chọn Giường</h3>
            </div>
            {beds.length === 0 ? (
              <p style={{ color: '#64748B', fontSize: 13 }}>Phòng này hiện đã hết giường trống.</p>
            ) : (
              <div className="bed-selection-grid">
                {beds.map((bed) => (
                  <div
                    key={bed.bedId}
                    className={`bed-card-item ${selectedBedId === bed.bedId ? 'selected' : ''}`}
                    onClick={() => setSelectedBedId(bed.bedId)}
                  >
                    <div className="bed-icon">🛏️</div>
                    <div className="bed-code">Giường {bed.bedCode}</div>
                    <div className="bed-pos">{bed.positionInRoom || 'Vị trí tiêu chuẩn'}</div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* HIỂN THỊ GIÁ TIỀN PHÒNG / HỌC KỲ */}
          {selectedBed && (
            <div className="booking-price-banner">
              <div>
                <div className="booking-price-label">ĐƠN GIÁ LƯU TRÚ KỲ {eligibility.nextTermName?.toUpperCase()}</div>
                <div style={{ fontSize: 12, color: '#7C2D12', marginTop: 2 }}>
                  Áp dụng cho Giường {selectedBed.bedCode} · Phòng {selectedRoom?.roomNumber}
                </div>
              </div>
              <div className="booking-price-value">
                {selectedBed.termPrice ? `${selectedBed.termPrice.toLocaleString('vi-VN')} VNĐ` : 'Chưa thiết lập giá'}
              </div>
            </div>
          )}

          {/* ĐỒNG Ý NỘI QUY */}
          <div className="regulations-check-container">
            <input
              type="checkbox"
              id="chkRegulations"
              checked={agreed}
              onChange={(e) => setAgreed(e.target.checked)}
            />
            <label htmlFor="chkRegulations" className="regulations-check-label">
              Tôi xác nhận đã đọc, hiểu rõ và cam kết chấp hành toàn bộ <strong>Nội quy & Quy chế Ký túc xá Trường Đại học FPT</strong>. Tôi cam đoan thông tin đăng ký là hoàn toàn chính xác.
            </label>
          </div>

          {/* NÚT [BOOKING NOW] */}
          <button
            type="button"
            className="btn-booking-submit"
            disabled={!selectedBedId || !agreed || submitting}
            onClick={handleSubmitBooking}
          >
            {submitting ? 'Đang xử lý...' : '🛏️ Booking now'}
          </button>
        </div>

        {/* Sidebar Summary */}
        <div className="booking-sidebar-summary">
          <h3 className="summary-title">Tóm tắt thông tin</h3>
          <div className="summary-row">
            <span>Sinh viên:</span>
            <strong>{activeUser?.fullName} ({activeUser?.code})</strong>
          </div>
          <div className="summary-row">
            <span>Giới tính:</span>
            <strong>{eligibility.studentGender === 'Female' ? 'Nữ' : 'Nam'}</strong>
          </div>
          <div className="summary-row">
            <span>Học kỳ:</span>
            <strong>{eligibility.nextTermName}</strong>
          </div>
          <div className="summary-row">
            <span>Ký túc xá:</span>
            <strong>{selectedDorm?.dormName || '—'}</strong>
          </div>
          <div className="summary-row">
            <span>Block:</span>
            <strong>{selectedBlock?.blockName || '—'}</strong>
          </div>
          <div className="summary-row">
            <span>Phòng:</span>
            <strong>{selectedRoom ? `Phòng ${selectedRoom.roomNumber}` : '—'}</strong>
          </div>
          <div className="summary-row">
            <span>Loại phòng:</span>
            <strong>{selectedRoom?.roomTypeName || (selectedRoom ? `${selectedRoom.bedCapacity} chỗ` : '—')}</strong>
          </div>
          <div className="summary-row">
            <span>Giường chọn:</span>
            <strong style={{ color: '#EA580C' }}>{selectedBed?.bedCode || '—'}</strong>
          </div>

          <div style={{ marginTop: 20, paddingTop: 16, borderTop: '1px solid #E2E8F0', display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <span style={{ fontSize: 13, color: '#64748B', fontWeight: 600 }}>TỔNG CỘNG:</span>
            <span style={{ fontSize: 18, fontWeight: 800, color: '#C2410C' }}>
              {selectedBed?.termPrice ? `${selectedBed.termPrice.toLocaleString('vi-VN')} VNĐ` : '—'}
            </span>
          </div>

          <div style={{ marginTop: 16, background: '#F8FAFC', borderRadius: 8, padding: 12, fontSize: 12, color: '#64748B', lineHeight: 1.5 }}>
            💡 <em>Lưu ý: Sau khi nhấn <strong>Booking now</strong>, yêu cầu của bạn sẽ được chuyển sang Ban Quản lý để xét duyệt. Bạn không cần thanh toán QR pay ở giai đoạn này.</em>
          </div>
        </div>
      </div>
    </div>
  );
};
