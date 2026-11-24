import React, { useState, useEffect, useCallback } from 'react';
import { auditApi, type AuditLogItem, type AuditLogDetail } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime } from '../../utils/dateUtils';
import '../../styles/violation.css';

const PAGE_SIZE = 15;

const LOG_TYPES = [
  { value: 'All', label: 'Tất cả loại sự kiện' },
  { value: 'Impersonation', label: 'Phiên đăng nhập hộ (Impersonation)' },
  { value: 'RoleChange', label: 'Phân quyền & Vai trò (Role Change)' },
  { value: 'AccountStatus', label: 'Trạng thái tài khoản (Account Status)' },
  { value: 'System', label: 'Tác vụ hệ thống khác (System)' },
];

const LOG_META: Record<string, { label: string; bg: string; color: string; border: string }> = {
  Impersonation: { label: 'Đăng nhập hộ', bg: '#FEF3C7', color: '#B45309', border: '#FCD34D' },
  RoleChange: { label: 'Đổi vai trò', bg: '#EFF6FF', color: '#1D4ED8', border: '#BFDBFE' },
  AccountStatus: { label: 'Đổi trạng thái', bg: '#FEE2E2', color: '#B91C1C', border: '#FECACA' },
  System: { label: 'Hệ thống', bg: '#F1F5F9', color: '#475569', border: '#CBD5E1' },
};

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '8px 12px',
  borderRadius: '6px',
  border: '1px solid #CBD5E1',
  fontSize: '13px',
  backgroundColor: '#FFF',
  boxSizing: 'border-box',
};

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: '11px',
  fontWeight: 700,
  color: '#475569',
  marginBottom: 4,
};

const primaryBtn: React.CSSProperties = {
  padding: '8px 16px',
  backgroundColor: '#2563EB',
  color: '#FFF',
  border: 'none',
  borderRadius: '6px',
  fontWeight: 600,
  fontSize: '13px',
  cursor: 'pointer',
  display: 'inline-flex',
  alignItems: 'center',
  gap: '6px',
};

const secondaryBtn: React.CSSProperties = {
  padding: '8px 16px',
  backgroundColor: '#F1F5F9',
  color: '#334155',
  border: '1px solid #CBD5E1',
  borderRadius: '6px',
  fontWeight: 600,
  fontSize: '13px',
  cursor: 'pointer',
};

