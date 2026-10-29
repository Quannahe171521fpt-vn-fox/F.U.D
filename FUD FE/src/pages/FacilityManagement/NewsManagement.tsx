import React, { useState } from 'react';
import type { NewsItem } from '../../types/management';

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

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 8;

  // Form Editor Modal State
  const [editingId, setEditingId] = useState<number | null>(null);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [attachments, setAttachments] = useState<{ name: string; size: string; type: string }[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [isEditorOpen, setIsEditorOpen] = useState(false);

  // File drag state
  const [isDragging, setIsDragging] = useState(false);

  // Filtered news
  const filteredNews = newsList.filter((item) => {
    if (filter === 'All') return true;
    return item.status === filter;
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

  const showNotification = (msg: string) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), 3500);
  };

  const openCreateEditor = () => {
    setEditingId(null);
    setTitle('');
    setContent('');
    setAttachments([]);
    setErrorMessage(null);
    setIsEditorOpen(true);
  };

  const openEditEditor = (item: NewsItem) => {
    setEditingId(item.id);
    setTitle(item.title);
    setContent(item.content);
    setAttachments(item.attachments ? [...item.attachments] : []);
    setErrorMessage(null);
    setIsEditorOpen(true);
  };

  const handleFileUpload = (files: FileList | null) => {
    if (!files || files.length === 0) return;
    const file = files[0];

    const allowedExtensions = ['pdf', 'doc', 'docx', 'jpg', 'png'];
    const fileExt = file.name.split('.').pop()?.toLowerCase() || '';

    if (!allowedExtensions.includes(fileExt)) {
      setErrorMessage(`Định dạng tệp không hợp lệ (.${fileExt}). Chỉ chấp nhận PDF, DOC, DOCX, JPG, PNG.`);
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setErrorMessage('Dung lượng tệp vượt quá giới hạn cho phép (tối đa 5MB).');
      return;
    }

    const formattedSize =
      file.size > 1024 * 1024
        ? `${(file.size / (1024 * 1024)).toFixed(1)} MB`
        : `${Math.round(file.size / 1024)} KB`;

    setAttachments((prev) => [
      ...prev,
      {
        name: file.name,
        size: formattedSize,
        type: fileExt,
      },
    ]);
    setErrorMessage(null);
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
      attachments: [...attachments],
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

          <div className="table-responsive">
            <table className="mgmt-table">
              <thead>
                <tr>
                  <th style={{ width: '38%' }}>Tiêu đề</th>
                  <th style={{ width: '12%' }}>Trạng thái</th>
                  <th style={{ width: '13%' }}>Ngày tạo</th>
                  <th style={{ width: '13%' }}>Ngày đăng</th>
                  <th style={{ width: '13%' }}>Ngày ẩn</th>
                  <th style={{ width: '11%', textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {filteredNews.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="empty-table-cell">
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
                        {news.attachments && news.attachments.length > 0 && (
                          <span className="attachment-badge">
                            [{news.attachments.length} tệp đính kèm]
                          </span>
                        )}
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
                      <td className="cell-date">
                        {news.status === 'Draft' && news.hiddenDate ? news.hiddenDate : '—'}
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
          {totalPages > 1 && (
            <div className="pagination-bar">
              <span className="pagination-info">
                Hiển thị {(validCurrentPage - 1) * pageSize + 1} -{' '}
                {Math.min(validCurrentPage * pageSize, filteredNews.length)} trên tổng số{' '}
                {filteredNews.length} tin
              </span>
              <div className="pagination-buttons">
                <button
                  type="button"
                  className="page-btn nav-btn"
                  onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                  disabled={validCurrentPage === 1}
                >
                  Trước
                </button>
                {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                  <button
                    key={page}
                    type="button"
                    className={`page-btn ${page === validCurrentPage ? 'active' : ''}`}
                    onClick={() => setCurrentPage(page)}
                  >
                    {page}
                  </button>
                ))}
                <button
                  type="button"
                  className="page-btn nav-btn"
                  onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                  disabled={validCurrentPage === totalPages}
                >
                  Sau
                </button>
              </div>
            </div>
          )}
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

              {/* Attachments drag & drop */}
              <div className="form-group">
                <label className="input-label">Tệp đính kèm</label>
                <div
                  className={`dropzone-box ${isDragging ? 'drag-over' : ''}`}
                  onDragOver={(e) => {
                    e.preventDefault();
                    setIsDragging(true);
                  }}
                  onDragLeave={() => setIsDragging(false)}
                  onDrop={(e) => {
                    e.preventDefault();
                    setIsDragging(false);
                    handleFileUpload(e.dataTransfer.files);
                  }}
                >
                  <p className="dropzone-text">
                    Kéo thả tệp vào đây hoặc{' '}
                    <label htmlFor="file-upload" className="dropzone-browse-label">
                      chọn tệp từ máy
                    </label>
                  </p>
                  <span className="dropzone-hint">
                    Định dạng: PDF, DOC, DOCX, JPG, PNG (Tối đa 5MB)
                  </span>
                  <input
                    id="file-upload"
                    type="file"
                    className="hidden-file-input"
                    onChange={(e) => handleFileUpload(e.target.files)}
                    accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"
                  />
                </div>

                {/* Uploaded attachments list */}
                {attachments.length > 0 && (
                  <div className="attachments-list" style={{ marginTop: '10px' }}>
                    {attachments.map((file, idx) => (
                      <div key={idx} className="attachment-chip">
                        <span className="file-name">{file.name}</span>
                        <span className="file-size">({file.size})</span>
                        <button
                          type="button"
                          className="file-remove-btn"
                          onClick={() =>
                            setAttachments(attachments.filter((_, i) => i !== idx))
                          }
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                  </div>
                )}
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
