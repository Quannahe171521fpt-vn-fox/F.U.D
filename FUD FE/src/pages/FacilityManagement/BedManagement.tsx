import React, { useState, useEffect, useCallback } from 'react';
import {
  bedApi,
  roomApi,
  type BedDto,
  type RoomDto,
} from '../../services/api';
import { Pagination } from '../../components/Pagination';
import '../../styles/facilities.css';

interface BedFormData {
  roomId: number;
  bedCode: string;
  positionInRoom: string;
}

const EMPTY_FORM: BedFormData = {
  roomId: 0,
  bedCode: '',
  positionInRoom: '',
};

export const BedManagement: React.FC = () => {
  const [beds, setBeds] = useState<BedDto[]>([]);
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Filters
  const [searchBedCode, setSearchBedCode] = useState('');
  const [filterRoomId, setFilterRoomId] = useState<number | 'All'>('All');
  const [filterStatus, setFilterStatus] = useState('All');

  // Modal Create / Edit
  const [showModal, setShowModal] = useState(false);
  const [editingBed, setEditingBed] = useState<BedDto | null>(null);
  const [formData, setFormData] = useState<BedFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Modal Status Change
  const [statusModalTarget, setStatusModalTarget] = useState<BedDto | null>(null);
  const [isChangingStatus, setIsChangingStatus] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // Load rooms lookup for dropdown
  const loadRoomsLookup = useCallback(async () => {
    try {
      const res = await roomApi.getAll({ pageSize: 500 });
      setRooms(res.items || []);
    } catch (err: any) {
      console.error('Không thể tải danh sách phòng:', err);
    }
  }, []);

  useEffect(() => {
    loadRoomsLookup();
  }, [loadRoomsLookup]);

  // Load beds
  const loadBeds = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await bedApi.getAll({
        roomId: filterRoomId !== 'All' ? filterRoomId : undefined,
        bedCode: searchBedCode || undefined,
        status: filterStatus !== 'All' ? filterStatus : undefined,
        page,
        pageSize,
      });
      setBeds(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      setError(err.message || 'Không thể tải danh sách giường');
    } finally {
      setIsLoading(false);
    }
  }, [filterRoomId, searchBedCode, filterStatus, page, pageSize]);

  useEffect(() => {
    loadBeds();
  }, [loadBeds]);

  const showNotification = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 3500);
  };

  // Open Create
  const openCreate = () => {
    if (rooms.length === 0) {
      loadRoomsLookup();
    }
    setEditingBed(null);
    setFormData({
      roomId: rooms[0]?.id || 0,
      bedCode: '',
      positionInRoom: '',
    });
    setFormError(null);
    setShowModal(true);
  };

  // Open Edit
  const openEdit = (bed: BedDto) => {
    setEditingBed(bed);
    setFormData({
      roomId: bed.roomId,
      bedCode: bed.bedCode,
      positionInRoom: bed.positionInRoom || '',
    });
    setFormError(null);
    setShowModal(true);
  };

  // Save handler (Create or Update)
  const handleSave = async () => {
    if (!formData.bedCode.trim()) {
      setFormError('Mã giường là bắt buộc');
      return;
    }
    if (!editingBed && (!formData.roomId || formData.roomId === 0)) {
      setFormError('Vui lòng chọn phòng');
      return;
    }

    setIsSaving(true);
    setFormError(null);
    try {
      if (editingBed) {
        await bedApi.update(editingBed.id, {
          bedCode: formData.bedCode.trim(),
          positionInRoom: formData.positionInRoom.trim() || undefined,
        });
        showNotification(`Cập nhật thông tin giường ${formData.bedCode} thành công`);
      } else {
        await bedApi.create({
          roomId: formData.roomId,
          bedCode: formData.bedCode.trim(),
          positionInRoom: formData.positionInRoom.trim() || undefined,
        });
        showNotification(`Thêm mới giường ${formData.bedCode} thành công`);
      }
      setShowModal(false);
      await loadBeds();
    } catch (err: any) {
      setFormError(err.message || 'Lỗi khi lưu thông tin giường');
    } finally {
      setIsSaving(false);
    }
  };

  // Open Status modal
  const openStatusModal = (bed: BedDto) => {
    if (bed.status === 'Occupied' || bed.status === 'Holding' || bed.status === 'Pending Transfer') {
      setError(`Không thể thay đổi trạng thái của giường ${bed.bedCode} vì đang có sinh viên cư trú hoặc giữ chỗ.`);
      return;
    }
    setStatusModalTarget(bed);
    setStatusError(null);
  };

  // Status toggle handler
  const handleToggleStatus = async () => {
    if (!statusModalTarget) return;
    const targetStatus: 'Free' | 'Maintenance' =
      statusModalTarget.status === 'Free' ? 'Maintenance' : 'Free';

    setIsChangingStatus(true);
    setStatusError(null);
    try {
      await bedApi.updateStatus(statusModalTarget.id, targetStatus);
      showNotification(
        `Đã chuyển trạng thái giường ${statusModalTarget.bedCode} sang ${
          targetStatus === 'Free' ? 'Trống (Hoạt động)' : 'Bảo trì'
        }`
      );
      setStatusModalTarget(null);
      await loadBeds();
    } catch (err: any) {
      setStatusError(err.message || 'Không thể đổi trạng thái giường');
    } finally {
      setIsChangingStatus(false);
    }
  };

  // Helper render status badge
  const renderStatusBadge = (bed: BedDto) => {
    switch (bed.status) {
      case 'Free':
        return (
          <span
            className="badge-bed-free badge-status-clickable"
            onClick={() => openStatusModal(bed)}
            title="Nhấn để chuyển sang Bảo trì"
          >
            Trống
          </span>
        );
      case 'Occupied':
        return (
          <span className="badge-bed-occupied" title="Đang có sinh viên cư trú">
            Đang ở
          </span>
        );
      case 'Holding':
        return (
          <span className="badge-bed-holding" title="Đang trong quá trình giữ chỗ">
            Đang giữ chỗ
          </span>
        );
      case 'Pending Transfer':
        return (
          <span className="badge-bed-transfer" title="Chờ xử lý chuyển giường">
            Chờ chuyển
          </span>
        );
      case 'Maintenance':
        return (
          <span
            className="badge-bed-maintenance badge-status-clickable"
            onClick={() => openStatusModal(bed)}
            title="Nhấn để mở lại hoạt động (Trống)"
          >
            Bảo trì
          </span>
        );
      default:
        return <span className="badge-code">{bed.status}</span>;
    }
  };

  return (
    <div className="facilities-container">
      {/* Header */}
      <div className="facilities-header">
        <div className="facilities-title-group">
          <span className="facilities-sub">Hệ thống KTX FUD</span>
          <h1 className="facilities-title">Quản lý Giường (Bed)</h1>
        </div>
        <div>
          <button type="button" className="btn-fud-primary" onClick={openCreate}>
            + Thêm giường mới
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
          <h2 className="facilities-card-title">Danh sách giường ký túc xá</h2>
          <p className="facilities-card-desc">
            Theo dõi tình trạng sử dụng, bảo trì và thông tin vị trí các giường trong từng phòng.
          </p>
        </div>

        {/* Filters */}
        <div className="facilities-filter-row">
          <input
            className="facilities-search-input"
            placeholder="Tìm theo mã giường (vd: 101L-B1)..."
            value={searchBedCode}
            onChange={(e) => {
              setSearchBedCode(e.target.value);
              setPage(1);
            }}
          />

          <select
            className="facilities-select"
            value={filterRoomId}
            onChange={(e) => {
              const val = e.target.value;
              setFilterRoomId(val === 'All' ? 'All' : Number(val));
              setPage(1);
            }}
          >
            <option value="All">Tất cả phòng</option>
            {rooms.map((r) => (
              <option key={r.id} value={r.id}>
                {r.roomNumber} ({r.blockName})
              </option>
            ))}
          </select>

          <select
            className="facilities-select"
            value={filterStatus}
            onChange={(e) => {
              setFilterStatus(e.target.value);
              setPage(1);
            }}
          >
            <option value="All">Tất cả trạng thái</option>
            <option value="Free">Trống (Free)</option>
            <option value="Occupied">Đang ở (Occupied)</option>
            <option value="Holding">Đang giữ chỗ (Holding)</option>
            <option value="Pending Transfer">Chờ chuyển (Pending Transfer)</option>
            <option value="Maintenance">Bảo trì (Maintenance)</option>
          </select>

          <button type="button" className="btn-fud-secondary" onClick={() => loadBeds()}>
            Làm mới
          </button>
        </div>

        {/* Table */}
        <div className="facilities-table-wrap">
          {isLoading ? (
            <div className="facilities-empty-cell">
              <p>Đang tải dữ liệu danh sách giường...</p>
            </div>
          ) : beds.length === 0 ? (
            <div className="facilities-empty-cell">
              <p style={{ fontWeight: 600, color: '#0f172a', marginBottom: '4px' }}>
                Không tìm thấy giường nào
              </p>
              <p>Vui lòng thử lại với bộ lọc khác hoặc thêm giường mới vào phòng</p>
            </div>
          ) : (
            <table className="facilities-table">
              <thead>
                <tr>
                  <th style={{ width: '16%' }}>Mã giường</th>
                  <th style={{ width: '16%' }}>Thuộc phòng</th>
                  <th style={{ width: '18%' }}>Thuộc Block</th>
                  <th style={{ width: '20%' }}>Vị trí trong phòng</th>
                  <th style={{ width: '16%', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ width: '14%', textAlign: 'center' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {beds.map((bed) => {
                  const isBusy =
                    bed.status === 'Occupied' ||
                    bed.status === 'Holding' ||
                    bed.status === 'Pending Transfer';

                  return (
                    <tr key={bed.id}>
                      <td>
                        <span className="badge-code">{bed.bedCode}</span>
                      </td>
                      <td style={{ fontWeight: 600 }}>{bed.roomNumber}</td>
                      <td style={{ color: '#475569' }}>{bed.blockName}</td>
                      <td style={{ color: '#64748b' }}>
                        {bed.positionInRoom || '—'}
                      </td>
                      <td style={{ textAlign: 'center' }}>{renderStatusBadge(bed)}</td>
                      <td style={{ textAlign: 'center' }}>
                        <div className="facilities-actions" style={{ justifyContent: 'center' }}>
                          <button
                            type="button"
                            className="btn-tbl-action"
                            onClick={() => openEdit(bed)}
                            title="Chỉnh sửa mã hoặc vị trí"
                          >
                            Sửa
                          </button>
                          <button
                            type="button"
                            className="btn-tbl-action-sub"
                            disabled={isBusy}
                            onClick={() => openStatusModal(bed)}
                            title={
                              isBusy
                                ? 'Không thể đổi trạng thái khi đang có người ở/giữ chỗ'
                                : bed.status === 'Free'
                                ? 'Chuyển sang bảo trì'
                                : 'Mở lại hoạt động'
                            }
                          >
                            {bed.status === 'Maintenance' ? 'Mở lại' : 'Bảo trì'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
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
          itemName="giường"
        />
      </div>

      {/* ── Modal Thêm / Chỉnh sửa Giường ──────────────────────── */}
      {showModal && (
        <div
          className="facilities-modal-backdrop"
          onClick={() => !isSaving && setShowModal(false)}
        >
          <div className="facilities-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">
                {editingBed ? `Chỉnh sửa giường ${editingBed.bedCode}` : 'Thêm Giường Mới'}
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

              {/* Chỉ cho chọn phòng khi thêm mới */}
              {!editingBed && (
                <div className="facilities-form-group">
                  <label className="facilities-label">
                    Chọn Phòng <span style={{ color: '#ef4444' }}>*</span>
                  </label>
                  <select
                    className="facilities-select"
                    style={{ width: '100%' }}
                    value={formData.roomId}
                    onChange={(e) =>
                      setFormData({ ...formData, roomId: Number(e.target.value) })
                    }
                  >
                    <option value={0} disabled>
                      -- Chọn phòng --
                    </option>
                    {rooms.map((r) => (
                      <option key={r.id} value={r.id}>
                        Phòng {r.roomNumber} ({r.blockName}) - Loại {r.roomTypeName || `${r.bedCapacity} chỗ`}
                      </option>
                    ))}
                  </select>
                  {rooms.length === 0 && (
                    <span className="facilities-help-text" style={{ color: '#ef4444', marginTop: '6px', display: 'block' }}>
                      ⚠️ Chưa có dữ liệu phòng hoặc không tải được danh sách phòng. Vui lòng kiểm tra lại kết nối hoặc thêm phòng tại trang Quản lý phòng.
                    </span>
                  )}
                </div>
              )}

              {editingBed && (
                <div className="facilities-form-group">
                  <label className="facilities-label">Phòng hiện tại</label>
                  <input
                    type="text"
                    className="facilities-input"
                    value={`${editingBed.roomNumber} (${editingBed.blockName})`}
                    disabled
                    style={{ backgroundColor: '#f8fafc', color: '#64748b' }}
                  />
                </div>
              )}

              <div className="facilities-form-group">
                <label className="facilities-label">
                  Mã giường <span style={{ color: '#ef4444' }}>*</span>
                </label>
                <input
                  type="text"
                  className="facilities-input"
                  placeholder="Ví dụ: 101L-B1, 202R-B2..."
                  value={formData.bedCode}
                  onChange={(e) =>
                    setFormData({ ...formData, bedCode: e.target.value })
                  }
                />
                <span className="facilities-help-text">
                  Mã giường là duy nhất trong từng phòng và sẽ hiển thị cho sinh viên khi đăng ký.
                </span>
              </div>

              <div className="facilities-form-group">
                <label className="facilities-label">Vị trí trong phòng (tùy chọn)</label>
                <input
                  type="text"
                  className="facilities-input"
                  placeholder="Ví dụ: Tầng trên gần cửa sổ, Tầng dưới cửa ra vào..."
                  value={formData.positionInRoom}
                  onChange={(e) =>
                    setFormData({ ...formData, positionInRoom: e.target.value })
                  }
                />
              </div>

              {!editingBed && (
                <div
                  style={{
                    backgroundColor: '#f8fafc',
                    border: '1px solid #e2e8f0',
                    borderRadius: '6px',
                    padding: '10px 14px',
                    fontSize: '13px',
                    color: '#64748b',
                  }}
                >
                  ℹ️ Giường mới tạo sẽ tự động có trạng thái <strong>Trống (Free)</strong> sẵn sàng cho sinh viên đăng ký.
                </div>
              )}
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
                {isSaving ? 'Đang lưu...' : editingBed ? 'Lưu thay đổi' : 'Tạo giường'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal Đổi trạng thái (Free <-> Maintenance) ────────── */}
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
              <h3 className="facilities-modal-title">Xác nhận đổi trạng thái giường</h3>
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
                Bạn có chắc chắn muốn chuyển trạng thái giường{' '}
                <strong>{statusModalTarget.bedCode}</strong> (Phòng {statusModalTarget.roomNumber}) sang:{' '}
                <span
                  style={{
                    fontWeight: 700,
                    color: statusModalTarget.status === 'Free' ? '#dc2626' : '#059669',
                  }}
                >
                  {statusModalTarget.status === 'Free' ? 'Bảo trì (Maintenance)' : 'Trống (Free)'}
                </span>
                ?
              </p>

              {statusModalTarget.status === 'Free' && (
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
                  ⚠️ Khi chuyển sang trạng thái <strong>Bảo trì</strong>, sinh viên sẽ không thể đăng ký hoặc giữ chỗ tại giường này.
                </div>
              )}
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
                className={statusModalTarget.status === 'Free' ? 'btn-fud-danger' : 'btn-fud-primary'}
                onClick={handleToggleStatus}
                disabled={isChangingStatus}
              >
                {isChangingStatus ? 'Đang cập nhật...' : 'Xác nhận'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

