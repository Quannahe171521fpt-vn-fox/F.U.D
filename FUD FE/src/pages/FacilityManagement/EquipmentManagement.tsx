import React, { useState, useId } from 'react';
import type { EquipmentRecord, EquipmentCatalogItem } from '../../types/management';
import { AVAILABLE_LOCATIONS } from '../../data/managementData';

interface EquipmentManagementProps {
  equipmentList: EquipmentRecord[];
  catalogList: EquipmentCatalogItem[];
  locationList?: string[];
  onSaveEquipment: (record: EquipmentRecord) => void;
  onDeleteEquipment: (id: number) => void;
  onSaveCatalogItem: (item: EquipmentCatalogItem) => void;
  onDeleteCatalogItem: (id: number) => void;
}

export const EquipmentManagement: React.FC<EquipmentManagementProps> = ({
  equipmentList,
  catalogList,
  onSaveEquipment,
  onDeleteEquipment,
  onSaveCatalogItem,
  onDeleteCatalogItem,
}) => {
  // Tabs: 'records' (Thiết bị theo Asset Code) vs 'catalog' (Danh mục thiết bị)
  const [activeTab, setActiveTab] = useState<'records' | 'catalog'>('records');

  // Search & Filter
  const [searchQuery, setSearchQuery] = useState('');
  const [conditionFilter, setConditionFilter] = useState<'All' | 'Good' | 'Under repair' | 'Broken'>('All');

  // Pagination State
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 6;

  // Equipment Form Editor State
  const [isEquipFormOpen, setIsEquipFormOpen] = useState(false);
  const [editingEquipId, setEditingEquipId] = useState<number | null>(null);
  const [assetCode, setAssetCode] = useState('');
  const [equipName, setEquipName] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('');
  const [selectedLocation, setSelectedLocation] = useState('');
  const [equipCondition, setEquipCondition] = useState<'Good' | 'Under repair' | 'Broken'>('Good');

  // Move Equipment Quick Modal State
  const [movingEquip, setMovingEquip] = useState<EquipmentRecord | null>(null);
  const [newMoveLocation, setNewMoveLocation] = useState('');

  // Catalog Form Modal State
  const [isCatalogModalOpen, setIsCatalogModalOpen] = useState(false);
  const [catalogCategory, setCatalogCategory] = useState('');
  const [catalogName, setCatalogName] = useState('');

  // Notifications & Inline Errors
  const [inlineAssetError, setInlineAssetError] = useState<string | null>(null);
  const [generalError, setGeneralError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const searchInputId = useId();
  const conditionFilterId = useId();

  const showNotification = (msg: string) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), 3500);
  };

  // Filtered Equipment Records
  const filteredEquipment = equipmentList.filter((item) => {
    const matchesSearch =
      !searchQuery.trim() ||
      item.assetCode.toLowerCase().includes(searchQuery.toLowerCase().trim()) ||
      item.name.toLowerCase().includes(searchQuery.toLowerCase().trim());

    const matchesCondition =
      conditionFilter === 'All' || item.condition === conditionFilter;

    return matchesSearch && matchesCondition;
  });

  // Calculate pagination for equipment records
  const totalPages = Math.max(1, Math.ceil(filteredEquipment.length / pageSize));
  const validCurrentPage = Math.min(currentPage, totalPages);
  const paginatedEquipment = filteredEquipment.slice(
    (validCurrentPage - 1) * pageSize,
    validCurrentPage * pageSize
  );

  const handleSearchChange = (query: string) => {
    setSearchQuery(query);
    setCurrentPage(1);
  };

  const handleConditionChange = (condition: 'All' | 'Good' | 'Under repair' | 'Broken') => {
    setConditionFilter(condition);
    setCurrentPage(1);
  };

  // Open Add Equipment Form
  const openAddEquipmentForm = () => {
    setEditingEquipId(null);
    setAssetCode(`TB-${String(Math.floor(100000 + Math.random() * 900000))}`);
    setEquipName('');
    setSelectedCategory(catalogList[0]?.name || 'Quạt treo tường');
    setSelectedLocation(AVAILABLE_LOCATIONS[0] || 'Phòng A101');
    setEquipCondition('Good');
    setInlineAssetError(null);
    setGeneralError(null);
    setIsEquipFormOpen(true);
  };

  // Open Edit Equipment Form
  const openEditEquipmentForm = (item: EquipmentRecord) => {
    setEditingEquipId(item.id);
    setAssetCode(item.assetCode);
    setEquipName(item.name);
    setSelectedCategory(item.name);
    setSelectedLocation(item.location);
    setEquipCondition(item.condition);
    setInlineAssetError(null);
    setGeneralError(null);
    setIsEquipFormOpen(true);
  };

  // Handle Save Equipment
  const handleSaveEquipment = (e: React.FormEvent) => {
    e.preventDefault();
    setInlineAssetError(null);
    setGeneralError(null);

    const trimmedCode = assetCode.trim().toUpperCase();
    if (!trimmedCode) {
      setInlineAssetError('Mã thiết bị không được để trống.');
      return;
    }

    if (trimmedCode.length > 30) {
      setInlineAssetError('Mã thiết bị không được vượt quá 30 ký tự.');
      return;
    }

    const duplicate = equipmentList.find(
      (e) => e.assetCode.toLowerCase() === trimmedCode.toLowerCase() && e.id !== editingEquipId
    );
    if (duplicate) {
      setInlineAssetError('Mã thiết bị này đã tồn tại trong hệ thống. Vui lòng nhập mã khác.');
      return;
    }

    if (!equipName.trim()) {
      setGeneralError('Vui lòng nhập tên thiết bị.');
      return;
    }

    if (equipName.trim().length > 100) {
      setGeneralError('Tên thiết bị không được vượt quá 100 ký tự.');
      return;
    }

    const matchedCatalog = catalogList.find((c) => c.name === selectedCategory);
    const categoryName = matchedCatalog ? matchedCatalog.category : 'Thiết bị khác';

    const updatedRecord: EquipmentRecord = {
      id: editingEquipId || Date.now(),
      assetCode: trimmedCode,
      name: equipName.trim(),
      category: categoryName,
      location: selectedLocation,
      condition: equipCondition,
      hasOpenTicket: editingEquipId
        ? (equipmentList.find((e) => e.id === editingEquipId)?.hasOpenTicket || false)
        : false,
    };

    onSaveEquipment(updatedRecord);
    setIsEquipFormOpen(false);
    showNotification(
      editingEquipId
        ? `Đã cập nhật thông tin thiết bị [${trimmedCode}] thành công.`
        : `Đã thêm mới thiết bị [${trimmedCode}] vào hệ thống.`
    );
  };

  // Handle Delete Equipment
  const handleDeleteEquipment = (item: EquipmentRecord) => {
    if (item.hasOpenTicket) {
      alert(`Không thể xóa thiết bị [${item.assetCode}] vì đang có phiếu báo hỏng hóc hoặc sự cố chưa đóng.`);
      return;
    }

    if (window.confirm(`Bạn có chắc chắn muốn xóa thiết bị "${item.name}" (Mã: ${item.assetCode}) không?`)) {
      onDeleteEquipment(item.id);
      showNotification(`Đã xóa thiết bị [${item.assetCode}] khỏi danh sách.`);
      if (editingEquipId === item.id) {
        setIsEquipFormOpen(false);
      }
    }
  };

  // Handle Move Equipment
  const handleOpenMoveModal = (item: EquipmentRecord) => {
    setMovingEquip(item);
    setNewMoveLocation(item.location);
  };

  const handleConfirmMove = () => {
    if (!movingEquip) return;
    if (newMoveLocation === movingEquip.location) {
      alert('Vị trí mới phải khác với vị trí hiện tại.');
      return;
    }

    const updated: EquipmentRecord = {
      ...movingEquip,
      location: newMoveLocation,
    };
    onSaveEquipment(updated);
    showNotification(`Đã chuyển thiết bị [${movingEquip.assetCode}] sang ${newMoveLocation}.`);
    setMovingEquip(null);
  };

  // Catalog Actions
  const handleCreateCatalogItem = (e: React.FormEvent) => {
    e.preventDefault();
    if (!catalogName.trim() || !catalogCategory.trim()) {
      alert('Vui lòng nhập đầy đủ nhóm danh mục và tên loại thiết bị.');
      return;
    }

    const newItem: EquipmentCatalogItem = {
      id: Date.now(),
      category: catalogCategory.trim(),
      name: catalogName.trim(),
      inUseCount: 0,
    };
    onSaveCatalogItem(newItem);
    setIsCatalogModalOpen(false);
    setCatalogCategory('');
    setCatalogName('');
    showNotification(`Đã thêm thiết bị vào danh mục thành công.`);
  };

  const handleDeleteCatalog = (item: EquipmentCatalogItem) => {
    const isUsed = equipmentList.some((e) => e.name.toLowerCase() === item.name.toLowerCase());
    if (isUsed || item.inUseCount > 0) {
      alert(`Không thể xóa "${item.name}" vì đang có thiết bị thực tế thuộc loại này trong ký túc xá.`);
      return;
    }

    if (window.confirm(`Bạn có chắc muốn xóa loại "${item.name}" khỏi danh mục?`)) {
      onDeleteCatalogItem(item.id);
      showNotification(`Đã xóa "${item.name}" khỏi danh mục.`);
    }
  };

  return (
    <div className="management-page-container">
      {/* Toast Notification */}
      {successMessage && (
        <div className="mgmt-toast success-toast">
          <span className="toast-text">{successMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="mgmt-header-row">
        <div>
          <span className="mgmt-subheading">Hệ thống KTX FUD</span>
          <h1 className="mgmt-page-title">Quản lý Thiết bị</h1>
        </div>
        <div className="mgmt-header-actions">
          {activeTab === 'records' ? (
            <button
              type="button"
              className="btn-create-news"
              onClick={openAddEquipmentForm}
            >
              + Thêm thiết bị
            </button>
          ) : (
            <button
              type="button"
              className="btn-create-news"
              onClick={() => setIsCatalogModalOpen(true)}
            >
              + Thêm vào danh mục
            </button>
          )}
        </div>
      </div>

      {/* Top Tab Bar */}
      <div className="equipment-view-tabs">
        <button
          type="button"
          className={`equipment-tab-btn ${activeTab === 'records' ? 'active' : ''}`}
          onClick={() => setActiveTab('records')}
        >
          Danh sách thiết bị ({equipmentList.length})
        </button>
        <button
          type="button"
          className={`equipment-tab-btn ${activeTab === 'catalog' ? 'active' : ''}`}
          onClick={() => setActiveTab('catalog')}
        >
          Danh mục thiết bị ({catalogList.length})
        </button>
      </div>

      {activeTab === 'records' ? (
        <div className="mgmt-full-grid">
          {/* Equipment Records List */}
          <div className="mgmt-card list-card">
            <div className="card-header-flex">
              <div>
                <h2 className="card-heading">Danh sách thiết bị hiện có</h2>
                <p className="card-subtext">Quản lý mã tài sản, vị trí phòng/giường và tình trạng sử dụng</p>
              </div>
            </div>

            {/* Filters Row: Search + Condition Dropdown */}
            <div className="equipment-filters-row">
              <div className="search-field-wrap">
                <input
                  id={searchInputId}
                  type="text"
                  placeholder="Tìm kiếm theo mã tài sản hoặc tên..."
                  value={searchQuery}
                  onChange={(e) => handleSearchChange(e.target.value)}
                  maxLength={50}
                  className="mgmt-search-input"
                />
                {searchQuery && (
                  <button
                    type="button"
                    className="clear-search-btn"
                    onClick={() => handleSearchChange('')}
                  >
                    Đóng
                  </button>
                )}
              </div>

              <div className="filter-dropdown-wrap">
                <label htmlFor={conditionFilterId} className="dropdown-label">Tình trạng:</label>
                <select
                  id={conditionFilterId}
                  value={conditionFilter}
                  onChange={(e) => handleConditionChange(e.target.value as any)}
                  className="mgmt-select"
                >
                  <option value="All">Tất cả tình trạng</option>
                  <option value="Good">Tốt</option>
                  <option value="Under repair">Đang sửa chữa</option>
                  <option value="Broken">Hỏng</option>
                </select>
              </div>
            </div>

            {/* Table */}
            <div className="table-responsive">
              <table className="mgmt-table">
                <thead>
                  <tr>
                    <th style={{ width: '16%' }}>Mã thiết bị</th>
                    <th style={{ width: '24%' }}>Tên thiết bị</th>
                    <th style={{ width: '15%' }}>Phân loại</th>
                    <th style={{ width: '17%' }}>Vị trí</th>
                    <th style={{ width: '13%' }}>Tình trạng</th>
                    <th style={{ width: '15%', textAlign: 'right' }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredEquipment.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="empty-table-cell">
                        Không tìm thấy thiết bị nào khớp với từ khóa tìm kiếm.
                      </td>
                    </tr>
                  ) : (
                    paginatedEquipment.map((equip) => {
                      const isGood = equip.condition === 'Good';
                      const isRepair = equip.condition === 'Under repair';
                      const isBroken = equip.condition === 'Broken';

                      return (
                        <tr
                          key={equip.id}
                          className={editingEquipId === equip.id && isEquipFormOpen ? 'row-selected' : ''}
                        >
                          <td className="cell-asset-code">
                            <code>{equip.assetCode}</code>
                          </td>
                          <td className="cell-title font-semibold">
                            {equip.name}
                            {equip.hasOpenTicket && (
                              <span className="incident-badge" title="Đang có phiếu báo sự cố">
                                Đang báo hỏng
                              </span>
                            )}
                          </td>
                          <td>
                            <span className="category-pill">{equip.category}</span>
                          </td>
                          <td className="cell-location">
                            <span>{equip.location}</span>
                          </td>
                          <td>
                            {isGood && <span className="status-badge badge-good">Tốt</span>}
                            {isRepair && <span className="status-badge badge-repair">Đang sửa</span>}
                            {isBroken && <span className="status-badge badge-broken">Hỏng</span>}
                          </td>
                          <td className="cell-actions">
                            <button
                              type="button"
                              className="action-link edit-link"
                              onClick={() => openEditEquipmentForm(equip)}
                              title="Chỉnh sửa thông tin"
                            >
                              Sửa
                            </button>
                            <button
                              type="button"
                              className="action-link move-link"
                              onClick={() => handleOpenMoveModal(equip)}
                              title="Chuyển phòng hoặc giường khác"
                            >
                              Chuyển
                            </button>
                            <button
                              type="button"
                              className={`action-link delete-link ${equip.hasOpenTicket ? 'disabled-link' : ''}`}
                              onClick={() => handleDeleteEquipment(equip)}
                              title={equip.hasOpenTicket ? 'Đang có phiếu sự cố, không thể xóa' : 'Xóa thiết bị'}
                              disabled={equip.hasOpenTicket}
                            >
                              Xóa
                            </button>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination Controls */}
            {filteredEquipment.length > pageSize && (
              <div className="pagination-bar">
                <span className="pagination-info">
                  Hiển thị {(validCurrentPage - 1) * pageSize + 1} -{' '}
                  {Math.min(validCurrentPage * pageSize, filteredEquipment.length)} trên tổng số{' '}
                  {filteredEquipment.length} thiết bị
                </span>
                <div className="pagination-buttons">
                  <button
                    type="button"
                    className="page-btn nav-btn"
                    onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                    disabled={validCurrentPage === 1}
                  >
                    Trước
                  </button>
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((page) => (
                    <button
                      key={page}
                      type="button"
                      className={`page-btn ${page === validCurrentPage ? 'active' : ''}`}
                      onClick={() => setCurrentPage(page)}
                    >
                      {page}
                    </button>
                  ))}
                  <button
                    type="button"
                    className="page-btn nav-btn"
                    onClick={() => setCurrentPage((p) => Math.min(p + 1, totalPages))}
                    disabled={validCurrentPage === totalPages}
                  >
                    Sau
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      ) : (
        /* Equipment Catalog Management Tab */
        <div className="mgmt-card list-card">
          <div className="card-header-flex">
            <div>
              <h2 className="card-heading">Danh mục thiết bị tiêu chuẩn</h2>
              <p className="card-subtext">
                Định nghĩa các tiêu chuẩn loại thiết bị cấp phát cho các phòng ký túc xá
              </p>
            </div>
            <button
              type="button"
              className="btn-primary"
              onClick={() => setIsCatalogModalOpen(true)}
            >
              + Thêm loại thiết bị
            </button>
          </div>

          <div className="table-responsive">
            <table className="mgmt-table">
              <thead>
                <tr>
                  <th style={{ width: '12%' }}>Mã định danh</th>
                  <th style={{ width: '28%' }}>Phân loại</th>
                  <th style={{ width: '35%' }}>Tên thiết bị chuẩn</th>
                  <th style={{ width: '15%' }}>Số lượng đang dùng</th>
                  <th style={{ width: '10%', textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {catalogList.map((cat) => (
                  <tr key={cat.id}>
                    <td>#{cat.id}</td>
                    <td>
                      <span className="category-pill">{cat.category}</span>
                    </td>
                    <td className="font-semibold">{cat.name}</td>
                    <td>
                      <span className="in-use-badge">{cat.inUseCount} thiết bị</span>
                    </td>
                    <td className="cell-actions">
                      <button
                        type="button"
                        className="action-link delete-link"
                        onClick={() => handleDeleteCatalog(cat)}
                        title="Xóa loại khỏi danh mục"
                      >
                        Xóa
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Alternative Flow A1: Move Equipment Modal */}
      {movingEquip && (
        <div className="modal-backdrop" onClick={() => setMovingEquip(null)}>
          <div className="modal-card modal-compact" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div className="modal-title-wrap">
                <h3 className="modal-title">Điều chuyển vị trí thiết bị</h3>
                <p className="modal-subtitle">
                  Mã tài sản: <strong>{movingEquip.assetCode}</strong> ({movingEquip.name})
                </p>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setMovingEquip(null)}
              >
                ✕
              </button>
            </div>

            <div className="modal-form-content">
              <div className="info-box-current">
                <span className="info-label">Vị trí hiện tại:</span>
                <span className="info-val font-semibold">{movingEquip.location}</span>
              </div>

              <div className="form-group" style={{ marginTop: '16px' }}>
                <label htmlFor="move-target-location" className="input-label required-label">
                  Vị trí mới (Phòng / Giường tiếp nhận):
                </label>
                <select
                  id="move-target-location"
                  className="mgmt-select"
                  value={newMoveLocation}
                  onChange={(e) => setNewMoveLocation(e.target.value)}
                >
                  {AVAILABLE_LOCATIONS.map((loc) => (
                    <option key={loc} value={loc}>
                      {loc} {loc === movingEquip.location ? '(Hiện tại)' : ''}
                    </option>
                  ))}
                </select>
              </div>

              <div className="editor-actions-row" style={{ marginTop: '20px' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setMovingEquip(null)}
                >
                  Hủy
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  onClick={handleConfirmMove}
                >
                  Xác nhận điều chuyển
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* POPUP MODAL: Add / Edit Equipment (Thay vì để form bên cạnh) */}
      {isEquipFormOpen && (
        <div className="modal-backdrop" onClick={() => setIsEquipFormOpen(false)}>
          <div className="modal-card modal-editor-dialog" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div className="modal-title-wrap">
                <span className="mgmt-subheading">
                  {editingEquipId ? 'Cập nhật' : 'Thêm mới'}
                </span>
                <h3 className="modal-title">
                  {editingEquipId ? 'Sửa thông tin thiết bị' : 'Thêm thiết bị mới'}
                </h3>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setIsEquipFormOpen(false)}
                title="Đóng popup"
              >
                ✕
              </button>
            </div>

            {generalError && (
              <div className="mgmt-error-alert" style={{ marginBottom: '16px' }}>
                <span>{generalError}</span>
              </div>
            )}

            <form onSubmit={handleSaveEquipment} className="editor-form-body">
              {/* Asset Code */}
              <div className="form-group">
                <div className="label-row">
                  <label htmlFor="equip-asset-code" className="input-label required-label">
                    Mã tài sản (Asset Code)
                  </label>
                  <span className="form-hint-inline">Mã duy nhất</span>
                </div>
                <input
                  id="equip-asset-code"
                  type="text"
                  maxLength={30}
                  className={`mgmt-input ${inlineAssetError ? 'input-error' : ''}`}
                  placeholder="Ví dụ: TB-000120"
                  value={assetCode}
                  onChange={(e) => {
                    setAssetCode(e.target.value);
                    if (inlineAssetError) setInlineAssetError(null);
                  }}
                  required
                />
                {inlineAssetError && (
                  <p className="inline-error-text">{inlineAssetError}</p>
                )}
              </div>

              {/* Danh mục */}
              <div className="form-group">
                <label htmlFor="equip-catalog-select" className="input-label required-label">
                  Danh mục loại thiết bị
                </label>
                <select
                  id="equip-catalog-select"
                  className="mgmt-select"
                  value={selectedCategory}
                  onChange={(e) => {
                    setSelectedCategory(e.target.value);
                    setEquipName(e.target.value);
                  }}
                  required
                >
                  {catalogList.map((cat) => (
                    <option key={cat.id} value={cat.name}>
                      {cat.name} ({cat.category})
                    </option>
                  ))}
                </select>
              </div>

              {/* Tên thiết bị */}
              <div className="form-group">
                <div className="label-row">
                  <label htmlFor="equip-name-input" className="input-label required-label">
                    Tên thiết bị
                  </label>
                  <span className="char-count">{equipName.length}/100</span>
                </div>
                <input
                  id="equip-name-input"
                  type="text"
                  maxLength={100}
                  className="mgmt-input"
                  placeholder="Ví dụ: Quạt treo tường Mitsubishi..."
                  value={equipName}
                  onChange={(e) => setEquipName(e.target.value)}
                  required
                />
              </div>

              {/* Gán vào Phòng / Giường */}
              <div className="form-group">
                <label htmlFor="equip-location-select" className="input-label required-label">
                  Vị trí phân bổ (Phòng / Giường)
                </label>
                <select
                  id="equip-location-select"
                  className="mgmt-select"
                  value={selectedLocation}
                  onChange={(e) => setSelectedLocation(e.target.value)}
                  required
                >
                  {AVAILABLE_LOCATIONS.map((loc) => (
                    <option key={loc} value={loc}>
                      {loc}
                    </option>
                  ))}
                </select>
              </div>

              {/* Tình trạng */}
              <div className="form-group">
                <label htmlFor="equip-condition-select" className="input-label required-label">
                  Tình trạng hoạt động
                </label>
                <select
                  id="equip-condition-select"
                  className="mgmt-select"
                  value={equipCondition}
                  onChange={(e) => setEquipCondition(e.target.value as any)}
                  required
                >
                  <option value="Good">Tốt</option>
                  <option value="Under repair">Đang sửa chữa</option>
                  <option value="Broken">Hỏng</option>
                </select>
              </div>

              {/* Action Buttons: Lưu / Hủy */}
              <div className="editor-actions-row" style={{ marginTop: '24px' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setIsEquipFormOpen(false)}
                >
                  Hủy
                </button>
                <button type="submit" className="btn-primary">
                  Lưu thiết bị
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Catalog Item Modal */}
      {isCatalogModalOpen && (
        <div className="modal-backdrop" onClick={() => setIsCatalogModalOpen(false)}>
          <div className="modal-card modal-compact" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <div className="modal-title-wrap">
                <h3 className="modal-title">Thêm vào Danh mục thiết bị</h3>
                <p className="modal-subtitle">Tạo loại tài sản chuẩn mới cho hệ thống KTX</p>
              </div>
              <button
                type="button"
                className="modal-close-btn"
                onClick={() => setIsCatalogModalOpen(false)}
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleCreateCatalogItem} className="modal-form-content">
              <div className="form-group">
                <label htmlFor="cat-group-input" className="input-label required-label">
                  Nhóm danh mục
                </label>
                <input
                  id="cat-group-input"
                  type="text"
                  placeholder="Ví dụ: Thiết bị mạng, Thiết bị PCCC..."
                  className="mgmt-input"
                  value={catalogCategory}
                  onChange={(e) => setCatalogCategory(e.target.value)}
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="cat-name-input" className="input-label required-label">
                  Tên thiết bị mẫu
                </label>
                <input
                  id="cat-name-input"
                  type="text"
                  placeholder="Ví dụ: Router Wifi 6, Bình chữa cháy mini..."
                  className="mgmt-input"
                  value={catalogName}
                  onChange={(e) => setCatalogName(e.target.value)}
                  required
                />
              </div>

              <div className="editor-actions-row" style={{ marginTop: '20px' }}>
                <button
                  type="button"
                  className="btn-secondary"
                  onClick={() => setIsCatalogModalOpen(false)}
                >
                  Hủy
                </button>
                <button type="submit" className="btn-primary">
                  Thêm vào danh mục
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
