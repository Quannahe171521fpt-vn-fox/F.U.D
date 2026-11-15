import React, { useState, useEffect, useRef, useCallback } from 'react';
import '../../styles/facilities.css';
import {
  ewApi,
  type BillingPeriodLookupDto,
  type ExcelPreviewResponseDto,
  type ImportBatchItemDto,
  type EwUsageRow,
  type EwReadingDetailItem,
} from '../../services/api';

// ── Quota constants ─────────────────────────────────────
const MONTHLY_ELECTRIC_QUOTA = 50;
const MONTHLY_WATER_QUOTA = 3;

function meterTypeLabel(t: string): string {
  if (!t) return t;
  if (t.toLowerCase().includes('electric')) return 'Điện';
  if (t.toLowerCase().includes('water')) return 'Nước';
  return t;
}

// ── Usage table row (computed) ──────────────────────────
interface UsageTableRow {
  id: number;
  block: string;
  blockName: string;
  blockCode: string;
  roomNumber: string;
  monthYear: string;         // "11 - 2026"
  recordDate: string;        // "01/11/2026"
  type: string;              // "Electricity" | "Water"
  typeLabel: string;         // "Điện" | "Nước"
  usage: number;
  overUsage: number;
  quota: number;
  readings: EwReadingDetailItem[];
}

interface MonthUsageSubgroup {
  monthYear: string;
  recordDate: string;
  rows: UsageTableRow[];
}

interface BlockUsageGroup {
  block: string;
  blockName: string;
  blockCode: string;
  roomNumber: string;
  monthYear: string;
  monthSubgroups: MonthUsageSubgroup[];
  rows: UsageTableRow[];
}

function buildUsageRows(rawRows: EwUsageRow[]): UsageTableRow[] {
  return rawRows.map((r) => {
    let month = r.targetMonth || 0;
    let year = r.targetYear || 0;
    if ((!month || !year) && r.ngayTao) {
      const d = new Date(r.ngayTao);
      month = d.getMonth() + 1;
      year = d.getFullYear();
    }

    const quota =
      r.loaiSuDung.toLowerCase().includes('electric')
        ? MONTHLY_ELECTRIC_QUOTA
        : MONTHLY_WATER_QUOTA;

    // Tên block/phòng: ưu tiên block -> tenBlock -> blockCode
    let blockDisplayName = r.block || r.tenBlock || r.blockCode || '—';
    if (r.roomNumber) {
      const duplicateSuffix = `-${r.roomNumber}-${r.roomNumber}`;
      if (blockDisplayName.endsWith(duplicateSuffix)) {
        blockDisplayName = blockDisplayName.slice(0, -r.roomNumber.length - 1);
      }
    }

    return {
      id: r.id,
      block: blockDisplayName,
      blockName: r.blockName || '',
      blockCode: r.blockCode || '',
      roomNumber: r.roomNumber || '',
      monthYear: month && year ? `${month} - ${year}` : r.hocKy || '—',
      recordDate: r.ngayTao ? new Date(r.ngayTao).toLocaleDateString('vi-VN') : '—',
      type: r.loaiSuDung,
      typeLabel: meterTypeLabel(r.loaiSuDung),
      usage: r.totalConsumption,
      overUsage: r.totalConsumption - quota,
      quota,
      readings: r.readings || [],
    };
  });
}

