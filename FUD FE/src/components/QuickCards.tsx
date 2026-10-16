import React from 'react';
import { ArrowRightIcon } from './Icons';

interface QuickCardsProps {
  onSelectAction: (sectionId: string) => void;
}

export const QuickCards: React.FC<QuickCardsProps> = ({ onSelectAction }) => {
  const cards = [
    {
      id: 'dorm-info',
      title: 'Thông tin KTX Đại học FPT',
      actionText: 'Thông tin',
      targetId: 'dorm-info',
    },
    {
      id: 'dorm-registration',
      title: 'Đăng ký sử dụng KTX',
      actionText: 'Xem hướng dẫn',
      targetId: 'registration-guide',
    },
    {
      id: 'dorm-faq',
      title: 'Các câu hỏi thường gặp',
      actionText: 'FAQ',
      targetId: 'faq-section',
    },
  ];

  return (
    <section className="quick-cards-section">
      <div className="quick-cards-grid">
        {cards.map((card) => (
          <div
            key={card.id}
            className="action-card"
            onClick={() => onSelectAction(card.targetId)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onSelectAction(card.targetId);
              }
            }}
          >
            <h2 className="action-card-title">{card.title}</h2>
            <div className="action-card-link">
              <span>{card.actionText}</span>
              <ArrowRightIcon size={18} />
            </div>
          </div>
        ))}
      </div>
    </section>
  );
};
