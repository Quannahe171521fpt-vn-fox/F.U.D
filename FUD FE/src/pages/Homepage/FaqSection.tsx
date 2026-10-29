import React, { useState, useEffect } from 'react';
import { ChevronRightIcon } from '../../components/Icons';
import type { FaqItem } from '../../types/dormitory';
import { dormInfoApi } from '../../services/api';

export const FaqSection: React.FC = () => {
  const [faqs, setFaqs] = useState<FaqItem[]>([]);
  const [activeCategory, setActiveCategory] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [expandedId, setExpandedId] = useState<string | null>('faq-1');

  useEffect(() => {
    dormInfoApi.getFaqs()
      .then((data) => {
        if (data && data.length > 0) {
          setFaqs(data);
        }
      })
      .catch((err) => console.error('Error fetching FAQs:', err));
  }, []);

  const filteredFaqs = faqs.filter((faq) => {
    const matchesCategory = activeCategory === 'all' || faq.category === activeCategory;
    const matchesQuery =
      faq.question.toLowerCase().includes(searchQuery.toLowerCase()) ||
      faq.answer.toLowerCase().includes(searchQuery.toLowerCase());
    return matchesCategory && matchesQuery;
  });

  const toggleExpand = (id: string) => {
    setExpandedId((prev) => (prev === id ? null : id));
  };

  return (
    <section className="faq-section-container" id="faq-section">
      <div className="section-header">
        <span className="section-kicker">Giải đáp thắc mắc</span>
        <h2 className="section-title">Các câu hỏi thường gặp (FAQ)</h2>
        <p className="section-description">
          Tổng hợp những câu hỏi phổ biến nhất của sinh viên về quy chế lưu trú,
          thủ tục đăng ký và đời sống sinh hoạt tại KTX Đại học FPT.
        </p>
      </div>

      {/* Category Tabs & Search Bar */}
      <div className="faq-controls">
        <div className="faq-tabs">
          <button
            type="button"
            className={`faq-tab-btn ${activeCategory === 'all' ? 'active' : ''}`}
            onClick={() => setActiveCategory('all')}
          >
            Tất cả câu hỏi
          </button>
          <button
            type="button"
            className={`faq-tab-btn ${activeCategory === 'dang-ky' ? 'active' : ''}`}
            onClick={() => setActiveCategory('dang-ky')}
          >
            Đăng ký & Xếp phòng
          </button>
          <button
            type="button"
            className={`faq-tab-btn ${activeCategory === 'chi-phi' ? 'active' : ''}`}
            onClick={() => setActiveCategory('chi-phi')}
          >
            Chi phí & Thanh toán
          </button>
          <button
            type="button"
            className={`faq-tab-btn ${activeCategory === 'noi-quy' ? 'active' : ''}`}
            onClick={() => setActiveCategory('noi-quy')}
          >
            Nội quy & Giờ giấc
          </button>
          <button
            type="button"
            className={`faq-tab-btn ${activeCategory === 'tien-ich' ? 'active' : ''}`}
            onClick={() => setActiveCategory('tien-ich')}
          >
            Tiện ích & Dịch vụ
          </button>
        </div>

        <div className="faq-search-box">
          <input
            type="text"
            className="faq-search-input"
            placeholder="Tìm kiếm câu hỏi hoặc nội dung..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>
      </div>

      {/* FAQ Accordion List */}
      <div className="faq-accordion-list">
        {filteredFaqs.length > 0 ? (
          filteredFaqs.map((faq) => {
            const isExpanded = expandedId === faq.id;
            return (
              <div
                key={faq.id}
                className={`faq-item ${isExpanded ? 'expanded' : ''}`}
              >
                <button
                  type="button"
                  className="faq-question-btn"
                  onClick={() => toggleExpand(faq.id)}
                  aria-expanded={isExpanded}
                >
                  <span className="faq-question-text">{faq.question}</span>
                  <span className="faq-icon-arrow">
                    <ChevronRightIcon size={18} />
                  </span>
                </button>
                {isExpanded && (
                  <div className="faq-answer-content">
                    <p>{faq.answer}</p>
                  </div>
                )}
              </div>
            );
          })
        ) : (
          <div className="empty-faq-message">
            Không tìm thấy câu hỏi phù hợp với từ khóa của bạn.
          </div>
        )}
      </div>
    </section>
  );
};