// ── Component ───────────────────────────────────────────
export const EwManagement: React.FC = () => {
  const [activeSection, setActiveSection] = useState<'import' | 'usage' | 'history'>('usage');

  // ── Billing Periods ──
  const [periods, setPeriods] = useState<BillingPeriodLookupDto[]>([]);
  const [selectedPeriodId, setSelectedPeriodId] = useState<number | ''>('');
  const [periodsLoading, setPeriodsLoading] = useState(false);

  // ── Import ──
  const [importFile, setImportFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<ExcelPreviewResponseDto | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [importLoading, setImportLoading] = useState(false);
  const [importMsg, setImportMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  // ── Usage view ──
  const [usageRows, setUsageRows] = useState<UsageTableRow[]>([]);
  const [usageLoading, setUsageLoading] = useState(false);
  const [usageError, setUsageError] = useState<string | null>(null);
  const [usageFilter, setUsageFilter] = useState({
    termId: '',
    billingPeriodId: '',
    blockCode: '',
    meterType: '',
    month: '',
    year: '',
  });

  // ── Modal Detail ──
  const [detailGroup, setDetailGroup] = useState<BlockUsageGroup | null>(null);

  // ── Inline Edit Reading in Modal ──
  const [editingReadingId, setEditingReadingId] = useState<number | null>(null);
  const [editReadingValue, setEditReadingValue] = useState<string>('');
  const [editReadingNote, setEditReadingNote] = useState<string>('');
  const [editReadingLoading, setEditReadingLoading] = useState(false);
  const [editReadingMsg, setEditReadingMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const handleStartEdit = (m: EwReadingDetailItem) => {
    setEditingReadingId(m.readingId);
    setEditReadingValue(m.currentValue.toString());
    setEditReadingNote(m.note || '');
    setEditReadingMsg(null);
  };

  const handleCancelEdit = () => {
    setEditingReadingId(null);
    setEditReadingValue('');
    setEditReadingNote('');
    setEditReadingMsg(null);
  };

  const handleSaveEdit = async (m: EwReadingDetailItem) => {
    const val = parseFloat(editReadingValue);
    if (isNaN(val) || val < 0) {
      setEditReadingMsg({ type: 'error', text: 'Chỉ số phải là số hợp lệ không âm' });
      return;
    }
    setEditReadingLoading(true);
    setEditReadingMsg(null);
    try {
      await ewApi.updateReading(m.readingId, val, editReadingNote);
      setEditReadingMsg({ type: 'success', text: 'Đã cập nhật chỉ số thành công!' });
      // Update local state in detailGroup
      setDetailGroup((prev) => {
        if (!prev) return null;
        const newRows = prev.rows.map((r) => {
          const newReadings = (r.readings || []).map((rd) => {
            if (rd.readingId === m.readingId) {
              const newConsumption = val - rd.previousValue;
              const newStatus = val < rd.previousValue ? 'Flagged' : 'Recorded';
              return {
                ...rd,
                currentValue: val,
                consumption: newConsumption,
                status: newStatus,
                note: editReadingNote,
              };
            }
            return rd;
          });
          const newTotal = newReadings.reduce((s, x) => s + x.consumption, 0);
          return {
            ...r,
            readings: newReadings,
            usage: newTotal,
            overUsage: newTotal - r.quota,
          };
        });
        return {
          ...prev,
          rows: newRows,
        };
      });
      setEditingReadingId(null);
      // Reload main table
      loadUsage();
    } catch (err: any) {
      setEditReadingMsg({ type: 'error', text: err.message || 'Lỗi khi cập nhật chỉ số' });
    } finally {
      setEditReadingLoading(false);
    }
  };

  // ── History ──
  const [batches, setBatches] = useState<ImportBatchItemDto[]>([]);
  const [batchLoading, setBatchLoading] = useState(false);

  // ── Load billing periods on mount ──
  useEffect(() => {
    setPeriodsLoading(true);
    ewApi
      .getBillingPeriods()
      .then((data) => {
        setPeriods(data);
        const open = data.find((p) => p.status === 'Open');
        if (open) setSelectedPeriodId(open.id);
        else if (data.length) setSelectedPeriodId(data[0].id);
      })
      .catch(() => { })
      .finally(() => setPeriodsLoading(false));
  }, []);

  const selectedPeriod = periods.find((p) => p.id === selectedPeriodId);
  const isClosed = selectedPeriod?.status !== 'Open';

  // ── Terms extracted from Billing Periods ──
  const availableTerms = React.useMemo(() => {
    const map = new Map<number, string>();
    for (const p of periods) {
      if (p.termId && p.termName) {
        map.set(p.termId, p.termName);
      }
    }
    return Array.from(map.entries()).map(([id, name]) => ({ id, name }));
  }, [periods]);

  const filteredPeriodsForUsage = React.useMemo(() => {
    if (!usageFilter.termId) return periods;
    return periods.filter((p) => p.termId === Number(usageFilter.termId));
  }, [periods, usageFilter.termId]);

  const isEntireTermView = Boolean(usageFilter.termId && !usageFilter.billingPeriodId);

  // ── Auto Preview & File change ──
  const triggerPreview = async (file: File, periodId: number) => {
    setPreviewLoading(true);
    setImportMsg(null);
    try {
      const data = await ewApi.previewImport(file, periodId);
      setPreview(data);
    } catch (err: any) {
      setImportMsg({ type: 'error', text: err.message || 'Lỗi khi đọc file Excel' });
      setPreview(null);
    } finally {
      setPreviewLoading(false);
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] || null;
    setImportFile(file);
    setPreview(null);
    setImportMsg(null);
    if (file) {
      if (!selectedPeriodId) {
        setImportMsg({ type: 'error', text: 'Vui lòng chọn kỳ thanh toán trước khi chọn file Excel' });
        return;
      }
      await triggerPreview(file, selectedPeriodId as number);
    }
  };

  const handleUpdateRowValue = (rowIndex: number, side: 'left' | 'right', valStr: string) => {
    if (!preview) return;
    const newRows = [...preview.rows];
    const row = { ...newRows[rowIndex] };
    const numVal = valStr.trim() === '' ? undefined : parseFloat(valStr);

    if (side === 'left') {
      row.leftValue = numVal;
      if (numVal !== undefined && !isNaN(numVal)) {
        row.leftConsumption = numVal - row.leftOldReading;
      } else {
        row.leftConsumption = 0;
      }
    } else {
      row.rightValue = numVal;
      if (numVal !== undefined && !isNaN(numVal)) {
        row.rightConsumption = numVal - row.rightOldReading;
      } else {
        row.rightConsumption = 0;
      }
    }

    // Re-evaluate validity
    const leftBelow = row.leftValue !== undefined && row.leftValue < row.leftOldReading;
    const rightBelow = row.rightValue !== undefined && row.rightValue < row.rightOldReading;
    const leftZero = row.leftValue !== undefined && row.leftValue === row.leftOldReading;
    const rightZero = row.rightValue !== undefined && row.rightValue === row.rightOldReading;

    if (leftBelow || rightBelow) {
      row.status = 'Warning';
      row.flagReason = `Chỉ số mới nhỏ hơn chỉ số cũ (${leftBelow ? 'Phòng L' : ''} ${rightBelow ? 'Phòng R' : ''})`;
    } else if (leftZero || rightZero) {
      row.status = 'Warning';
      row.flagReason = 'Mức tiêu thụ bằng 0';
    } else if (row.leftValue === undefined || row.rightValue === undefined) {
      row.status = 'Warning';
      row.flagReason = 'Chưa nhập đủ chỉ số';
    } else {
      row.status = 'Valid';
      row.flagReason = undefined;
      row.errorMessage = undefined;
    }

    newRows[rowIndex] = row;

    const validRows = newRows.filter((r) => r.status === 'Valid').length;
    const warningRows = newRows.filter((r) => r.status === 'Warning').length;
    const errorRows = newRows.filter((r) => r.status === 'Error').length;

    setPreview({
      ...preview,
      rows: newRows,
      validRows,
      warningRows,
      errorRows,
    });
  };

  // ── Preview Import (manual retry if needed) ──
  const handlePreview = async () => {
    if (!importFile || !selectedPeriodId) return;
    await triggerPreview(importFile, selectedPeriodId as number);
  };

  // ── Confirm import ──
  const handleConfirmImport = async () => {
    if (!preview || !selectedPeriodId || !importFile) return;
    setImportLoading(true);
    setImportMsg(null);
    try {
      const result = await ewApi.confirmImport({
        billingPeriodId: selectedPeriodId as number,
        fileName: importFile.name,
        importOnlyValid: true,
        overwriteExisting: true,
        rows: preview.rows,
      });
      setImportMsg({ type: 'success', text: result.message || `Đã lưu ${result.savedReadings} chỉ số thành công!` });
      setPreview(null);
      setImportFile(null);
      if (fileRef.current) fileRef.current.value = '';
      loadUsage();
    } catch (err: any) {
      setImportMsg({ type: 'error', text: err.message });
    } finally {
      setImportLoading(false);
    }
  };

  // ── Load usage ──
  const loadUsage = useCallback(async () => {
    setUsageLoading(true);
    setUsageError(null);
    try {
      const params: Record<string, any> = {};
      if (usageFilter.termId) params.termId = Number(usageFilter.termId);
      if (usageFilter.billingPeriodId) params.billingPeriodId = Number(usageFilter.billingPeriodId);
      if (usageFilter.blockCode) params.blockCode = usageFilter.blockCode;
      if (usageFilter.meterType) params.meterType = usageFilter.meterType;
      if (usageFilter.month) params.month = Number(usageFilter.month);
      if (usageFilter.year) params.year = Number(usageFilter.year);
      const raw = await ewApi.getUsages(params);
      setUsageRows(buildUsageRows(raw));
    } catch (err: any) {
      setUsageError(err.message);
    } finally {
      setUsageLoading(false);
    }
  }, [usageFilter]);

  useEffect(() => {
    if (activeSection === 'usage') {
      loadUsage();
    }
  }, [activeSection, loadUsage]);

  // ── Load batches ──
  const loadBatches = useCallback(async () => {
    setBatchLoading(true);
    try {
      const data = await ewApi.getImportBatches(selectedPeriodId ? (selectedPeriodId as number) : undefined);
      setBatches(data);
    } catch {
      setBatches([]);
    } finally {
      setBatchLoading(false);
    }
  }, [selectedPeriodId]);

  useEffect(() => {
    if (activeSection === 'history') loadBatches();
  }, [activeSection, loadBatches]);

  // ── Usage table: group by block -> then monthYear ──
  const groupedUsage: BlockUsageGroup[] = React.useMemo(() => {
    const blockMap = new Map<string, UsageTableRow[]>();
    for (const row of usageRows) {
      const list = blockMap.get(row.block) || [];
      list.push(row);
      blockMap.set(row.block, list);
    }

    const groups: BlockUsageGroup[] = [];
    for (const [block, rows] of blockMap.entries()) {
      const monthMap = new Map<string, UsageTableRow[]>();
      for (const r of rows) {
        const mList = monthMap.get(r.monthYear) || [];
        mList.push(r);
        monthMap.set(r.monthYear, mList);
      }

      const monthSubgroups: MonthUsageSubgroup[] = [];
      for (const [mYear, mRows] of monthMap.entries()) {
        monthSubgroups.push({
          monthYear: mYear,
          recordDate: mRows[0]?.recordDate || '—',
          rows: mRows,
        });
      }

      const firstRow = rows[0];
      groups.push({
        block,
        blockName: firstRow?.blockName || '',
        blockCode: firstRow?.blockCode || '',
        roomNumber: firstRow?.roomNumber || '',
        monthYear: monthSubgroups.map((m) => m.monthYear).join(', '),
        monthSubgroups,
        rows,
      });
    }

    return groups;
  }, [usageRows]);

  const totalElectric = usageRows.filter((r) => r.type.toLowerCase().includes('electric'));
  const totalWater = usageRows.filter((r) => r.type.toLowerCase().includes('water'));
  const sumElectricUsage = totalElectric.reduce((s, r) => s + r.usage, 0);
  const sumElectricOver = totalElectric.reduce((s, r) => s + r.overUsage, 0);
  const sumWaterUsage = totalWater.reduce((s, r) => s + r.usage, 0);
  const sumWaterOver = totalWater.reduce((s, r) => s + r.overUsage, 0);

  return (
    <div className="facilities-container" style={{ padding: '24px 32px' }}>
      {/* ── Header ── */}
      <div className="facilities-header">
        <div className="facilities-title-group">
          <span className="facilities-sub">QUẢN LÝ ĐIỆN & NƯỚC</span>
          <h1 className="facilities-title">Quản lý Điện – Nước (EW)</h1>
          <p style={{ margin: '4px 0 0', fontSize: 13, color: '#64748b' }}>
            Theo dõi mức tiêu thụ, đối chiếu định mức và kiểm tra chi tiết công tơ từng phòng
          </p>
        </div>
      </div>

      {/* ── Tab nav ── */}
      <div style={{ display: 'flex', gap: 0, borderBottom: '2px solid #E2E8F0', marginBottom: 24 }}>
        {([
          { key: 'usage', label: 'Bảng theo dõi sử dụng' },
          { key: 'import', label: 'Import chỉ số Excel' },
          { key: 'history', label: 'Lịch sử import' },
        ] as const).map((tab) => (
          <button
            key={tab.key}
            onClick={() => setActiveSection(tab.key)}
            style={{
              padding: '10px 20px',
              border: 'none',
              borderBottom: activeSection === tab.key ? '2px solid #D97026' : '2px solid transparent',
              marginBottom: -2,
              background: 'none',
              color: activeSection === tab.key ? '#D97026' : '#64748b',
              fontWeight: activeSection === tab.key ? 700 : 500,
              fontSize: 14,
              cursor: 'pointer',
            }}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* ════════════════════════════════════════════════
          SECTION: BẢNG SỬ DỤNG
          ════════════════════════════════════════════════ */}
      {activeSection === 'usage' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          {/* Filters */}
          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              padding: '16px 20px',
              display: 'flex',
              gap: 14,
              flexWrap: 'wrap',
              alignItems: 'flex-end',
            }}
          >
            {/* 1. Học kỳ */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>Học kỳ</label>
              <select
                value={usageFilter.termId}
                onChange={(e) => {
                  const newTermId = e.target.value;
                  setUsageFilter((prev) => ({
                    ...prev,
                    termId: newTermId,
                    billingPeriodId: '',
                  }));
                }}
                style={{
                  padding: '8px 12px',
                  border: '1px solid #CBD5E1',
                  borderRadius: 8,
                  fontSize: 13,
                  minWidth: 170,
                  background: '#fff',
                }}
              >
                <option value="">Tất cả học kỳ</option>
                {availableTerms.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
            </div>

            {/* 2. Kỳ thanh toán / Tháng trong kỳ */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>Kỳ thanh toán</label>
              <select
                value={usageFilter.billingPeriodId}
                onChange={(e) => {
                  const bpId = e.target.value;
                  setUsageFilter((prev) => ({ ...prev, billingPeriodId: bpId }));
                }}
                style={{
                  padding: '8px 12px',
                  border: '1px solid #CBD5E1',
                  borderRadius: 8,
                  fontSize: 13,
                  minWidth: 200,
                  background: '#fff',
                }}
              >
                <option value="">{usageFilter.termId ? 'Tất cả các tháng trong kỳ' : 'Tất cả các kỳ / tháng'}</option>
                {filteredPeriodsForUsage.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.displayName} {!usageFilter.termId && p.termName ? `(${p.termName})` : ''} {p.status === 'Open' ? '• Đang mở' : '• Đã đóng'}
                  </option>
                ))}
              </select>
            </div>

            {/* 3. Mã Block / Phòng */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>Mã Block / Phòng</label>
              <input
                value={usageFilter.blockCode}
                onChange={(e) => setUsageFilter((prev) => ({ ...prev, blockCode: e.target.value }))}
                placeholder="VD: A1, 101..."
                style={{
                  padding: '8px 12px',
                  border: '1px solid #CBD5E1',
                  borderRadius: 8,
                  fontSize: 13,
                  width: 140,
                }}
              />
            </div>

            {/* 4. Loại đồng hồ */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>Loại đồng hồ</label>
              <select
                value={usageFilter.meterType}
                onChange={(e) => setUsageFilter((prev) => ({ ...prev, meterType: e.target.value }))}
                style={{ padding: '8px 12px', border: '1px solid #CBD5E1', borderRadius: 8, fontSize: 13, minWidth: 150, background: '#fff' }}
              >
                <option value="">Tất cả (Điện & Nước)</option>
                <option value="Electricity">Điện</option>
                <option value="Water">Nước</option>
              </select>
            </div>

            {/* 5. Nút tìm kiếm */}
            <button
              onClick={loadUsage}
              style={{
                padding: '8px 22px',
                borderRadius: 8,
                border: 'none',
                background: '#D97026',
                color: '#fff',
                fontWeight: 600,
                fontSize: 13,
                cursor: 'pointer',
              }}
            >
              {usageLoading ? 'Đang lọc...' : 'Tìm kiếm'}
            </button>

            {/* 6. Xóa bộ lọc */}
            {(usageFilter.termId || usageFilter.billingPeriodId || usageFilter.blockCode || usageFilter.meterType || usageFilter.month || usageFilter.year) && (
              <button
                onClick={() => {
                  setUsageFilter({ termId: '', billingPeriodId: '', blockCode: '', meterType: '', month: '', year: '' });
                }}
                style={{
                  padding: '8px 16px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#F8FAFC',
                  color: '#64748b',
                  fontSize: 13,
                  cursor: 'pointer',
                }}
              >
                Xóa bộ lọc
              </button>
            )}
          </div>

          {/* Usage table */}
          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              overflow: 'hidden',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
            }}
          >
            {usageError ? (
              <div style={{ padding: 24, color: '#DC2626', fontWeight: 600 }}>{usageError}</div>
            ) : usageLoading ? (
              <div style={{ padding: 40, color: '#94a3b8', textAlign: 'center' }}>
                <p style={{ margin: 0, fontSize: 14 }}>Đang tải danh sách chỉ số từ máy chủ...</p>
              </div>
            ) : usageRows.length === 0 ? (
              <div style={{ padding: 40, color: '#94a3b8', textAlign: 'center' }}>
                Không tìm thấy dữ liệu phù hợp. Hãy điều chỉnh bộ lọc hoặc chọn kỳ thanh toán khác.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ background: '#F8FAFC' }}>
                    <th style={{ padding: '12px 16px', textAlign: 'left', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '22%' }}>
                      Block / Cụm phòng
                    </th>
                    <th style={{ padding: '12px 16px', textAlign: 'center', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '14%' }}>
                      Tháng – Năm
                    </th>
                    <th style={{ padding: '12px 16px', textAlign: 'center', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '14%' }}>
                      Ngày ghi
                    </th>
                    <th style={{ padding: '12px 16px', textAlign: 'center', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '12%' }}>
                      Loại
                    </th>
                    <th style={{ padding: '12px 16px', textAlign: 'right', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '14%' }}>
                      Tiêu thụ
                    </th>
                    <th style={{ padding: '12px 16px', textAlign: 'right', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '14%' }}>
                      Vượt định mức
                    </th>
                    <th style={{ padding: '12px 16px', textAlign: 'center', fontWeight: 700, color: '#374151', borderBottom: '2px solid #E2E8F0', width: '10%' }}>
                      Thao tác
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {groupedUsage.map((blockGroup) => {
                    let blockRowCounter = 0;
                    return blockGroup.monthSubgroups.map((monthGroup) => {
                      return monthGroup.rows.map((row, monthRowIndex) => {
                        const isFirstBlockRow = blockRowCounter === 0;
                        blockRowCounter++;
                        const isFirstMonthRow = monthRowIndex === 0;

                        return (
                          <tr
                            key={`${blockGroup.block}-${monthGroup.monthYear}-${row.type}-${monthRowIndex}`}
                            style={{
                              borderBottom: '1px solid #F1F5F9',
                              background: blockRowCounter % 2 === 0 ? '#ffffff' : '#FAFAFA',
                            }}
                          >
                            {/* 1. Block cell — merged across all months of this block */}
                            {isFirstBlockRow && (
                              <td
                                rowSpan={blockGroup.rows.length}
                                style={{
                                  padding: '12px 16px',
                                  verticalAlign: 'middle',
                                  borderRight: '1px solid #F1F5F9',
                                  background: '#fff',
                                }}
                              >
                                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                  <span
                                    style={{
                                      display: 'inline-block',
                                      padding: '4px 10px',
                                      borderRadius: 6,
                                      background: '#EFF6FF',
                                      color: '#1D4ED8',
                                      fontWeight: 700,
                                      fontSize: 13,
                                      cursor: 'pointer',
                                    }}
                                    onClick={() => setDetailGroup(blockGroup)}
                                    title="Bấm để xem chi tiết Block"
                                  >
                                    {blockGroup.block}
                                  </span>
                                </div>
                                {blockGroup.blockName && (
                                  <div style={{ fontSize: 11, color: '#64748B', marginTop: 3 }}>
                                    {blockGroup.blockName}
                                  </div>
                                )}
                              </td>
                            )}

                            {/* 2. Month-Year cell — merged across types (Electricity + Water) */}
                            {isFirstMonthRow && (
                              <td
                                rowSpan={monthGroup.rows.length}
                                style={{
                                  padding: '12px 16px',
                                  textAlign: 'center',
                                  color: '#374151',
                                  fontWeight: 600,
                                  verticalAlign: 'middle',
                                  borderRight: '1px solid #F1F5F9',
                                  background: '#fff',
                                }}
                              >
                                {monthGroup.monthYear}
                              </td>
                            )}

                            {/* 3. Record Date — merged across types of that month */}
                            {isFirstMonthRow && (
                              <td
                                rowSpan={monthGroup.rows.length}
                                style={{
                                  padding: '12px 16px',
                                  textAlign: 'center',
                                  color: '#475569',
                                  verticalAlign: 'middle',
                                  borderRight: '1px solid #F1F5F9',
                                  background: '#fff',
                                }}
                              >
                                {monthGroup.recordDate}
                              </td>
                            )}

                            {/* 4. Meter Type */}
                            <td style={{ padding: '12px 16px', textAlign: 'center' }}>
                              <span
                                style={{
                                  display: 'inline-block',
                                  padding: '2px 10px',
                                  borderRadius: 12,
                                  fontSize: 12,
                                  fontWeight: 600,
                                  background: row.type.toLowerCase().includes('electric') ? '#FFF7ED' : '#EFF6FF',
                                  color: row.type.toLowerCase().includes('electric') ? '#C2410C' : '#1D4ED8',
                                }}
                              >
                                {row.typeLabel}
                              </span>
                            </td>

                            {/* 5. Actual Usage */}
                            <td style={{ padding: '12px 16px', textAlign: 'right', fontWeight: 700, color: '#0F172A' }}>
                              {row.usage.toFixed(2)}{' '}
                              <span style={{ fontSize: 11, color: '#64748B', fontWeight: 400 }}>
                                {row.type.toLowerCase().includes('electric') ? 'kWh' : 'm³'}
                              </span>
                            </td>

                            {/* 6. Over Usage */}
                            <td
                              style={{
                                padding: '12px 16px',
                                textAlign: 'right',
                                fontWeight: 700,
                                color: row.overUsage > 0 ? '#DC2626' : '#15803D',
                              }}
                            >
                              {row.overUsage > 0 ? `+${row.overUsage.toFixed(2)}` : row.overUsage.toFixed(2)}
                            </td>

                            {/* 7. Action Button — merged per block */}
                            {isFirstBlockRow && (
                              <td
                                rowSpan={blockGroup.rows.length}
                                style={{
                                  padding: '12px 16px',
                                  textAlign: 'center',
                                  verticalAlign: 'middle',
                                  borderLeft: '1px solid #F1F5F9',
                                  background: '#fff',
                                }}
                              >
                                <button
                                  onClick={() => setDetailGroup(blockGroup)}
                                  style={{
                                    padding: '6px 12px',
                                    borderRadius: 6,
                                    border: '1px solid #CBD5E1',
                                    background: '#F8FAFC',
                                    color: '#1E293B',
                                    fontWeight: 600,
                                    fontSize: 12,
                                    cursor: 'pointer',
                                    transition: 'all 0.15s ease',
                                    whiteSpace: 'nowrap',
                                  }}
                                  onMouseOver={(e) => {
                                    e.currentTarget.style.background = '#D97026';
                                    e.currentTarget.style.color = '#fff';
                                    e.currentTarget.style.borderColor = '#D97026';
                                  }}
                                  onMouseOut={(e) => {
                                    e.currentTarget.style.background = '#F8FAFC';
                                    e.currentTarget.style.color = '#1E293B';
                                    e.currentTarget.style.borderColor = '#CBD5E1';
                                  }}
                                >
                                  Xem chi tiết
                                </button>
                              </td>
                            )}
                          </tr>
                        );
                      });
                    });
                  })}

                  {/* ── Totals rows ── */}
                  {totalElectric.length > 0 && (
                    <tr style={{ background: '#FFF7ED', borderTop: '2px solid #FED7AA' }}>
                      <td colSpan={3} style={{ padding: '12px 16px', fontWeight: 700, color: '#0F172A', textAlign: 'center' }}>
                        Total electric {isEntireTermView ? '(Định mức kỳ: 200 kWh - 50 kWh/tháng × 4)' : `(Định mức tháng: ${MONTHLY_ELECTRIC_QUOTA} kWh)`}
                      </td>
                      <td style={{ padding: '12px 16px', textAlign: 'center', fontWeight: 700, color: '#C2410C' }}>Điện</td>
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontWeight: 700, color: '#0F172A' }}>
                        {sumElectricUsage.toFixed(2)} kWh
                      </td>
                      <td
                        style={{
                          padding: '12px 16px',
                          textAlign: 'right',
                          fontWeight: 700,
                          color: sumElectricOver > 0 ? '#DC2626' : '#15803D',
                        }}
                      >
                        {sumElectricOver > 0 ? `+${sumElectricOver.toFixed(2)}` : sumElectricOver.toFixed(2)}
                      </td>
                      <td style={{ background: '#FFF7ED' }}></td>
                    </tr>
                  )}
                  {totalWater.length > 0 && (
                    <tr style={{ background: '#EFF6FF', borderTop: '1px solid #BFDBFE' }}>
                      <td colSpan={3} style={{ padding: '12px 16px', fontWeight: 700, color: '#0F172A', textAlign: 'center' }}>
                        Total water {isEntireTermView ? '(Định mức kỳ: 12 m³ - 3 m³/tháng × 4)' : `(Định mức tháng: ${MONTHLY_WATER_QUOTA} m³)`}
                      </td>
                      <td style={{ padding: '12px 16px', textAlign: 'center', fontWeight: 700, color: '#1D4ED8' }}>Nước</td>
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontWeight: 700, color: '#0F172A' }}>
                        {sumWaterUsage.toFixed(2)} m³
                      </td>
                      <td
                        style={{
                          padding: '12px 16px',
                          textAlign: 'right',
                          fontWeight: 700,
                          color: sumWaterOver > 0 ? '#DC2626' : '#15803D',
                        }}
                      >
                        {sumWaterOver > 0 ? `+${sumWaterOver.toFixed(2)}` : sumWaterOver.toFixed(2)}
                      </td>
                      <td style={{ background: '#EFF6FF' }}></td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </div>

          {/* Quota notes */}
          {usageRows.length > 0 && (
            <div
              style={{
                fontSize: 12,
                color: '#64748B',
                background: '#F8FAFC',
                border: '1px solid #E2E8F0',
                borderRadius: 8,
                padding: '12px 16px',
                display: 'flex',
                alignItems: 'center',
                gap: 12,
              }}
            >
              <div style={{ flex: 1 }}>
                <strong>Quy định định mức miễn phí:</strong> Mỗi phòng/tháng được miễn phí{' '}
                <span style={{ color: '#D97026', fontWeight: 700 }}>50 kWh điện</span> và{' '}
                <span style={{ color: '#2563EB', fontWeight: 700 }}>3 m³ nước</span>. Vượt quá hạn mức sẽ được tính phí phát sinh theo giá bậc thang.
              </div>
            </div>
          )}
        </div>
      )}

      {/* ════════════════════════════════════════════════
          SECTION: IMPORT CHỈ SỐ
          ════════════════════════════════════════════════ */}
      {activeSection === 'import' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
          {/* Period selector + download template */}
          <div className="facilities-card" style={{ padding: 20, background: '#fff', border: '1px solid #E2E8F0', borderRadius: 12 }}>
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16, alignItems: 'flex-end' }}>
              <div style={{ flex: 1, minWidth: 240 }}>
                <label style={{ display: 'block', fontSize: 13, fontWeight: 600, color: '#374151', marginBottom: 6 }}>
                  Kỳ thanh toán
                </label>
                {periodsLoading ? (
                  <div style={{ fontSize: 13, color: '#94A3B8' }}>Đang nạp danh sách kỳ...</div>
                ) : (
                  <select
                    className="facilities-select"
                    value={selectedPeriodId}
                    onChange={(e) => {
                      const newPeriodId = e.target.value ? Number(e.target.value) : '';
                      setSelectedPeriodId(newPeriodId);
                      setPreview(null);
                      setImportMsg(null);
                      if (importFile && newPeriodId) {
                        triggerPreview(importFile, newPeriodId);
                      }
                    }}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: 8, border: '1px solid #CBD5E1', fontSize: 14 }}
                  >
                    <option value="">-- Chọn kỳ tính điện nước --</option>
                    {periods.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.displayName} ({p.termName}) — {p.status === 'Open' ? 'Đang mở' : p.status === 'Calculated' ? 'Đã tính' : 'Đã đóng'}
                      </option>
                    ))}
                  </select>
                )}
              </div>

              {/* Status badge */}
              {selectedPeriod && (
                <div>
                  <span
                    style={{
                      display: 'inline-block',
                      padding: '6px 14px',
                      borderRadius: 20,
                      fontSize: 12,
                      fontWeight: 700,
                      background: selectedPeriod.status === 'Open' ? '#DCFCE7' : '#FEE2E2',
                      color: selectedPeriod.status === 'Open' ? '#15803D' : '#DC2626',
                    }}
                  >
                    {selectedPeriod.status === 'Open' ? 'Kỳ đang mở' : 'Kỳ đã đóng'}
                  </span>
                </div>
              )}

              {/* Download template */}
              <button
                className="btn-fud-secondary"
                onClick={() => ewApi.downloadTemplate(selectedPeriodId ? (selectedPeriodId as number) : undefined)}
                style={{
                  padding: '8px 18px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#F8FAFC',
                  color: '#374151',
                  fontWeight: 600,
                  fontSize: 13,
                  cursor: 'pointer',
                }}
              >
                Tải file mẫu Excel
              </button>
            </div>
          </div>

          {/* Import area */}
          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              padding: 24,
            }}
          >
            <h3 style={{ margin: '0 0 16px', fontSize: 16, fontWeight: 700, color: '#0F172A' }}>
              Nhập chỉ số điện – nước từ file Excel
            </h3>

            {isClosed && selectedPeriod ? (
              <div
                style={{
                  padding: '14px 20px',
                  background: '#FEF2F2',
                  border: '1px solid #FECACA',
                  borderRadius: 10,
                  color: '#DC2626',
                  fontWeight: 600,
                  fontSize: 14,
                }}
              >
                Kỳ thanh toán này đã đóng — nút import chỉ số đã được vô hiệu hóa.
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
                  <input
                    ref={fileRef}
                    type="file"
                    accept=".xlsx"
                    onChange={handleFileChange}
                    style={{ flex: 1, fontSize: 13, color: '#374151' }}
                  />
                  {previewLoading && (
                    <span style={{ fontSize: 13, color: '#D97026', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 6 }}>
                      ⏳ Đang phân tích file Excel...
                    </span>
                  )}
                  {preview && (
                    <button
                      className="btn-fud-secondary"
                      onClick={handlePreview}
                      disabled={!importFile || !selectedPeriodId || previewLoading}
                      style={{
                        padding: '8px 16px',
                        borderRadius: 8,
                        border: '1px solid #CBD5E1',
                        background: '#F8FAFC',
                        color: '#374151',
                        fontSize: 13,
                        cursor: 'pointer',
                      }}
                      title="Nạp lại dữ liệu gốc từ file Excel"
                    >
                      Đọc lại file
                    </button>
                  )}
                  <button
                    onClick={handleConfirmImport}
                    disabled={!preview || importLoading || isClosed}
                    style={{
                      padding: '8px 24px',
                      borderRadius: 8,
                      border: 'none',
                      background: !preview || isClosed ? '#94A3B8' : '#D97026',
                      color: '#fff',
                      fontWeight: 700,
                      fontSize: 13,
                      cursor: !preview || isClosed ? 'not-allowed' : 'pointer',
                      boxShadow: preview && !isClosed ? '0 2px 4px rgba(217,112,38,0.2)' : 'none',
                    }}
                  >
                    {importLoading ? 'Đang lưu vào DB...' : isClosed ? 'Đã đóng kỳ' : `Xác nhận lưu (${preview?.validRows || 0} dòng)`}
                  </button>
                </div>

                {/* Alert message */}
                {importMsg && (
                  <div
                    style={{
                      padding: '12px 16px',
                      borderRadius: 8,
                      background: importMsg.type === 'success' ? '#DCFCE7' : '#FEF2F2',
                      border: `1px solid ${importMsg.type === 'success' ? '#86EFAC' : '#FECACA'}`,
                      color: importMsg.type === 'success' ? '#15803D' : '#DC2626',
                      fontSize: 13,
                      fontWeight: 600,
                    }}
                  >
                    {importMsg.text}
                  </div>
                )}

                {/* Preview table */}
                {preview && (
                  <div>
                    {/* Guidance hint */}
                    <div
                      style={{
                        padding: '10px 14px',
                        background: '#EFF6FF',
                        border: '1px solid #BFDBFE',
                        borderRadius: 8,
                        marginBottom: 12,
                        fontSize: 12,
                        color: '#1D4ED8',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                      }}
                    >
                      <span>
                        💡 <strong>Chỉnh sửa trực tiếp:</strong> Bạn có thể gõ sửa trực tiếp chỉ số tại cột <strong>Giá trị L</strong> và <strong>Giá trị R</strong> bên dưới. Mức tiêu thụ sẽ được tự động tính toán lại tức thì trước khi bạn bấm <strong>Xác nhận lưu</strong>.
                      </span>
                    </div>

                    {/* Summary */}
                    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', marginBottom: 12 }}>
                      {[
                        { label: 'Tổng dòng', val: preview.totalRows, color: '#374151' },
                        { label: 'Hợp lệ', val: preview.validRows, color: '#15803D' },
                        { label: 'Cảnh báo', val: preview.warningRows, color: '#B45309' },
                        { label: 'Lỗi', val: preview.errorRows, color: '#DC2626' },
                      ].map((s) => (
                        <div
                          key={s.label}
                          style={{
                            padding: '8px 16px',
                            background: '#F8FAFC',
                            border: '1px solid #E2E8F0',
                            borderRadius: 8,
                            fontSize: 13,
                            fontWeight: 600,
                            color: s.color,
                          }}
                        >
                          {s.label}: {s.val}
                        </div>
                      ))}
                      {preview.alreadyHasDataInPeriod && (
                        <div
                          style={{
                            padding: '8px 16px',
                            background: '#FEF9C3',
                            border: '1px solid #FDE047',
                            borderRadius: 8,
                            fontSize: 13,
                            fontWeight: 600,
                            color: '#854D0E',
                          }}
                        >
                          Kỳ này đã có dữ liệu — hệ thống sẽ ghi đè và cập nhật chỉ số mới
                        </div>
                      )}
                    </div>

                    {/* Preview rows table */}
                    <div style={{ overflowX: 'auto', border: '1px solid #E2E8F0', borderRadius: 8 }}>
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                        <thead>
                          <tr style={{ background: '#F8FAFC' }}>
                            {['STT', 'Block', 'Loại', 'Ngày', 'Phòng', 'Giá trị', 'Tiêu thụ', 'Phòng', 'Giá trị', 'Tiêu thụ', 'Trạng thái'].map((h) => (
                              <th
                                key={h}
                                style={{
                                  padding: '10px 12px',
                                  textAlign: h.includes('Giá trị') || h.includes('Tiêu thụ') ? 'right' : 'left',
                                  fontWeight: 700,
                                  color: '#374151',
                                  borderBottom: '2px solid #E2E8F0',
                                  whiteSpace: 'nowrap',
                                }}
                              >
                                {h}
                              </th>
                            ))}
                          </tr>
                        </thead>
                        <tbody>
                          {preview.rows.map((row, idx) => {
                            const statusColor =
                              row.status === 'Error'
                                ? { bg: '#FEF2F2', text: '#DC2626' }
                                : row.status === 'Warning'
                                  ? { bg: '#FEF9C3', text: '#B45309' }
                                  : { bg: '#DCFCE7', text: '#15803D' };
                            return (
                              <tr
                                key={idx}
                                style={{ background: idx % 2 === 0 ? '#fff' : '#F8FAFC' }}
                              >
                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9' }}>{idx + 1}</td>
                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9', fontWeight: 600 }}>{row.blockCode}</td>
                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9' }}>{row.meterType === 'Electricity' ? 'Điện' : 'Nước'}</td>
                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9' }}>{row.dateStr}</td>
                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9', fontWeight: 600 }}>{row.leftRoomNumber}</td>

                                {/* Giá trị L (Editable) */}
                                <td style={{ padding: '6px 8px', borderBottom: '1px solid #F1F5F9', textAlign: 'right' }}>
                                  <input
                                    type="number"
                                    step="0.01"
                                    value={row.leftValue !== undefined && row.leftValue !== null ? row.leftValue : ''}
                                    onChange={(e) => handleUpdateRowValue(idx, 'left', e.target.value)}
                                    placeholder="0"
                                    style={{
                                      width: 80,
                                      padding: '4px 6px',
                                      borderRadius: 6,
                                      border: row.leftValue !== undefined && row.leftValue < row.leftOldReading ? '1px solid #EF4444' : '1px solid #CBD5E1',
                                      background: row.leftValue !== undefined && row.leftValue < row.leftOldReading ? '#FEF2F2' : '#fff',
                                      fontSize: 13,
                                      fontWeight: 600,
                                      textAlign: 'right',
                                      color: '#0F172A',
                                      outline: 'none',
                                    }}
                                    title={`Chỉ số cũ: ${row.leftOldReading}`}
                                  />
                                </td>

                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9', textAlign: 'right', fontWeight: 600, color: '#D97026' }}>
                                  {row.leftConsumption !== undefined ? row.leftConsumption.toFixed(2) : '—'}
                                </td>

                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9', fontWeight: 600 }}>{row.rightRoomNumber}</td>

                                {/* Giá trị R (Editable) */}
                                <td style={{ padding: '6px 8px', borderBottom: '1px solid #F1F5F9', textAlign: 'right' }}>
                                  <input
                                    type="number"
                                    step="0.01"
                                    value={row.rightValue !== undefined && row.rightValue !== null ? row.rightValue : ''}
                                    onChange={(e) => handleUpdateRowValue(idx, 'right', e.target.value)}
                                    placeholder="0"
                                    style={{
                                      width: 80,
                                      padding: '4px 6px',
                                      borderRadius: 6,
                                      border: row.rightValue !== undefined && row.rightValue < row.rightOldReading ? '1px solid #EF4444' : '1px solid #CBD5E1',
                                      background: row.rightValue !== undefined && row.rightValue < row.rightOldReading ? '#FEF2F2' : '#fff',
                                      fontSize: 13,
                                      fontWeight: 600,
                                      textAlign: 'right',
                                      color: '#0F172A',
                                      outline: 'none',
                                    }}
                                    title={`Chỉ số cũ: ${row.rightOldReading}`}
                                  />
                                </td>

                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9', textAlign: 'right', fontWeight: 600, color: '#D97026' }}>
                                  {row.rightConsumption !== undefined ? row.rightConsumption.toFixed(2) : '—'}
                                </td>

                                <td style={{ padding: '8px 12px', borderBottom: '1px solid #F1F5F9' }}>
                                  <span
                                    style={{
                                      display: 'inline-block',
                                      padding: '2px 10px',
                                      borderRadius: 12,
                                      background: statusColor.bg,
                                      color: statusColor.text,
                                      fontWeight: 700,
                                      fontSize: 11,
                                    }}
                                  >
                                    {row.status === 'Error' ? 'Lỗi' : row.status === 'Warning' ? 'Cảnh báo' : 'Hợp lệ'}
                                  </span>
                                  {(row.errorMessage || row.flagReason) && (
                                    <div style={{ fontSize: 11, color: '#64748B', marginTop: 2 }}>
                                      {row.errorMessage || row.flagReason}
                                    </div>
                                  )}
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ════════════════════════════════════════════════
          SECTION: LỊCH SỬ IMPORT
          ════════════════════════════════════════════════ */}
      {activeSection === 'history' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
            <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: '#0F172A' }}>
              Lịch sử các đợt import chỉ số
            </h3>
            <button
              onClick={loadBatches}
              style={{
                padding: '8px 18px',
                borderRadius: 8,
                border: '1px solid #CBD5E1',
                background: '#F8FAFC',
                color: '#374151',
                fontWeight: 600,
                fontSize: 13,
                cursor: 'pointer',
              }}
            >
              Làm mới
            </button>
          </div>

          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              overflow: 'hidden',
            }}
          >
            {batchLoading ? (
              <div style={{ padding: 24, color: '#94A3B8', textAlign: 'center' }}>Đang nạp dữ liệu lịch sử...</div>
            ) : batches.length === 0 ? (
              <div style={{ padding: 24, color: '#94A3B8', textAlign: 'center' }}>Chưa có đợt import nào được ghi nhận.</div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ background: '#F8FAFC' }}>
                    {['Mã đợt', 'Kỳ tính phí', 'Block', 'Phương thức', 'Tên file', 'Tổng dòng', 'Dòng lỗi', 'Cán bộ thực hiện', 'Thời gian'].map((h) => (
                      <th
                        key={h}
                        style={{
                          padding: '10px 14px',
                          textAlign: 'left',
                          fontWeight: 700,
                          color: '#374151',
                          borderBottom: '2px solid #E2E8F0',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {batches.map((b, idx) => (
                    <tr key={b.id} style={{ background: idx % 2 === 0 ? '#fff' : '#F8FAFC', borderBottom: '1px solid #F1F5F9' }}>
                      <td style={{ padding: '10px 14px', color: '#94A3B8' }}>#{b.id}</td>
                      <td style={{ padding: '10px 14px' }}>{b.billingPeriodDisplay}</td>
                      <td style={{ padding: '10px 14px', fontWeight: 600 }}>{b.blockCode}</td>
                      <td style={{ padding: '10px 14px' }}>{b.method}</td>
                      <td style={{ padding: '10px 14px', color: '#64748B', fontSize: 12 }}>{b.fileName || '—'}</td>
                      <td style={{ padding: '10px 14px', textAlign: 'center' }}>{b.totalRows}</td>
                      <td
                        style={{
                          padding: '10px 14px',
                          textAlign: 'center',
                          color: b.errorRows > 0 ? '#DC2626' : '#15803D',
                          fontWeight: b.errorRows > 0 ? 700 : 400,
                        }}
                      >
                        {b.errorRows}
                      </td>
                      <td style={{ padding: '10px 14px' }}>{b.guardName}</td>
                      <td style={{ padding: '10px 14px', color: '#64748B', fontSize: 12 }}>
                        {new Date(b.importTime).toLocaleString('vi-VN')}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* ════════════════════════════════════════════════
          MODAL: CHI TIẾT BLOCK & CÔNG TƠ TỪNG PHÒNG
          ════════════════════════════════════════════════ */}
      {detailGroup && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.65)',
            backdropFilter: 'blur(3px)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: 16,
          }}
          onClick={() => setDetailGroup(null)}
        >
          <div
            style={{
              backgroundColor: '#fff',
              borderRadius: 16,
              width: '100%',
              maxWidth: 820,
              maxHeight: '90vh',
              overflowY: 'auto',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.2)',
              padding: '24px 28px',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', borderBottom: '1px solid #E2E8F0', paddingBottom: 16, marginBottom: 20 }}>
              <div>
                <span style={{ fontSize: 11, fontWeight: 700, color: '#D97026', textTransform: 'uppercase', letterSpacing: 0.8 }}>
                  HỒ SƠ CHI TIẾT CHỈ SỐ
                </span>
                <h2 style={{ fontSize: 20, fontWeight: 700, color: '#0F172A', margin: '4px 0' }}>
                  Block {detailGroup.block} {detailGroup.blockName ? `(${detailGroup.blockName})` : ''}
                </h2>
                <div style={{ fontSize: 13, color: '#64748B' }}>
                  Kỳ tính phí: <strong style={{ color: '#0F172A' }}>{detailGroup.monthYear}</strong>
                </div>
              </div>
              <button
                onClick={() => setDetailGroup(null)}
                style={{
                  background: '#F1F5F9',
                  border: 'none',
                  borderRadius: '50%',
                  width: 32,
                  height: 32,
                  cursor: 'pointer',
                  fontSize: 16,
                  color: '#64748B',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                }}
              >
                ✕
              </button>
            </div>

            {/* Quick Summary Cards */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 16, marginBottom: 24 }}>
              {detailGroup.rows.map((r, ri) => {
                const isElec = r.type.toLowerCase().includes('electric');
                const isOver = r.overUsage > 0;
                return (
                  <div
                    key={`${r.type}-${r.monthYear}-${ri}`}
                    style={{
                      background: isElec ? '#FFF7ED' : '#EFF6FF',
                      border: `1px solid ${isElec ? '#FED7AA' : '#BFDBFE'}`,
                      borderRadius: 12,
                      padding: 16,
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 8 }}>
                      <span style={{ fontSize: 13, fontWeight: 700, color: isElec ? '#C2410C' : '#1D4ED8' }}>
                        Đồng hồ {r.typeLabel} ({r.monthYear})
                      </span>
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          padding: '2px 8px',
                          borderRadius: 10,
                          background: isOver ? '#FEE2E2' : '#DCFCE7',
                          color: isOver ? '#DC2626' : '#15803D',
                        }}
                      >
                        {isOver ? 'Vượt định mức' : 'Trong định mức'}
                      </span>
                    </div>

                    <div style={{ fontSize: 24, fontWeight: 800, color: '#0F172A', marginBottom: 6 }}>
                      {r.usage.toFixed(2)}{' '}
                      <span style={{ fontSize: 13, fontWeight: 500, color: '#64748B' }}>
                        {isElec ? 'kWh' : 'm³'}
                      </span>
                    </div>

                    <div style={{ fontSize: 12, color: '#475569', display: 'flex', justifyContent: 'space-between', borderTop: '1px dashed #CBD5E1', paddingTop: 8 }}>
                      <span>Định mức miễn phí:</span>
                      <strong>{r.quota} {isElec ? 'kWh' : 'm³'}</strong>
                    </div>
                    <div style={{ fontSize: 12, color: isOver ? '#DC2626' : '#15803D', display: 'flex', justifyContent: 'space-between', marginTop: 4, fontWeight: 700 }}>
                      <span>Chênh lệch:</span>
                      <span>{isOver ? `+${r.overUsage.toFixed(2)}` : r.overUsage.toFixed(2)} {isElec ? 'kWh' : 'm³'}</span>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Meter Readings Table */}
            <div style={{ marginBottom: 20 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
                <h4 style={{ fontSize: 14, fontWeight: 700, color: '#0F172A', margin: 0 }}>
                  Chỉ số công tơ chi tiết từng phòng (Phòng Trái & Phải)
                </h4>
                <span style={{ fontSize: 12, color: '#64748B' }}>
                  Có thể bấm <strong>"Sửa"</strong> để cập nhật lại chỉ số công tơ
                </span>
              </div>

              {editReadingMsg && (
                <div
                  style={{
                    padding: '8px 14px',
                    borderRadius: 8,
                    marginBottom: 12,
                    fontSize: 12,
                    fontWeight: 600,
                    background: editReadingMsg.type === 'success' ? '#DCFCE7' : '#FEE2E2',
                    color: editReadingMsg.type === 'success' ? '#15803D' : '#DC2626',
                    border: `1px solid ${editReadingMsg.type === 'success' ? '#86EFAC' : '#FCA5A5'}`,
                  }}
                >
                  {editReadingMsg.text}
                </div>
              )}

              {(() => {
                const allReadings = detailGroup.rows.flatMap((r) => r.readings);
                if (allReadings.length === 0) {
                  return (
                    <div style={{ padding: 16, background: '#F8FAFC', borderRadius: 8, textAlign: 'center', color: '#94A3B8', fontSize: 13 }}>
                      Không có bản ghi công tơ chi tiết riêng rẽ cho block này.
                    </div>
                  );
                }

                return (
                  <div style={{ border: '1px solid #E2E8F0', borderRadius: 8, overflow: 'hidden' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: '#F8FAFC' }}>
                          <th style={{ padding: '8px 10px', textAlign: 'left', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Phòng</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Loại</th>
                          <th style={{ padding: '8px 10px', textAlign: 'right', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Chỉ số cũ</th>
                          <th style={{ padding: '8px 10px', textAlign: 'right', color: '#475569', borderBottom: '1px solid #E2E8F0', width: 110 }}>Chỉ số mới</th>
                          <th style={{ padding: '8px 10px', textAlign: 'right', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Tiêu thụ</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Trạng thái</th>
                          <th style={{ padding: '8px 10px', textAlign: 'left', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Người ghi</th>
                          <th style={{ padding: '8px 10px', textAlign: 'left', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Thời gian</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0', width: 100 }}>Thao tác</th>
                        </tr>
                      </thead>
                      <tbody>
                        {allReadings.map((m, idx) => {
                          const isEditing = editingReadingId === m.readingId;

                          return (
                            <tr key={idx} style={{ background: idx % 2 === 0 ? '#fff' : '#FAFAFA', borderBottom: '1px solid #F1F5F9' }}>
                              <td style={{ padding: '8px 10px', fontWeight: 700, color: '#0F172A' }}>{m.roomNumber}</td>
                              <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                                <span
                                  style={{
                                    display: 'inline-block',
                                    padding: '1px 6px',
                                    borderRadius: 4,
                                    fontSize: 11,
                                    fontWeight: 600,
                                    background: m.meterType.toLowerCase().includes('electric') ? '#FFF7ED' : '#EFF6FF',
                                    color: m.meterType.toLowerCase().includes('electric') ? '#C2410C' : '#1D4ED8',
                                  }}
                                >
                                  {meterTypeLabel(m.meterType)}
                                </span>
                              </td>
                              <td style={{ padding: '8px 10px', textAlign: 'right', color: '#64748B' }}>{m.previousValue.toFixed(2)}</td>

                              {/* Chỉ số mới (editable) */}
                              <td style={{ padding: '8px 10px', textAlign: 'right' }}>
                                {isEditing ? (
                                  <input
                                    type="number"
                                    step="0.01"
                                    min="0"
                                    value={editReadingValue}
                                    onChange={(e) => setEditReadingValue(e.target.value)}
                                    style={{
                                      width: '100%',
                                      padding: '4px 6px',
                                      border: '1px solid #D97026',
                                      borderRadius: 4,
                                      textAlign: 'right',
                                      fontSize: 12,
                                      fontWeight: 700,
                                      outline: 'none',
                                    }}
                                    autoFocus
                                  />
                                ) : (
                                  <span style={{ fontWeight: 600, color: '#0F172A' }}>
                                    {m.currentValue.toFixed(2)}
                                  </span>
                                )}
                              </td>

                              <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 700, color: '#D97026' }}>
                                {isEditing && !isNaN(parseFloat(editReadingValue))
                                  ? (parseFloat(editReadingValue) - m.previousValue).toFixed(2)
                                  : m.consumption.toFixed(2)}
                              </td>
                              <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                                <span
                                  style={{
                                    display: 'inline-block',
                                    padding: '1px 8px',
                                    borderRadius: 10,
                                    fontSize: 10,
                                    fontWeight: 700,
                                    background: m.status === 'Flagged' ? '#FEE2E2' : '#DCFCE7',
                                    color: m.status === 'Flagged' ? '#DC2626' : '#15803D',
                                  }}
                                >
                                  {m.status === 'Flagged' ? 'Bất thường' : 'Đã ghi nhận'}
                                </span>
                              </td>
                              <td style={{ padding: '8px 10px', color: '#475569' }}>{m.recordedByName || 'Bảo vệ'}</td>
                              <td style={{ padding: '8px 10px', color: '#64748B' }}>
                                {m.recordedTime ? new Date(m.recordedTime).toLocaleString('vi-VN') : '—'}
                              </td>

                              {/* Thao tác Sửa / Lưu / Hủy */}
                              <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                                {isEditing ? (
                                  <div style={{ display: 'flex', gap: 4, justifyContent: 'center' }}>
                                    <button
                                      onClick={() => handleSaveEdit(m)}
                                      disabled={editReadingLoading}
                                      style={{
                                        padding: '3px 8px',
                                        borderRadius: 4,
                                        border: 'none',
                                        background: '#D97026',
                                        color: '#fff',
                                        fontWeight: 600,
                                        fontSize: 11,
                                        cursor: 'pointer',
                                      }}
                                    >
                                      {editReadingLoading ? '...' : 'Lưu'}
                                    </button>
                                    <button
                                      onClick={handleCancelEdit}
                                      style={{
                                        padding: '3px 6px',
                                        borderRadius: 4,
                                        border: '1px solid #CBD5E1',
                                        background: '#fff',
                                        color: '#64748B',
                                        fontSize: 11,
                                        cursor: 'pointer',
                                      }}
                                    >
                                      Hủy
                                    </button>
                                  </div>
                                ) : (
                                  <button
                                    onClick={() => handleStartEdit(m)}
                                    style={{
                                      padding: '3px 10px',
                                      borderRadius: 4,
                                      border: '1px solid #CBD5E1',
                                      background: '#F8FAFC',
                                      color: '#1E293B',
                                      fontSize: 11,
                                      fontWeight: 600,
                                      cursor: 'pointer',
                                    }}
                                    onMouseOver={(e) => {
                                      e.currentTarget.style.borderColor = '#D97026';
                                      e.currentTarget.style.color = '#D97026';
                                    }}
                                    onMouseOut={(e) => {
                                      e.currentTarget.style.borderColor = '#CBD5E1';
                                      e.currentTarget.style.color = '#1E293B';
                                    }}
                                  >
                                    Sửa
                                  </button>
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                );
              })()}
            </div>

            {/* Modal Actions */}
            <div style={{ display: 'flex', justifyContent: 'flex-end', borderTop: '1px solid #E2E8F0', paddingTop: 16 }}>
              <button
                className="btn-fud-secondary"
                onClick={() => setDetailGroup(null)}
                style={{
                  padding: '8px 24px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#F8FAFC',
                  color: '#374151',
                  fontWeight: 600,
                  fontSize: 13,
                  cursor: 'pointer',
                }}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
