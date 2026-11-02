import React, { useState, useEffect } from 'react';
import type {
  TermManagementItem,
  CreateTermPayload,
  SaveSchedulePayload,
} from '../../../types/management';

interface TermFormModalProps {
  isOpen: boolean;
  term: TermManagementItem | null; // null => Thêm mới, khác null => Chỉnh sửa
  initialTab?: 'info' | 'schedule';
  onClose: () => void;
  onSave: (
    termPayload: CreateTermPayload,
    termId?: number,
    schedulePayload?: SaveSchedulePayload
  ) => Promise<void>;
  onGoToPricing?: (termId: number) => void;
}

export const TermFormModal: React.FC<TermFormModalProps> = ({
  isOpen,
  term,
  initialTab = 'info',
  onClose,
  onSave,
  onGoToPricing,
}) => {
  const [activeTab, setActiveTab] = useState<'info' | 'schedule'>(initialTab);

  // Tab 1: Thông tin học kỳ
  const [name, setName] = useState<string>('Spring');
  const [academicYear, setAcademicYear] = useState<string>('2026-2027');
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');

  // Tab 2: Lịch trình đăng ký
  // Phase 1: Keeping
  const [keepingActive, setKeepingActive] = useState<boolean>(false);
  const [keepingStart, setKeepingStart] = useState<string>('');
  const [keepingEnd, setKeepingEnd] = useState<string>('');
  const [keepingId, setKeepingId] = useState<number | undefined>(undefined);

  // Phase 2: Booking
  const [bookingActive, setBookingActive] = useState<boolean>(false);
  const [bookingStart, setBookingStart] = useState<string>('');
  const [bookingEnd, setBookingEnd] = useState<string>('');
  const [bookingId, setBookingId] = useState<number | undefined>(undefined);

  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState<boolean>(false);

  useEffect(() => {
    setActiveTab(initialTab);

    if (term) {
      setName(term.name);
      setAcademicYear(term.academicYear);
      setStartDate(term.startDate);
      setEndDate(term.endDate);

      // Load Keeping phase
      if (term.keepingPeriod) {
        setKeepingId(term.keepingPeriod.id);
        setKeepingActive(term.keepingPeriod.activeFlag);
        setKeepingStart(term.keepingPeriod.startDate);
        setKeepingEnd(term.keepingPeriod.endDate);
      } else {
        setKeepingId(undefined);
        setKeepingActive(false);
        setKeepingStart('');
        setKeepingEnd('');
      }

      // Load Booking phase
      if (term.bookingPeriod) {
        setBookingId(term.bookingPeriod.id);
        setBookingActive(term.bookingPeriod.activeFlag);
        setBookingStart(term.bookingPeriod.startDate);
        setBookingEnd(term.bookingPeriod.endDate);
      } else {
        setBookingId(undefined);
        setBookingActive(false);
        setBookingStart('');
        setBookingEnd('');
      }
    } else {
      setName('Spring');
      setAcademicYear('2026-2027');
      const now = new Date();
      const currentYear = now.getFullYear();
      setStartDate(`${currentYear}-01-01`);
      setEndDate(`${currentYear}-04-30`);

      setKeepingId(undefined);
      setKeepingActive(false);
      setKeepingStart('');
      setKeepingEnd('');

      setBookingId(undefined);
      setBookingActive(false);
      setBookingStart('');
      setBookingEnd('');
    }
    setError(null);
  }, [term, isOpen, initialTab]);

  if (!isOpen) return null;

  const isPricePublished = term?.isPricePublished ?? false;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // 1. Validate Thông tin học kỳ (Tab 1)
    if (!name.trim()) {
      setError('Tên học kỳ không được để trống.');
      setActiveTab('info');
      return;
    }
    if (!academicYear.trim()) {
      setError('Năm học không được để trống (Ví dụ: 2026-2027).');
      setActiveTab('info');
      return;
    }
    if (!startDate || !endDate) {
      setError('Vui lòng chọn ngày bắt đầu và kết thúc học kỳ.');
      setActiveTab('info');
      return;
    }
    if (endDate <= startDate) {
      setError('Ngày kết thúc học kỳ phải lớn hơn ngày bắt đầu.');
      setActiveTab('info');
      return;
    }

    // 2. Validate Lịch trình đăng ký (Tab 2 - nếu có thiết lập)
    const hasKeeping = Boolean(keepingStart || keepingEnd);
    const hasBooking = Boolean(bookingStart || bookingEnd);

    if (hasKeeping) {
      if (!keepingStart || !keepingEnd) {
        setError('Đợt Giữ phòng cũ (Keeping): Vui lòng chọn cả ngày bắt đầu và ngày kết thúc.');
        setActiveTab('schedule');
        return;
      }
      if (keepingEnd <= keepingStart) {
        setError('Đợt Giữ phòng cũ (Keeping): Ngày kết thúc phải lớn hơn ngày bắt đầu.');
        setActiveTab('schedule');
        return;
      }
    }

    if (hasBooking) {
      if (!bookingStart || !bookingEnd) {
        setError('Đợt Đăng ký mới (Booking): Vui lòng chọn cả ngày bắt đầu và ngày kết thúc.');
        setActiveTab('schedule');
        return;
      }
      if (bookingEnd <= bookingStart) {
        setError('Đợt Đăng ký phòng mới (Booking): Ngày kết thúc phải lớn hơn ngày bắt đầu.');
        setActiveTab('schedule');
        return;
      }
    }

    // Quy tắc tuần tự: EndDate_Keeping <= StartDate_Booking
    if (hasKeeping && hasBooking && keepingEnd > bookingStart) {
      setError(
        'Giai đoạn Giữ phòng cũ phải kết thúc trước hoặc cùng ngày khi giai đoạn Đăng ký phòng mới bắt đầu.'
      );
      setActiveTab('schedule');
      return;
    }

    // Tuân thủ Trigger trg_RegPeriod_RequirePublishedPrice: Bảng giá phải Published mới được kích hoạt
    const willActivate = (hasKeeping && keepingActive) || (hasBooking && bookingActive);
    if (willActivate && !isPricePublished) {
      setError(
        'Không thể kích hoạt đợt đăng ký: Học kỳ chưa có bảng giá ở trạng thái Published. Vui lòng thiết lập bảng giá trước.'
      );
      setActiveTab('schedule');
      return;
    }

    try {
      setSaving(true);

      const termPayload: CreateTermPayload = {
        name: name.trim(),
        academicYear: academicYear.trim(),
        startDate,
        endDate,
      };

      let schedulePayload: SaveSchedulePayload | undefined = undefined;
      if (hasKeeping || hasBooking) {
        const periodsPayload: any[] = [];
        if (hasKeeping) {
          periodsPayload.push({
            id: keepingId,
            periodType: 'Keeping',
            startDate: keepingStart,
            endDate: keepingEnd,
            activeFlag: isPricePublished ? keepingActive : false,
          });
        }
        if (hasBooking) {
          periodsPayload.push({
            id: bookingId,
            periodType: 'Booking',
            startDate: bookingStart,
            endDate: bookingEnd,
            activeFlag: isPricePublished ? bookingActive : false,
          });
        }
        schedulePayload = {
          termId: term?.id ?? 0,
          periods: periodsPayload,
        };
      }

      await onSave(termPayload, term?.id, schedulePayload);
      onClose();
    } catch (err: any) {
      setError(err.message || 'Lỗi khi lưu thông tin học kỳ');
    } finally {
      setSaving(false);
    }
  };

  const hasConfiguredSchedule = Boolean(
    keepingStart || keepingEnd || bookingStart || bookingEnd
  );

  return (
    <div className="modal-backdrop-custom">
      <div className="term-modal-card schedule-modal-card">
        {/* Modal Header */}
        <div className="modal-header-custom">
          <div className="modal-title-group">
            <div>
              <h3 className="modal-main-title">
                {term ? `Chỉnh sửa học kỳ — ${term.displayName}` : 'Thêm học kỳ mới'}
              </h3>
              <p className="modal-sub-title">
                {term
                  ? `Thời gian học kỳ: ${term.startDate} đến ${term.endDate}`
                  : 'Thiết lập thông tin học kỳ và lịch trình mở cổng đăng ký'}
              </p>
            </div>
          </div>
          <button type="button" className="modal-close-btn" onClick={onClose} disabled={saving}>
            ✕
          </button>
        </div>

        {/* Unified Tab Switcher */}
        <div className="modal-tabs-bar">
          <button
            type="button"
            className={`modal-tab-item ${activeTab === 'info' ? 'active' : ''}`}
            onClick={() => setActiveTab('info')}
          >
            <span>Thông tin học kỳ</span>
          </button>
          <button
            type="button"
            className={`modal-tab-item ${activeTab === 'schedule' ? 'active' : ''}`}
            onClick={() => setActiveTab('schedule')}
          >
            <span>Lịch trình đăng ký (Keeping & Booking)</span>
            {hasConfiguredSchedule && (
              <span className="tab-indicator-badge">Đã thiết lập</span>
            )}
          </button>
        </div>

        <form onSubmit={handleSubmit} className="modal-form-custom">
          <div className="modal-body-custom">
            {error && (
              <div className="term-error-banner">
                <span>{error}</span>
              </div>
            )}

            {/* TAB 1: THÔNG TIN HỌC KỲ */}
            {activeTab === 'info' && (
              <div className="tab-pane-content">
                <div className="form-group-custom">
                  <label className="form-label-custom">
                    Tên học kỳ <span className="required-star">*</span>
                  </label>
                  <div className="term-name-preset-buttons">
                    {['Spring', 'Summer', 'Fall'].map((preset) => (
                      <button
                        key={preset}
                        type="button"
                        className={`preset-btn ${name === preset ? 'active' : ''}`}
                        onClick={() => setName(preset)}
                      >
                        {preset === 'Spring'
                          ? 'Spring (Xuân)'
                          : preset === 'Summer'
                          ? 'Summer (Hè)'
                          : 'Fall (Thu)'}
                      </button>
                    ))}
                  </div>
                  <input
                    type="text"
                    className="form-input-custom"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    placeholder="Ví dụ: Spring, Summer, Fall..."
                    required
                  />
                </div>

                <div className="form-group-custom">
                  <label className="form-label-custom">
                    Năm học <span className="required-star">*</span>
                  </label>
                  <input
                    type="text"
                    className="form-input-custom"
                    value={academicYear}
                    onChange={(e) => setAcademicYear(e.target.value)}
                    placeholder="Ví dụ: 2026-2027"
                    required
                  />
                  <span className="field-hint">
                    Định dạng khuyến nghị: YYYY-YYYY (ví dụ: 2026-2027)
                  </span>
                </div>

                <div className="form-row-2col">
                  <div className="form-group-custom">
                    <label className="form-label-custom">
                      Ngày bắt đầu học kỳ <span className="required-star">*</span>
                    </label>
                    <input
                      type="date"
                      className="form-input-custom"
                      value={startDate}
                      onChange={(e) => setStartDate(e.target.value)}
                      required
                    />
                  </div>

                  <div className="form-group-custom">
                    <label className="form-label-custom">
                      Ngày kết thúc học kỳ <span className="required-star">*</span>
                    </label>
                    <input
                      type="date"
                      className="form-input-custom"
                      value={endDate}
                      onChange={(e) => setEndDate(e.target.value)}
                      required
                    />
                  </div>
                </div>
              </div>
            )}

            {/* TAB 2: LỊCH TRÌNH ĐĂNG KÝ (KEEPING & BOOKING) */}
            {activeTab === 'schedule' && (
              <div className="tab-pane-content">
                {/* Warning/Success banner về Bảng giá phòng */}
                {term && !isPricePublished ? (
                  <div className="price-warning-callout">
                    <div className="callout-content">
                      <div className="callout-title">Học kỳ chưa công bố Bảng giá phòng</div>
                      <p className="callout-desc">
                        Theo quy tắc hệ thống, học kỳ bắt buộc phải có bảng giá ở trạng thái <strong>Published</strong> mới được phép kích hoạt mở đợt đăng ký cho sinh viên. Bạn có thể lưu ngày dự kiến (nháp), nhưng công tắc kích hoạt sẽ bị khóa.
                      </p>
                      {onGoToPricing && (
                        <button
                          type="button"
                          className="callout-action-btn"
                          onClick={() => {
                            onClose();
                            onGoToPricing(term.id);
                          }}
                        >
                          <span>Đi tới Thiết lập bảng giá</span>
                          <span className="arrow-icon">→</span>
                        </button>
                      )}
                    </div>
                  </div>
                ) : term && isPricePublished ? (
                  <div className="price-success-callout">
                    <div className="callout-content">
                      <div className="callout-title">Bảng giá học kỳ đã được công bố (Published)</div>
                      <p className="callout-desc">
                        Học kỳ đã sẵn sàng. Bạn có thể bật công tắc <strong>Kích hoạt</strong> để sinh viên đăng ký trong khung thời gian quy định.
                      </p>
                    </div>
                  </div>
                ) : null}

                {/* Giai đoạn 1: Giữ phòng cũ (Keeping) */}
                <div className="phase-config-card keeping-phase-card">
                  <div className="phase-card-header">
                    <div className="phase-badge-title">
                      <span className="phase-num">Giai đoạn 1</span>
                      <h4 className="phase-name">Giữ phòng cũ (Keeping)</h4>
                    </div>

                    <div
                      className="phase-toggle-control"
                      title={!isPricePublished ? 'Cần công bố bảng giá phòng trước khi kích hoạt đợt đăng ký' : ''}
                    >
                      <label className="switch-toggle-label">
                        <input
                          type="checkbox"
                          className="switch-toggle-input"
                          checked={keepingActive}
                          disabled={!isPricePublished}
                          onChange={(e) => setKeepingActive(e.target.checked)}
                        />
                        <span className={`switch-toggle-slider ${!isPricePublished ? 'disabled' : ''}`}></span>
                      </label>
                      <span className="switch-text-status">
                        {keepingActive ? (
                          <strong className="status-text-active">Đã kích hoạt</strong>
                        ) : (
                          <span className="status-text-inactive">Đang tắt</span>
                        )}
                      </span>
                    </div>
                  </div>

                  <p className="phase-description-text">
                    Dành cho <strong>sinh viên đang nội trú kỳ hiện tại</strong> muốn tiếp tục giữ nguyên giường ở kỳ mới. Hoàn tất thanh toán trong đợt này sẽ được tự động gia hạn.
                  </p>

                  <div className="form-row-2col">
                    <div className="form-group-custom">
                      <label className="form-label-custom">Ngày bắt đầu giữ phòng</label>
                      <input
                        type="date"
                        className="form-input-custom"
                        value={keepingStart}
                        onChange={(e) => setKeepingStart(e.target.value)}
                      />
                    </div>

                    <div className="form-group-custom">
                      <label className="form-label-custom">Ngày kết thúc giữ phòng</label>
                      <input
                        type="date"
                        className="form-input-custom"
                        value={keepingEnd}
                        onChange={(e) => setKeepingEnd(e.target.value)}
                      />
                    </div>
                  </div>
                </div>

                {/* Giai đoạn 2: Đăng ký phòng mới (Booking) */}
                <div className="phase-config-card booking-phase-card">
                  <div className="phase-card-header">
                    <div className="phase-badge-title">
                      <span className="phase-num">Giai đoạn 2</span>
                      <h4 className="phase-name">Đăng ký phòng mới (Booking)</h4>
                    </div>

                    <div
                      className="phase-toggle-control"
                      title={!isPricePublished ? 'Cần công bố bảng giá phòng trước khi kích hoạt đợt đăng ký' : ''}
                    >
                      <label className="switch-toggle-label">
                        <input
                          type="checkbox"
                          className="switch-toggle-input"
                          checked={bookingActive}
                          disabled={!isPricePublished}
                          onChange={(e) => setBookingActive(e.target.checked)}
                        />
                        <span className={`switch-toggle-slider ${!isPricePublished ? 'disabled' : ''}`}></span>
                      </label>
                      <span className="switch-text-status">
                        {bookingActive ? (
                          <strong className="status-text-active">Đã kích hoạt</strong>
                        ) : (
                          <span className="status-text-inactive">Đang tắt</span>
                        )}
                      </span>
                    </div>
                  </div>

                  <p className="phase-description-text">
                    Dành cho <strong>tân sinh viên và sinh viên chưa có phòng / chuyển phòng</strong>. Mở sau đợt giữ chỗ để xác định chính xác số lượng giường trống thực tế.
                  </p>

                  <div className="form-row-2col">
                    <div className="form-group-custom">
                      <label className="form-label-custom">Ngày mở đăng ký mới</label>
                      <input
                        type="date"
                        className="form-input-custom"
                        value={bookingStart}
                        onChange={(e) => setBookingStart(e.target.value)}
                      />
                    </div>

                    <div className="form-group-custom">
                      <label className="form-label-custom">Ngày đóng đăng ký mới</label>
                      <input
                        type="date"
                        className="form-input-custom"
                        value={bookingEnd}
                        onChange={(e) => setBookingEnd(e.target.value)}
                      />
                    </div>
                  </div>
                </div>

                {/* Tóm tắt quy tắc */}
                <div className="schedule-rule-box">
                  <p style={{ margin: 0, fontSize: '13px', color: '#64748b' }}>
                    Ban Quản lý có thể linh hoạt thiết lập 1 hoặc cả 2 giai đoạn (Giữ phòng cũ, Đăng ký mới) tùy theo kế hoạch tuyển sinh và tiếp nhận của KTX.
                  </p>
                </div>
              </div>
            )}
          </div>

          <div className="modal-footer-custom">
            <button
              type="button"
              className="btn-secondary-custom"
              onClick={onClose}
              disabled={saving}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="btn-primary-custom"
              disabled={saving}
            >
              {saving
                ? 'Đang lưu...'
                : term
                ? 'Lưu học kỳ & Lịch trình'
                : 'Tạo học kỳ mới'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
