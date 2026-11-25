import React, { useState } from 'react';
import type { NewsItem } from '../../types/management';
import { Pagination } from '../../components/Pagination';
import { removeVietnameseTones } from '../../utils/textUtils';

interface NewsManagementProps {
  newsList: NewsItem[];
  onSaveNews: (news: NewsItem) => void;
  onPublishNews?: (id: number) => void;
  onUnpublishNews: (id: number) => void;
}

export const NewsManagement: React.FC<NewsManagementProps> = ({
  newsList,
  onSaveNews,
  onPublishNews,
  onUnpublishNews,
}) => {
  // Filters: All ("Tất cả"), Published ("Đã đăng"), Draft ("Đã ẩn / Bản nháp")
  const [filter, setFilter] = useState<'All' | 'Published' | 'Draft'>('All');
  const [searchKeyword, setSearchKeyword] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 8;

  // Form Editor Modal State
  const [editingId, setEditingId] = useState<number | null>(null);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isEditorOpen, setIsEditorOpen] = useState(false);

  // Helper parse dd/MM/yyyy to Date
  const parseVNDate = (str?: string | null): Date | null => {
    if (!str) return null;
    const parts = str.split('/');
    if (parts.length === 3) {
      return new Date(Number(parts[2]), Number(parts[1]) - 1, Number(parts[0]));
    }
    const d = new Date(str);
    return isNaN(d.getTime()) ? null : d;
  };

  // Filtered news with advanced search (accent and unaccent)
  const filteredNews = newsList.filter((item) => {
    if (filter !== 'All' && item.status !== filter) return false;

    if (searchKeyword.trim()) {
      const term = removeVietnameseTones(searchKeyword);
      const titleNorm = removeVietnameseTones(item.title || '');
      const contentNorm = removeVietnameseTones(item.content || '');
      if (!titleNorm.includes(term) && !contentNorm.includes(term)) {
        return false;
      }
    }

    const itemDate = parseVNDate(item.publishedDate || item.createdDate);
    if (dateFrom && itemDate) {
      const from = new Date(dateFrom);
      from.setHours(0, 0, 0, 0);
      if (itemDate < from) return false;
    }
    if (dateTo && itemDate) {
      const to = new Date(dateTo);
      to.setHours(23, 59, 59, 999);
      if (itemDate > to) return false;
    }

    return true;
  });

  // Calculate pagination
  const totalPages = Math.max(1, Math.ceil(filteredNews.length / pageSize));
  const validCurrentPage = Math.min(currentPage, totalPages);
  const paginatedNews = filteredNews.slice(
    (validCurrentPage - 1) * pageSize,
    validCurrentPage * pageSize
  );

  const handleFilterChange = (newFilter: 'All' | 'Published' | 'Draft') => {
    setFilter(newFilter);
    setCurrentPage(1);
  };

  const handleResetFilters = () => {
    setSearchKeyword('');
    setDateFrom('');
    setDateTo('');
    setFilter('All');
    setCurrentPage(1);
  };

  const showNotification = (msg: string) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), 3500);
  };

  const openCreateEditor = () => {
    setEditingId(null);
    setTitle('');
    setContent('');
    setErrorMessage(null);
    setIsEditorOpen(true);
  };

  const openEditEditor = (item: NewsItem) => {
    setEditingId(item.id);
    setTitle(item.title);
    setContent(item.content);
    setErrorMessage(null);
    setIsEditorOpen(true);
  };

  const handleSave = (status: 'Published' | 'Draft') => {
    if (!title.trim()) {
      setErrorMessage('Tiêu đề tin tức không được để trống.');
      return;
    }
    if (!content.trim()) {
      setErrorMessage('Nội dung tin tức không được để trống.');
      return;
    }

    const today = new Date();
    const formattedToday = `${String(today.getDate()).padStart(2, '0')}/${String(
      today.getMonth() + 1
    ).padStart(2, '0')}/${today.getFullYear()}`;

    const existingNews = editingId ? newsList.find((n) => n.id === editingId) : null;
    const publishedDate = existingNews?.publishedDate || formattedToday;

    const newNewsItem: NewsItem = {
      id: editingId || Date.now(),
      title: title.trim(),
      content: content.trim(),
      status: status,
      publishedDate: status === 'Published' ? (publishedDate || formattedToday) : null,
    };

    onSaveNews(newNewsItem);
    setErrorMessage(null);
    showNotification(
      status === 'Published'
        ? (editingId ? 'Đã cập nhật và đăng tin thành công.' : 'Tạo mới và đăng tin tức thành công.')
        : (editingId ? 'Đã cập nhật bản tin (ở chế độ ẩn/nháp).' : 'Đã lưu bản tin ở chế độ ẩn/nháp.')
    );
    setIsEditorOpen(false);
  };

  const handlePublish = (id: number) => {
    if (window.confirm('Bạn có muốn xuất bản bản tin này để sinh viên có thể xem được không?')) {
      if (onPublishNews) {
        onPublishNews(id);
      } else {
        const item = newsList.find((n) => n.id === id);
        if (item) {
          onSaveNews({ ...item, status: 'Published' });
        }
      }
      showNotification('Đã xuất bản tin tức thành công.');
    }
  };

  const handleUnpublish = (id: number) => {
    if (window.confirm('Bản tin này sẽ được ẩn và không còn hiển thị cho sinh viên. Bạn có muốn ẩn bản tin?')) {
      onUnpublishNews(id);
      showNotification('Đã ẩn bản tin thành công. Tin tức đã được chuyển về bản nháp/ẩn.');
    }
  };

  return (
    <div className="management-page-container">
      {/* Toast Notification */}
      {successMessage && (
        <div className="mgmt-toast success-toast">
          <span className="toast-text">{successMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="mgmt-header-row">
        <div>
          <span className="mgmt-subheading">Hệ thống KTX FUD</span>
          <h1 className="mgmt-page-title">Quản lý Tin tức</h1>
        </div>
        <button
          type="button"
          className="btn-create-news"
          onClick={openCreateEditor}
        >
          + Tạo tin mới
        </button>
      </div>

      <div className="mgmt-full-grid">
        {/* Full-width News List */}
        <div className="mgmt-card list-card">
          <div className="card-header-flex">
            <h2 className="card-heading">Danh sách tin tức</h2>
            {/* Filter Chips */}
            <div className="filter-chips-group">
              <button
                type="button"
                className={`filter-chip ${filter === 'All' ? 'active' : ''}`}
                onClick={() => handleFilterChange('All')}
              >
                Tất cả ({newsList.length})
              </button>
              <button
                type="button"
                className={`filter-chip ${filter === 'Published' ? 'active' : ''}`}
                onClick={() => handleFilterChange('Published')}
              >
                Đã đăng ({newsList.filter((n) => n.status === 'Published').length})
              </button>
              <button
                type="button"
                className={`filter-chip ${filter === 'Draft' ? 'active' : ''}`}
                onClick={() => handleFilterChange('Draft')}
              >
                Đã ẩn / Bản nháp ({newsList.filter((n) => n.status === 'Draft').length})
              </button>
            </div>
          </div>

          {/* Advance Search Bar */}
          <div
            style={{
              padding: '12px 18px',
              backgroundColor: '#F8FAFC',
              borderBottom: '1px solid #E2E8F0',
              display: 'flex',
              gap: '12px',
              flexWrap: 'wrap',
              alignItems: 'center',
            }}
          >
            <div style={{ flex: '1 1 240px', minWidth: '200px' }}>
              <input
                type="text"
                placeholder="Tìm tiêu đề, nội dung (có dấu hoặc không dấu)..."
                value={searchKeyword}
                onChange={(e) => {
                  setSearchKeyword(e.target.value);
                  setCurrentPage(1);
                }}
                style={{
                  width: '100%',
                  padding: '7px 12px',
                  borderRadius: '6px',
                  border: '1px solid #CBD5E1',
                  fontSize: '13px',
                  outline: 'none',
                }}
              />
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span style={{ fontSize: '12px', color: '#64748B', fontWeight: 600 }}>Từ ngày:</span>
              <input
                type="date"
                value={dateFrom}
                onChange={(e) => {
                  setDateFrom(e.target.value);
                  setCurrentPage(1);
                }}
                style={{
                  padding: '5px 8px',
                  borderRadius: '6px',
                  border: '1px solid #CBD5E1',
                  fontSize: '12px',
                }}
              />
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span style={{ fontSize: '12px', color: '#64748B', fontWeight: 600 }}>Đến ngày:</span>
              <input
                type="date"
                value={dateTo}
                onChange={(e) => {
                  setDateTo(e.target.value);
                  setCurrentPage(1);
                }}
                style={{
                  padding: '5px 8px',
                  borderRadius: '6px',
                  border: '1px solid #CBD5E1',
                  fontSize: '12px',
                }}
              />
            </div>

            {(searchKeyword || dateFrom || dateTo || filter !== 'All') && (
              <button
                type="button"
                onClick={handleResetFilters}
                style={{
                  padding: '6px 12px',
                  borderRadius: '6px',
                  border: '1px solid #CBD5E1',
                  backgroundColor: '#FFF',
                  color: '#64748B',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Đặt lại
              </button>
            )}
          </div>

          <div className="table-responsive">
            <table className="mgmt-table">
              <thead>
                <tr>
                  <th style={{ width: '44%' }}>Tiêu đề</th>
                  <th style={{ width: '14%' }}>Trạng thái</th>
                  <th style={{ width: '15%' }}>Ngày tạo</th>
                  <th style={{ width: '15%' }}>Ngày đăng</th>
                  <th style={{ width: '12%', textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {filteredNews.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="empty-table-cell">
                      Không có tin tức nào phù hợp.
                    </td>
                  </tr>
                ) : (
                  paginatedNews.map((news) => (
                    <tr key={news.id}>
                      <td className="cell-title">
                        <span className="news-title-text" title={news.title}>
                          {news.title}
                        </span>
                      </td>
                      <td>
                        {news.status === 'Published' ? (
                          <span className="status-badge badge-published">Đang hiển thị</span>
                        ) : (
                          <span className="status-badge badge-draft">Đã ẩn / Nháp</span>
                        )}
                      </td>
                      <td className="cell-date">
                        {news.createdDate ? news.createdDate : (news.publishedDate || '—')}
                      </td>
                      <td className="cell-date">
                        {news.publishedDate ? news.publishedDate : '—'}
                      </td>
                      <td className="cell-actions">
                        <button
                          type="button"
                          className="action-link edit-link"
                          onClick={() => openEditEditor(news)}
                        >
                          Sửa
                        </button>

                        {news.status === 'Published' ? (
                          <button
                            type="button"
                            className="action-link unpublish-link"
                            onClick={() => handleUnpublish(news.id)}
                            title="Ẩn tin tức khỏi sinh viên"
                          >
                            Ẩn tin
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="action-link publish-link"
                            onClick={() => handlePublish(news.id)}
                            style={{ color: '#059669' }}
                            title="Hiển thị / Xuất bản tin tức"
                          >
                            Hiển thị
                          </button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Controls */}
          <Pagination
            currentPage={validCurrentPage}
            totalItems={filteredNews.length}
            pageSize={pageSize}
            onPageChange={setCurrentPage}
            itemName="tin"
          />
        </div>
      </div>

      {/* POPUP MODAL: Add / Edit News (Thay vì để bên cạnh) */}
      {isEditorOpen && (
        <div className="modal-backdrop" onClick={() => setIsEditorOpen(false)}>
          <div
            className="modal-card modal-editor-dialog"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-header">
              <div className="modal-title-wrap">
                <span className="mgmt-subheading">{editingId ? 'Cập nhật' : 'Tạo mới'}</span>
                <h3 className="modal-title">{editingId ? 'Chỉnh sửa tin tức' : 'Soạn tin tức mới'}</h3>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setIsEditorOpen(false)}
                title="Đóng popup"
              >
                ✕
              </button>
            </div>

            {errorMessage && (
              <div className="mgmt-error-alert" style={{ marginBottom: '16px' }}>
                <span>{errorMessage}</span>
              </div>
            )}

            <div className="editor-form-body">
              {/* Title field */}
              <div className="form-group">
                <div className="label-row">
                  <label htmlFor="news-title" className="input-label required-label">
                    Tiêu đề
                  </label>
                  <span className="char-count">{title.length}/200</span>
                </div>
                <input
                  id="news-title"
                  type="text"
                  maxLength={200}
                  className="mgmt-input"
                  placeholder="Nhập tiêu đề tin tức..."
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                />
              </div>

              {/* Content field */}
              <div className="form-group">
                <label htmlFor="news-content" className="input-label required-label">
                  Nội dung
                </label>
                <textarea
                  id="news-content"
                  rows={6}
                  className="mgmt-textarea"
                  placeholder="Nhập nội dung thông báo..."
                  value={content}
                  onChange={(e) => setContent(e.target.value)}
                />
              </div>

              {/* Action Buttons */}
              <div className="editor-actions-row" style={{ marginTop: '24px' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setIsEditorOpen(false)}
                >
                  Đóng
                </button>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => handleSave('Draft')}
                >
                  Lưu nháp
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={() => handleSave('Published')}
                >
                  Đăng tin
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
