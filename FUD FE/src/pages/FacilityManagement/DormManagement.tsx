import React, { useState, useEffect, useCallback } from 'react';
import { dormApi, type DormItem } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import '../../styles/facilities.css';

interface DormFormData {
  campusId: number;
  code: string;
  name: string;
  address: string;
}

const EMPTY_FORM: DormFormData = {
  campusId: 1,
  code: '',
  name: '',
  address: '',
};

export const DormManagement: React.FC = () => {
  const [dorms, setDorms] = useState<DormItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Filters
  const [searchName, setSearchName] = useState('');
  const [filterStatus, setFilterStatus] = useState('All');

  // Modal Create / Edit
  const [showModal, setShowModal] = useState(false);
  const [editingDorm, setEditingDorm] = useState<DormItem | null>(null);
  const [formData, setFormData] = useState<DormFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Status Modal
  const [statusModalTarget, setStatusModalTarget] = useState<DormItem | null>(null);
  const [isChangingStatus, setIsChangingStatus] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // Delete Confirm Modal
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

  useEffect(() => {
    loadDorms();
  }, [loadDorms]);

  const showNotification = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 3500);
  };

  // ── Handlers ────────────────────────────────────────────
  const openCreate = () => {
    setEditingDorm(null);
    setFormData(EMPTY_FORM);
    setFormError(null);
    setShowModal(true);
  };

  const openEdit = (dorm: DormItem) => {
    setEditingDorm(dorm);
    setFormData({
      campusId: dorm.campusId,
      code: dorm.code,
      name: dorm.name,
      address: dorm.address || '',
    });
    setFormError(null);
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!formData.code.trim()) {
      setFormError('Mã KTX là bắt buộc');
      return;
    }
    if (!formData.name.trim()) {
      setFormError('Tên KTX là bắt buộc');
      return;
    }

    setIsSaving(true);
    setFormError(null);
    try {
      if (editingDorm) {
        await dormApi.update(editingDorm.id, {
          name: formData.name.trim(),
          address: formData.address.trim() || undefined,
        });
        showNotification(`Cập nhật thông tin ${formData.name} thành công`);
      } else {
        await dormApi.create({
          campusId: formData.campusId,
          code: formData.code.trim(),
          name: formData.name.trim(),
          address: formData.address.trim() || undefined,
        });
        showNotification(`Thêm mới ký túc xá ${formData.name} thành công`);
      }
      setShowModal(false);
      await loadDorms();
    } catch (err: any) {
      setFormError(err.message || 'Lỗi khi lưu KTX');
    } finally {
      setIsSaving(false);
    }
  };

  // ── Toggle Status ─────────────────────────────────────────
  const openStatusModal = (dorm: DormItem) => {
    setStatusModalTarget(dorm);
    setStatusError(null);
  };

  const handleToggleStatus = async () => {
    if (!statusModalTarget) return;
    const newStatus = statusModalTarget.status === 'Active' ? 'Inactive' : 'Active';
    setIsChangingStatus(true);
    setStatusError(null);
    try {
      await dormApi.updateStatus(statusModalTarget.id, newStatus);
      showNotification(
        `Đã chuyển trạng thái KTX ${statusModalTarget.name} sang ${
          newStatus === 'Active' ? 'Đang hoạt động' : 'Ngừng hoạt động'
        }`
      );
      setStatusModalTarget(null);
      await loadDorms();
    } catch (err: any) {
      setStatusError(err.message || 'Lỗi khi đổi trạng thái KTX');
    } finally {
      setIsChangingStatus(false);
    }
  };

  // ── Delete ────────────────────────────────────────────────
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
      showNotification(`Đã xóa KTX ${deleteTarget.name} thành công`);
      setDeleteTarget(null);
      await loadDorms();
    } catch (err: any) {
      setDeleteError(err.message || 'Lỗi khi xóa KTX');
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="facilities-container">
      {/* Header */}
      <div className="facilities-header">
        <div className="facilities-title-group">
          <span className="facilities-sub">Hệ thống KTX FUD</span>
          <h1 className="facilities-title">Quản lý Ký túc xá (Dorm)</h1>
        </div>
        <div>
          <button type="button" className="btn-fud-primary" onClick={openCreate}>
            + Thêm KTX mới
          </button>
        </div>
      </div>

      {/* Notifications */}
      {successMsg && <div className="facilities-alert-success">{successMsg}</div>}

      {error && (
        <div className="facilities-alert-error">
          <span>{error}</span>
          <button type="button" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      {/* Card: Filters + Table */}
      <div className="facilities-card">
        <div className="facilities-card-header">
          <h2 className="facilities-card-title">Danh sách Ký túc xá</h2>
          <p className="facilities-card-desc">
            Quản lý các tòa nhà ký túc xá trong hệ thống, cơ sở (Campus), địa chỉ và trạng thái hoạt động.
          </p>
        </div>

        {/* Filters */}
        <div className="facilities-filter-row">
          <input
            className="facilities-search-input"
            placeholder="Tìm theo tên hoặc mã KTX..."
            value={searchName}
            onChange={(e) => {
              setSearchName(e.target.value);
              setPage(1);
            }}
          />

          <select
            className="facilities-select"
            value={filterStatus}
            onChange={(e) => {
              setFilterStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="All">Tất cả trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Inactive">Ngừng hoạt động</option>
          </select>

          <button type="button" className="btn-fud-secondary" onClick={() => loadDorms()}>
            Làm mới
          </button>
        </div>

        {/* Table */}
        <div className="facilities-table-wrap">
          {isLoading ? (
            <div className="facilities-empty-cell">
              <p>Đang tải dữ liệu danh sách KTX...</p>
            </div>
          ) : dorms.length === 0 ? (
            <div className="facilities-empty-cell">
              <p style={{ fontWeight: 600, color: '#0f172a', marginBottom: '4px' }}>
                Không tìm thấy Ký túc xá nào
              </p>
              <p>Vui lòng thay đổi từ khóa tìm kiếm hoặc nhấn thêm KTX mới</p>
            </div>
          ) : (
            <table className="facilities-table">
              <thead>
                <tr>
                  <th style={{ width: '12%' }}>Mã KTX</th>
                  <th style={{ width: '22%' }}>Tên Ký Túc Xá</th>
                  <th style={{ width: '18%' }}>Thuộc Campus</th>
                  <th style={{ width: '24%' }}>Địa chỉ</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Số Block</th>
                  <th style={{ width: '14%', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ width: '16%', textAlign: 'center' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {dorms.map((dorm) => (
                  <tr key={dorm.id}>
                    <td>
                      <span className="badge-code">{dorm.code}</span>
                    </td>
                    <td style={{ fontWeight: 600 }}>{dorm.name}</td>
                    <td style={{ color: '#475569' }}>{dorm.campusName || 'Cơ sở chính'}</td>
                    <td style={{ color: '#64748b' }}>{dorm.address || '—'}</td>
                    <td style={{ textAlign: 'center' }}>
                      <span className="badge-capacity">{dorm.totalBlocks} block</span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <span
                        className={dorm.status === 'Active' ? 'badge-active' : 'badge-maintenance'}
                        onClick={() => openStatusModal(dorm)}
                        title="Nhấn để đổi trạng thái"
                      >
                        {dorm.status === 'Active' ? 'Hoạt động' : 'Ngừng hoạt động'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <div className="facilities-actions" style={{ justifyContent: 'center' }}>
                        <button
                          type="button"
                          className="btn-tbl-action"
                          onClick={() => openEdit(dorm)}
                          title="Chỉnh sửa thông tin"
                        >
                          Sửa
                        </button>
                        <button
                          type="button"
                          className="btn-tbl-action-sub"
                          onClick={() => openStatusModal(dorm)}
                          title="Đổi trạng thái hoạt động"
                        >
                          {dorm.status === 'Active' ? 'Vô hiệu' : 'Kích hoạt'}
                        </button>
                        <button
                          type="button"
                          className="btn-tbl-action-sub"
                          style={{ color: '#ef4444' }}
                          onClick={() => confirmDelete(dorm)}
                          title="Xóa KTX"
                        >
                          Xóa
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
        <Pagination
          currentPage={page}
          totalItems={totalCount}
          pageSize={pageSize}
          onPageChange={setPage}
          itemName="KTX"
        />
      </div>

      {/* ── Modal Thêm / Chỉnh sửa KTX ────────────────────────────── */}
      {showModal && (
        <div className="facilities-modal-backdrop" onClick={() => !isSaving && setShowModal(false)}>
          <div className="facilities-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">
                {editingDorm ? `Chỉnh sửa KTX ${editingDorm.name}` : 'Thêm Ký Túc Xá Mới'}
              </h3>
              <button
                type="button"
                className="facilities-modal-close"
                onClick={() => setShowModal(false)}
                disabled={isSaving}
              >
                ✕
              </button>
            </div>

            <div className="facilities-modal-body">
              {formError && (
                <div className="facilities-modal-error" style={{ marginBottom: '14px' }}>
                  {formError}
                </div>
              )}

              {!editingDorm && (
                <>
                  <div className="facilities-form-group">
                    <label className="facilities-label">
                      Mã KTX <span style={{ color: '#ef4444' }}>*</span>
                    </label>
                    <input
                      type="text"
                      className="facilities-input"
                      placeholder="VD: DOM-A, DOM-B..."
                      value={formData.code}
                      onChange={(e) => setFormData({ ...formData, code: e.target.value })}
                    />
                    <span className="facilities-help-text">
                      Mã duy nhất trong cùng Campus (không thể thay đổi sau khi tạo).
                    </span>
                  </div>

                  <div className="facilities-form-group">
                    <label className="facilities-label">
                      Campus ID <span style={{ color: '#ef4444' }}>*</span>
                    </label>
                    <input
                      type="number"
                      className="facilities-input"
                      min={1}
                      value={formData.campusId}
                      onChange={(e) =>
                        setFormData({ ...formData, campusId: Number(e.target.value) })
                      }
                    />
                  </div>
                </>
              )}

              {editingDorm && (
                <div className="facilities-form-group">
                  <label className="facilities-label">Mã KTX</label>
                  <input
                    type="text"
                    className="facilities-input"
                    value={editingDorm.code}
                    disabled
                    style={{ backgroundColor: '#f8fafc', color: '#64748b' }}
                  />
                  <span className="facilities-help-text">Mã KTX không thể chỉnh sửa.</span>
                </div>
              )}

              <div className="facilities-form-group">
                <label className="facilities-label">
                  Tên Ký Túc Xá <span style={{ color: '#ef4444' }}>*</span>
                </label>
                <input
                  type="text"
                  className="facilities-input"
                  placeholder="VD: Ký túc xá Dom B"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                />
              </div>

              <div className="facilities-form-group">
                <label className="facilities-label">Địa chỉ</label>
                <input
                  type="text"
                  className="facilities-input"
                  placeholder="VD: Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội"
                  value={formData.address}
                  onChange={(e) => setFormData({ ...formData, address: e.target.value })}
                />
              </div>
            </div>

            <div className="facilities-modal-footer">
              <button
                type="button"
                className="btn-fud-secondary"
                onClick={() => setShowModal(false)}
                disabled={isSaving}
              >
                Hủy
              </button>
              <button
                type="button"
                className="btn-fud-primary"
                onClick={handleSave}
                disabled={isSaving}
              >
                {isSaving ? 'Đang lưu...' : editingDorm ? 'Lưu thay đổi' : 'Tạo KTX'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal Đổi trạng thái (Active <-> Inactive) ────────────── */}
      {statusModalTarget && (
        <div
          className="facilities-modal-backdrop"
          onClick={() => !isChangingStatus && setStatusModalTarget(null)}
        >
          <div
            className="facilities-modal-card"
            style={{ maxWidth: '440px' }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">Đổi trạng thái Ký túc xá</h3>
              <button
                type="button"
                className="facilities-modal-close"
                onClick={() => setStatusModalTarget(null)}
                disabled={isChangingStatus}
              >
                ✕
              </button>
            </div>

            <div className="facilities-modal-body">
              {statusError && (
                <div className="facilities-modal-error" style={{ marginBottom: '12px' }}>
                  {statusError}
                </div>
              )}

              <p style={{ fontSize: '14px', color: '#334155', lineHeight: '1.5', margin: 0 }}>
                Bạn có chắc chắn muốn chuyển trạng thái KTX{' '}
                <strong>{statusModalTarget.name}</strong> sang:{' '}
                <span
                  style={{
                    fontWeight: 700,
                    color: statusModalTarget.status === 'Active' ? '#dc2626' : '#059669',
                  }}
                >
                  {statusModalTarget.status === 'Active'
                    ? 'Ngừng hoạt động (Inactive)'
                    : 'Đang hoạt động (Active)'}
                </span>
                ?
              </p>
            </div>

            <div className="facilities-modal-footer">
              <button
                type="button"
                className="btn-fud-secondary"
                onClick={() => setStatusModalTarget(null)}
                disabled={isChangingStatus}
              >
                Hủy
              </button>
              <button
                type="button"
                className={statusModalTarget.status === 'Active' ? 'btn-fud-danger' : 'btn-fud-primary'}
                onClick={handleToggleStatus}
                disabled={isChangingStatus}
              >
                {isChangingStatus ? 'Đang xử lý...' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal Xác nhận Xóa KTX ─────────────────────────────────── */}
      {deleteTarget && (
        <div
          className="facilities-modal-backdrop"
          onClick={() => !isDeleting && setDeleteTarget(null)}
        >
          <div
            className="facilities-modal-card"
            style={{ maxWidth: '460px' }}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">Xác nhận xóa Ký túc xá</h3>
              <button
                type="button"
                className="facilities-modal-close"
                onClick={() => setDeleteTarget(null)}
                disabled={isDeleting}
              >
                ✕
              </button>
            </div>

            <div className="facilities-modal-body">
              {deleteError ? (
                <div className="facilities-modal-error" style={{ marginBottom: '12px' }}>
                  <div style={{ fontWeight: 600, marginBottom: '4px' }}>Không thể xóa KTX này</div>
                  <div>{deleteError}</div>
                  <div style={{ marginTop: '6px', fontSize: '12px', color: '#64748b' }}>
                    Vui lòng xóa hoặc chuyển các Block bên trong KTX này trước khi thực hiện xóa.
                  </div>
                </div>
              ) : (
                <>
                  <p style={{ fontSize: '14px', color: '#334155', lineHeight: '1.5', margin: 0 }}>
                    Bạn có chắc chắn muốn xóa KTX{' '}
                    <strong>{deleteTarget.name}</strong> ({deleteTarget.code}) không?
                  </p>
                  {deleteTarget.totalBlocks > 0 && (
                    <div
                      style={{
                        backgroundColor: '#fffbeb',
                        border: '1px solid #fde68a',
                        borderRadius: '6px',
                        padding: '10px 12px',
                        fontSize: '13px',
                        color: '#92400e',
                        marginTop: '12px',
                      }}
                    >
                      ⚠️ KTX này đang có <strong>{deleteTarget.totalBlocks} block</strong>. Hệ thống sẽ chặn xóa nếu còn dữ liệu phụ thuộc.
                    </div>
                  )}
                  <p style={{ fontSize: '12px', color: '#64748b', marginTop: '10px', marginBottom: 0 }}>
                    Thao tác này sẽ đánh dấu xóa mềm KTX khỏi hệ thống.
                  </p>
                </>
              )}
            </div>

            <div className="facilities-modal-footer">
              {deleteError ? (
                <button
                  type="button"
                  className="btn-fud-primary"
                  onClick={() => setDeleteTarget(null)}
                >
                  Đã hiểu
                </button>
              ) : (
                <>
                  <button
                    type="button"
                    className="btn-fud-secondary"
                    onClick={() => setDeleteTarget(null)}
                    disabled={isDeleting}
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    className="btn-fud-danger"
                    onClick={handleDelete}
                    disabled={isDeleting}
                  >
                    {isDeleting ? 'Đang xóa...' : 'Xác nhận xóa'}
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
