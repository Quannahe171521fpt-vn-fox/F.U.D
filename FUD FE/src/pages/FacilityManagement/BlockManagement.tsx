import React, { useState, useEffect, useCallback } from 'react';
import { blockApi, dormApi, type BlockDto, type DormItem } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import '../../styles/facilities.css';

interface BlockFormData {
  dormId: number;
  code: string;
  name: string;
  numberOfFloors: number | '';
  genderType: string;
}

const EMPTY_FORM: BlockFormData = {
  dormId: 0,
  code: '',
  name: '',
  numberOfFloors: 5,
  genderType: 'Male',
};

export const BlockManagement: React.FC = () => {
  const [blocks, setBlocks] = useState<BlockDto[]>([]);
  const [dorms, setDorms] = useState<DormItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Filters
  const [searchBlockName, setSearchBlockName] = useState('');
  const [searchDormName, setSearchDormName] = useState('');
  const [filterGender, setFilterGender] = useState('All');
  const [filterStatus, setFilterStatus] = useState('All');

  // Modal states
  const [showModal, setShowModal] = useState(false);
  const [editingBlock, setEditingBlock] = useState<BlockDto | null>(null);
  const [formData, setFormData] = useState<BlockFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Gender Change Modal
  const [genderModalTarget, setGenderModalTarget] = useState<BlockDto | null>(null);
  const [newGender, setNewGender] = useState<'Male' | 'Female'>('Male');
  const [isChangingGender, setIsChangingGender] = useState(false);
  const [genderError, setGenderError] = useState<string | null>(null);

  // Status Change Modal
  const [statusModalTarget, setStatusModalTarget] = useState<BlockDto | null>(null);
  const [isChangingStatus, setIsChangingStatus] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // Load dorm lookup
  useEffect(() => {
    dormApi.getAll({ pageSize: 100 })
      .then(res => {
        setDorms(res.items || []);
        if (res.items?.length > 0 && formData.dormId === 0) {
          setFormData(f => ({ ...f, dormId: res.items[0].id }));
        }
      })
      .catch(() => {});
  }, []);

  // Load blocks
  const loadBlocks = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await blockApi.getAll({
        blockName: searchBlockName || undefined,
        dormName: searchDormName || undefined,
        genderType: filterGender !== 'All' ? filterGender : undefined,
        status: filterStatus !== 'All' ? filterStatus : undefined,
        page,
        pageSize,
      });
      setBlocks(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      setError(err.message || 'Không thể tải danh sách block');
    } finally {
      setIsLoading(false);
    }
  }, [searchBlockName, searchDormName, filterGender, filterStatus, page, pageSize]);

  useEffect(() => {
    loadBlocks();
  }, [loadBlocks]);

  const showNotification = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 3500);
  };

  // Handlers
  const openCreate = () => {
    setEditingBlock(null);
    setFormData({
      dormId: dorms[0]?.id || 1,
      code: '',
      name: '',
      numberOfFloors: 5,
      genderType: 'Male',
    });
    setFormError(null);
    setShowModal(true);
  };

  const openEdit = (block: BlockDto) => {
    setEditingBlock(block);
    setFormData({
      dormId: block.dormId,
      code: block.code,
      name: block.name,
      numberOfFloors: block.numberOfFloors ?? '',
      genderType: block.genderType,
    });
    setFormError(null);
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!editingBlock && !formData.code.trim()) {
      setFormError('Mã block là bắt buộc');
      return;
    }
    if (!formData.name.trim()) {
      setFormError('Tên block là bắt buộc');
      return;
    }
    if (formData.numberOfFloors !== '' && (formData.numberOfFloors < 1 || formData.numberOfFloors > 100)) {
      setFormError('Số tầng phải từ 1 đến 100');
      return;
    }

    setIsSaving(true);
    setFormError(null);
    try {
      if (editingBlock) {
        await blockApi.update(editingBlock.id, {
          name: formData.name.trim(),
          numberOfFloors: formData.numberOfFloors !== '' ? Number(formData.numberOfFloors) : undefined,
        });
        showNotification(`Cập nhật thông tin block ${formData.name} thành công`);
      } else {
        await blockApi.create({
          dormId: formData.dormId,
          code: formData.code.trim().toUpperCase(),
          name: formData.name.trim(),
          numberOfFloors: formData.numberOfFloors !== '' ? Number(formData.numberOfFloors) : undefined,
          genderType: formData.genderType,
        });
        showNotification(`Thêm mới block ${formData.name} thành công`);
      }
      setShowModal(false);
      await loadBlocks();
    } catch (err: any) {
      setFormError(err.message || 'Lỗi khi lưu thông tin block');
    } finally {
      setIsSaving(false);
    }
  };

  const openStatusModal = (block: BlockDto) => {
    setStatusModalTarget(block);
    setStatusError(null);
  };

  const handleToggleStatus = async () => {
    if (!statusModalTarget) return;
    const targetStatus = statusModalTarget.status === 'Active' ? 'Maintenance' : 'Active';
    setIsChangingStatus(true);
    setStatusError(null);
    try {
      await blockApi.updateStatus(statusModalTarget.id, targetStatus);
      showNotification(`Đã chuyển trạng thái block ${statusModalTarget.name} sang ${targetStatus === 'Active' ? 'Hoạt động' : 'Bảo trì'}`);
      setStatusModalTarget(null);
      await loadBlocks();
    } catch (err: any) {
      setStatusError(err.message || 'Không thể đổi trạng thái block');
    } finally {
      setIsChangingStatus(false);
    }
  };

  const openGenderModal = (block: BlockDto) => {
    setGenderModalTarget(block);
    setNewGender(block.genderType === 'Male' ? 'Female' : 'Male');
    setGenderError(null);
  };

  const handleChangeGender = async () => {
    if (!genderModalTarget) return;
    setIsChangingGender(true);
    setGenderError(null);
    try {
      await blockApi.updateGender(genderModalTarget.id, newGender);
      showNotification(`Đã chuyển đổi giới tính block ${genderModalTarget.name} sang ${newGender === 'Male' ? 'Nam' : 'Nữ'}`);
      setGenderModalTarget(null);
      await loadBlocks();
    } catch (err: any) {
      setGenderError(err.message || 'Không thể đổi giới tính block');
    } finally {
      setIsChangingGender(false);
    }
  };

  return (
    <div className="facilities-container">
      {/* Header */}
      <div className="facilities-header">
        <div className="facilities-title-group">
          <span className="facilities-sub">Hệ thống KTX FUD</span>
          <h1 className="facilities-title">Quản lý Block</h1>
        </div>
        <div>
          <button type="button" className="btn-fud-primary" onClick={openCreate}>
            + Thêm block
          </button>
        </div>
      </div>

      {/* Notifications */}
      {successMsg && (
        <div className="facilities-alert-success">
          {successMsg}
        </div>
      )}

      {error && (
        <div className="facilities-alert-error">
          <span>{error}</span>
          <button type="button" onClick={() => setError(null)}>✕</button>
        </div>
      )}

      {/* Card: Filters + Table */}
      <div className="facilities-card">
        <div className="facilities-card-header">
          <h2 className="facilities-card-title">Danh sách Block KTX</h2>
          <p className="facilities-card-desc">Quản lý danh mục các block, số tầng, giới tính phân bổ và số lượng phòng</p>
        </div>

        {/* Filters */}
        <div className="facilities-filter-row">
          <input
            className="facilities-search-input"
            placeholder="Tìm theo tên hoặc mã block..."
            value={searchBlockName}
            onChange={e => { setSearchBlockName(e.target.value); setPage(1); }}
          />
          <input
            className="facilities-search-input"
            placeholder="Tìm theo tên ký túc xá..."
            value={searchDormName}
            onChange={e => { setSearchDormName(e.target.value); setPage(1); }}
          />
          <select
            className="facilities-select"
            value={filterGender}
            onChange={e => { setFilterGender(e.target.value); setPage(1); }}
          >
            <option value="All">Tất cả giới tính</option>
            <option value="Male">Block Nam</option>
            <option value="Female">Block Nữ</option>
          </select>
          <select
            className="facilities-select"
            value={filterStatus}
            onChange={e => { setFilterStatus(e.target.value); setPage(1); }}
          >
            <option value="All">Tất cả trạng thái</option>
            <option value="Active">Đang hoạt động</option>
            <option value="Maintenance">Đang bảo trì</option>
          </select>
          <button type="button" className="btn-fud-secondary" onClick={() => loadBlocks()}>
            Làm mới
          </button>
        </div>

        {/* Table */}
        <div className="facilities-table-wrap">
          {isLoading ? (
            <div className="facilities-empty-cell">
              <p>Đang tải dữ liệu danh sách block...</p>
            </div>
          ) : blocks.length === 0 ? (
            <div className="facilities-empty-cell">
              <p style={{ fontWeight: 600, color: '#0f172a', marginBottom: '4px' }}>Không tìm thấy block nào</p>
              <p>Vui lòng thay đổi từ khóa tìm kiếm hoặc thêm block mới</p>
            </div>
          ) : (
            <table className="facilities-table">
              <thead>
                <tr>
                  <th style={{ width: '12%' }}>Mã block</th>
                  <th style={{ width: '22%' }}>Tên block</th>
                  <th style={{ width: '20%' }}>Thuộc KTX</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Số tầng</th>
                  <th style={{ width: '12%', textAlign: 'center' }}>Giới tính</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Số phòng</th>
                  <th style={{ width: '14%', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ width: '16%', textAlign: 'center' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {blocks.map(block => (
                  <tr key={block.id}>
                    <td>
                      <span className="badge-code">{block.code}</span>
                    </td>
                    <td style={{ fontWeight: 600 }}>{block.name}</td>
                    <td>{block.dormName || '—'}</td>
                    <td style={{ textAlign: 'center', color: '#64748b' }}>{block.numberOfFloors ? `${block.numberOfFloors} tầng` : '—'}</td>
                    <td style={{ textAlign: 'center' }}>
                      <span className={block.genderType === 'Male' ? 'badge-gender-male' : 'badge-gender-female'}>
                        {block.genderType === 'Male' ? 'Nam' : 'Nữ'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <span className="badge-capacity">{block.totalRooms} phòng</span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <span
                        className={block.status === 'Active' ? 'badge-active' : 'badge-maintenance'}
                        onClick={() => openStatusModal(block)}
                        title="Nhấn để đổi trạng thái"
                      >
                        {block.status === 'Active' ? 'Hoạt động' : 'Bảo trì'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <div className="facilities-actions" style={{ justifyContent: 'center' }}>
                        <button
                          type="button"
                          className="btn-tbl-action"
                          onClick={() => openEdit(block)}
                        >
                          Sửa
                        </button>
                        <button
                          type="button"
                          className="btn-tbl-action-sub"
                          onClick={() => openGenderModal(block)}
                        >
                          Đổi giới tính
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
          itemName="block"
        />
      </div>

      {/* ── Create / Edit Modal ────────────────────────────── */}
      {showModal && (
        <div className="facilities-modal-backdrop" onClick={() => !isSaving && setShowModal(false)}>
          <div className="facilities-modal-card" onClick={e => e.stopPropagation()}>
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">{editingBlock ? 'Chỉnh sửa Block' : 'Thêm Block Mới'}</h3>
              <button type="button" className="facilities-modal-close" onClick={() => setShowModal(false)} disabled={isSaving}>✕</button>
            </div>
            <div className="facilities-modal-body">
              {formError && <div className="facilities-form-error">{formError}</div>}

              {!editingBlock && (
                <>
                  <div className="facilities-form-group">
                    <label className="facilities-form-label">Thuộc Ký Túc Xá <span className="required">*</span></label>
                    <select
                      className="facilities-form-input"
                      value={formData.dormId}
                      onChange={e => setFormData(f => ({ ...f, dormId: Number(e.target.value) }))}
                    >
                      {dorms.map(d => (
                        <option key={d.id} value={d.id}>
                          {d.name} ({d.code})
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="facilities-form-group">
                    <label className="facilities-form-label">Mã block <span className="required">*</span></label>
                    <input
                      className="facilities-form-input"
                      placeholder="Ví dụ: 101, 102"
                      value={formData.code}
                      onChange={e => setFormData(f => ({ ...f, code: e.target.value }))}
                    />
                    <span className="facilities-form-hint">Mã phân biệt block trong ký túc xá</span>
                  </div>

                  <div className="facilities-form-group">
                    <label className="facilities-form-label">Giới tính phân bổ <span className="required">*</span></label>
                    <select
                      className="facilities-form-input"
                      value={formData.genderType}
                      onChange={e => setFormData(f => ({ ...f, genderType: e.target.value }))}
                    >
                      <option value="Male">Block Nam (Male)</option>
                      <option value="Female">Block Nữ (Female)</option>
                    </select>
                  </div>
                </>
              )}

              {editingBlock && (
                <div className="facilities-form-group">
                  <label className="facilities-form-label">Mã block</label>
                  <input className="facilities-form-input" value={editingBlock.code} disabled />
                  <span className="facilities-form-hint">Mã block cố định không thể thay đổi</span>
                </div>
              )}

              <div className="facilities-form-group">
                <label className="facilities-form-label">Tên block <span className="required">*</span></label>
                <input
                  className="facilities-form-input"
                  placeholder="Ví dụ: Block 101 (Nam)"
                  value={formData.name}
                  onChange={e => setFormData(f => ({ ...f, name: e.target.value }))}
                />
              </div>

              <div className="facilities-form-group">
                <label className="facilities-form-label">Số tầng</label>
                <input
                  type="number"
                  min={1}
                  max={100}
                  className="facilities-form-input"
                  placeholder="Ví dụ: 5"
                  value={formData.numberOfFloors}
                  onChange={e => setFormData(f => ({ ...f, numberOfFloors: e.target.value === '' ? '' : Number(e.target.value) }))}
                />
              </div>
            </div>
            <div className="facilities-modal-footer">
              <button type="button" className="btn-fud-secondary" onClick={() => setShowModal(false)} disabled={isSaving}>
                Hủy
              </button>
              <button type="button" className="btn-fud-primary" onClick={handleSave} disabled={isSaving}>
                {isSaving ? 'Đang lưu...' : (editingBlock ? 'Cập nhật' : 'Tạo mới')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Toggle Status Modal ────────────────────────────── */}
      {statusModalTarget && (
        <div className="facilities-modal-backdrop" onClick={() => !isChangingStatus && setStatusModalTarget(null)}>
          <div className="facilities-modal-card" onClick={e => e.stopPropagation()}>
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">Đổi trạng thái block</h3>
              <button type="button" className="facilities-modal-close" onClick={() => setStatusModalTarget(null)} disabled={isChangingStatus}>✕</button>
            </div>
            <div className="facilities-modal-body">
              {statusError && <div className="facilities-form-error">{statusError}</div>}
              <p style={{ margin: 0 }}>Bạn có chắc chắn muốn chuyển đổi trạng thái cho block:</p>
              <p style={{ fontWeight: 700, color: '#0f172a', margin: '4px 0 10px', fontSize: '15px' }}>
                {statusModalTarget.name} ({statusModalTarget.code})
              </p>
              <p style={{ fontSize: '13px', color: '#475569', lineHeight: 1.5, margin: 0 }}>
                Trạng thái hiện tại: <strong>{statusModalTarget.status === 'Active' ? 'Đang hoạt động' : 'Đang bảo trì'}</strong>
                <br />
                Trạng thái mới: <strong style={{ color: statusModalTarget.status === 'Active' ? '#dc2626' : '#059669' }}>
                  {statusModalTarget.status === 'Active' ? 'Bảo trì (Tạm khóa đăng ký & hoạt động)' : 'Hoạt động bình thường'}
                </strong>
              </p>
            </div>
            <div className="facilities-modal-footer">
              <button type="button" className="btn-fud-secondary" onClick={() => setStatusModalTarget(null)} disabled={isChangingStatus}>
                Hủy
              </button>
              <button
                type="button"
                className={statusModalTarget.status === 'Active' ? 'btn-fud-danger' : 'btn-fud-primary'}
                onClick={handleToggleStatus}
                disabled={isChangingStatus}
              >
                {isChangingStatus ? 'Đang xử lý...' : 'Xác nhận chuyển trạng thái'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Change Gender Modal ────────────────────────────── */}
      {genderModalTarget && (
        <div className="facilities-modal-backdrop" onClick={() => !isChangingGender && setGenderModalTarget(null)}>
          <div className="facilities-modal-card" onClick={e => e.stopPropagation()}>
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">Đổi giới tính block</h3>
              <button type="button" className="facilities-modal-close" onClick={() => setGenderModalTarget(null)} disabled={isChangingGender}>✕</button>
            </div>
            <div className="facilities-modal-body">
              {genderError && <div className="facilities-form-error">{genderError}</div>}
              <p style={{ margin: 0 }}>Chọn giới tính mới cho block <strong>{genderModalTarget.name}</strong>:</p>
              <div className="facilities-form-group" style={{ marginTop: '12px' }}>
                <select
                  className="facilities-form-input"
                  value={newGender}
                  onChange={e => setNewGender(e.target.value as 'Male' | 'Female')}
                >
                  <option value="Male">Block Nam</option>
                  <option value="Female">Block Nữ</option>
                </select>
              </div>
              <p style={{ fontSize: '12px', color: '#64748b', marginTop: '4px', margin: 0 }}>
                Lưu ý: Chỉ có thể chuyển đổi giới tính khi block không còn sinh viên nào đang lưu trú (Residency).
              </p>
            </div>
            <div className="facilities-modal-footer">
              <button type="button" className="btn-fud-secondary" onClick={() => setGenderModalTarget(null)} disabled={isChangingGender}>
                Hủy
              </button>
              <button type="button" className="btn-fud-primary" onClick={handleChangeGender} disabled={isChangingGender}>
                {isChangingGender ? 'Đang lưu...' : 'Xác nhận đổi giới tính'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
