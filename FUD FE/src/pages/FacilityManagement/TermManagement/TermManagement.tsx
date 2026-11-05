import React, { useState, useEffect, useCallback } from 'react';
import type {
  TermManagementItem,
  CreateTermPayload,
  SaveSchedulePayload,
} from '../../../types/management';
import { termManagementApi } from '../../../services/api';
import { TermFormModal } from './TermFormModal';
import { DeleteTermModal } from './DeleteTermModal';

interface TermManagementProps {
  onGoToPricing?: (termId: number) => void;
}

export const TermManagement: React.FC<TermManagementProps> = ({ onGoToPricing }) => {
  const [terms, setTerms] = useState<TermManagementItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [statusFilter, setStatusFilter] = useState<'All' | 'Ongoing' | 'Upcoming' | 'Ended'>('All');

  // Modals
  const [isFormOpen, setIsFormOpen] = useState<boolean>(false);
  const [editingTerm, setEditingTerm] = useState<TermManagementItem | null>(null);
  const [formInitialTab, setFormInitialTab] = useState<'info' | 'schedule'>('info');

  const [isDeleteOpen, setIsDeleteOpen] = useState<boolean>(false);
  const [deletingTerm, setDeletingTerm] = useState<TermManagementItem | null>(null);

  const loadTerms = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await termManagementApi.getAll();
      setTerms(data);
    } catch (err: any) {
      console.error(err);
      setError(err.message || 'Lỗi khi tải danh sách học kỳ');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadTerms();
  }, [loadTerms]);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => {
      setToastMessage(null);
    }, 4000);
  };

  // Handlers
  const handleSaveTerm = async (
    payload: CreateTermPayload,
    termId?: number,
    schedulePayload?: SaveSchedulePayload
  ) => {
    let targetTermId = termId;
    if (termId) {
      await termManagementApi.update(termId, payload);
    } else {
      const created = await termManagementApi.create(payload);
      targetTermId = created?.id;
    }

    if (schedulePayload && targetTermId) {
      await termManagementApi.saveSchedule(targetTermId, {
        ...schedulePayload,
        termId: targetTermId,
      });
    }

    showToast(
      termId
        ? 'Cập nhật học kỳ & lịch trình thành công!'
        : 'Tạo học kỳ mới thành công!'
    );
    await loadTerms();
  };

  const handleDeleteTerm = async (termId: number) => {
    await termManagementApi.delete(termId);
    showToast('Đã xóa học kỳ thành công!');
    await loadTerms();
  };

  // Filtered list
  const filteredTerms = terms.filter((t) => {
    const matchesSearch =
      t.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      t.academicYear.toLowerCase().includes(searchQuery.toLowerCase()) ||
      t.displayName.toLowerCase().includes(searchQuery.toLowerCase());

    const matchesStatus =
      statusFilter === 'All' || t.termStatus === statusFilter;

    return matchesSearch && matchesStatus;
  });

  // Calculate KPIs
  const currentOngoingTerm = terms.find((t) => t.termStatus === 'Ongoing');
  const activeRegistration = terms.find(
    (t) =>
      t.keepingPeriod?.statusText === 'Đang mở' ||
      t.bookingPeriod?.statusText === 'Đang mở'
  );

  const formatDateVi = (dateStr: string) => {
    if (!dateStr) return '---';
    const parts = dateStr.split('-');
    if (parts.length === 3) {
      return `${parts[2]}/${parts[1]}/${parts[0]}`;
    }
    return dateStr;
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Ongoing':
        return <span className="term-badge badge-ongoing">Đang diễn ra</span>;
      case 'Upcoming':
        return <span className="term-badge badge-upcoming">Sắp diễn ra</span>;
      case 'Ended':
        return <span className="term-badge badge-ended">Đã kết thúc</span>;
      default:
        return null;
    }
  };

  const getPeriodBadge = (period?: { startDate: string; endDate: string; activeFlag: boolean; statusText?: string } | null) => {
    if (!period) {
      return <span className="period-badge badge-none">Chưa cấu hình</span>;
    }

    switch (period.statusText) {
      case 'Đang mở':
        return <span className="period-badge badge-active">🟢 Đang mở</span>;
      case 'Chưa bắt đầu':
        return <span className="period-badge badge-pending">🔵 Chưa bắt đầu</span>;
      case 'Đã kết thúc':
        return <span className="period-badge badge-closed">⚪ Đã kết thúc</span>;
      case 'Đã tắt':
        return <span className="period-badge badge-off">🔴 Đã tắt</span>;
      default:
        return <span className="period-badge badge-none">Chưa cấu hình</span>;
    }
  };

  return (
    <div className="term-management-container">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="term-toast-alert">
          <span className="toast-icon">✨</span>
          <span className="toast-text">{toastMessage}</span>
          <button type="button" className="toast-close-btn" onClick={() => setToastMessage(null)}>
            ✕
          </button>
        </div>
      )}

      {/* Header section */}
      <div className="term-page-header">
        <div className="header-info-group">
          <h1 className="header-page-title">Cấu hình học kỳ & Lịch trình đăng ký</h1>
          <p className="header-page-sub">
            Thiết lập các học kỳ trong năm học và lịch trình 2 giai đoạn: Giữ phòng cũ (Keeping) và Đăng ký phòng mới (Booking).
          </p>
        </div>

        <button
          type="button"
          className="btn-create-term-primary"
          onClick={() => {
            setEditingTerm(null);
            setFormInitialTab('info');
            setIsFormOpen(true);
          }}
        >
          <span className="btn-icon">+</span>
          <span>Thêm học kỳ mới</span>
        </button>
      </div>

      {/* KPI Stat Cards */}
      <div className="term-kpi-grid">
        <div className="kpi-card card-blue">
          <div className="kpi-icon-wrap">🎓</div>
          <div className="kpi-content">
            <span className="kpi-label">Học kỳ đang diễn ra</span>
            <div className="kpi-value">
              {currentOngoingTerm ? currentOngoingTerm.displayName : 'Chưa có kỳ nào'}
            </div>
            <span className="kpi-subtext">
              {currentOngoingTerm
                ? `${formatDateVi(currentOngoingTerm.startDate)} — ${formatDateVi(currentOngoingTerm.endDate)}`
                : 'Đang giữa 2 kỳ học'}
            </span>
          </div>
        </div>

        <div className="kpi-card card-emerald">
          <div className="kpi-icon-wrap">⚡</div>
          <div className="kpi-content">
            <span className="kpi-label">Đợt đăng ký phòng hiện tại</span>
            <div className="kpi-value">
              {activeRegistration ? (
                activeRegistration.keepingPeriod?.statusText === 'Đang mở' &&
                activeRegistration.bookingPeriod?.statusText === 'Đang mở'
                  ? `Cả 2 đợt: ${activeRegistration.displayName}`
                  : activeRegistration.keepingPeriod?.statusText === 'Đang mở'
                  ? `Giữ phòng: ${activeRegistration.displayName}`
                  : `Đăng ký mới: ${activeRegistration.displayName}`
              ) : (
                'Không có đợt nào mở'
              )}
            </div>
            <span className="kpi-subtext">
              {activeRegistration
                ? 'Cổng đăng ký KTX đang mở cho sinh viên'
                : 'Cổng đăng ký KTX hiện đang đóng'}
            </span>
          </div>
        </div>

        <div className="kpi-card card-purple">
          <div className="kpi-icon-wrap">📚</div>
          <div className="kpi-content">
            <span className="kpi-label">Tổng số học kỳ</span>
            <div className="kpi-value">{terms.length} học kỳ</div>
            <span className="kpi-subtext">Đã cấu hình trong hệ thống KTX</span>
          </div>
        </div>
      </div>

      {/* Filters and search */}
      <div className="term-controls-bar">
        <div className="search-input-wrap">
          <span className="search-icon">🔍</span>
          <input
            type="text"
            className="search-input-field"
            placeholder="Tìm theo tên học kỳ, năm học (ví dụ: Fall, 2026-2027)..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button
              type="button"
              className="search-clear-btn"
              onClick={() => setSearchQuery('')}
            >
              ✕
            </button>
          )}
        </div>

        <div className="status-tabs-wrap">
          <button
            type="button"
            className={`filter-tab-btn ${statusFilter === 'All' ? 'active' : ''}`}
            onClick={() => setStatusFilter('All')}
          >
            Tất cả ({terms.length})
          </button>
          <button
            type="button"
            className={`filter-tab-btn ${statusFilter === 'Ongoing' ? 'active' : ''}`}
            onClick={() => setStatusFilter('Ongoing')}
          >
            Đang diễn ra ({terms.filter((t) => t.termStatus === 'Ongoing').length})
          </button>
          <button
            type="button"
            className={`filter-tab-btn ${statusFilter === 'Upcoming' ? 'active' : ''}`}
            onClick={() => setStatusFilter('Upcoming')}
          >
            Sắp tới ({terms.filter((t) => t.termStatus === 'Upcoming').length})
          </button>
          <button
            type="button"
            className={`filter-tab-btn ${statusFilter === 'Ended' ? 'active' : ''}`}
            onClick={() => setStatusFilter('Ended')}
          >
            Đã kết thúc ({terms.filter((t) => t.termStatus === 'Ended').length})
          </button>
        </div>
      </div>

      {/* Main Table */}
      {loading ? (
        <div className="term-loading-box">
          <div className="loading-spinner"></div>
          <p>Đang tải danh sách học kỳ và cấu hình lịch trình...</p>
        </div>
      ) : error ? (
        <div className="term-error-box">
          <span className="error-icon">❌</span>
          <p>{error}</p>
          <button type="button" className="btn-retry" onClick={loadTerms}>
            Thử lại
          </button>
        </div>
      ) : filteredTerms.length === 0 ? (
        <div className="term-empty-state">
          <div className="empty-icon">📂</div>
          <h3>Không tìm thấy học kỳ nào</h3>
          <p>
            {searchQuery || statusFilter !== 'All'
              ? 'Không có học kỳ nào khớp với bộ lọc tìm kiếm.'
              : 'Chưa có học kỳ nào được thiết lập. Hãy tạo học kỳ đầu tiên!'}
          </p>
          {(!searchQuery && statusFilter === 'All') && (
            <button
              type="button"
              className="btn-create-term-primary"
              onClick={() => {
                setEditingTerm(null);
                setFormInitialTab('info');
                setIsFormOpen(true);
              }}
            >
              + Thêm học kỳ mới
            </button>
          )}
        </div>
      ) : (
        <div className="term-table-wrapper">
          <table className="term-custom-table">
            <thead>
              <tr>
                <th style={{ width: '22%' }}>Học kỳ & Năm học</th>
                <th style={{ width: '18%' }}>Thời gian học kỳ</th>
                <th style={{ width: '16%' }}>Bảng giá phòng</th>
                <th style={{ width: '18%' }}>Giai đoạn 1: Giữ phòng cũ</th>
                <th style={{ width: '18%' }}>Giai đoạn 2: Đăng ký mới</th>
                <th style={{ width: '8%', textAlign: 'center' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredTerms.map((term) => (
                <tr key={term.id} className="term-table-row">
                  {/* Cột 1: Tên kỳ & Năm học */}
                  <td>
                    <div className="term-name-group">
                      <span className="term-name-text">{term.displayName}</span>
                      <div className="term-status-tags">
                        {getStatusBadge(term.termStatus)}
                      </div>
                    </div>
                  </td>

                  {/* Cột 2: Thời gian học kỳ */}
                  <td>
                    <div className="term-dates-group">
                      <span className="date-range-text">
                        {formatDateVi(term.startDate)} — {formatDateVi(term.endDate)}
                      </span>
                    </div>
                  </td>

                  {/* Cột 3: Bảng giá phòng */}
                  <td>
                    <div className="price-status-group">
                      {term.priceListStatus === 'Published' ? (
                        <span className="price-tag-published" title="Bảng giá đã công bố và có thể mở đăng ký">
                          ✓ Đã công bố
                        </span>
                      ) : (
                        <div className="price-tag-draft-group">
                          <span className="price-tag-draft">
                            {term.priceListStatus === 'Draft' ? 'Bản nháp' : 'Chưa tạo'}
                          </span>
                          {onGoToPricing && (
                            <button
                              type="button"
                              className="link-btn-pricing"
                              onClick={() => onGoToPricing(term.id)}
                            >
                              Cấu hình giá →
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </td>

                  {/* Cột 4: Đợt Giữ phòng cũ */}
                  <td>
                    <div
                      className="period-cell-content"
                      style={{ cursor: 'pointer' }}
                      title="Bấm để cấu hình đợt Giữ phòng cũ (Keeping)"
                      onClick={() => {
                        setEditingTerm(term);
                        setFormInitialTab('schedule');
                        setIsFormOpen(true);
                      }}
                    >
                      {getPeriodBadge(term.keepingPeriod)}
                      {term.keepingPeriod && (
                        <span className="period-dates-sub">
                          {formatDateVi(term.keepingPeriod.startDate)} — {formatDateVi(term.keepingPeriod.endDate)}
                        </span>
                      )}
                    </div>
                  </td>

                  {/* Cột 5: Đợt Đăng ký mới */}
                  <td>
                    <div
                      className="period-cell-content"
                      style={{ cursor: 'pointer' }}
                      title="Bấm để cấu hình đợt Đăng ký mới (Booking)"
                      onClick={() => {
                        setEditingTerm(term);
                        setFormInitialTab('schedule');
                        setIsFormOpen(true);
                      }}
                    >
                      {getPeriodBadge(term.bookingPeriod)}
                      {term.bookingPeriod && (
                        <span className="period-dates-sub">
                          {formatDateVi(term.bookingPeriod.startDate)} — {formatDateVi(term.bookingPeriod.endDate)}
                        </span>
                      )}
                    </div>
                  </td>

                  {/* Cột 6: Thao tác (Đã gộp ⏱️ và ✏️ thành 1 nút Chỉnh sửa duy nhất) */}
                  <td>
                    <div className="term-row-actions">
                      <button
                        type="button"
                        className="action-icon-btn btn-edit"
                        title="Chỉnh sửa thông tin học kỳ & Cấu hình lịch trình đăng ký"
                        onClick={() => {
                          setEditingTerm(term);
                          setFormInitialTab('info');
                          setIsFormOpen(true);
                        }}
                      >
                        ✏️
                      </button>

                      <button
                        type="button"
                        className={`action-icon-btn btn-delete ${term.hasDependentData ? 'disabled' : ''}`}
                        title={
                          term.hasDependentData
                            ? 'Không thể xóa kỳ đã có dữ liệu sinh viên cư trú/đăng ký'
                            : 'Xóa học kỳ'
                        }
                        onClick={() => {
                          setDeletingTerm(term);
                          setIsDeleteOpen(true);
                        }}
                      >
                        🗑️
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Unified Modals */}
      <TermFormModal
        isOpen={isFormOpen}
        term={editingTerm}
        initialTab={formInitialTab}
        onClose={() => {
          setIsFormOpen(false);
          setEditingTerm(null);
        }}
        onSave={handleSaveTerm}
        onGoToPricing={onGoToPricing}
      />

      <DeleteTermModal
        isOpen={isDeleteOpen}
        term={deletingTerm}
        onClose={() => {
          setIsDeleteOpen(false);
          setDeletingTerm(null);
        }}
        onConfirm={handleDeleteTerm}
      />
    </div>
  );
};
