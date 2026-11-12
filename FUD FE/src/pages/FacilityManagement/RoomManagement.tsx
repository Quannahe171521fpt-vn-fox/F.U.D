import React, { useState, useEffect, useCallback } from 'react';
import {
  roomApi,
  blockApi,
  type RoomDto,
  type BlockDto,
  type RoomTypeLookup,
} from '../../services/api';
import { Pagination } from '../../components/Pagination';
import '../../styles/facilities.css';

interface RoomFormData {
  blockId: number;
  roomTypeId: number;
  roomNumber: string;
  floorLevel: number | '';
}

const EMPTY_FORM: RoomFormData = {
  blockId: 0,
  roomTypeId: 0,
  roomNumber: '',
  floorLevel: 1,
};

export const RoomManagement: React.FC = () => {
  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [blocks, setBlocks] = useState<BlockDto[]>([]);
  const [roomTypes, setRoomTypes] = useState<RoomTypeLookup[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Filters
  const [searchRoomNumber, setSearchRoomNumber] = useState('');
  const [searchBlockName, setSearchBlockName] = useState('');
  const [filterRoomTypeId, setFilterRoomTypeId] = useState<number | 'All'>('All');
  const [filterGender, setFilterGender] = useState('All');
  const [filterStatus, setFilterStatus] = useState('All');

  // Modal create/edit
  const [showModal, setShowModal] = useState(false);
  const [editingRoom, setEditingRoom] = useState<RoomDto | null>(null);
  const [formData, setFormData] = useState<RoomFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Status Change Modal
  const [statusModalTarget, setStatusModalTarget] = useState<RoomDto | null>(null);
  const [isChangingStatus, setIsChangingStatus] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // Load lookups (blocks & room types)
  useEffect(() => {
    Promise.all([
      blockApi.getAll({ pageSize: 100 }),
      roomApi.getTypes().catch(() => [] as RoomTypeLookup[]),
    ])
      .then(([blockRes, typesRes]) => {
        setBlocks(blockRes.items || []);
        setRoomTypes(typesRes || []);
      })
      .catch(() => { });
  }, []);

  // Load rooms
  const loadRooms = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await roomApi.getAll({
        roomNumber: searchRoomNumber || undefined,
        blockName: searchBlockName || undefined,
        roomTypeId: filterRoomTypeId !== 'All' ? filterRoomTypeId : undefined,
        genderType: filterGender !== 'All' ? filterGender : undefined,
        status: filterStatus !== 'All' ? filterStatus : undefined,
        page,
        pageSize,
      });
      setRooms(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      setError(err.message || 'Không thể tải danh sách phòng');
    } finally {
      setIsLoading(false);
    }
  }, [searchRoomNumber, searchBlockName, filterRoomTypeId, filterGender, filterStatus, page, pageSize]);

  useEffect(() => {
    loadRooms();
  }, [loadRooms]);

  const showNotification = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 3500);
  };

  // Handlers
  const openCreate = () => {
    setEditingRoom(null);
    setFormData({
      blockId: blocks[0]?.id || 1,
      roomTypeId: roomTypes[0]?.id || 1,
      roomNumber: '',
      floorLevel: 1,
    });
    setFormError(null);
    setShowModal(true);
  };

  const openEdit = (room: RoomDto) => {
    setEditingRoom(room);
    setFormData({
      blockId: room.blockId,
      roomTypeId: room.roomTypeId,
      roomNumber: room.roomNumber,
      floorLevel: room.floorLevel ?? '',
    });
    setFormError(null);
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!formData.roomNumber.trim()) {
      setFormError('Số phòng là bắt buộc');
      return;
    }
    if (!formData.blockId && !editingRoom) {
      setFormError('Vui lòng chọn Block');
      return;
    }
    if (!formData.roomTypeId) {
      setFormError('Vui lòng chọn loại phòng');
      return;
    }
    if (formData.floorLevel !== '' && (formData.floorLevel < 1 || formData.floorLevel > 50)) {
      setFormError('Số tầng phải từ 1 đến 50');
      return;
    }

    setIsSaving(true);
    setFormError(null);
    try {
      if (editingRoom) {
        await roomApi.update(editingRoom.id, {
          roomTypeId: formData.roomTypeId,
          roomNumber: formData.roomNumber.trim(),
          floorLevel: formData.floorLevel !== '' ? Number(formData.floorLevel) : undefined,
        });
        showNotification(`Cập nhật thông tin phòng ${formData.roomNumber} thành công`);
      } else {
        await roomApi.create({
          blockId: formData.blockId,
          roomTypeId: formData.roomTypeId,
          roomNumber: formData.roomNumber.trim(),
          floorLevel: formData.floorLevel !== '' ? Number(formData.floorLevel) : undefined,
        });
        showNotification(`Thêm mới phòng ${formData.roomNumber} thành công`);
      }
      setShowModal(false);
      await loadRooms();
    } catch (err: any) {
      setFormError(err.message || 'Lỗi khi lưu thông tin phòng');
    } finally {
      setIsSaving(false);
    }
  };

  const openStatusModal = (room: RoomDto) => {
    setStatusModalTarget(room);
    setStatusError(null);
  };

  const handleToggleStatus = async () => {
    if (!statusModalTarget) return;
    const targetStatus = statusModalTarget.status === 'Active' ? 'Maintenance' : 'Active';
    setIsChangingStatus(true);
    setStatusError(null);
    try {
      await roomApi.updateStatus(statusModalTarget.id, targetStatus);
      showNotification(`Đã chuyển trạng thái phòng ${statusModalTarget.roomNumber} sang ${targetStatus === 'Active' ? 'Hoạt động' : 'Bảo trì'}`);
      setStatusModalTarget(null);
      await loadRooms();
    } catch (err: any) {
      setStatusError(err.message || 'Không thể đổi trạng thái phòng');
    } finally {
      setIsChangingStatus(false);
    }
  };

  return (
    <div className="facilities-container">
      {/* Header */}
      <div className="facilities-header">
        <div className="facilities-title-group">
          <span className="facilities-sub">Hệ thống KTX FUD</span>
          <h1 className="facilities-title">Quản lý Phòng (Room)</h1>
        </div>
        <div>
          <button type="button" className="btn-fud-primary" onClick={openCreate}>
            + Thêm phòng mới
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
          <h2 className="facilities-card-title">Danh sách phòng ở</h2>
          <p className="facilities-card-desc">Quản lý danh sách các phòng, tầng, sức chứa giường và trạng thái bảo trì</p>
        </div>

        {/* Filters */}
        <div className="facilities-filter-row">
          <input
            className="facilities-search-input"
            placeholder="Tìm theo số phòng..."
            value={searchRoomNumber}
            onChange={e => { setSearchRoomNumber(e.target.value); setPage(1); }}
          />
          <input
            className="facilities-search-input"
            placeholder="Tìm theo tên Block..."
            value={searchBlockName}
            onChange={e => { setSearchBlockName(e.target.value); setPage(1); }}
          />
          <select
            className="facilities-select"
            value={filterRoomTypeId}
            onChange={e => {
              const val = e.target.value;
              setFilterRoomTypeId(val === 'All' ? 'All' : Number(val));
              setPage(1);
            }}
          >
            <option value="All">Tất cả loại phòng</option>
            {roomTypes.map(rt => (
              <option key={rt.id} value={rt.id}>
                {rt.name} ({rt.bedCapacity} giường)
              </option>
            ))}
          </select>
          <select
            className="facilities-select"
            value={filterGender}
            onChange={e => { setFilterGender(e.target.value); setPage(1); }}
          >
            <option value="All">Tất cả giới tính</option>
            <option value="Male">Phòng Nam</option>
            <option value="Female">Phòng Nữ</option>
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
          <button type="button" className="btn-fud-secondary" onClick={() => loadRooms()}>
            Làm mới
          </button>
        </div>

        {/* Table */}
        <div className="facilities-table-wrap">
          {isLoading ? (
            <div className="facilities-empty-cell">
              <p>Đang tải dữ liệu danh sách phòng...</p>
            </div>
          ) : rooms.length === 0 ? (
            <div className="facilities-empty-cell">
              <p style={{ fontWeight: 600, color: '#0f172a', marginBottom: '4px' }}>Không tìm thấy phòng nào</p>
              <p>Vui lòng thay đổi từ khóa tìm kiếm hoặc thêm phòng mới</p>
            </div>
          ) : (
            <table className="facilities-table">
              <thead>
                <tr>
                  <th style={{ width: '12%' }}>Số phòng</th>
                  <th style={{ width: '18%' }}>Thuộc Block</th>
                  <th style={{ width: '12%', textAlign: 'center' }}>Giới tính</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Tầng</th>
                  <th style={{ width: '20%' }}>Loại phòng</th>
                  <th style={{ width: '14%', textAlign: 'center' }}>Giường trống</th>
                  <th style={{ width: '14%', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {rooms.map(room => (
                  <tr key={room.id}>
                    <td>
                      <span className="badge-code">{room.roomNumber}</span>
                    </td>
                    <td style={{ fontWeight: 600 }}>{room.blockName}</td>
                    <td style={{ textAlign: 'center' }}>
                      <span className={room.blockGenderType === 'Male' ? 'badge-gender-male' : (room.blockGenderType === 'Female' ? 'badge-gender-female' : 'badge-code')}>
                        {room.blockGenderType === 'Male' ? 'Nam' : (room.blockGenderType === 'Female' ? 'Nữ' : '—')}
                      </span>
                    </td>
                    <td style={{ textAlign: 'center', color: '#64748b' }}>
                      {room.floorLevel ? `Tầng ${room.floorLevel}` : '—'}
                    </td>
                    <td>{room.roomTypeName || `${room.bedCapacity} giường`}</td>
                    <td style={{ textAlign: 'center' }}>
                      <span className={`badge-capacity ${room.freeBeds > 0 ? 'has-free' : ''}`}>
                        {room.freeBeds} / {room.totalBeds} trống
                      </span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <span
                        className={room.status === 'Active' ? 'badge-active' : 'badge-maintenance'}
                        onClick={() => openStatusModal(room)}
                        title="Nhấn để đổi trạng thái phòng"
                      >
                        {room.status === 'Active' ? 'Hoạt động' : 'Bảo trì'}
                      </span>
                    </td>
                    <td style={{ textAlign: 'center' }}>
                      <button
                        type="button"
                        className="btn-tbl-action"
                        onClick={() => openEdit(room)}
                      >
                        Sửa
                      </button>
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
          itemName="phòng"
        />
      </div>

      {/* ── Create / Edit Modal ────────────────────────────── */}
      {showModal && (
        <div className="facilities-modal-backdrop" onClick={() => !isSaving && setShowModal(false)}>
          <div className="facilities-modal-card" onClick={e => e.stopPropagation()}>
            <div className="facilities-modal-header">
              <h3 className="facilities-modal-title">{editingRoom ? 'Chỉnh sửa Phòng' : 'Thêm Phòng Mới'}</h3>
              <button type="button" className="facilities-modal-close" onClick={() => setShowModal(false)} disabled={isSaving}>✕</button>
            </div>
            <div className="facilities-modal-body">
              {formError && <div className="facilities-form-error">{formError}</div>}

              {!editingRoom && (
                <div className="facilities-form-group">
                  <label className="facilities-form-label">Block<span className="required">*</span></label>
                  <select
                    className="facilities-form-input"
                    value={formData.blockId}
                    onChange={e => setFormData(f => ({ ...f, blockId: Number(e.target.value) }))}
                  >
                    {blocks.map(b => (
                      <option key={b.id} value={b.id}>
                        {b.name} ({b.dormName || b.code}) - {b.genderType === 'Male' ? 'Block Nam' : 'Block Nữ'}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {editingRoom && (
                <div className="facilities-form-group">
                  <label className="facilities-form-label">Block</label>
                  <input className="facilities-form-input" value={editingRoom.blockName} disabled />
                  <span className="facilities-form-hint">Không thể chuyển phòng sang BLock khác sau khi tạo</span>
                </div>
              )}

              <div className="facilities-form-group">
                <label className="facilities-form-label">Loại phòng <span className="required">*</span></label>
                <select
                  className="facilities-form-input"
                  value={formData.roomTypeId}
                  onChange={e => setFormData(f => ({ ...f, roomTypeId: Number(e.target.value) }))}
                >
                  {roomTypes.map(rt => (
                    <option key={rt.id} value={rt.id}>
                      {rt.name} ({rt.bedCapacity} giường)
                    </option>
                  ))}
                </select>
              </div>

              <div className="facilities-form-group">
                <label className="facilities-form-label">Số phòng / Tên phòng <span className="required">*</span></label>
                <input
                  className="facilities-form-input"
                  placeholder="Ví dụ: 101, 201"
                  value={formData.roomNumber}
                  onChange={e => setFormData(f => ({ ...f, roomNumber: e.target.value }))}
                />
              </div>

              <div className="facilities-form-group">
                <label className="facilities-form-label">Tầng</label>
                <input
                  type="number"
                  min={1}
                  max={50}
                  className="facilities-form-input"
                  placeholder="Ví dụ: 1"
                  value={formData.floorLevel}
                  onChange={e => setFormData(f => ({ ...f, floorLevel: e.target.value === '' ? '' : Number(e.target.value) }))}
                />
              </div>
            </div>
            <div className="facilities-modal-footer">
              <button type="button" className="btn-fud-secondary" onClick={() => setShowModal(false)} disabled={isSaving}>
                Hủy
              </button>
              <button type="button" className="btn-fud-primary" onClick={handleSave} disabled={isSaving}>
                {isSaving ? 'Đang lưu...' : (editingRoom ? 'Cập nhật' : 'Tạo mới')}
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
              <h3 className="facilities-modal-title">Đổi trạng thái phòng</h3>
              <button type="button" className="facilities-modal-close" onClick={() => setStatusModalTarget(null)} disabled={isChangingStatus}>✕</button>
            </div>
            <div className="facilities-modal-body">
              {statusError && <div className="facilities-form-error">{statusError}</div>}
              <p style={{ margin: 0 }}>Bạn có chắc chắn muốn chuyển đổi trạng thái cho phòng:</p>
              <p style={{ fontWeight: 700, color: '#0f172a', margin: '4px 0 10px', fontSize: '15px' }}>
                Phòng {statusModalTarget.roomNumber} - Block {statusModalTarget.blockName}
              </p>
              <p style={{ fontSize: '13px', color: '#475569', lineHeight: 1.5, margin: 0 }}>
                Trạng thái hiện tại: <strong>{statusModalTarget.status === 'Active' ? 'Đang hoạt động' : 'Đang bảo trì'}</strong>
                <br />
                Trạng thái mới: <strong style={{ color: statusModalTarget.status === 'Active' ? '#dc2626' : '#059669' }}>
                  {statusModalTarget.status === 'Active' ? 'Bảo trì (Tạm khóa toàn bộ giường trống)' : 'Hoạt động bình thường'}
                </strong>
              </p>
              {statusModalTarget.status === 'Active' && (
                <p style={{ fontSize: '13px', color: '#d97026', margin: 0, backgroundColor: '#fff7ed', padding: '10px', borderRadius: '6px', border: '1px solid #ffedd5' }}>
                  Lưu ý: Khi chuyển phòng sang trạng thái Bảo trì, toàn bộ giường trống trong phòng sẽ tự động chuyển sang Bảo trì.
                </p>
              )}
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
    </div>
  );
};
