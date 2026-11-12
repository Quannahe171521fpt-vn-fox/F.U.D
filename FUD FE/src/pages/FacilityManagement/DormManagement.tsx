import React, { useState, useEffect, useCallback } from 'react';
import { dormApi, type DormItem } from '../../services/api';

// ─── Types ──────────────────────────────────────────────────
interface DormFormData {
  campusId: number;
  code: string;
  name: string;
  address: string;
}

const EMPTY_FORM: DormFormData = { campusId: 1, code: '', name: '', address: '' };

// ─── Component ──────────────────────────────────────────────
export const DormManagement: React.FC = () => {
  const [dorms, setDorms] = useState<DormItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchName, setSearchName] = useState('');
  const [filterStatus, setFilterStatus] = useState('All');

  // Modal state
  const [showModal, setShowModal] = useState(false);
  const [editingDorm, setEditingDorm] = useState<DormItem | null>(null);
  const [formData, setFormData] = useState<DormFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Delete confirm
  const [deleteTarget, setDeleteTarget] = useState<DormItem | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  // ── Load data ───────────────────────────────────────────
  const loadDorms = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await dormApi.getAll({
        dormName: searchName || undefined,
        status: filterStatus !== 'All' ? filterStatus : undefined,
        page,
        pageSize,
      });
      setDorms(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      setError(err.message || 'Không thể tải danh sách KTX');
    } finally {
      setIsLoading(false);
    }
  }, [searchName, filterStatus, page, pageSize]);

  useEffect(() => { loadDorms(); }, [loadDorms]);

  // ── Handlers ────────────────────────────────────────────
  const openCreate = () => {
    setEditingDorm(null);
    setFormData(EMPTY_FORM);
    setFormError(null);
    setShowModal(true);
  };

  const openEdit = (dorm: DormItem) => {
    setEditingDorm(dorm);
    setFormData({ campusId: dorm.campusId, code: dorm.code, name: dorm.name, address: dorm.address || '' });
    setFormError(null);
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!formData.code.trim()) { setFormError('Mã KTX là bắt buộc'); return; }
    if (!formData.name.trim()) { setFormError('Tên KTX là bắt buộc'); return; }
    setIsSaving(true);
    setFormError(null);
    try {
      if (editingDorm) {
        await dormApi.update(editingDorm.id, { name: formData.name, address: formData.address || undefined });
      } else {
        await dormApi.create({
          campusId: formData.campusId,
          code: formData.code,
          name: formData.name,
          address: formData.address || undefined,
        });
      }
      setShowModal(false);
      await loadDorms();
    } catch (err: any) {
      setFormError(err.message || 'Lỗi khi lưu KTX');
    } finally {
      setIsSaving(false);
    }
  };

  const handleToggleStatus = async (dorm: DormItem) => {
    const newStatus = dorm.status === 'Active' ? 'Inactive' : 'Active';
    try {
      await dormApi.updateStatus(dorm.id, newStatus);
      await loadDorms();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi đổi trạng thái KTX');
    }
  };

  const confirmDelete = (dorm: DormItem) => {
    setDeleteTarget(dorm);
    setDeleteError(null);
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    setDeleteError(null);
    try {
      await dormApi.softDelete(deleteTarget.id);
      setDeleteTarget(null);
      await loadDorms();
    } catch (err: any) {
      setDeleteError(err.message || 'Lỗi khi xóa KTX');
    } finally {
      setIsDeleting(false);
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize);

  // ── Render ──────────────────────────────────────────────
  return (
    <div className="dorm-management-container">
      {/* Header */}
      <div className="section-header">
        <div>
          <h2 className="section-title">Quản lý Ký túc xá</h2>
          <p className="section-subtitle">Quản lý danh sách các Ký túc xá trong hệ thống FUD</p>
        </div>
        <button className="btn-primary" onClick={openCreate}>
          <span>＋</span> Thêm KTX
        </button>
      </div>

      {/* Filters */}
      <div className="filter-bar">
        <div className="filter-group">
          <input
            className="filter-input"
            placeholder="Tìm theo tên hoặc mã KTX..."
            value={searchName}
            onChange={e => { setSearchName(e.target.value); setPage(1); }}
          />
        </div>
        <div className="filter-group">
          <select
            className="filter-select"
            value={filterStatus}
            onChange={e => { setFilterStatus(e.target.value); setPage(1); }}
          >
            <option value="All">Tất cả trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Inactive">Ngừng hoạt động</option>
          </select>
        </div>
        <button className="btn-secondary" onClick={() => loadDorms()}>
          🔄 Làm mới
        </button>
      </div>

      {/* Error banner */}
      {error && (
        <div className="error-banner">
          <span>⚠️ {error}</span>
          <button onClick={() => setError(null)}>✕</button>
        </div>
      )}

      {/* Table */}
      <div className="table-container">
        {isLoading ? (
          <div className="loading-state">
            <div className="loading-spinner" />
            <p>Đang tải dữ liệu KTX...</p>
          </div>
        ) : dorms.length === 0 ? (
          <div className="empty-state">
            <div className="empty-icon">🏢</div>
            <p className="empty-title">Chưa có KTX nào</p>
            <p className="empty-subtitle">Nhấn "Thêm KTX" để tạo mới</p>
          </div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Mã KTX</th>
                <th>Tên Ký Túc Xá</th>
                <th>Thuộc Campus</th>
                <th>Địa chỉ</th>
                <th style={{ textAlign: 'center' }}>Số block</th>
                <th style={{ textAlign: 'center' }}>Trạng thái</th>
                <th style={{ textAlign: 'center' }}>Hành động</th>
              </tr>
            </thead>
            <tbody>
              {dorms.map(dorm => (
                <tr key={dorm.id}>
                  <td>
                    <span className="code-badge">{dorm.code}</span>
                  </td>
                  <td className="name-cell">{dorm.name}</td>
                  <td>{dorm.campusName}</td>
                  <td className="address-cell">{dorm.address || <span className="text-muted">—</span>}</td>
                  <td style={{ textAlign: 'center' }}>
                    <span className="count-badge">{dorm.totalBlocks}</span>
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <button
                      className={`status-badge ${dorm.status === 'Active' ? 'status-active' : 'status-inactive'}`}
                      onClick={() => handleToggleStatus(dorm)}
                      title="Nhấn để đổi trạng thái"
                    >
                      {dorm.status === 'Active' ? '✓ Hoạt động' : '✗ Ngừng'}
                    </button>
                  </td>
                  <td style={{ textAlign: 'center' }}>
                    <div className="action-btns">
                      <button className="btn-action btn-edit" onClick={() => openEdit(dorm)} title="Chỉnh sửa">
                        ✏️
                      </button>
                      <button
                        className="btn-action btn-delete"
                        onClick={() => confirmDelete(dorm)}
                        title="Xóa KTX"
                      >
                        🗑️
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="pagination-bar">
          <span className="pagination-info">
            Hiển thị {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, totalCount)} / {totalCount} KTX
          </span>
          <div className="pagination-btns">
            <button
              className="btn-page"
              disabled={page <= 1}
              onClick={() => setPage(p => p - 1)}
            >← Trước</button>
            {(() => {
              const pages: (number | '...')[] = [];
              for (let i = 1; i <= totalPages; i++) {
                if (i === 1 || i === totalPages || Math.abs(i - page) <= 1) {
                  pages.push(i);
                } else if (pages[pages.length - 1] !== '...') {
                  pages.push('...');
                }
              }
              return pages.map((p, idx) =>
                p === '...' ? (
                  <span key={`dots-${idx}`} style={{ padding: '0 6px', color: '#94a3b8', lineHeight: '30px' }}>
                    …
                  </span>
                ) : (
                  <button
                    key={p}
                    className={`btn-page ${p === page ? 'active' : ''}`}
                    onClick={() => setPage(p as number)}
                  >{p}</button>
                )
              );
            })()}
            <button
              className="btn-page"
              disabled={page >= totalPages}
              onClick={() => setPage(p => p + 1)}
            >Sau →</button>
          </div>
        </div>
      )}

      {/* ── Create/Edit Modal ─────────────────────────────── */}
      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-box" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{editingDorm ? 'Chỉnh sửa Ký Túc Xá' : 'Thêm Ký Túc Xá Mới'}</h3>
              <button className="modal-close" onClick={() => setShowModal(false)}>✕</button>
            </div>
            <div className="modal-body">
              {formError && <div className="form-error-box">⚠️ {formError}</div>}

              {!editingDorm && (
                <>
                  <div className="form-group">
                    <label className="form-label">Mã KTX <span className="required">*</span></label>
                    <input
                      className="form-input"
                      placeholder="VD: DOM-B"
                      value={formData.code}
                      onChange={e => setFormData(f => ({ ...f, code: e.target.value }))}
                    />
                    <span className="form-hint">Mã duy nhất trong cùng Campus (không thể thay đổi sau khi tạo)</span>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Campus ID <span className="required">*</span></label>
                    <input
                      className="form-input"
                      type="number"
                      min={1}
                      value={formData.campusId}
                      onChange={e => setFormData(f => ({ ...f, campusId: Number(e.target.value) }))}
                    />
                  </div>
                </>
              )}

              {editingDorm && (
                <div className="form-group">
                  <label className="form-label">Mã KTX</label>
                  <input className="form-input" value={editingDorm.code} disabled />
                  <span className="form-hint">Mã KTX không thể thay đổi</span>
                </div>
              )}

              <div className="form-group">
                <label className="form-label">Tên Ký Túc Xá <span className="required">*</span></label>
                <input
                  className="form-input"
                  placeholder="VD: Ký túc xá Dom B"
                  value={formData.name}
                  onChange={e => setFormData(f => ({ ...f, name: e.target.value }))}
                />
              </div>

              <div className="form-group">
                <label className="form-label">Địa chỉ</label>
                <input
                  className="form-input"
                  placeholder="VD: Khu Công nghệ cao Hòa Lạc"
                  value={formData.address}
                  onChange={e => setFormData(f => ({ ...f, address: e.target.value }))}
                />
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn-secondary" onClick={() => setShowModal(false)} disabled={isSaving}>
                Hủy
              </button>
              <button className="btn-primary" onClick={handleSave} disabled={isSaving}>
                {isSaving ? 'Đang lưu...' : (editingDorm ? 'Cập nhật' : 'Tạo mới')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Delete Confirm Modal ─────────────────────────── */}
      {deleteTarget && (
        <div className="modal-overlay" onClick={() => !isDeleting && setDeleteTarget(null)}>
          <div className="modal-box modal-confirm" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Xác nhận xóa KTX</h3>
              <button className="modal-close" onClick={() => setDeleteTarget(null)} disabled={isDeleting}>✕</button>
            </div>
            <div className="modal-body">
              {deleteError ? (
                <div className="delete-error-box">
                  <div className="delete-error-icon">🚫</div>
                  <p className="delete-error-title">Không thể xóa KTX này</p>
                  <p className="delete-error-msg">{deleteError}</p>
                  <p className="delete-error-hint">
                    Hãy xóa hoặc vô hiệu hóa tất cả các <strong>Block</strong> bên trong KTX trước khi xóa.
                  </p>
                </div>
              ) : (
                <div className="confirm-body">
                  <div className="confirm-icon">⚠️</div>
                  <p>Bạn có chắc muốn xóa KTX:</p>
                  <p className="confirm-target-name">
                    <strong>{deleteTarget.name}</strong> ({deleteTarget.code})
                  </p>
                  {deleteTarget.totalBlocks > 0 && (
                    <div className="confirm-warning">
                      ⚠️ KTX này đang có <strong>{deleteTarget.totalBlocks} block</strong>. Thao tác sẽ bị từ chối.
                    </div>
                  )}
                  <p className="confirm-note">Dữ liệu sẽ bị ẩn khỏi hệ thống (xóa mềm).</p>
                </div>
              )}
            </div>
            <div className="modal-footer">
              {deleteError ? (
                <button className="btn-primary" onClick={() => setDeleteTarget(null)}>
                  Đã hiểu
                </button>
              ) : (
                <>
                  <button className="btn-secondary" onClick={() => setDeleteTarget(null)} disabled={isDeleting}>
                    Hủy
                  </button>
                  <button className="btn-danger" onClick={handleDelete} disabled={isDeleting}>
                    {isDeleting ? 'Đang xóa...' : 'Xác nhận xóa'}
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      <style>{`
        /* ── Container ── */
        .dorm-management-container {
          padding: 0;
          display: flex;
          flex-direction: column;
          gap: 1.25rem;
        }

        /* ── Header ── */
        .section-header {
          display: flex;
          align-items: flex-start;
          justify-content: space-between;
          gap: 1rem;
          padding-bottom: 1.25rem;
          border-bottom: 1px solid rgba(148, 163, 184, 0.15);
        }
        .section-title {
          font-size: 1.4rem;
          font-weight: 700;
          color: #f1f5f9;
          margin: 0 0 0.25rem 0;
        }
        .section-subtitle {
          font-size: 0.85rem;
          color: #64748b;
          margin: 0;
        }

        /* ── Buttons ── */
        .btn-primary {
          display: inline-flex;
          align-items: center;
          gap: 0.4rem;
          padding: 0.55rem 1.2rem;
          background: linear-gradient(135deg, #6366f1, #4f46e5);
          color: #fff;
          border: none;
          border-radius: 8px;
          font-size: 0.875rem;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.2s ease;
          white-space: nowrap;
        }
        .btn-primary:hover { background: linear-gradient(135deg, #4f46e5, #4338ca); transform: translateY(-1px); }
        .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; transform: none; }

        .btn-secondary {
          padding: 0.55rem 1rem;
          background: rgba(255,255,255,0.06);
          color: #94a3b8;
          border: 1px solid rgba(148,163,184,0.15);
          border-radius: 8px;
          font-size: 0.875rem;
          cursor: pointer;
          transition: all 0.15s ease;
        }
        .btn-secondary:hover { background: rgba(255,255,255,0.1); color: #e2e8f0; }
        .btn-secondary:disabled { opacity: 0.5; cursor: not-allowed; }

        .btn-danger {
          padding: 0.55rem 1rem;
          background: linear-gradient(135deg, #ef4444, #dc2626);
          color: #fff;
          border: none;
          border-radius: 8px;
          font-size: 0.875rem;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.2s ease;
        }
        .btn-danger:hover { background: linear-gradient(135deg, #dc2626, #b91c1c); }
        .btn-danger:disabled { opacity: 0.6; cursor: not-allowed; }

        /* ── Filter bar ── */
        .filter-bar {
          display: flex;
          align-items: center;
          gap: 0.75rem;
          flex-wrap: wrap;
        }
        .filter-group { display: flex; align-items: center; }
        .filter-input, .filter-select {
          padding: 0.5rem 0.85rem;
          background: rgba(255,255,255,0.05);
          border: 1px solid rgba(148,163,184,0.2);
          border-radius: 8px;
          color: #e2e8f0;
          font-size: 0.875rem;
          outline: none;
          transition: border-color 0.2s;
          min-width: 200px;
        }
        .filter-input:focus, .filter-select:focus { border-color: #6366f1; }
        .filter-input::placeholder { color: #475569; }
        .filter-select option { background: #1e293b; }

        /* ── Error banner ── */
        .error-banner {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 0.75rem 1rem;
          background: rgba(239,68,68,0.12);
          border: 1px solid rgba(239,68,68,0.25);
          border-radius: 8px;
          color: #fca5a5;
          font-size: 0.875rem;
        }
        .error-banner button { background: none; border: none; color: #fca5a5; cursor: pointer; font-size: 1rem; }

        /* ── Table ── */
        .table-container {
          background: rgba(255,255,255,0.03);
          border: 1px solid rgba(148,163,184,0.1);
          border-radius: 12px;
          overflow: hidden;
        }
        .data-table {
          width: 100%;
          border-collapse: collapse;
          font-size: 0.875rem;
        }
        .data-table thead { background: rgba(99,102,241,0.08); }
        .data-table th {
          padding: 0.85rem 1rem;
          color: #94a3b8;
          font-weight: 600;
          font-size: 0.78rem;
          text-transform: uppercase;
          letter-spacing: 0.05em;
          text-align: left;
        }
        .data-table td {
          padding: 0.85rem 1rem;
          color: #e2e8f0;
          border-top: 1px solid rgba(148,163,184,0.08);
        }
        .data-table tbody tr { transition: background 0.15s; }
        .data-table tbody tr:hover { background: rgba(255,255,255,0.03); }

        /* ── Table cell styles ── */
        .code-badge {
          display: inline-block;
          padding: 0.2rem 0.6rem;
          background: rgba(99,102,241,0.15);
          color: #a5b4fc;
          border-radius: 6px;
          font-size: 0.78rem;
          font-weight: 700;
          font-family: monospace;
        }
        .name-cell { font-weight: 600; color: #f1f5f9; }
        .address-cell { color: #94a3b8; font-size: 0.82rem; max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
        .text-muted { color: #475569; }
        .count-badge {
          display: inline-block;
          padding: 0.15rem 0.6rem;
          background: rgba(148,163,184,0.1);
          color: #94a3b8;
          border-radius: 20px;
          font-size: 0.8rem;
          font-weight: 600;
        }

        /* ── Status badge ── */
        .status-badge {
          display: inline-flex;
          align-items: center;
          gap: 0.3rem;
          padding: 0.25rem 0.75rem;
          border-radius: 20px;
          font-size: 0.78rem;
          font-weight: 600;
          cursor: pointer;
          border: none;
          transition: all 0.2s;
        }
        .status-active { background: rgba(34,197,94,0.15); color: #4ade80; }
        .status-active:hover { background: rgba(34,197,94,0.25); }
        .status-inactive { background: rgba(239,68,68,0.12); color: #f87171; }
        .status-inactive:hover { background: rgba(239,68,68,0.22); }

        /* ── Action buttons ── */
        .action-btns { display: flex; align-items: center; gap: 0.4rem; justify-content: center; }
        .btn-action {
          padding: 0.35rem 0.6rem;
          border: none;
          border-radius: 6px;
          cursor: pointer;
          font-size: 0.9rem;
          transition: all 0.15s;
          background: transparent;
        }
        .btn-edit:hover { background: rgba(99,102,241,0.15); }
        .btn-delete:hover { background: rgba(239,68,68,0.15); }

        /* ── Loading / Empty ── */
        .loading-state {
          display: flex;
          flex-direction: column;
          align-items: center;
          gap: 1rem;
          padding: 3rem;
          color: #64748b;
        }
        .loading-spinner {
          width: 32px; height: 32px;
          border: 3px solid rgba(99,102,241,0.2);
          border-top-color: #6366f1;
          border-radius: 50%;
          animation: spin 0.8s linear infinite;
        }
        @keyframes spin { to { transform: rotate(360deg); } }
        .empty-state { padding: 4rem 2rem; text-align: center; color: #64748b; }
        .empty-icon { font-size: 2.5rem; margin-bottom: 0.75rem; }
        .empty-title { font-size: 1rem; font-weight: 600; color: #94a3b8; margin: 0 0 0.35rem; }
        .empty-subtitle { font-size: 0.85rem; margin: 0; }

        /* ── Pagination ── */
        .pagination-bar {
          display: flex;
          align-items: center;
          justify-content: space-between;
          flex-wrap: wrap;
          gap: 0.75rem;
        }
        .pagination-info { font-size: 0.82rem; color: #64748b; }
        .pagination-btns { display: flex; gap: 0.35rem; }
        .btn-page {
          padding: 0.4rem 0.75rem;
          background: rgba(255,255,255,0.04);
          border: 1px solid rgba(148,163,184,0.15);
          border-radius: 6px;
          color: #94a3b8;
          font-size: 0.82rem;
          cursor: pointer;
          transition: all 0.15s;
        }
        .btn-page:hover:not(:disabled) { background: rgba(99,102,241,0.15); color: #a5b4fc; border-color: #6366f1; }
        .btn-page.active { background: #6366f1; color: #fff; border-color: #6366f1; }
        .btn-page:disabled { opacity: 0.4; cursor: not-allowed; }

        /* ── Modal ── */
        .modal-overlay {
          position: fixed; inset: 0;
          background: rgba(0,0,0,0.65);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 1000;
          backdrop-filter: blur(4px);
        }
        .modal-box {
          background: #1e293b;
          border: 1px solid rgba(148,163,184,0.15);
          border-radius: 16px;
          width: 100%;
          max-width: 480px;
          display: flex;
          flex-direction: column;
          box-shadow: 0 25px 60px rgba(0,0,0,0.5);
          animation: slideUp 0.2s ease;
        }
        .modal-confirm { max-width: 420px; }
        @keyframes slideUp { from { opacity: 0; transform: translateY(20px); } to { opacity: 1; transform: translateY(0); } }
        .modal-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 1.25rem 1.5rem 1rem;
          border-bottom: 1px solid rgba(148,163,184,0.1);
        }
        .modal-header h3 { font-size: 1.05rem; font-weight: 700; color: #f1f5f9; margin: 0; }
        .modal-close {
          background: none; border: none;
          color: #64748b; font-size: 1.1rem; cursor: pointer;
          padding: 0.25rem; line-height: 1; border-radius: 4px;
          transition: color 0.15s;
        }
        .modal-close:hover { color: #e2e8f0; }
        .modal-body { padding: 1.25rem 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
        .modal-footer {
          display: flex;
          justify-content: flex-end;
          gap: 0.75rem;
          padding: 1rem 1.5rem 1.25rem;
          border-top: 1px solid rgba(148,163,184,0.1);
        }

        /* ── Form fields ── */
        .form-group { display: flex; flex-direction: column; gap: 0.4rem; }
        .form-label { font-size: 0.85rem; font-weight: 600; color: #94a3b8; }
        .required { color: #f87171; }
        .form-input {
          padding: 0.6rem 0.85rem;
          background: rgba(255,255,255,0.04);
          border: 1px solid rgba(148,163,184,0.2);
          border-radius: 8px;
          color: #e2e8f0;
          font-size: 0.875rem;
          outline: none;
          transition: border-color 0.2s;
        }
        .form-input:focus { border-color: #6366f1; }
        .form-input:disabled { opacity: 0.5; cursor: not-allowed; }
        .form-input::placeholder { color: #475569; }
        .form-hint { font-size: 0.78rem; color: #475569; }
        .form-error-box {
          padding: 0.65rem 0.9rem;
          background: rgba(239,68,68,0.1);
          border: 1px solid rgba(239,68,68,0.2);
          border-radius: 8px;
          color: #fca5a5;
          font-size: 0.83rem;
        }

        /* ── Delete confirm ── */
        .confirm-body { text-align: center; display: flex; flex-direction: column; align-items: center; gap: 0.5rem; }
        .confirm-icon { font-size: 2rem; }
        .confirm-body p { margin: 0; color: #94a3b8; font-size: 0.9rem; }
        .confirm-target-name { font-size: 1rem; color: #f1f5f9; }
        .confirm-warning {
          padding: 0.6rem 1rem;
          background: rgba(251,191,36,0.1);
          border: 1px solid rgba(251,191,36,0.2);
          border-radius: 8px;
          color: #fcd34d;
          font-size: 0.82rem;
        }
        .confirm-note { font-size: 0.78rem; color: #475569; }
        .delete-error-box {
          display: flex; flex-direction: column; align-items: center;
          gap: 0.5rem; text-align: center;
          padding: 1rem;
          background: rgba(239,68,68,0.08);
          border: 1px solid rgba(239,68,68,0.2);
          border-radius: 10px;
        }
        .delete-error-icon { font-size: 2rem; }
        .delete-error-title { font-size: 1rem; font-weight: 700; color: #fca5a5; margin: 0; }
        .delete-error-msg { font-size: 0.85rem; color: #f87171; margin: 0; }
        .delete-error-hint { font-size: 0.8rem; color: #94a3b8; margin: 0; }
      `}</style>
    </div>
  );
};
