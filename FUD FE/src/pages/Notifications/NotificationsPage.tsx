import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { BellIcon } from '../../components/Icons';
import { NotificationDetailModal } from '../../components/NotificationDetailModal';
import { Pagination } from '../../components/Pagination';
import {
  notificationApi,
  type NotificationItem,
  type NotificationReadStatusFilter,
} from '../../services/api';
import { formatDateTime } from '../../utils/dateUtils';
import {
  getNotificationCategoryMeta,
  getNotificationFilterCategories,
  type UserPrimaryRole,
} from '../../utils/notificationRouting';

interface NotificationsPageProps {
  userRole?: UserPrimaryRole;
  syncKey?: number;
  onBack: () => void;
  onNavigateTarget: (item: NotificationItem) => void;
  onNotificationsChanged?: () => void;
}

const PAGE_SIZE = 20;

const READ_STATUS_TABS: ReadonlyArray<{ value: NotificationReadStatusFilter; label: string }> = [
  { value: 'all', label: 'Tất cả' },
  { value: 'unread', label: 'Chưa đọc' },
  { value: 'read', label: 'Đã đọc' },
];

export const NotificationsPage: React.FC<NotificationsPageProps> = ({
  userRole,
  syncKey,
  onBack,
  onNavigateTarget,
  onNotificationsChanged,
}) => {
  const [readStatus, setReadStatus] = useState<NotificationReadStatusFilter>('all');
  const [referenceType, setReferenceType] = useState<string>('all');
  const [searchInput, setSearchInput] = useState<string>('');
  const [debouncedSearch, setDebouncedSearch] = useState<string>('');
  const [page, setPage] = useState<number>(1);
  const [reloadToken, setReloadToken] = useState<number>(0);

  const [items, setItems] = useState<NotificationItem[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [unreadCount, setUnreadCount] = useState<number>(0);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [isMarkingAll, setIsMarkingAll] = useState<boolean>(false);
  const [selectedItem, setSelectedItem] = useState<NotificationItem | null>(null);

  const requestSeqRef = useRef(0);

  const categoryOptions = useMemo(
    () => getNotificationFilterCategories(userRole),
    [userRole]
  );

  useEffect(() => {
    const trimmed = searchInput.trim();
    if (trimmed === debouncedSearch) return;

    const timer = window.setTimeout(() => {
      setIsLoading(true);
      setError(null);
      setDebouncedSearch(trimmed);
      setPage(1);
    }, 350);

    return () => window.clearTimeout(timer);
  }, [searchInput, debouncedSearch]);

  useEffect(() => {
    const seq = ++requestSeqRef.current;
    const controller = new AbortController();

    async function fetchNotifications() {
      try {
        const data = await notificationApi.getList(
          {
            readStatus,
            referenceType: referenceType !== 'all' ? referenceType : undefined,
            search: debouncedSearch || undefined,
            page,
            pageSize: PAGE_SIZE,
          },
          { signal: controller.signal }
        );
        if (controller.signal.aborted || seq !== requestSeqRef.current) return;

        setItems(data.items);
        setTotalCount(data.totalCount);
        setUnreadCount(data.unreadCount);
        setError(null);
        setIsLoading(false);
      } catch (err: unknown) {
        if (controller.signal.aborted || seq !== requestSeqRef.current) return;
        setError(err instanceof Error ? err.message : 'Không thể tải danh sách thông báo');
        setIsLoading(false);
      }
    }

    void fetchNotifications();
    return () => controller.abort();
  }, [page, readStatus, referenceType, debouncedSearch, reloadToken, syncKey]);

  const handleReadStatusChange = (nextStatus: NotificationReadStatusFilter) => {
    if (nextStatus === readStatus) return;
    setIsLoading(true);
    setError(null);
    setActionError(null);
    setReadStatus(nextStatus);
    setPage(1);
  };

  const handleReferenceTypeChange = (nextRefType: string) => {
    if (nextRefType === referenceType) return;
    setIsLoading(true);
    setError(null);
    setActionError(null);
    setReferenceType(nextRefType);
    setPage(1);
  };

  const handlePageChange = (nextPage: number) => {
    if (nextPage === page) return;
    setIsLoading(true);
    setError(null);
    setPage(nextPage);
  };

  const handleClearSearch = () => {
    setSearchInput('');
    if (debouncedSearch !== '') {
      setIsLoading(true);
      setError(null);
      setDebouncedSearch('');
      setPage(1);
    }
  };

  const handleResetFilters = () => {
    setIsLoading(true);
    setError(null);
    setActionError(null);
    setReadStatus('all');
    setReferenceType('all');
    setSearchInput('');
    setDebouncedSearch('');
    setPage(1);
    setReloadToken((t) => t + 1);
  };

  const handleRefresh = () => {
    setIsLoading(true);
    setError(null);
    setActionError(null);
    setReloadToken((t) => t + 1);
  };

  const handleMarkAllAsRead = async () => {
    if (unreadCount === 0 || isMarkingAll) return;

    try {
      setIsMarkingAll(true);
      setActionError(null);
      const res = await notificationApi.markAllAsRead();
      const nowIso = new Date().toISOString();

      setUnreadCount(res.unreadCount);
      setSelectedItem((prev) =>
        prev && !prev.readFlag
          ? { ...prev, readFlag: true, readTime: prev.readTime ?? nowIso }
          : prev
      );

      if (readStatus === 'unread') {
        setItems([]);
        setTotalCount(0);
        setPage(1);
      } else if (readStatus === 'all') {
        setItems((prev) =>
          prev.map((x) =>
            x.readFlag ? x : { ...x, readFlag: true, readTime: x.readTime ?? nowIso }
          )
        );
      } else {
        setIsLoading(true);
        setError(null);
        setPage(1);
        setReloadToken((t) => t + 1);
      }

      onNotificationsChanged?.();
    } catch (err: unknown) {
      setActionError(
        err instanceof Error ? err.message : 'Không thể đánh dấu tất cả thông báo đã đọc'
      );
    } finally {
      setIsMarkingAll(false);
    }
  };

  const handleSelectItem = async (item: NotificationItem) => {
    setActionError(null);
    setSelectedItem(item.readFlag ? item : { ...item, readFlag: true });

    if (item.readFlag) return;

    const prevItems = items;
    const prevTotalCount = totalCount;
    const prevUnreadCount = unreadCount;

    setUnreadCount((prev) => Math.max(0, prev - 1));
    if (readStatus === 'unread') {
      setItems((prev) => prev.filter((x) => x.id !== item.id));
      setTotalCount((prev) => Math.max(0, prev - 1));
    } else {
      setItems((prev) =>
        prev.map((x) => (x.id === item.id ? { ...x, readFlag: true } : x))
      );
    }

    try {
      const res = await notificationApi.markAsRead(item.id);
      setUnreadCount(res.unreadCount);
      if (readStatus !== 'unread') {
        setItems((prev) =>
          prev.map((x) =>
            x.id === item.id
              ? { ...x, readFlag: res.readFlag, readTime: res.readTime ?? x.readTime }
              : x
          )
        );
      }
      setSelectedItem((prev) =>
        prev && prev.id === item.id
          ? { ...prev, readFlag: res.readFlag, readTime: res.readTime ?? prev.readTime }
          : prev
      );
      onNotificationsChanged?.();

      if (readStatus === 'unread' && prevItems.length === 1 && page > 1 && prevTotalCount - 1 > 0) {
        const nextTotalPages = Math.max(1, Math.ceil((prevTotalCount - 1) / PAGE_SIZE));
        setIsLoading(true);
        setPage(Math.min(page - 1, nextTotalPages));
      }
    } catch (err: unknown) {
      setItems(prevItems);
      setTotalCount(prevTotalCount);
      setUnreadCount(prevUnreadCount);
      setSelectedItem((prev) =>
        prev && prev.id === item.id ? { ...prev, readFlag: false } : prev
      );
      setActionError(
        err instanceof Error ? err.message : 'Không thể đánh dấu thông báo đã đọc'
      );
    }
  };

  const handleCloseModal = useCallback(() => {
    setSelectedItem(null);
  }, []);

  const hasActiveFilters =
    readStatus !== 'all' || referenceType !== 'all' || debouncedSearch !== '';

  return (
    <main className="notifications-page-wrapper">
      <div className="notifications-page-container">
        <header className="notifications-page-header">
          <div className="notifications-page-title-block">
            <button
              type="button"
              className="notifications-page-back-btn"
              onClick={onBack}
            >
              ← Quay lại
            </button>
            <div className="notifications-page-heading-row">
              <div className="notifications-page-icon">
                <BellIcon size={22} />
              </div>
              <div>
                <h1 className="notifications-page-title">Tất cả thông báo</h1>
                <p className="notifications-page-subtitle">
                  <span className="notifications-page-unread-chip">{unreadCount} chưa đọc</span>
                  <span className="notifications-page-dot">•</span>
                  <span>{totalCount} kết quả</span>
                </p>
              </div>
            </div>
          </div>

          <div className="notifications-page-actions">
            <button
              type="button"
              className="btn-retry"
              onClick={handleRefresh}
              disabled={isLoading}
            >
              Làm mới
            </button>
            <button
              type="button"
              className="notifications-page-mark-all-btn"
              onClick={() => void handleMarkAllAsRead()}
              disabled={unreadCount === 0 || isMarkingAll}
            >
              {isMarkingAll ? 'Đang xử lý...' : 'Đánh dấu tất cả đã đọc'}
            </button>
          </div>
        </header>

        <section className="notifications-page-toolbar" aria-label="Bộ lọc thông báo">
          <div className="notifications-page-tabs" role="tablist" aria-label="Trạng thái đọc">
            {READ_STATUS_TABS.map((tab) => (
              <button
                key={tab.value}
                type="button"
                role="tab"
                aria-selected={readStatus === tab.value}
                className={`notifications-page-tab ${readStatus === tab.value ? 'active' : ''}`}
                onClick={() => handleReadStatusChange(tab.value)}
              >
                {tab.label}
              </button>
            ))}
          </div>

          <div className="notifications-page-filters-right">
            <select
              className="notifications-page-select"
              aria-label="Lọc theo loại thông báo"
              value={referenceType}
              onChange={(e) => handleReferenceTypeChange(e.target.value)}
            >
              <option value="all">Tất cả loại</option>
              {categoryOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>

            <div className="notifications-page-search">
              <input
                type="text"
                className="notifications-page-search-input"
                placeholder="Tìm theo tiêu đề hoặc nội dung..."
                aria-label="Tìm kiếm thông báo"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
              />
              {searchInput && (
                <button
                  type="button"
                  className="notifications-page-search-clear"
                  onClick={handleClearSearch}
                  aria-label="Xóa từ khóa tìm kiếm"
                  title="Xóa từ khóa"
                >
                  ×
                </button>
              )}
            </div>
          </div>
        </section>

        {actionError && (
          <div className="notifications-page-action-error" role="alert">
            <span>{actionError}</span>
            <button
              type="button"
              className="notifications-page-action-error-close"
              onClick={() => setActionError(null)}
              aria-label="Đóng thông báo lỗi"
            >
              ×
            </button>
          </div>
        )}

        <section className="notifications-page-list-card">
          {isLoading ? (
            <div className="notifications-page-state">Đang tải thông báo...</div>
          ) : error ? (
            <div className="notifications-page-state notifications-page-state-error">
              <p>{error}</p>
              <button type="button" className="btn-retry" onClick={handleRefresh}>
                Thử lại
              </button>
            </div>
          ) : items.length === 0 ? (
            <div className="notifications-page-state">
              <p>
                {hasActiveFilters
                  ? 'Không tìm thấy thông báo phù hợp với bộ lọc.'
                  : 'Chưa có thông báo nào.'}
              </p>
              {hasActiveFilters && (
                <button
                  type="button"
                  className="btn-retry"
                  onClick={handleResetFilters}
                >
                  Xóa bộ lọc
                </button>
              )}
            </div>
          ) : (
            <div className="notifications-page-list">
              {items.map((item) => {
                const meta = getNotificationCategoryMeta(item.referenceType);
                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`notifications-page-card ${item.readFlag ? 'read' : 'unread'}`}
                    onClick={() => void handleSelectItem(item)}
                  >
                    <div className="notifications-page-card-top">
                      <div className="notifications-page-card-badges">
                        {!item.readFlag && <span className="notification-unread-dot" />}
                        <span
                          className="notification-category-badge"
                          style={{ backgroundColor: meta.bg, color: meta.color }}
                        >
                          {meta.label}
                        </span>
                        <span
                          className={`notification-status-pill ${
                            item.readFlag ? 'read' : 'unread'
                          }`}
                        >
                          {item.readFlag ? 'Đã đọc' : 'Mới'}
                        </span>
                      </div>
                      <span className="notification-item-time">
                        {formatDateTime(item.sentTime)}
                      </span>
                    </div>

                    <div className="notifications-page-card-title">{item.title}</div>

                    {item.content && (
                      <div className="notifications-page-card-preview">{item.content}</div>
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </section>

        {!isLoading && !error && totalCount > 0 && (
          <Pagination
            currentPage={page}
            totalItems={totalCount}
            pageSize={PAGE_SIZE}
            onPageChange={handlePageChange}
            itemName="thông báo"
          />
        )}
      </div>

      <NotificationDetailModal
        item={selectedItem}
        userRole={userRole}
        onClose={handleCloseModal}
        onNavigateTarget={onNavigateTarget}
      />
    </main>
  );
};
