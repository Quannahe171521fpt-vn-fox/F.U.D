import React, { useEffect } from 'react';
import { createPortal } from 'react-dom';
import { BellIcon, CloseIcon } from './Icons';
import type { NotificationItem } from '../services/api';
import { formatDateTime } from '../utils/dateUtils';
import {
  getNotificationCategoryMeta,
  resolveNotificationTarget,
  type UserPrimaryRole,
} from '../utils/notificationRouting';

interface NotificationDetailModalProps {
  item: NotificationItem | null;
  userRole?: UserPrimaryRole;
  onClose: () => void;
  onNavigateTarget?: (item: NotificationItem) => void;
}

export const NotificationDetailModal: React.FC<NotificationDetailModalProps> = ({
  item,
  userRole,
  onClose,
  onNavigateTarget,
}) => {
  useEffect(() => {
    if (!item) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [item, onClose]);

  if (!item) return null;

  const detailTarget = userRole
    ? resolveNotificationTarget(userRole, item.referenceType)
    : null;
  const detailCategory = getNotificationCategoryMeta(item.referenceType);

  return createPortal(
    <div
      className="modal-backdrop-custom"
      onClick={onClose}
      role="presentation"
    >
      <div
        className="notification-detail-modal"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="notification-detail-title"
      >
        <div className="modal-header-custom">
          <div className="modal-title-group">
            <div
              className="modal-icon-badge"
              style={{ backgroundColor: detailCategory.bg, color: detailCategory.color }}
            >
              <BellIcon size={20} />
            </div>
            <div>
              <h3 id="notification-detail-title" className="modal-main-title">
                Chi tiết thông báo
              </h3>
              <p className="modal-sub-title">
                {detailCategory.label}
                {item.referenceId ? ` · #${item.referenceId}` : ''}
              </p>
            </div>
          </div>
          <button
            type="button"
            className="modal-close-btn"
            onClick={onClose}
            aria-label="Đóng chi tiết thông báo"
          >
            <CloseIcon size={18} />
          </button>
        </div>

        <div className="modal-body-custom">
          <div className="notification-detail-meta-row">
            <span
              className={`notification-status-pill ${item.readFlag ? 'read' : 'unread'}`}
            >
              {item.readFlag ? 'Đã đọc' : 'Mới'}
            </span>
            <span className="notification-detail-time">
              Gửi lúc: {formatDateTime(item.sentTime)}
            </span>
            {item.readTime && (
              <span className="notification-detail-time">
                Đã xem: {formatDateTime(item.readTime)}
              </span>
            )}
          </div>

          <h4 className="notification-detail-heading">{item.title}</h4>

          <div className="notification-detail-content">
            {item.content || 'Không có nội dung chi tiết.'}
          </div>
        </div>

        <div className="modal-footer-custom">
          <button
            type="button"
            className="btn-retry"
            onClick={onClose}
          >
            Đóng
          </button>
          {detailTarget && onNavigateTarget && (
            <button
              type="button"
              className="notification-goto-btn"
              onClick={() => {
                onClose();
                onNavigateTarget(item);
              }}
            >
              {detailTarget.actionLabel}
            </button>
          )}
        </div>
      </div>
    </div>,
    document.body
  );
};