export const AuditLogPage: React.FC = () => {
  const [logs, setLogs] = useState<AuditLogItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [search, setSearch] = useState('');
  const [logType, setLogType] = useState('All');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  // Detail Modal
  const [selectedLog, setSelectedLog] = useState<AuditLogDetail | null>(null);
  const [modalLoading, setModalLoading] = useState(false);
  const [copied, setCopied] = useState(false);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await auditApi.getAll({
        page,
        pageSize: PAGE_SIZE,
        logType: logType !== 'All' ? logType : undefined,
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
        search: search.trim() || undefined,
      });
      setLogs(res.items || []);
      setTotalCount(res.totalCount || 0);
    } catch (err: any) {
      setError(err?.message || 'Không thể tải danh sách nhật ký kiểm toán');
    } finally {
      setLoading(false);
    }
  }, [page, logType, fromDate, toDate, search]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const handleResetFilters = () => {
    setSearch('');
    setLogType('All');
    setFromDate('');
    setToDate('');
    setPage(1);
  };

  const handleViewDetail = async (item: AuditLogItem) => {
    setModalLoading(true);
    setSelectedLog(item as AuditLogDetail);
    setCopied(false);
    try {
      const detail = await auditApi.getDetail(item.id, item.logType);
      setSelectedLog(detail);
    } catch {
      // fallback to the item already in state
    } finally {
      setModalLoading(false);
    }
  };

  const handleCopyPayload = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const renderActionSummary = (item: AuditLogItem) => {
    if (item.logType === 'Impersonation') {
      return (
        <div>
          <span style={{ fontWeight: 600, color: '#B45309' }}>Đăng nhập hộ sinh viên</span>
          {item.targetUserName && (
            <div style={{ fontSize: '11px', color: '#64748B' }}>
              Mục tiêu: {item.targetUserName} ({item.targetUserCode})
            </div>
          )}
        </div>
      );
    }

    if (item.logType === 'RoleChange') {
      return (
        <div>
          <span style={{ fontWeight: 600, color: '#1D4ED8' }}>
            {item.actionType === 'Grant'
              ? 'Cấp vai trò'
              : item.actionType === 'Revoke'
              ? 'Thu hồi vai trò'
              : 'Điều chỉnh vai trò'}
          </span>
          <div style={{ fontSize: '11px', color: '#64748B', marginTop: 2 }}>
            <span style={{ color: '#94A3B8', textDecoration: item.oldValue ? 'line-through' : 'none' }}>
              {item.oldValue || '(Không)'}
            </span>
            {' ➔ '}
            <span style={{ fontWeight: 600, color: '#0F172A' }}>{item.newValue || '(Không)'}</span>
          </div>
        </div>
      );
    }

    if (item.logType === 'AccountStatus') {
      const isSuspended = item.newValue === 'Suspended';
      return (
        <div>
          <span style={{ fontWeight: 600, color: isSuspended ? '#DC2626' : '#16A34A' }}>
            {isSuspended ? 'Tạm ngưng tài khoản' : 'Khôi phục tài khoản'}
          </span>
          {item.notes && (
            <div style={{ fontSize: '11px', color: '#64748B', marginTop: 2 }}>
              Lý do: {item.notes}
            </div>
          )}
        </div>
      );
    }

    return (
      <div>
        <span style={{ fontWeight: 600, color: '#334155' }}>{item.actionType || 'Hành động hệ thống'}</span>
        {item.targetEntity && (
          <div style={{ fontSize: '11px', color: '#64748B' }}>
            Thực thể: {item.targetEntity} {item.targetId ? `(#${item.targetId})` : ''}
          </div>
        )}
      </div>
    );
  };

  const formatJson = (str?: string | null) => {
    if (!str) return null;
    try {
      const obj = JSON.parse(str);
      return JSON.stringify(obj, null, 2);
    } catch {
      return str;
    }
  };

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
            Nhật ký Kiểm toán Hệ thống (Audit Logs)
          </h1>
          <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
            Giám sát minh bạch các phiên đăng nhập hộ (Impersonation), thay đổi quyền hạn tài khoản và trạng thái hoạt động.
          </p>
        </div>

        <button
          type="button"
          onClick={fetchLogs}
          disabled={loading}
          style={primaryBtn}
        >
          {loading ? 'Đang tải...' : '🔄 Làm mới'}
        </button>
      </div>

      {/* Error alert */}
      {error && (
        <div
          style={{
            backgroundColor: '#FEF2F2',
            border: '1px solid #FECACA',
            color: '#B91C1C',
            padding: '12px 16px',
            borderRadius: '8px',
            marginBottom: 16,
            fontSize: '13px',
          }}
        >
          {error}
        </div>
      )}

      {/* Filters Card */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          border: '1px solid #E2E8F0',
          borderRadius: '12px',
          padding: '16px 20px',
          marginBottom: 20,
          boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
        }}
      >
        <div style={{ display: 'grid', gridTemplateColumns: '2fr 1.5fr 1fr 1fr auto', gap: '12px', alignItems: 'flex-end' }}>
          <div>
            <label style={labelStyle}>Tìm kiếm</label>
            <input
              type="text"
              placeholder="Tên, mã số, ghi chú, hành động..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && setPage(1)}
              style={inputStyle}
            />
          </div>

          <div>
            <label style={labelStyle}>Loại sự kiện</label>
            <select
              value={logType}
              onChange={(e) => {
                setLogType(e.target.value);
                setPage(1);
              }}
              style={inputStyle}
            >
              {LOG_TYPES.map((t) => (
                <option key={t.value} value={t.value}>
                  {t.label}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={labelStyle}>Từ ngày</label>
            <input
              type="date"
              value={fromDate}
              onChange={(e) => {
                setFromDate(e.target.value);
                setPage(1);
              }}
              style={inputStyle}
            />
          </div>

          <div>
            <label style={labelStyle}>Đến ngày</label>
            <input
              type="date"
              value={toDate}
              onChange={(e) => {
                setToDate(e.target.value);
                setPage(1);
              }}
              style={inputStyle}
            />
          </div>

          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              onClick={handleResetFilters}
              style={secondaryBtn}
              title="Xóa toàn bộ bộ lọc"
            >
              Đặt lại
            </button>
          </div>
        </div>

        {/* Stats bar */}
        <div style={{ marginTop: 14, paddingTop: 12, borderTop: '1px solid #F1F5F9', display: 'flex', alignItems: 'center', justifyContent: 'space-between', fontSize: '12px', color: '#64748B' }}>
          <div>
            Tổng số sự kiện ghi nhận: <strong style={{ color: '#0F172A' }}>{totalCount}</strong>
            {logType !== 'All' && <span> • Đang lọc theo: <strong>{LOG_META[logType]?.label || logType}</strong></span>}
            {fromDate && <span> • Từ: <strong>{fromDate}</strong></span>}
            {toDate && <span> • Đến: <strong>{toDate}</strong></span>}
          </div>
          <div>Trang {page} / {Math.max(1, Math.ceil(totalCount / PAGE_SIZE))}</div>
        </div>
      </div>

      {/* Table */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          border: '1px solid #E2E8F0',
          borderRadius: '12px',
          overflow: 'hidden',
          boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
        }}
      >
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid #E2E8F0', color: '#475569', fontSize: '12px' }}>
                <th style={{ padding: '12px 16px', width: '60px' }}>#ID</th>
                <th style={{ padding: '12px 16px', width: '170px' }}>Thời điểm</th>
                <th style={{ padding: '12px 16px', width: '130px' }}>Loại sự kiện</th>
                <th style={{ padding: '12px 16px', width: '180px' }}>Người thực hiện (Actor)</th>
                <th style={{ padding: '12px 16px', width: '180px' }}>Đối tượng tác động</th>
                <th style={{ padding: '12px 16px' }}>Hành động & Tóm tắt</th>
                <th style={{ padding: '12px 16px', width: '100px', textAlign: 'center' }}>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {loading && logs.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: '48px', textAlign: 'center', color: '#94A3B8' }}>
                    Đang tải dữ liệu kiểm toán...
                  </td>
                </tr>
              ) : logs.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: '48px', textAlign: 'center', color: '#94A3B8' }}>
                    <div style={{ fontSize: '32px', marginBottom: 8 }}>📋</div>
                    Không tìm thấy bản ghi kiểm toán nào phù hợp với bộ lọc.
                  </td>
                </tr>
              ) : (
                logs.map((item) => {
                  const meta = LOG_META[item.logType] || LOG_META.System;
                  return (
                    <tr
                      key={`${item.logType}-${item.id}`}
                      style={{
                        borderBottom: '1px solid #F1F5F9',
                        transition: 'background-color 0.15s',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#F8FAFC')}
                      onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                    >
                      <td style={{ padding: '12px 16px', color: '#94A3B8', fontWeight: 600 }}>
                        #{item.id}
                      </td>

                      <td style={{ padding: '12px 16px', whiteSpace: 'nowrap' }}>
                        <div style={{ fontWeight: 600, color: '#0F172A' }}>
                          {formatDateTime(item.createdAt)}
                        </div>
                      </td>

                      <td style={{ padding: '12px 16px' }}>
                        <span
                          style={{
                            display: 'inline-block',
                            padding: '3px 8px',
                            borderRadius: '9999px',
                            backgroundColor: meta.bg,
                            color: meta.color,
                            border: `1px solid ${meta.border}`,
                            fontSize: '11px',
                            fontWeight: 700,
                            whiteSpace: 'nowrap',
                          }}
                        >
                          {meta.label}
                        </span>
                      </td>

                      <td style={{ padding: '12px 16px' }}>
                        <div style={{ fontWeight: 600, color: '#0F172A' }}>
                          {item.actorName || '(Không xác định)'}
                        </div>
                        {item.actorCode && (
                          <div style={{ fontSize: '11px', color: '#64748B' }}>
                            {item.actorCode} {item.actorId ? `[ID: ${item.actorId}]` : ''}
                          </div>
                        )}
                      </td>

                      <td style={{ padding: '12px 16px' }}>
                        {item.targetUserName ? (
                          <>
                            <div style={{ fontWeight: 600, color: '#0F172A' }}>
                              {item.targetUserName}
                            </div>
                            <div style={{ fontSize: '11px', color: '#64748B' }}>
                              {item.targetUserCode || `ID: ${item.targetUserId}`}
                            </div>
                          </>
                        ) : item.targetEntity ? (
                          <div style={{ color: '#64748B' }}>
                            {item.targetEntity} #{item.targetId || ''}
                          </div>
                        ) : (
                          <span style={{ color: '#94A3B8' }}>—</span>
                        )}
                      </td>

                      <td style={{ padding: '12px 16px' }}>
                        {renderActionSummary(item)}
                      </td>

                      <td style={{ padding: '12px 16px', textAlign: 'center' }}>
                        <button
                          type="button"
                          onClick={() => handleViewDetail(item)}
                          style={{
                            padding: '5px 10px',
                            backgroundColor: '#EFF6FF',
                            color: '#1D4ED8',
                            border: '1px solid #BFDBFE',
                            borderRadius: '6px',
                            fontSize: '12px',
                            fontWeight: 600,
                            cursor: 'pointer',
                          }}
                        >
                          Chi tiết
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalCount > 0 && (
          <div style={{ padding: '16px 20px', borderTop: '1px solid #E2E8F0', display: 'flex', justifyContent: 'center' }}>
            <Pagination
              currentPage={page}
              totalItems={totalCount}
              pageSize={PAGE_SIZE}
              onPageChange={setPage}
              itemName="nhật ký"
            />
          </div>
        )}
      </div>

      {/* Inspection Modal */}
      {selectedLog && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: '20px',
          }}
          onClick={() => setSelectedLog(null)}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              maxWidth: '680px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              padding: '24px',
              boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20, borderBottom: '1px solid #F1F5F9', paddingBottom: 14 }}>
              <div>
                <span
                  style={{
                    display: 'inline-block',
                    padding: '2px 8px',
                    borderRadius: '9999px',
                    backgroundColor: LOG_META[selectedLog.logType]?.bg || '#F1F5F9',
                    color: LOG_META[selectedLog.logType]?.color || '#475569',
                    fontSize: '11px',
                    fontWeight: 700,
                    marginBottom: 4,
                  }}
                >
                  {LOG_META[selectedLog.logType]?.label || selectedLog.logType}
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: '#0F172A' }}>
                  Chi tiết sự kiện kiểm toán #{selectedLog.id}
                </h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedLog(null)}
                style={{ background: 'none', border: 'none', fontSize: '14px', cursor: 'pointer', color: '#94A3B8' }}
              >
                ✕ Đóng
              </button>
            </div>

            {modalLoading ? (
              <div style={{ padding: '32px', textAlign: 'center', color: '#94A3B8' }}>
                Đang tải chi tiết...
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                {/* Meta details grid */}
                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: '1fr 1fr',
                    gap: '12px',
                    backgroundColor: '#F8FAFC',
                    padding: '16px',
                    borderRadius: '10px',
                    fontSize: '13px',
                  }}
                >
                  <div>
                    <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700 }}>THỜI ĐIỂM THỰC HIỆN</span>
                    <strong style={{ color: '#0F172A' }}>{formatDateTime(selectedLog.createdAt)}</strong>
                  </div>

                  <div>
                    <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700 }}>HÀNH ĐỘNG (ACTION)</span>
                    <strong style={{ color: '#0F172A' }}>{selectedLog.actionType || 'N/A'}</strong>
                  </div>

                  <div>
                    <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700 }}>NGƯỜI THỰC HIỆN (ACTOR)</span>
                    <strong style={{ color: '#0F172A' }}>
                      {selectedLog.actorName || 'Hệ thống'}
                      {selectedLog.actorCode && ` (${selectedLog.actorCode})`}
                    </strong>
                    {selectedLog.actorId && (
                      <span style={{ color: '#64748B', fontSize: '11px', display: 'block' }}>User ID: #{selectedLog.actorId}</span>
                    )}
                  </div>

                  <div>
                    <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700 }}>ĐỐI TƯỢNG TÁC ĐỘNG (TARGET)</span>
                    <strong style={{ color: '#0F172A' }}>
                      {selectedLog.targetUserName || selectedLog.targetEntity || 'N/A'}
                      {selectedLog.targetUserCode && ` (${selectedLog.targetUserCode})`}
                    </strong>
                    {selectedLog.targetUserId && (
                      <span style={{ color: '#64748B', fontSize: '11px', display: 'block' }}>User ID: #{selectedLog.targetUserId}</span>
                    )}
                  </div>

                  {selectedLog.ipAddress && (
                    <div>
                      <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700 }}>ĐỊA CHỈ IP</span>
                      <code style={{ color: '#0F172A', backgroundColor: '#E2E8F0', padding: '2px 6px', borderRadius: '4px', fontSize: '12px' }}>
                        {selectedLog.ipAddress}
                      </code>
                    </div>
                  )}

                  {selectedLog.userAgent && (
                    <div>
                      <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700 }}>TRÌNH DUYỆT / CLIENT</span>
                      <span style={{ color: '#334155', fontSize: '12px', wordBreak: 'break-all' }}>
                        {selectedLog.userAgent}
                      </span>
                    </div>
                  )}
                </div>

                {/* Impersonation Specific Note */}
                {selectedLog.logType === 'Impersonation' && (
                  <div
                    style={{
                      backgroundColor: '#FEF3C7',
                      border: '1px solid #FCD34D',
                      borderRadius: '8px',
                      padding: '12px 16px',
                      fontSize: '12px',
                      color: '#92400E',
                      lineHeight: 1.5,
                    }}
                  >
                    <strong>Cảnh báo an ninh:</strong> Phiên đăng nhập hộ cho phép Ban Quản lý thao tác với vai trò của sinh viên trong thời lượng 30 phút. Hành động được kiểm soát nghiêm ngặt và lưu vết IP/UserAgent của người thực hiện.
                  </div>
                )}

                {/* Change Comparison (Old vs New) */}
                {(selectedLog.oldValue || selectedLog.newValue) && (
                  <div>
                    <h4 style={{ fontSize: '13px', fontWeight: 700, margin: '0 0 8px', color: '#334155' }}>
                      Dữ liệu thay đổi
                    </h4>
                    <div style={{ display: 'grid', gridTemplateColumns: selectedLog.oldValue ? '1fr 1fr' : '1fr', gap: '12px' }}>
                      {selectedLog.oldValue && (
                        <div
                          style={{
                            border: '1px solid #FECACA',
                            backgroundColor: '#FEF2F2',
                            padding: '12px',
                            borderRadius: '8px',
                          }}
                        >
                          <span style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#B91C1C', marginBottom: 4 }}>
                            GIÁ TRỊ TRƯỚC THAY ĐỔI (OLD)
                          </span>
                          <div style={{ fontSize: '13px', color: '#7F1D1D', wordBreak: 'break-all' }}>
                            {selectedLog.oldValue}
                          </div>
                        </div>
                      )}

                      <div
                        style={{
                          border: '1px solid #BBF7D0',
                          backgroundColor: '#F0FDF4',
                          padding: '12px',
                          borderRadius: '8px',
                        }}
                      >
                        <span style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#15803D', marginBottom: 4 }}>
                          GIÁ TRỊ SAU THAY ĐỔI (NEW)
                        </span>
                        <div style={{ fontSize: '13px', color: '#14532D', wordBreak: 'break-all' }}>
                          {selectedLog.newValue}
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {/* Notes if any */}
                {selectedLog.notes && (
                  <div style={{ backgroundColor: '#F8FAFC', padding: '12px 16px', borderRadius: '8px', border: '1px solid #E2E8F0' }}>
                    <span style={{ color: '#64748B', display: 'block', fontSize: '11px', fontWeight: 700, marginBottom: 2 }}>
                      GHI CHÚ / LÝ DO
                    </span>
                    <span style={{ color: '#0F172A', fontSize: '13px' }}>{selectedLog.notes}</span>
                  </div>
                )}

                {/* Raw JSON Payload */}
                {(selectedLog.rawPayload || selectedLog.newValue) && (
                  <div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6 }}>
                      <span style={{ fontSize: '11px', fontWeight: 700, color: '#64748B' }}>
                        RAW JSON PAYLOAD
                      </span>
                      <button
                        type="button"
                        onClick={() => handleCopyPayload(selectedLog.rawPayload || selectedLog.newValue || '')}
                        style={{
                          background: 'none',
                          border: 'none',
                          color: '#2563EB',
                          fontSize: '11px',
                          fontWeight: 600,
                          cursor: 'pointer',
                        }}
                      >
                        {copied ? '✓ Đã sao chép!' : '📋 Sao chép JSON'}
                      </button>
                    </div>
                    <pre
                      style={{
                        backgroundColor: '#0F172A',
                        color: '#F8FAFC',
                        padding: '12px',
                        borderRadius: '8px',
                        fontSize: '11px',
                        overflowX: 'auto',
                        maxHeight: '160px',
                        margin: 0,
                      }}
                    >
                      {formatJson(selectedLog.rawPayload || selectedLog.newValue)}
                    </pre>
                  </div>
                )}

                {/* Close Button */}
                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 8 }}>
                  <button
                    type="button"
                    onClick={() => setSelectedLog(null)}
                    style={secondaryBtn}
                  >
                    Đóng
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

