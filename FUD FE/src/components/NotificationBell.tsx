import React, { useCallback, useEffect, useRef, useState } from 'react';
import { BellIcon } from './Icons';
import { NotificationDetailModal } from './NotificationDetailModal';
import { useAuth } from '../contexts/AuthContext';
import { notificationApi, type NotificationItem } from '../services/api';
import { formatDateTime } from '../utils/dateUtils';
import { useIdleAwarePolling } from '../utils/useIdleAwarePolling';
import { getNotificationCategoryMeta } from '../utils/notificationRouting';

interface NotificationBellProps {
  onNavigateTarget?: (item: NotificationItem) => void;
  onOpenAllNotifications?: () => void;
  syncKey?: number;
  onNotificationsChanged?: () => void;
}

const PAGE_SIZE = 10;

export const NotificationBell: React.FC<NotificationBellProps> = ({
  onNavigateTarget,
  onOpenAllNotifications,
  syncKey,
  onNotificationsChanged,
}) => {
  const { currentUser } = useAuth();
  const wrapperRef = useRef<HTMLDivElement>(null);
  const prevSyncKeyRef = useRef(syncKey);

  const [unreadCount, setUnreadCount] = useState(0);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const [filterUnreadOnly, setFilterUnreadOnly] = useState(false);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoadingList, setIsLoadingList] = useState(false);
  const [isMarkingAll, setIsMarkingAll] = useState(false);
  const [listError, setListError] = useState<string | null>(null);
  const [selectedItem, setSelectedItem] = useState<NotificationItem | null>(null);

  const fetchUnreadCount = useCallback(async () => {
    try {
      const count = await notificationApi.getUnreadCount();
      setUnreadCount(count);
    } catch {
      // Bỏ qua lỗi mạng tạm thời khi polling ngầm
    }
  }, []);

  useIdleAwarePolling(fetchUnreadCount, 15000, {
    enabled: Boolean(currentUser?.id && currentUser.primaryRole !== 'Admin'),
    immediate: true,
  });

  useEffect(() => {
    if (syncKey === undefined || syncKey === prevSyncKeyRef.current) return;
    prevSyncKeyRef.current = syncKey;
    void fetchUnreadCount();
  }, [syncKey, fetchUnreadCount]);

  const loadNotifications = useCallback(async (unreadOnly: boolean) => {
    try {
      setIsLoadingList(true);
      setListError(null);

      const data = await notificationApi.getList({
        page: 1,
        pageSize: PAGE_SIZE,
        unreadOnly,
      });

      setUnreadCount(data.unreadCount);
      setTotalCount(data.totalCount);
      setItems(data.items);
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : 'Không thể tải danh sách thông báo';
      setListError(message);
    } finally {
      setIsLoadingList(false);
    }
  }, []);

  useEffect(() => {
    if (!isDropdownOpen) return;

    const handleMouseDown = (event: MouseEvent) => {
      if (
        wrapperRef.current &&
        event.target instanceof Node &&
        !wrapperRef.current.contains(event.target)
      ) {
        setIsDropdownOpen(false);
      }
    };

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsDropdownOpen(false);
      }
    };

    document.addEventListener('mousedown', handleMouseDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handleMouseDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isDropdownOpen]);

  const handleToggleDropdown = () => {
    const nextOpen = !isDropdownOpen;
    setIsDropdownOpen(nextOpen);
    if (nextOpen) {
      void loadNotifications(filterUnreadOnly);
    }
  };

  const handleChangeFilter = (nextUnreadOnly: boolean) => {
    if (nextUnreadOnly === filterUnreadOnly) return;
    setFilterUnreadOnly(nextUnreadOnly);
    void loadNotifications(nextUnreadOnly);
  };

  const handleMarkAllAsRead = async () => {
    if (unreadCount === 0 || isMarkingAll) return;
    try {
      setIsMarkingAll(true);
      const res = await notificationApi.markAllAsRead();
      const nowIso = new Date().toISOString();
      setUnreadCount(res.unreadCount);
      if (filterUnreadOnly) {
        setItems([]);
        setTotalCount(0);
      } else {
        setItems((prev) =>
          prev.map((item) =>
            item.readFlag ? item : { ...item, readFlag: true, readTime: item.readTime ?? nowIso }
          )
        );
      }
      setSelectedItem((prev) =>
        prev && !prev.readFlag
          ? { ...prev, readFlag: true, readTime: prev.readTime ?? nowIso }
          : prev
      );
      onNotificationsChanged?.();
    } catch (err: unknown) {
      const message =
        err instanceof Error ? err.message : 'Không thể đánh dấu tất cả đã đọc';
      setListError(message);
    } finally {
      setIsMarkingAll(false);
    }
  };

  const handleSelectItem = async (item: NotificationItem) => {
    setIsDropdownOpen(false);
    setSelectedItem(item);

    if (item.readFlag) return;

    try {
      const res = await notificationApi.markAsRead(item.id);
      setUnreadCount(res.unreadCount);
      setItems((prev) => {
        if (filterUnreadOnly) {
          return prev.filter((x) => x.id !== item.id);
        }
        return prev.map((x) =>
          x.id === item.id
            ? { ...x, readFlag: res.readFlag, readTime: res.readTime ?? x.readTime }
            : x
        );
      });
      if (filterUnreadOnly) {
        setTotalCount((prev) => Math.max(0, prev - 1));
      }
      setSelectedItem((prev) =>
        prev && prev.id === item.id
          ? { ...prev, readFlag: res.readFlag, readTime: res.readTime ?? prev.readTime }
          : prev
      );
      onNotificationsChanged?.();
    } catch {
      // Giữ modal mở ngay cả khi cập nhật trạng thái đọc tạm thời lỗi
    }
  };

  const handleCloseModal = useCallback(() => {
    setSelectedItem(null);
  }, []);

  const badgeLabel = unreadCount > 99 ? '99+' : unreadCount.toString();

  return (
    <div className="notification-bell-wrapper" ref={wrapperRef}>
      <button
        type="button"
        className={`notification-bell-btn ${isDropdownOpen ? 'active' : ''}`}
        onClick={handleToggleDropdown}
        aria-label={`Thông báo (${unreadCount} chưa đọc)`}
        aria-expanded={isDropdownOpen}
        title="Xem thông báo"
      >
        <BellIcon size={19} />
        {unreadCount > 0 && <span className="notification-bell-badge">{badgeLabel}</span>}
      </button>

      {isDropdownOpen && (
        <div className="notification-dropdown" role="dialog" aria-label="Danh sách thông báo">
          <div className="notification-dropdown-header">
            <div className="notification-dropdown-title-row">
              <span className="notification-dropdown-title">Thông báo</span>
              {unreadCount > 0 && (
                <span className="notification-unread-pill">{unreadCount} chưa đọc</span>
              )}
            </div>
            <button
              type="button"
              className="notification-mark-all-btn"
              onClick={handleMarkAllAsRead}
              disabled={unreadCount === 0 || isMarkingAll}
            >
              {isMarkingAll ? 'Đang xử lý...' : 'Đánh dấu tất cả đã đọc'}
            </button>
          </div>

          <div className="notification-filter-tabs">
            <button
              type="button"
              className={`notification-filter-tab ${!filterUnreadOnly ? 'active' : ''}`}
              onClick={() => handleChangeFilter(false)}
            >
              Tất cả
            </button>
            <button
              type="button"
              className={`notification-filter-tab ${filterUnreadOnly ? 'active' : ''}`}
              onClick={() => handleChangeFilter(true)}
            >
              Chưa đọc
            </button>
          </div>

          <div className="notification-list">
            {isLoadingList ? (
              <div className="notification-empty-state">Đang tải thông báo...</div>
            ) : listError ? (
              <div className="notification-empty-state notification-error-state">
                <p>{listError}</p>
                <button
                  type="button"
                  className="btn-retry"
                  onClick={() => void loadNotifications(filterUnreadOnly)}
                >
                  Thử lại
                </button>
              </div>
            ) : items.length === 0 ? (
              <div className="notification-empty-state">
                {filterUnreadOnly ? 'Không có thông báo chưa đọc.' : 'Chưa có thông báo nào.'}
              </div>
            ) : (
              items.map((item) => {
                const meta = getNotificationCategoryMeta(item.referenceType);
                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`notification-item ${item.readFlag ? 'read' : 'unread'}`}
                    onClick={() => void handleSelectItem(item)}
                  >
                    <div className="notification-item-top">
                      <span
                        className="notification-category-badge"
                        style={{ backgroundColor: meta.bg, color: meta.color }}
                      >
                        {meta.label}
                      </span>
                      <span className="notification-item-time">
                        {formatDateTime(item.sentTime)}
                      </span>
                    </div>
                    <div className="notification-item-title-row">
                      {!item.readFlag && <span className="notification-unread-dot" />}
                      <span className="notification-item-title">{item.title}</span>
                    </div>
                    {item.content && (
                      <div className="notification-item-preview">{item.content}</div>
                    )}
                  </button>
                );
              })
            )}
          </div>

          {onOpenAllNotifications && (
            <div className="notification-dropdown-footer">
              <button
                type="button"
                className="notification-view-all-btn"
                onClick={() => {
                  setIsDropdownOpen(false);
                  onOpenAllNotifications();
                }}
              >
                {totalCount > items.length
                  ? `Xem tất cả thông báo (${totalCount}) →`
                  : 'Xem tất cả thông báo →'}
              </button>
            </div>
          )}
        </div>
      )}

      <NotificationDetailModal
        item={selectedItem}
        userRole={currentUser?.primaryRole}
        onClose={handleCloseModal}
        onNavigateTarget={onNavigateTarget}
      />
    </div>
  );
};
