import React, { useState } from 'react';
import type { NewsItem } from '../../types/management';

interface AnnouncementsPageProps {
  newsList: NewsItem[];
  onBackToHome: () => void;
}

export const AnnouncementsPage: React.FC<AnnouncementsPageProps> = ({
  newsList,
  onBackToHome,
}) => {
  const [selectedNews, setSelectedNews] = useState<NewsItem | null>(null);
  const [searchKeyword, setSearchKeyword] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 8; // 8 items per page

  // Only published news is visible to students
  const publishedNews = newsList.filter((n) => n.status === 'Published');

  const filteredNews = publishedNews.filter((item) => {
    if (!searchKeyword.trim()) return true;
    const query = searchKeyword.toLowerCase();
    return (
      item.title.toLowerCase().includes(query) ||
      item.content.toLowerCase().includes(query)
    );
  });

  const totalPages = Math.max(1, Math.ceil(filteredNews.length / pageSize));
  const validCurrentPage = Math.min(currentPage, totalPages);
  const paginatedNews = filteredNews.slice(
    (validCurrentPage - 1) * pageSize,
    validCurrentPage * pageSize
  );

  const handleSearchChange = (kw: string) => {
    setSearchKeyword(kw);
    setCurrentPage(1);
  };

  return (
    <div className="announcements-page-wrapper">
      <div className="announcements-hero-header">
        <div className="announcements-inner-header">
          <div className="announcements-breadcrumb">
            <button type="button" onClick={onBackToHome} className="breadcrumb-btn">
              Trang chủ
            </button>
            <span className="breadcrumb-sep">/</span>
            <span className="breadcrumb-current">Thông báo KTX</span>
          </div>
          <h1 className="announcements-main-title">Thông báo & Tin tức Ký túc xá FUD</h1>
          <p className="announcements-desc">
            Cổng thông báo chính thức của Ban Quản lý KTX Đại học FPT về lịch đăng ký phòng,
            bảo trì cơ sở vật chất, thủ tục tạm trú và các hoạt động đời sống sinh viên.
          </p>

          <div className="announcements-search-bar">
            <input
              type="text"
              placeholder="Tìm kiếm thông báo theo từ khóa hoặc tiêu đề..."
              value={searchKeyword}
              onChange={(e) => handleSearchChange(e.target.value)}
              className="announcements-search-input"
            />
            {searchKeyword && (
              <button
                type="button"
                className="announcements-search-clear"
                onClick={() => handleSearchChange('')}
              >
                Xóa tìm kiếm
              </button>
            )}
          </div>
        </div>
      </div>

      <div className="announcements-content-container">
        <div className="announcements-list-header">
          <h2 className="list-count-title">
            Danh sách thông báo ({filteredNews.length})
          </h2>
          <span className="list-status-badge">Thông tin chính thức</span>
        </div>

        {filteredNews.length === 0 ? (
          <div className="no-news-box">
            <p>Không tìm thấy thông báo nào phù hợp với từ khóa tìm kiếm.</p>
          </div>
        ) : (
          <div className="announcements-cards-grid">
            {paginatedNews.map((item) => (
              <article
                key={item.id}
                className="announcement-item-card"
                onClick={() => setSelectedNews(item)}
              >
                <div className="announcement-item-meta">
                  <span className="announcement-item-date">
                    Ngày đăng: {item.publishedDate}
                  </span>
                  {item.attachments && item.attachments.length > 0 && (
                    <span className="announcement-item-attach">
                      [{item.attachments.length} tệp đính kèm]
                    </span>
                  )}
                </div>
                <h3 className="announcement-item-title">{item.title}</h3>
                <p className="announcement-item-snippet">
                  {item.content.length > 130
                    ? `${item.content.substring(0, 130)}...`
                    : item.content}
                </p>
                <div className="announcement-item-footer">
                  <span className="announcement-read-more">Đọc toàn văn thông báo &rarr;</span>
                </div>
              </article>
            ))}
          </div>
        )}

        {/* Dedicated Announcements Pagination */}
        {filteredNews.length > pageSize && (
          <div className="announcements-pagination-row">
            <button
              type="button"
              className="announcement-page-nav-btn"
              onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
              disabled={validCurrentPage === 1}
            >
              Trang trước
            </button>
            <div className="announcement-pages-group">
              {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                <button
                  key={page}
                  type="button"
                  className={`announcement-page-btn ${page === validCurrentPage ? 'active' : ''}`}
                  onClick={() => setCurrentPage(page)}
                >
                  {page}
                </button>
              ))}
            </div>
            <button
              type="button"
              className="announcement-page-nav-btn"
              onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
              disabled={validCurrentPage === totalPages}
            >
              Trang sau
            </button>
          </div>
        )}
      </div>

      {/* Detail Modal */}
      {selectedNews && (
        <div className="modal-backdrop" onClick={() => setSelectedNews(null)}>
          <div className="modal-card modal-news-detail" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div className="modal-title-wrap">
                <span className="news-detail-date">Ngày đăng: {selectedNews.publishedDate}</span>
                <h3 className="modal-title">{selectedNews.title}</h3>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setSelectedNews(null)}
              >
                ✕
              </button>
            </div>

            <div className="news-detail-body">
              <p className="news-detail-content">{selectedNews.content}</p>

              {selectedNews.attachments && selectedNews.attachments.length > 0 && (
                <div className="news-detail-attachments">
                  <h4 className="attachments-title">Danh sách tệp đính kèm:</h4>
                  <div className="attachments-download-list">
                    {selectedNews.attachments.map((file, idx) => (
                      <div key={idx} className="file-download-item">
                        <div className="file-info-col">
                          <span className="file-name">{file.name}</span>
                          <span className="file-size">{file.size}</span>
                        </div>
                        <button
                          type="button"
                          className="btn-download"
                          onClick={() => alert(`Bắt đầu tải về tệp: ${file.name}`)}
                        >
                          Tải về
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>

            <div className="modal-footer-btn-row">
              <button
                type="button"
                className="btn-secondary"
                onClick={() => setSelectedNews(null)}
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
