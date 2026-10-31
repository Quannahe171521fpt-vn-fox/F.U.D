import React, { useState, useEffect } from 'react';
import type { TermLookupItem, PricingItem } from '../../types/management';
import { pricingApi } from '../../services/api';

// Nhóm items theo roomTypeId
interface RoomTypeGroup {
  roomTypeId: number;
  roomTypeName: string;
  bedCapacity: number;
  unit: string;
  previousSemesterPrice: number | null; // giá nhỏ nhất của nhóm kỳ trước
  currentPrice: number;                  // giá nhỏ nhất của nhóm hiện tại
  items: PricingItem[];                  // tất cả items thuộc nhóm (nhiều Dom)
}

export const PricingManagement: React.FC = () => {
  // State: Terms and selection
  const [terms, setTerms] = useState<TermLookupItem[]>([]);
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);

  // State: Current Pricing Data & Form values
  const [termStatus, setTermStatus] = useState<string>('Chưa bắt đầu');
  const [isRegistrationStarted, setIsRegistrationStarted] = useState<boolean>(false);
  const [priceItems, setPriceItems] = useState<PricingItem[]>([]);
  // editedPrices: roomTypeId -> giá đang nhập
  const [editedPrices, setEditedPrices] = useState<Record<number, number | string>>({});
  const [errors, setErrors] = useState<Record<number, string>>({});

  // Loading & notification states
  const [loading, setLoading] = useState<boolean>(false);
  const [saving, setSaving] = useState<boolean>(false);
  const [successToast, setSuccessToast] = useState<string | null>(null);

  // Dialog Modals
  const [showWarningModal, setShowWarningModal] = useState<boolean>(false);
  const [showDiscardModal, setShowDiscardModal] = useState<boolean>(false);
  const [pendingTermChange, setPendingTermChange] = useState<number | null>(null);

  // 1. Initial Load: Terms
  useEffect(() => {
    loadTerms();
  }, []);

  const loadTerms = async () => {
    try {
      setLoading(true);
      const data = await pricingApi.getTerms();
      setTerms(data);
      if (data && data.length > 0) {
        setSelectedTermId(data[0].id);
      }
    } catch (err: any) {
      console.error(err);
      alert(err.message || 'Lỗi khi tải danh sách học kỳ');
    } finally {
      setLoading(false);
    }
  };

  // 2. Load Pricing when selectedTermId changes
  useEffect(() => {
    if (selectedTermId) {
      loadPricingData(selectedTermId);
    }
  }, [selectedTermId]);

  const loadPricingData = async (termId: number) => {
    try {
      setLoading(true);
      setSuccessToast(null);
      setErrors({});
      const data = await pricingApi.getPricingByTerm(termId);

      setTermStatus(data.registrationStatus || 'Chưa bắt đầu');
      setIsRegistrationStarted(data.isRegistrationStarted || false);
      setPriceItems(data.items || []);

      // Build editedPrices: roomTypeId -> giá nhỏ nhất hiện tại của nhóm
      const initialMap: Record<number, number | string> = {};
      const groups = groupByRoomType(data.items || []);
      groups.forEach((g) => {
        initialMap[g.roomTypeId] = g.currentPrice;
      });
      setEditedPrices(initialMap);
    } catch (err: any) {
      console.error(err);
      alert(err.message || 'Lỗi khi tải bảng giá');
    } finally {
      setLoading(false);
    }
  };

  // Group items theo roomTypeId
  const groupByRoomType = (items: PricingItem[]): RoomTypeGroup[] => {
    const map = new Map<number, RoomTypeGroup>();
    items.forEach((item) => {
      if (!map.has(item.roomTypeId)) {
        map.set(item.roomTypeId, {
          roomTypeId: item.roomTypeId,
          roomTypeName: item.roomTypeName,
          bedCapacity: item.bedCapacity,
          unit: item.unit,
          previousSemesterPrice: item.previousSemesterPrice,
          currentPrice: item.currentPrice,
          items: [],
        });
      }
      const g = map.get(item.roomTypeId)!;
      g.items.push(item);
      // Lấy giá nhỏ nhất trong nhóm làm giá hiển thị
      if (item.currentPrice < g.currentPrice) {
        g.currentPrice = item.currentPrice;
      }
      if (
        item.previousSemesterPrice !== null &&
        (g.previousSemesterPrice === null || item.previousSemesterPrice < g.previousSemesterPrice)
      ) {
        g.previousSemesterPrice = item.previousSemesterPrice;
      }
    });
    // Sắp xếp: 4 giường trước, 6 giường sau
    return Array.from(map.values()).sort((a, b) => a.bedCapacity - b.bedCapacity);
  };

  const roomTypeGroups = groupByRoomType(priceItems);

  // Check whether form has unsaved modifications
  const hasUnsavedChanges = (): boolean => {
    return roomTypeGroups.some((g) => {
      const currentVal = Number(editedPrices[g.roomTypeId]);
      return currentVal !== g.currentPrice;
    });
  };

  // Validate inputs
  const validate = (): boolean => {
    const newErrors: Record<number, string> = {};
    let isValid = true;

    roomTypeGroups.forEach((g) => {
      const val = editedPrices[g.roomTypeId];
      const num = Number(val);
      if (val === '' || val === null || val === undefined || isNaN(num) || num <= 0 || !Number.isInteger(num)) {
        newErrors[g.roomTypeId] = 'Giá không hợp lệ (phải là số nguyên > 0)';
        isValid = false;
      }
    });

    setErrors(newErrors);
    return isValid;
  };

  // Handle Input Changes (by roomTypeId)
  const handlePriceChange = (roomTypeId: number, value: string) => {
    setEditedPrices((prev) => ({
      ...prev,
      [roomTypeId]: value === '' ? '' : Number(value),
    }));

    if (errors[roomTypeId]) {
      const num = Number(value);
      if (value !== '' && !isNaN(num) && num > 0) {
        setErrors((prev) => {
          const next = { ...prev };
          delete next[roomTypeId];
          return next;
        });
      }
    }
  };

  // Handle Save
  const handleSaveClick = async () => {
    if (!validate()) return;
    if (isRegistrationStarted) {
      setShowWarningModal(true);
      return;
    }
    await executeSave(false);
  };

  const executeSave = async (confirmApplyNewOnly: boolean) => {
    if (!selectedTermId) return;

    try {
      setSaving(true);
      // Expand: mỗi item trong group dùng chung 1 giá (giá của roomType)
      const itemsPayload = priceItems.map((item) => ({
        dormId: item.dormId,
        roomTypeId: item.roomTypeId,
        currentPrice: Number(editedPrices[item.roomTypeId]),
      }));

      const updated = await pricingApi.savePricing({
        termId: selectedTermId,
        items: itemsPayload,
        confirmApplyToNewBookingsOnly: confirmApplyNewOnly,
      });

      setShowWarningModal(false);
      setSuccessToast('Đã lưu bảng giá thành công!');
      setTimeout(() => setSuccessToast(null), 4000);

      // Refresh state
      setPriceItems(updated.items || []);
      const newMap: Record<number, number | string> = {};
      const updatedGroups = groupByRoomType(updated.items || []);
      updatedGroups.forEach((g) => {
        newMap[g.roomTypeId] = g.currentPrice;
      });
      setEditedPrices(newMap);
      setErrors({});
    } catch (err: any) {
      if (err.status === 409) {
        setShowWarningModal(true);
      } else {
        alert(err.message || 'Lỗi khi lưu bảng giá');
      }
    } finally {
      setSaving(false);
    }
  };

  // Handle Cancel / Discard Changes
  const handleCancelClick = () => {
    if (hasUnsavedChanges()) {
      setShowDiscardModal(true);
    } else {
      if (selectedTermId) loadPricingData(selectedTermId);
    }
  };

  const confirmDiscard = () => {
    setShowDiscardModal(false);
    if (pendingTermChange !== null) {
      setSelectedTermId(pendingTermChange);
      setPendingTermChange(null);
    } else if (selectedTermId) {
      loadPricingData(selectedTermId);
    }
  };

  const handleSelectTerm = (newTermId: number) => {
    if (newTermId === selectedTermId) return;
    if (hasUnsavedChanges()) {
      setPendingTermChange(newTermId);
      setShowDiscardModal(true);
    } else {
      setSelectedTermId(newTermId);
    }
  };

  const formatVND = (amount: number | null | undefined): string => {
    if (amount === null || amount === undefined || isNaN(amount as number)) return '—';
    return (amount as number).toLocaleString('vi-VN') + ' đ';
  };

  return (
    <div className="pricing-management-container" style={{ padding: '1.5rem', maxWidth: '900px', margin: '0 auto' }}>
      {/* 1. Header Section */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
        <div>
          <h1 style={{ fontSize: '1.75rem', fontWeight: 700, color: '#1e293b', margin: 0 }}>
            Thiết lập bảng giá phòng (UC12 - Set Pricing)
          </h1>
          <p style={{ color: '#64748b', fontSize: '0.925rem', marginTop: '0.35rem' }}>
            Cập nhật đơn giá theo loại phòng cho từng học kỳ. Giá áp dụng đồng nhất cho tất cả các tòa nhà.
          </p>
        </div>
      </div>

      {/* 2. Success Toast Alert */}
      {successToast && (
        <div
          style={{
            padding: '1rem 1.25rem',
            background: '#ecfdf5',
            border: '1px solid #10b981',
            borderRadius: '8px',
            color: '#065f46',
            marginBottom: '1.25rem',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '1.2rem' }}>✓</span>
            <span style={{ fontWeight: 600 }}>{successToast}</span>
          </div>
          <button
            onClick={() => setSuccessToast(null)}
            style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#065f46', fontWeight: 700 }}
          >
            ✕
          </button>
        </div>
      )}

      {/* 3. Semester Selection Bar */}
      <div
        style={{
          background: '#ffffff',
          border: '1px solid #e2e8f0',
          borderRadius: '10px',
          padding: '1.25rem',
          marginBottom: '1.5rem',
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: '1rem',
          boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <label htmlFor="term-select" style={{ fontWeight: 600, color: '#334155', fontSize: '0.95rem' }}>
            Chọn học kỳ:
          </label>
          <select
            id="term-select"
            value={selectedTermId || ''}
            onChange={(e) => handleSelectTerm(Number(e.target.value))}
            style={{
              padding: '0.5rem 1rem',
              borderRadius: '6px',
              border: '1px solid #cbd5e1',
              fontSize: '0.95rem',
              background: '#f8fafc',
              fontWeight: 500,
              cursor: 'pointer',
              minWidth: '220px',
            }}
          >
            {terms.map((t) => (
              <option key={t.id} value={t.id}>
                {t.displayName}
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.9rem', color: '#64748b', fontWeight: 500 }}>Trạng thái kỳ:</span>
          <span
            style={{
              display: 'inline-block',
              padding: '0.35rem 0.85rem',
              borderRadius: '20px',
              fontSize: '0.85rem',
              fontWeight: 600,
              background:
                termStatus === 'Đang mở đăng ký'
                  ? '#fef3c7'
                  : termStatus === 'Đã kết thúc'
                  ? '#fee2e2'
                  : '#e0f2fe',
              color:
                termStatus === 'Đang mở đăng ký'
                  ? '#b45309'
                  : termStatus === 'Đã kết thúc'
                  ? '#b91c1c'
                  : '#0369a1',
              border: `1px solid ${
                termStatus === 'Đang mở đăng ký'
                  ? '#fde68a'
                  : termStatus === 'Đã kết thúc'
                  ? '#fecaca'
                  : '#bae6fd'
              }`,
            }}
          >
            {termStatus}
          </span>
        </div>
      </div>

      {/* 4. Price Table — gom theo loại phòng */}
      <div
        style={{
          background: '#ffffff',
          borderRadius: '10px',
          border: '1px solid #e2e8f0',
          boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
          overflow: 'hidden',
          marginBottom: '1.5rem',
        }}
      >
        <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e2e8f0', background: '#f8fafc' }}>
          <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600, color: '#1e293b' }}>
            Bảng giá theo loại phòng
          </h3>
          <span style={{ fontSize: '0.85rem', color: '#64748b' }}>
            Giá áp dụng đồng nhất cho tất cả tòa nhà. Đơn vị: VNĐ / học kỳ.
          </span>
        </div>

        {loading ? (
          <div style={{ padding: '3rem', textAlign: 'center', color: '#64748b' }}>
            <p>Đang tải thông tin bảng giá...</p>
          </div>
        ) : roomTypeGroups.length === 0 ? (
          <div style={{ padding: '3rem', textAlign: 'center', color: '#94a3b8' }}>
            <p>Chưa có dữ liệu loại phòng cho học kỳ này.</p>
          </div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f1f5f9', borderBottom: '1px solid #cbd5e1' }}>
                  <th style={{ padding: '0.85rem 1.25rem', fontSize: '0.85rem', fontWeight: 600, color: '#475569' }}>
                    Loại phòng
                  </th>
                  <th style={{ padding: '0.85rem 1rem', fontSize: '0.85rem', fontWeight: 600, color: '#475569' }}>
                    Sức chứa
                  </th>
                  <th style={{ padding: '0.85rem 1rem', fontSize: '0.85rem', fontWeight: 600, color: '#475569' }}>
                    Giá học kỳ trước
                  </th>
                  <th style={{ padding: '0.85rem 1rem', fontSize: '0.85rem', fontWeight: 600, color: '#475569', width: '280px' }}>
                    Giá học kỳ này (VNĐ) <span style={{ color: '#ef4444' }}>*</span>
                  </th>
                  <th style={{ padding: '0.85rem 1rem', fontSize: '0.85rem', fontWeight: 600, color: '#475569' }}>
                    Đơn vị
                  </th>
                </tr>
              </thead>
              <tbody>
                {roomTypeGroups.map((group) => {
                  const hasError = Boolean(errors[group.roomTypeId]);
                  return (
                    <tr
                      key={group.roomTypeId}
                      style={{
                        borderBottom: '1px solid #e2e8f0',
                        background: hasError ? '#fef2f2' : 'transparent',
                        transition: 'background 0.2s',
                      }}
                    >
                      <td style={{ padding: '1rem 1.25rem', fontWeight: 700, color: '#1e293b', fontSize: '1rem' }}>
                        {group.roomTypeName}
                      </td>
                      <td style={{ padding: '1rem', color: '#475569' }}>
                        {group.bedCapacity} người / phòng
                      </td>
                      <td style={{ padding: '1rem', color: '#64748b', fontStyle: group.previousSemesterPrice ? 'normal' : 'italic' }}>
                        {formatVND(group.previousSemesterPrice)}
                      </td>
                      <td style={{ padding: '1rem' }}>
                        <input
                          type="number"
                          min="1"
                          step="100000"
                          value={editedPrices[group.roomTypeId] ?? ''}
                          onChange={(e) => handlePriceChange(group.roomTypeId, e.target.value)}
                          placeholder="Nhập đơn giá..."
                          style={{
                            width: '100%',
                            padding: '0.55rem 0.85rem',
                            borderRadius: '6px',
                            border: `1px solid ${hasError ? '#ef4444' : '#cbd5e1'}`,
                            background: hasError ? '#fff5f5' : '#ffffff',
                            color: '#1e293b',
                            fontWeight: 600,
                            fontSize: '0.95rem',
                            outline: 'none',
                          }}
                        />
                        {hasError && (
                          <div style={{ color: '#dc2626', fontSize: '0.8rem', marginTop: '0.35rem', fontWeight: 500 }}>
                            {errors[group.roomTypeId]}
                          </div>
                        )}
                      </td>
                      <td style={{ padding: '1rem', color: '#64748b', fontSize: '0.9rem' }}>
                        {group.unit}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* 5. Action Buttons */}
        <div
          style={{
            padding: '1.25rem',
            background: '#f8fafc',
            borderTop: '1px solid #e2e8f0',
            display: 'flex',
            justifyContent: 'flex-end',
            gap: '1rem',
          }}
        >
          <button
            type="button"
            onClick={handleCancelClick}
            disabled={saving || loading}
            style={{
              padding: '0.65rem 1.5rem',
              borderRadius: '6px',
              border: '1px solid #cbd5e1',
              background: '#ffffff',
              color: '#475569',
              fontWeight: 600,
              fontSize: '0.95rem',
              cursor: 'pointer',
            }}
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleSaveClick}
            disabled={saving || loading}
            style={{
              padding: '0.65rem 1.75rem',
              borderRadius: '6px',
              border: 'none',
              background: '#2563eb',
              color: '#ffffff',
              fontWeight: 600,
              fontSize: '0.95rem',
              cursor: saving ? 'not-allowed' : 'pointer',
              boxShadow: '0 2px 4px rgba(37, 99, 235, 0.25)',
              display: 'flex',
              alignItems: 'center',
              gap: '6px',
            }}
          >
            {saving ? 'Đang lưu...' : 'Lưu bảng giá'}
          </button>
        </div>
      </div>

      {/* Note */}
      <div
        style={{
          padding: '0.85rem 1.1rem',
          background: '#f0f9ff',
          border: '1px solid #bae6fd',
          borderRadius: '8px',
          color: '#0369a1',
          fontSize: '0.875rem',
          lineHeight: '1.5',
        }}
      >
        <strong>Lưu ý:</strong> Giá được nhập sẽ áp dụng đồng nhất cho tất cả các tòa nhà (Dom A đến Dom K)
        có cùng loại phòng. Giá mới sẽ đồng bộ ngay lên trang chủ sau khi lưu.
      </div>

      {/* ── Warning Dialog: Registration already started ── */}
      {showWarningModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15, 23, 42, 0.65)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            backdropFilter: 'blur(3px)',
          }}
        >
          <div
            style={{
              background: '#ffffff',
              borderRadius: '12px',
              width: '100%',
              maxWidth: '520px',
              padding: '1.75rem',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '1rem' }}>
              <div
                style={{
                  width: '40px',
                  height: '40px',
                  borderRadius: '50%',
                  background: '#fef3c7',
                  color: '#d97706',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: '1.4rem',
                  fontWeight: 700,
                }}
              >
                !
              </div>
              <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 700, color: '#1e293b' }}>
                Đã bắt đầu đăng ký
              </h3>
            </div>

            <p style={{ color: '#475569', fontSize: '0.95rem', lineHeight: '1.5', margin: '0 0 1.5rem 0' }}>
              Đợt đăng ký phòng cho học kỳ này <strong>đã bắt đầu</strong>. Giá mới sẽ{' '}
              <strong>chỉ áp dụng cho các đặt phòng mới</strong>, các đặt phòng cũ vẫn giữ nguyên mức giá ban đầu.
            </p>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                type="button"
                onClick={() => setShowWarningModal(false)}
                disabled={saving}
                style={{
                  padding: '0.6rem 1.25rem',
                  borderRadius: '6px',
                  border: '1px solid #cbd5e1',
                  background: '#ffffff',
                  color: '#475569',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                  cursor: 'pointer',
                }}
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={() => executeSave(true)}
                disabled={saving}
                style={{
                  padding: '0.6rem 1.25rem',
                  borderRadius: '6px',
                  border: 'none',
                  background: '#d97706',
                  color: '#ffffff',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                  cursor: saving ? 'not-allowed' : 'pointer',
                }}
              >
                {saving ? 'Đang lưu...' : 'Xác nhận – chỉ áp dụng cho đặt phòng mới'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Discard Changes Confirmation Dialog ── */}
      {showDiscardModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(15, 23, 42, 0.65)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 1000,
            backdropFilter: 'blur(3px)',
          }}
        >
          <div
            style={{
              background: '#ffffff',
              borderRadius: '12px',
              width: '100%',
              maxWidth: '460px',
              padding: '1.75rem',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <h3 style={{ margin: '0 0 0.75rem 0', fontSize: '1.2rem', fontWeight: 700, color: '#1e293b' }}>
              Hủy các thay đổi chưa lưu?
            </h3>
            <p style={{ color: '#475569', fontSize: '0.95rem', lineHeight: '1.5', margin: '0 0 1.5rem 0' }}>
              Bạn có các thay đổi về giá chưa được lưu. Nếu hủy bỏ, những thông tin vừa sửa đổi sẽ bị mất và trở
              về dữ liệu ban đầu.
            </p>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
              <button
                type="button"
                onClick={() => {
                  setShowDiscardModal(false);
                  setPendingTermChange(null);
                }}
                style={{
                  padding: '0.6rem 1.25rem',
                  borderRadius: '6px',
                  border: '1px solid #cbd5e1',
                  background: '#ffffff',
                  color: '#475569',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                  cursor: 'pointer',
                }}
              >
                Tiếp tục chỉnh sửa
              </button>
              <button
                type="button"
                onClick={confirmDiscard}
                style={{
                  padding: '0.6rem 1.25rem',
                  borderRadius: '6px',
                  border: 'none',
                  background: '#ef4444',
                  color: '#ffffff',
                  fontWeight: 600,
                  fontSize: '0.9rem',
                  cursor: 'pointer',
                }}
              >
                Xác nhận hủy
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
