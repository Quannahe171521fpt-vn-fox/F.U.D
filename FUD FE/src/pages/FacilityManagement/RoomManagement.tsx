import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  roomApi,
  blockApi,
  type RoomDto,
  type BlockDto,
  type RoomTypeLookup,
  type RoomDetailDto,
  type GuardAssignedBlockDto,
} from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
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

function formatDateDisplay(value?: string | null): string {
  if (!value) return '—';
  const datePart = value.split('T')[0];
  const parts = datePart.split('-');
  if (parts.length === 3) {
    return `${parts[2]}/${parts[1]}/${parts[0]}`;
  }
  return value;
}

function getBedStatusBadge(status: string): { className: string; label: string } {
  switch (status) {
    case 'Occupied':
      return { className: 'badge-bed-occupied', label: 'Đang ở' };
    case 'Holding':
      return { className: 'badge-bed-holding', label: 'Giữ chỗ (Chưa check-in)' };
    case 'Pending Transfer':
      return { className: 'badge-bed-transfer', label: 'Chờ chuyển giường' };
    case 'Maintenance':
      return { className: 'badge-bed-maintenance', label: 'Bảo trì' };
    default:
      return { className: 'badge-bed-free', label: 'Trống' };
  }
}

export const RoomManagement: React.FC = () => {
  const { currentUser } = useAuth();
  const isGuardOnly = Boolean(
    currentUser &&
    (currentUser.primaryRole === 'Guard' || currentUser.roles?.includes('Guard')) &&
    !currentUser.roles?.includes('Manager') &&
    !currentUser.roles?.includes('Admin')
  );

  const [rooms, setRooms] = useState<RoomDto[]>([]);
  const [blocks, setBlocks] = useState<BlockDto[]>([]);
  const [assignedBlocks, setAssignedBlocks] = useState<GuardAssignedBlockDto[]>([]);
  const [roomTypes, setRoomTypes] = useState<RoomTypeLookup[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Search & Filters
  const [searchRoomNumber, setSearchRoomNumber] = useState('');
  const [searchBlockName, setSearchBlockName] = useState('');
  const [filterBlockId, setFilterBlockId] = useState<number | 'All'>('All');
  const [filterExactRoom, setFilterExactRoom] = useState<string>('All');
  const [filterFloor, setFilterFloor] = useState<number | 'All'>('All');
  const [filterRoomTypeId, setFilterRoomTypeId] = useState<number | 'All'>('All');
  const [filterGender, setFilterGender] = useState('All');
  const [filterStatus, setFilterStatus] = useState('All');

  // Room Detail Modal (xem chi tiết phòng & sinh viên đã check-in vào ở)
  const [detailTargetId, setDetailTargetId] = useState<number | null>(null);
  const [roomDetail, setRoomDetail] = useState<RoomDetailDto | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  // Modal create/edit (Manager/Admin only)
  const [showModal, setShowModal] = useState(false);
  const [editingRoom, setEditingRoom] = useState<RoomDto | null>(null);
  const [formData, setFormData] = useState<RoomFormData>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  // Status Change Modal (Manager/Admin only)
  const [statusModalTarget, setStatusModalTarget] = useState<RoomDto | null>(null);
  const [isChangingStatus, setIsChangingStatus] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // Load lookups (assigned blocks, room types, and full blocks for Manager/Admin)
  const loadLookups = useCallback(async () => {
    try {
      const [assignedRes, typesRes, blockRes] = await Promise.all([
        roomApi.getAssignedBlocks().catch(() => [] as GuardAssignedBlockDto[]),
        roomApi.getTypes().catch(() => [] as RoomTypeLookup[]),
        isGuardOnly
          ? Promise.resolve({ items: [] as BlockDto[] })
          : blockApi.getAll({ pageSize: 200 }).catch(() => ({ items: [] as BlockDto[] })),
      ]);
      setAssignedBlocks(assignedRes || []);
      setRoomTypes(typesRes || []);
      if (!isGuardOnly) {
        setBlocks(blockRes.items || []);
      }
    } catch {
      // ignore lookup errors
    }
  }, [isGuardOnly]);

  useEffect(() => {
    loadLookups();
  }, [loadLookups]);

  const candidateBlocks = useMemo(() => {
    const blockKw = searchBlockName.trim().toLowerCase();
    return assignedBlocks.filter(b => {
      if (filterBlockId !== 'All' && b.id !== filterBlockId) return false;
      if (filterGender !== 'All' && b.genderType !== filterGender) return false;
      if (!isGuardOnly && blockKw) {
        const matchName = b.name?.toLowerCase().includes(blockKw);
        const matchCode = b.code?.toLowerCase().includes(blockKw);
        const matchDorm = b.dormName?.toLowerCase().includes(blockKw);
        if (!matchName && !matchCode && !matchDorm) return false;
      }
      return true;
    });
  }, [assignedBlocks, filterBlockId, filterGender, isGuardOnly, searchBlockName]);

  // Danh sách tầng khả dụng dựa trên các Block và Giới tính đang chọn
  const availableFloors = useMemo(() => {
    const floorSet = new Set<number>();

    if (assignedBlocks.length > 0) {
      candidateBlocks.forEach(b => {
        if (b.numberOfFloors && b.numberOfFloors > 0) {
          for (let f = 1; f <= b.numberOfFloors; f++) {
            floorSet.add(f);
          }
        }
        (b.rooms || []).forEach(r => {
          if (r.floorLevel && r.floorLevel > 0) {
            floorSet.add(r.floorLevel);
          }
        });
      });
    } else {
      blocks
        .filter(b => {
          if (filterBlockId !== 'All' && b.id !== filterBlockId) return false;
          if (filterGender !== 'All' && b.genderType !== filterGender) return false;
          return true;
        })
        .forEach(b => {
          if (b.numberOfFloors && b.numberOfFloors > 0) {
            for (let f = 1; f <= b.numberOfFloors; f++) {
              floorSet.add(f);
            }
          }
        });
      rooms.forEach(r => {
        if (r.floorLevel && r.floorLevel > 0) {
          floorSet.add(r.floorLevel);
        }
      });
    }

    return Array.from(floorSet).sort((a, b) => a - b);
  }, [assignedBlocks.length, candidateBlocks, blocks, rooms, filterBlockId, filterGender]);

  // Danh sách tên phòng khả dụng để lọc theo tên phòng (dựa trên Block / Tầng / Giới tính đang chọn)
  const availableRoomNames = useMemo(() => {
    const roomSet = new Set<string>();

    if (assignedBlocks.length > 0) {
      candidateBlocks.forEach(b => {
        (b.rooms || []).forEach(r => {
          if (filterFloor !== 'All' && r.floorLevel !== filterFloor) return;
          if (r.roomNumber) {
            roomSet.add(r.roomNumber);
          }
        });
      });
    } else {
      rooms.forEach(r => {
        if (filterFloor !== 'All' && r.floorLevel !== filterFloor) return;
        if (filterGender !== 'All' && r.blockGenderType !== filterGender) return;
        if (r.roomNumber) {
          roomSet.add(r.roomNumber);
        }
      });
    }

    return Array.from(roomSet).sort((a, b) =>
      a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })
    );
  }, [assignedBlocks.length, candidateBlocks, rooms, filterFloor, filterGender]);

  // Tự động đặt lại filterExactRoom hoặc filterFloor nếu không còn khớp với Block/Giới tính vừa chọn
  useEffect(() => {
    if (filterFloor !== 'All' && availableFloors.length > 0 && !availableFloors.includes(filterFloor)) {
      setFilterFloor('All');
    }
  }, [availableFloors, filterFloor]);

  useEffect(() => {
    if (filterExactRoom !== 'All' && availableRoomNames.length > 0 && !availableRoomNames.includes(filterExactRoom)) {
      setFilterExactRoom('All');
    }
  }, [availableRoomNames, filterExactRoom]);

  // Load rooms
  const loadRooms = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await roomApi.getAll({
        blockId: filterBlockId !== 'All' ? filterBlockId : undefined,
        blockName: !isGuardOnly && searchBlockName ? searchBlockName : undefined,
        roomNumber: searchRoomNumber.trim() || undefined,
        exactRoomNumber: filterExactRoom !== 'All' ? filterExactRoom : undefined,
        floorLevel: filterFloor !== 'All' ? filterFloor : undefined,
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
  }, [
    filterBlockId,
    isGuardOnly,
    searchBlockName,
    searchRoomNumber,
    filterExactRoom,
    filterFloor,
    filterRoomTypeId,
    filterGender,
    filterStatus,
    page,
    pageSize,
  ]);

  useEffect(() => {
    loadRooms();
  }, [loadRooms]);

  const hasActiveFilters = Boolean(
    searchRoomNumber.trim() ||
    searchBlockName.trim() ||
    filterBlockId !== 'All' ||
    filterExactRoom !== 'All' ||
    filterFloor !== 'All' ||
    filterRoomTypeId !== 'All' ||
    filterGender !== 'All' ||
    filterStatus !== 'All'
  );

  const resetFilters = () => {
    setSearchRoomNumber('');
    setSearchBlockName('');
    setFilterBlockId('All');
    setFilterExactRoom('All');
    setFilterFloor('All');
    setFilterRoomTypeId('All');
    setFilterGender('All');
    setFilterStatus('All');
    setPage(1);
  };

  const showNotification = (msg: string) => {
    setSuccessMsg(msg);
    setTimeout(() => setSuccessMsg(null), 3500);
  };

  // Xem chi tiết phòng & danh sách sinh viên đã check-in vào ở
  const openRoomDetail = async (room: RoomDto) => {
    setDetailTargetId(room.id);
    setRoomDetail(null);
    setDetailError(null);
    setIsLoadingDetail(true);
    try {
      const detail = await roomApi.getDetail(room.id);
      setRoomDetail(detail);
    } catch (err: any) {
      setDetailError(err.message || 'Không thể tải thông tin chi tiết phòng');
    } finally {
      setIsLoadingDetail(false);
    }
  };

  const closeRoomDetail = () => {
    setDetailTargetId(null);
    setRoomDetail(null);
    setDetailError(null);
  };

  // Handlers (Manager/Admin)
  const openCreate = () => {
    setEditingRoom(null);
    const defaultBlock = blocks[0];
    setFormData({
      blockId: defaultBlock?.id || 1,
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
      floorLevel: room.floorLevel ?? 1,
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
    const activeBlockId = editingRoom ? editingRoom.blockId : formData.blockId;
    const currentBlock = blocks.find(b => b.id === activeBlockId);
    const maxFloors = currentBlock?.numberOfFloors && currentBlock.numberOfFloors > 0
      ? currentBlock.numberOfFloors
      : null;

    if (maxFloors && (formData.floorLevel === '' || formData.floorLevel < 1 || formData.floorLevel > maxFloors)) {
      setFormError(`Số tầng không hợp lệ. ${currentBlock?.name || 'Block này'} chỉ có từ tầng 1 đến tầng ${maxFloors}`);
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
      await Promise.all([loadRooms(), loadLookups()]);
      if (editingRoom && detailTargetId === editingRoom.id) {
        roomApi.getDetail(editingRoom.id).then(setRoomDetail).catch(() => {});
      }
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
    const targetId = statusModalTarget.id;
    const targetStatus = statusModalTarget.status === 'Active' ? 'Maintenance' : 'Active';
    setIsChangingStatus(true);
    setStatusError(null);
    try {
      await roomApi.updateStatus(targetId, targetStatus);
      showNotification(`Đã chuyển trạng thái phòng ${statusModalTarget.roomNumber} sang ${targetStatus === 'Active' ? 'Hoạt động' : 'Bảo trì'}`);
      setStatusModalTarget(null);
      await loadRooms();
      if (detailTargetId === targetId) {
        roomApi.getDetail(targetId).then(setRoomDetail).catch(() => {});
      }
    } catch (err: any) {
      setStatusError(err.message || 'Không thể đổi trạng thái phòng');
    } finally {
      setIsChangingStatus(false);
    }
  };

  const handleRefresh = () => {
    void loadRooms();
    void loadLookups();
  };

  const guardDormLabel = assignedBlocks[0]?.dormName || currentUser?.dormName || '';
  const blockDropdownItems = useMemo(() => {
    if (assignedBlocks.length > 0) return assignedBlocks;
    return blocks.map(b => ({
      id: b.id,
      dormId: b.dormId,
      dormName: b.dormName,
      code: b.code,
      name: b.name,
      genderType: b.genderType,
      numberOfFloors: b.numberOfFloors,
      status: b.status,
      rooms: [],
    }));
  }, [assignedBlocks, blocks]);

  return (
    <div className="facilities-container">
      {/* Header */}
      <div className="facilities-header">
        <div className="facilities-title-group">
          <span className="facilities-sub">
            {isGuardOnly
              ? `An ninh KTX FUD${guardDormLabel ? ` · Khu phụ trách: ${guardDormLabel}` : ''}`
              : 'Ban Quản lý KTX FUD'}
          </span>
          <h1 className="facilities-title">Quản lý Phòng (Room)</h1>
        </div>
        {!isGuardOnly && (
          <div>
            <button type="button" className="btn-fud-primary" onClick={openCreate}>
              + Thêm phòng mới
            </button>
          </div>
        )}
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
          <h2 className="facilities-card-title">
            {isGuardOnly ? 'Danh sách phòng thuộc Block phụ trách' : 'Danh sách phòng ở Ký túc xá'}
          </h2>
          <p className="facilities-card-desc">
            {isGuardOnly
              ? 'Tra cứu, lọc phòng theo tên phòng, tầng, giới tính và nhấn vào phòng để xem chi tiết các sinh viên đã check-in vào ở'
              : 'Quản lý thông tin phòng, tìm kiếm theo tên phòng, lọc theo tên phòng, tầng, giới tính và nhấn vào phòng để xem chi tiết sinh viên đã check-in vào ở'}
          </p>
        </div>

        {/* Search & Filters */}
        <div className="facilities-filter-row">
          {/* Tìm kiếm theo tên phòng */}
          <input
            className="facilities-search-input"
            placeholder="Tìm kiếm theo tên phòng (VD: 101, 101L)..."
            aria-label="Tìm kiếm theo tên phòng"
            value={searchRoomNumber}
            onChange={e => { setSearchRoomNumber(e.target.value); setPage(1); }}
          />

          {/* Lọc theo tên phòng */}
          <select
            className="facilities-select"
            aria-label="Lọc theo tên phòng"
            value={filterExactRoom}
            onChange={e => { setFilterExactRoom(e.target.value); setPage(1); }}
          >
            <option value="All">Lọc tên phòng: Tất cả</option>
            {availableRoomNames.map(roomName => (
              <option key={roomName} value={roomName}>
                Phòng {roomName}
              </option>
            ))}
          </select>

          {/* Lọc theo tầng */}
          <select
            className="facilities-select"
            aria-label="Lọc theo tầng"
            value={filterFloor}
            onChange={e => {
              const val = e.target.value;
              setFilterFloor(val === 'All' ? 'All' : Number(val));
              setPage(1);
            }}
          >
            <option value="All">Tất cả tầng</option>
            {availableFloors.map(floor => (
              <option key={floor} value={floor}>
                Tầng {floor}
              </option>
            ))}
          </select>

          {/* Lọc theo giới tính */}
          <select
            className="facilities-select"
            aria-label="Lọc theo giới tính"
            value={filterGender}
            onChange={e => { setFilterGender(e.target.value); setPage(1); }}
          >
            <option value="All">Tất cả giới tính</option>
            <option value="Male">Phòng Nam</option>
            <option value="Female">Phòng Nữ</option>
          </select>

          {/* Lọc theo Block */}
          <select
            className="facilities-select"
            aria-label="Lọc theo Block"
            value={filterBlockId}
            onChange={e => {
              const val = e.target.value;
              setFilterBlockId(val === 'All' ? 'All' : Number(val));
              setPage(1);
            }}
          >
            <option value="All">{isGuardOnly ? 'Tất cả Block quản lý' : 'Tất cả Block'}</option>
            {blockDropdownItems.map(b => {
              const hasDormInName = Boolean(b.dormName && b.name.toLowerCase().includes(b.dormName.toLowerCase()));
              const dormSuffix = !isGuardOnly && b.dormName && !hasDormInName ? ` — ${b.dormName}` : '';
              const hasGenderInName = /\((Nam|Nữ)\)/i.test(b.name);
              const genderSuffix = hasGenderInName ? '' : ` (${b.genderType === 'Male' ? 'Nam' : 'Nữ'})`;
              return (
                <option key={b.id} value={b.id}>
                  {b.name}{dormSuffix}{genderSuffix}
                </option>
              );
            })}
          </select>

          {!isGuardOnly && (
            <>
              <select
                className="facilities-select"
                aria-label="Lọc theo loại phòng"
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
                aria-label="Lọc theo trạng thái"
                value={filterStatus}
                onChange={e => { setFilterStatus(e.target.value); setPage(1); }}
              >
                <option value="All">Tất cả trạng thái</option>
                <option value="Active">Đang hoạt động</option>
                <option value="Maintenance">Đang bảo trì</option>
              </select>
            </>
          )}

          {hasActiveFilters && (
            <button type="button" className="btn-fud-secondary" onClick={resetFilters}>
              Đặt lại lọc
            </button>
          )}
          <button type="button" className="btn-fud-secondary" onClick={handleRefresh}>
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
              <p>
                {isGuardOnly
                  ? 'Vui lòng thay đổi bộ lọc tên phòng, tầng, giới tính hoặc từ khóa tìm kiếm'
                  : 'Vui lòng thay đổi bộ lọc tên phòng, tầng, giới tính, từ khóa tìm kiếm hoặc thêm phòng mới'}
              </p>
            </div>
          ) : (
            <table className="facilities-table">
              <thead>
                <tr>
                  <th style={{ width: '10%' }}>Tên phòng</th>
                  <th style={{ width: '18%' }}>Thuộc Block</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Giới tính</th>
                  <th style={{ width: '9%', textAlign: 'center' }}>Tầng</th>
                  <th style={{ width: '15%' }}>Loại phòng</th>
                  <th style={{ width: '13%', textAlign: 'center' }}>SV đã Check-in</th>
                  <th style={{ width: '11%', textAlign: 'center' }}>Giường trống</th>
                  <th style={{ width: '10%', textAlign: 'center' }}>Trạng thái</th>
                  <th style={{ width: '14%', textAlign: 'center' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {rooms.map(room => {
                  const checkedInCount = room.checkedInStudentsCount ?? 0;
                  const showDormSubtitle = Boolean(
                    !isGuardOnly &&
                    room.dormName &&
                    !room.blockName.toLowerCase().includes(room.dormName.toLowerCase())
                  );
                  return (
                    <tr
                      key={room.id}
                      className="facilities-clickable-row"
                      onClick={() => openRoomDetail(room)}
                      title="Nhấn để xem chi tiết phòng và danh sách sinh viên đã check-in vào ở"
                    >
                      <td>
                        <span className="badge-code">{room.roomNumber}</span>
                      </td>
                      <td>
                        <div style={{ fontWeight: 600 }}>{room.blockName}</div>
                        {showDormSubtitle && (
                          <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
                            {room.dormName}
                          </div>
                        )}
                      </td>
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
                        <span className={`badge-checkedin ${checkedInCount > 0 ? 'has-residents' : ''}`}>
                          {checkedInCount} SV đang ở
                        </span>
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        <span className={`badge-capacity ${room.freeBeds > 0 ? 'has-free' : ''}`}>
                          {room.freeBeds} / {room.totalBeds} trống
                        </span>
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        {isGuardOnly ? (
                          <span
                            className={`${room.status === 'Active' ? 'badge-active' : 'badge-maintenance'} badge-readonly`}
                          >
                            {room.status === 'Active' ? 'Hoạt động' : 'Bảo trì'}
                          </span>
                        ) : (
                          <span
                            className={room.status === 'Active' ? 'badge-active' : 'badge-maintenance'}
                            onClick={e => { e.stopPropagation(); openStatusModal(room); }}
                            title="Nhấn để đổi trạng thái phòng"
                          >
                            {room.status === 'Active' ? 'Hoạt động' : 'Bảo trì'}
                          </span>
                        )}
                      </td>
                      <td style={{ textAlign: 'center' }} onClick={e => e.stopPropagation()}>
                        <div className="facilities-actions" style={{ justifyContent: 'center' }}>
                          <button
                            type="button"
                            className="btn-tbl-action"
                            onClick={() => openRoomDetail(room)}
                          >
                            Chi tiết
                          </button>
                          {!isGuardOnly && (
                            <button
                              type="button"
                              className={`btn-tbl-action${!room.isEditable ? ' btn-tbl-action-disabled' : ''}`}
                              onClick={() => room.isEditable && openEdit(room)}
                              disabled={!room.isEditable}
                              title={
                                !room.isEditable
                                  ? `Phòng đang có ${room.occupiedBeds} sinh viên cư trú — không thể chỉnh sửa`
                                  : 'Chỉnh sửa thông tin phòng'
                              }
                            >
                              Sửa
                            </button>
                          )}
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
          itemName="phòng"
        />
      </div>

      {/* ── Room Detail Modal (Danh sách SV đã Check-in vào ở) ── */}
      {detailTargetId !== null && (
        <div className="facilities-modal-backdrop" onClick={closeRoomDetail}>
          <div
            className="facilities-modal-card facilities-modal-card-wide"
            onClick={e => e.stopPropagation()}
          >
            <div className="facilities-modal-header">
              <div>
                <h3 className="facilities-modal-title">
                  {roomDetail
                    ? `Chi tiết Phòng ${roomDetail.roomNumber} — ${roomDetail.blockName}`
                    : 'Chi tiết thông tin phòng'}
                </h3>
                {roomDetail && (
                  <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', marginTop: '6px', alignItems: 'center' }}>
                    {roomDetail.dormName && (
                      <span className="badge-code">{roomDetail.dormName}</span>
                    )}
                    <span className={roomDetail.blockGenderType === 'Male' ? 'badge-gender-male' : 'badge-gender-female'}>
                      {roomDetail.blockGenderType === 'Male' ? 'Phòng Nam' : 'Phòng Nữ'}
                    </span>
                    <span className="badge-code">
                      {roomDetail.floorLevel ? `Tầng ${roomDetail.floorLevel}` : 'Chưa rõ tầng'}
                    </span>
                    <span className="badge-code">
                      {roomDetail.roomTypeName || `${roomDetail.bedCapacity} giường`}
                    </span>
                    <span className={`${roomDetail.status === 'Active' ? 'badge-active' : 'badge-maintenance'} badge-readonly`}>
                      {roomDetail.status === 'Active' ? 'Đang hoạt động' : 'Đang bảo trì'}
                    </span>
                  </div>
                )}
              </div>
              <button type="button" className="facilities-modal-close" onClick={closeRoomDetail}>✕</button>
            </div>

            <div className="facilities-modal-body room-detail-body">
              {isLoadingDetail ? (
                <div className="facilities-empty-cell">
                  <p>Đang tải danh sách sinh viên đã check-in vào ở...</p>
                </div>
              ) : detailError ? (
                <div className="facilities-form-error">{detailError}</div>
              ) : roomDetail ? (
                <>
                  {/* Thống kê tổng quan phòng */}
                  <div className="room-detail-summary-grid">
                    <div className="room-detail-stat-box">
                      <span className="room-detail-stat-label">Tổng số giường</span>
                      <span className="room-detail-stat-value">{roomDetail.totalBeds} giường</span>
                    </div>
                    <div className="room-detail-stat-box" style={{ backgroundColor: '#eff6ff', borderColor: '#bfdbfe' }}>
                      <span className="room-detail-stat-label" style={{ color: '#1e40af' }}>Sinh viên đã Check-in vào ở</span>
                      <span className="room-detail-stat-value" style={{ color: '#1d4ed8' }}>
                        {roomDetail.checkedInCount} sinh viên
                      </span>
                    </div>
                    <div className="room-detail-stat-box" style={{ backgroundColor: '#ecfdf5', borderColor: '#a7f3d0' }}>
                      <span className="room-detail-stat-label" style={{ color: '#065f46' }}>Giường trống hiện tại</span>
                      <span className="room-detail-stat-value" style={{ color: '#059669' }}>
                        {roomDetail.freeBeds} giường
                      </span>
                    </div>
                  </div>

                  {/* Danh sách sinh viên đã Check-in vào ở */}
                  <div className="room-detail-section">
                    <div className="room-detail-section-header">
                      <h4 className="room-detail-section-title">
                        Danh sách sinh viên đã Check-in vào ở ({roomDetail.checkedInStudents.length})
                      </h4>
                    </div>

                    {roomDetail.checkedInStudents.length === 0 ? (
                      <div
                        className="facilities-empty-cell"
                        style={{
                          border: '1px dashed #cbd5e1',
                          borderRadius: '8px',
                          backgroundColor: '#f8fafc',
                          padding: '28px 16px',
                        }}
                      >
                        <p style={{ fontWeight: 600, color: '#0f172a', margin: '0 0 4px' }}>
                          Chưa có sinh viên nào check-in vào ở trong phòng này
                        </p>
                        <p style={{ margin: 0, fontSize: '13px' }}>
                          Sinh viên sẽ hiển thị tại đây sau khi hoàn tất thủ tục nhận phòng (Check-in)
                        </p>
                      </div>
                    ) : (
                      <div className="facilities-table-wrap">
                        <table className="facilities-table">
                          <thead>
                            <tr>
                              <th>MSSV</th>
                              <th>Họ và tên</th>
                              <th>Giường ở</th>
                              <th>Ngành / Khóa</th>
                              <th>Liên hệ</th>
                              <th>Học kỳ</th>
                              <th style={{ textAlign: 'center' }}>Ngày Check-in</th>
                            </tr>
                          </thead>
                          <tbody>
                            {roomDetail.checkedInStudents.map(st => (
                              <tr key={st.residencyId}>
                                <td>
                                  <span className="badge-code">{st.studentCode}</span>
                                </td>
                                <td>
                                  <div style={{ fontWeight: 600, color: '#0f172a' }}>{st.fullName}</div>
                                  {st.gender && (
                                    <div style={{ fontSize: '12px', color: '#64748b' }}>
                                      {st.gender === 'Male' ? 'Nam' : st.gender === 'Female' ? 'Nữ' : st.gender}
                                    </div>
                                  )}
                                </td>
                                <td>
                                  <span className="badge-bed-occupied">{st.bedCode}</span>
                                  {st.positionInRoom && (
                                    <div style={{ fontSize: '12px', color: '#64748b', marginTop: '3px' }}>
                                      {st.positionInRoom}
                                    </div>
                                  )}
                                </td>
                                <td>
                                  <div style={{ fontSize: '13px', color: '#334155' }}>
                                    {st.major || '—'}
                                  </div>
                                  {st.cohort && (
                                    <div style={{ fontSize: '12px', color: '#64748b' }}>Khóa {st.cohort}</div>
                                  )}
                                </td>
                                <td>
                                  <div style={{ fontSize: '13px', color: '#334155' }}>{st.phone || '—'}</div>
                                  {st.email && (
                                    <div style={{ fontSize: '12px', color: '#64748b' }}>{st.email}</div>
                                  )}
                                </td>
                                <td style={{ fontSize: '13px' }}>{st.termName || '—'}</td>
                                <td style={{ textAlign: 'center', fontWeight: 600, color: '#059669' }}>
                                  {formatDateDisplay(st.checkInDate || st.handoverCompletedTime)}
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>

                  {/* Tình trạng từng giường trong phòng */}
                  {roomDetail.beds && roomDetail.beds.length > 0 && (
                    <div className="room-detail-section">
                      <h4 className="room-detail-section-title">
                        Tình trạng các giường trong phòng ({roomDetail.beds.length})
                      </h4>
                      <div className="room-beds-mini-grid">
                        {roomDetail.beds.map(bed => {
                          const effectiveStatus = bed.checkedInStudentName && bed.status === 'Free' ? 'Occupied' : bed.status;
                          const badge = getBedStatusBadge(effectiveStatus);
                          return (
                            <div key={bed.bedId} className="room-bed-mini-card">
                              <div className="room-bed-mini-top">
                                <span className="badge-code">{bed.bedCode}</span>
                                <span className={badge.className}>{badge.label}</span>
                              </div>
                              {bed.positionInRoom && (
                                <div style={{ fontSize: '12px', color: '#64748b' }}>
                                  Vị trí: {bed.positionInRoom}
                                </div>
                              )}
                              <div style={{ fontSize: '12.5px', color: bed.checkedInStudentName ? '#0f172a' : '#94a3b8', fontWeight: bed.checkedInStudentName ? 600 : 400 }}>
                                {bed.checkedInStudentName
                                  ? `${bed.checkedInStudentName} (${bed.checkedInStudentCode})`
                                  : 'Chưa có SV check-in'}
                              </div>
                            </div>
                          );
                        })}
                      </div>
                    </div>
                  )}
                </>
              ) : null}
            </div>

            <div className="facilities-modal-footer">
              {!isGuardOnly && roomDetail && (
                <>
                  <button
                    type="button"
                    className="btn-fud-secondary"
                    onClick={() => {
                      const targetRoom: RoomDto = {
                        id: roomDetail.id,
                        dormId: roomDetail.dormId,
                        dormName: roomDetail.dormName,
                        blockId: roomDetail.blockId,
                        blockName: roomDetail.blockName,
                        blockGenderType: roomDetail.blockGenderType,
                        roomTypeId: roomDetail.roomTypeId,
                        roomTypeName: roomDetail.roomTypeName,
                        bedCapacity: roomDetail.bedCapacity,
                        roomNumber: roomDetail.roomNumber,
                        floorLevel: roomDetail.floorLevel,
                        status: roomDetail.status,
                        totalBeds: roomDetail.totalBeds,
                        freeBeds: roomDetail.freeBeds,
                        occupiedBeds: roomDetail.occupiedBeds,
                        checkedInStudentsCount: roomDetail.checkedInCount,
                        isEditable: roomDetail.occupiedBeds === 0,
                      };
                      closeRoomDetail();
                      openStatusModal(targetRoom);
                    }}
                  >
                    {roomDetail.status === 'Active' ? 'Chuyển sang Bảo trì' : 'Mở lại Hoạt động'}
                  </button>
                  <button
                    type="button"
                    className="btn-fud-primary"
                    disabled={roomDetail.occupiedBeds > 0}
                    title={
                      roomDetail.occupiedBeds > 0
                        ? `Phòng đang có ${roomDetail.occupiedBeds} giường đang sử dụng/giữ chỗ — không thể chỉnh sửa`
                        : 'Chỉnh sửa thông tin phòng'
                    }
                    onClick={() => {
                      if (roomDetail.occupiedBeds > 0) return;
                      const targetRoom: RoomDto = {
                        id: roomDetail.id,
                        dormId: roomDetail.dormId,
                        dormName: roomDetail.dormName,
                        blockId: roomDetail.blockId,
                        blockName: roomDetail.blockName,
                        blockGenderType: roomDetail.blockGenderType,
                        roomTypeId: roomDetail.roomTypeId,
                        roomTypeName: roomDetail.roomTypeName,
                        bedCapacity: roomDetail.bedCapacity,
                        roomNumber: roomDetail.roomNumber,
                        floorLevel: roomDetail.floorLevel,
                        status: roomDetail.status,
                        totalBeds: roomDetail.totalBeds,
                        freeBeds: roomDetail.freeBeds,
                        occupiedBeds: roomDetail.occupiedBeds,
                        checkedInStudentsCount: roomDetail.checkedInCount,
                        isEditable: true,
                      };
                      closeRoomDetail();
                      openEdit(targetRoom);
                    }}
                  >
                    Chỉnh sửa phòng
                  </button>
                </>
              )}
              <button type="button" className="btn-fud-secondary" onClick={closeRoomDetail}>
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Create / Edit Modal (Manager/Admin) ────────────── */}
      {!isGuardOnly && showModal && (
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
                  <label className="facilities-form-label">Block <span className="required">*</span></label>
                  <select
                    className="facilities-form-input"
                    value={formData.blockId}
                    onChange={e => {
                      const newBlockId = Number(e.target.value);
                      const targetBlock = blocks.find(b => b.id === newBlockId);
                      const maxF = targetBlock?.numberOfFloors && targetBlock.numberOfFloors > 0 ? targetBlock.numberOfFloors : 1;
                      setFormData(f => ({
                        ...f,
                        blockId: newBlockId,
                        floorLevel: (typeof f.floorLevel === 'number' && f.floorLevel > maxF) ? 1 : (f.floorLevel || 1),
                      }));
                    }}
                  >
                    {blocks.map(b => (
                      <option key={b.id} value={b.id}>
                        {b.name} ({b.dormName || b.code}) - {b.genderType === 'Male' ? 'Block Nam' : 'Block Nữ'}{b.numberOfFloors ? ` (${b.numberOfFloors} tầng)` : ''}
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {editingRoom && (
                <div className="facilities-form-group">
                  <label className="facilities-form-label">Block</label>
                  <input className="facilities-form-input" value={editingRoom.blockName} disabled />
                  <span className="facilities-form-hint">Không thể chuyển phòng sang Block khác sau khi tạo</span>
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

              {/* Floor Dropdown theo số tầng của Block */}
              {(() => {
                const activeBlockId = editingRoom ? editingRoom.blockId : formData.blockId;
                const currentBlock = blocks.find(b => b.id === activeBlockId);
                const maxFloors = currentBlock?.numberOfFloors && currentBlock.numberOfFloors > 0
                  ? currentBlock.numberOfFloors
                  : 1;
                const floorOptions = Array.from({ length: maxFloors }, (_, i) => i + 1);

                return (
                  <div className="facilities-form-group">
                    <label className="facilities-form-label">
                      Tầng <span className="required">*</span>
                      {currentBlock?.numberOfFloors ? (
                        <span className="facilities-form-hint" style={{ marginLeft: 8 }}>
                          ({currentBlock.name} có {currentBlock.numberOfFloors} tầng)
                        </span>
                      ) : null}
                    </label>
                    <select
                      className="facilities-form-input"
                      value={formData.floorLevel || 1}
                      onChange={e => setFormData(f => ({ ...f, floorLevel: Number(e.target.value) }))}
                    >
                      {floorOptions.map(floor => (
                        <option key={floor} value={floor}>
                          Tầng {floor}
                        </option>
                      ))}
                    </select>
                  </div>
                );
              })()}
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

      {/* ── Toggle Status Modal (Manager/Admin) ────────────── */}
      {!isGuardOnly && statusModalTarget && (
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
