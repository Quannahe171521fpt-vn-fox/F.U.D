import React, { useState } from 'react';
import type { NewsItem } from '../types/management';

interface StudentNewsSectionProps {
  newsList: NewsItem[];
}

export const StudentNewsSection: React.FC<StudentNewsSectionProps> = ({ newsList }) => {
  const [selectedNews, setSelectedNews] = useState<NewsItem | null>(null);

  // Pagination specifically for student homepage (cards view)
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 6; // 6 cards per page (3 columns x 2 rows)

  // Filter only Published news for students
  const publishedNews = newsList.filter((n) => n.status === 'Published');

  const totalPages = Math.max(1, Math.ceil(publishedNews.length / pageSize));
  const validCurrentPage = Math.min(currentPage, totalPages);
  const paginatedNews = publishedNews.slice(
    (validCurrentPage - 1) * pageSize,
    validCurrentPage * pageSize
  );

  return (
    <section id="student-news-section" className="section-container bg-white">
      <div className="section-inner">
        <div className="section-title-wrap text-center">
          <span className="section-tag">TIN TỨC & THÔNG BÁO</span>
          <h2 className="section-heading">Bản tin Ký túc xá FUD mới nhất</h2>
          <p className="section-subheading">
            Cập nhật các thông báo quan trọng, lịch đăng ký phòng và sự kiện dành cho sinh viên nội trú
          </p>
        </div>

        {publishedNews.length === 0 ? (
          <div className="no-news-box">
            <p>Hiện chưa có thông báo mới nào được đăng tải.</p>
          </div>
        ) : (
          <>
            <div className="student-news-grid">
              {paginatedNews.map((item) => (
                <article
                  key={item.id}
                  className="student-news-card"
                  onClick={() => setSelectedNews(item)}
                >
                  <div className="news-card-header">
                    <span className="news-date-badge">Ngày đăng: {item.publishedDate}</span>
                    {item.attachments && item.attachments.length > 0 && (
                      <span className="news-attach-icon">
                        [{item.attachments.length} tệp đính kèm]
                      </span>
                    )}
                  </div>
                  <h3 className="news-item-title">{item.title}</h3>
                  <p className="news-item-excerpt">
                    {item.content.length > 120
                      ? `${item.content.substring(0, 120)}...`
                      : item.content}
                  </p>
                  <div className="news-card-footer">
                    <span className="read-more-text">Xem chi tiết &rarr;</span>
                  </div>
                </article>
              ))}
            </div>

            {/* Homepage Student Pagination Controls */}
            {publishedNews.length > pageSize && (
              <div className="homepage-news-pagination">
                <button
                  type="button"
                  className="home-page-btn"
                  onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                  disabled={validCurrentPage === 1}
                >
                  &larr; Trang trước
                </button>
                <div className="home-page-numbers">
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                    <button
                      key={page}
                      type="button"
                      className={`home-number-btn ${page === validCurrentPage ? 'active' : ''}`}
                      onClick={() => setCurrentPage(page)}
                    >
                      {page}
                    </button>
                  ))}
                </div>
                <button
                  type="button"
                  className="home-page-btn"
                  onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                  disabled={validCurrentPage === totalPages}
                >
                  Trang sau &rarr;
                </button>
              </div>
            )}
          </>
        )}
      </div>

      {/* Modal View Detail News */}
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
    </section>
  );
};
