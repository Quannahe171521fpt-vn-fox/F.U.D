import React, { useState, useEffect, useRef, useCallback } from 'react';
import '../../styles/facilities.css';
import { Pagination } from '../../components/Pagination';
import { MeterPhotoScanner } from '../../components/MeterPhotoScanner';
import { ImageLightboxModal } from '../../components/ImageLightboxModal';
import { useAuth } from '../../contexts/AuthContext';
import {
  ewApi,
  dormApi,
  type BillingPeriodLookupDto,
  type ExcelPreviewResponseDto,
  type ImportBatchItemDto,
  type EwUsageRow,
  type EwReadingDetailItem,
  type UsageAnomalyItemDto,
  type BlockProgressResponseDto,
  type BlockFormDataResponseDto,
  type RoomOldReadingDto,
  type MeterPhotoScanResponseDto,
  type ManualConfirmRequestDto,
} from '../../services/api';

// ── Usage table row (computed) ──────────────────────────
interface UsageTableRow {
  id: number;
  dormName: string;          // "Dom A"
  block: string;
  blockName: string;
  blockCode: string;
  roomNumber: string;
  monthYear: string;         // "11 - 2026"
  recordDate: string;        // "01/11/2026"
  ngayTao: string;           // "2026-08-30" (YYYY-MM-DD)
  hocKy: string;             // "Summer - 2026"
  meterNumberDisplay: string;// "29721 - 22300"
  currentMonthConsumption: number; // Tiêu thụ tháng này
  totalConsumption: number;  // Tổng tiêu thụ cả kỳ
  donvi: string;             // "kW" | "m³"
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
  dormName?: string;
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
    let ngayTaoFormatted = '—';
    if (r.ngayTao) {
      try {
        const d = new Date(r.ngayTao);
        const yyyy = d.getFullYear();
        const mm = String(d.getMonth() + 1).padStart(2, '0');
        const dd = String(d.getDate()).padStart(2, '0');
        ngayTaoFormatted = `${yyyy}-${mm}-${dd}`;
      } catch {
        ngayTaoFormatted = String(r.ngayTao).substring(0, 10);
      }
    }

    const isElec =
      (r.loaiSuDung || '').toLowerCase().includes('điện') ||
      (r.loaiSuDung || '').toLowerCase().includes('electric');
    const typeLabel = isElec ? 'Điện' : 'Nước';
    const quota = isElec ? 200 : 12; // Định mức kỳ: 200 kWh điện, 12 m3 nước

    // Tên dorm
    let dormName = r.dormName || '';
    if (!dormName) {
      if (r.blockName && r.blockName.includes('Dom')) {
        dormName = r.blockName.split('-').pop()?.trim() || 'Dom A';
      } else if (r.blockCode && r.blockCode.toUpperCase().startsWith('A')) {
        dormName = 'Dom A';
      } else if (r.blockCode && r.blockCode.toUpperCase().startsWith('B')) {
        dormName = 'Dom B';
      } else {
        dormName = 'Dom A';
      }
    }

    // Tên block/phòng: ưu tiên block -> tenBlock -> blockCode
    let blockDisplayName = r.block || r.tenBlock || r.blockCode || '—';
    if (r.roomNumber) {
      const duplicateSuffix = `-${r.roomNumber}-${r.roomNumber}`;
      if (blockDisplayName.endsWith(duplicateSuffix)) {
        blockDisplayName = blockDisplayName.slice(0, -r.roomNumber.length - 1);
      }
    }

    const donvi = r.donvi || (isElec ? 'kW' : 'm³');

    let currentMonthCons = (r as any).currentMonthConsumption ?? (r as any).CurrentMonthConsumption;
    if (currentMonthCons === undefined || currentMonthCons === null || currentMonthCons === 0) {
      if (r.readings && r.readings.length > 0) {
        currentMonthCons = r.readings.reduce(
          (sum: number, rd: EwReadingDetailItem) => sum + (rd.consumption ?? (rd.currentValue - rd.previousValue)),
          0
        );
      }
    }
    if (currentMonthCons === undefined || currentMonthCons === null) {
      currentMonthCons = 0;
    }

    let totalCons = r.totalConsumption ?? (r as any).TotalConsumption ?? 0;
    if (totalCons === 0 && currentMonthCons > 0) {
      totalCons = currentMonthCons;
    }

    return {
      id: r.id,
      dormName: dormName,
      block: blockDisplayName,
      blockName: r.blockName || '',
      blockCode: r.blockCode || '',
      roomNumber: r.roomNumber || '',
      monthYear: r.hocKy || '—',
      recordDate: ngayTaoFormatted,
      ngayTao: ngayTaoFormatted,
      hocKy: r.hocKy || '—',
      meterNumberDisplay: r.meterNumberDisplay || '0 - 0',
      currentMonthConsumption: currentMonthCons,
      totalConsumption: totalCons,
      donvi: donvi,
      type: r.loaiSuDung,
      typeLabel: typeLabel,
      usage: totalCons,
      overUsage: totalCons - quota,
      quota,
      readings: r.readings || [],
    };
  });
}

type CaptureSlotKey = 'electricityLeft' | 'electricityRight' | 'waterLeft' | 'waterRight';

interface CaptureSlotState {
  value: string;
  evidenceUrl?: string;
  source?: 'Manual' | 'PhotoOCR';
  editedManually: boolean;
}

const EMPTY_CAPTURE_SLOTS: Record<CaptureSlotKey, CaptureSlotState> = {
  electricityLeft: { value: '', editedManually: false },
  electricityRight: { value: '', editedManually: false },
  waterLeft: { value: '', editedManually: false },
  waterRight: { value: '', editedManually: false },
};

// ── Component ───────────────────────────────────────────
export const EwManagement: React.FC = () => {
  const { currentUser } = useAuth();
  const roles = currentUser?.roles || [];
  const isManager =
    currentUser?.primaryRole === 'Manager' ||
    currentUser?.primaryRole === 'Admin' ||
    roles.includes('Manager') ||
    roles.includes('Admin');
  const isGuard =
    !isManager &&
    (currentUser?.primaryRole === 'Guard' || roles.includes('Guard'));

  const [activeSection, setActiveSection] = useState<'import' | 'capture' | 'usage' | 'anomalies' | 'history'>('usage');

  // ── Lightbox preview for evidence images ──
  const [lightboxImage, setLightboxImage] = useState<{ url: string; title: string; subtitle?: string } | null>(null);

  // ── AI Photo Capture by Block (Guard) ──
  const [blockProgress, setBlockProgress] = useState<BlockProgressResponseDto | null>(null);
  const [progressLoading, setProgressLoading] = useState(false);
  const [blockSearch, setBlockSearch] = useState('');
  const [blockFilter, setBlockFilter] = useState<'all' | 'incomplete' | 'completed'>('all');
  const [selectedBlockCode, setSelectedBlockCode] = useState<string>('');
  const [blockFormData, setBlockFormData] = useState<BlockFormDataResponseDto | null>(null);
  const [blockFormLoading, setBlockFormLoading] = useState(false);
  const [captureSlots, setCaptureSlots] = useState<Record<CaptureSlotKey, CaptureSlotState>>(EMPTY_CAPTURE_SLOTS);
  const [captureNote, setCaptureNote] = useState('');
  const [savingBlock, setSavingBlock] = useState(false);
  const [captureMsg, setCaptureMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // ── Billing Periods ──
  const [periods, setPeriods] = useState<BillingPeriodLookupDto[]>([]);
  const [selectedPeriodId, setSelectedPeriodId] = useState<number | ''>('');
  const [periodsLoading, setPeriodsLoading] = useState(false);

  // ── Anomalies & Billing Calculation (Manager) ──
  const [anomalies, setAnomalies] = useState<UsageAnomalyItemDto[]>([]);
  const [anomaliesLoading, setAnomaliesLoading] = useState(false);
  const [anomalyStatusFilter, setAnomalyStatusFilter] = useState<string>('Flagged');
  const [anomalyDormFilter, setAnomalyDormFilter] = useState<string>('');
  const [anomalyRoomSearch, setAnomalyRoomSearch] = useState<string>('');
  const [anomalyMeterFilter, setAnomalyMeterFilter] = useState<string>('');
  const [anomalyPage, setAnomalyPage] = useState<number>(1);
  const [anomalyPageSize] = useState<number>(6);
  const [resolvingAnomaly, setResolvingAnomaly] = useState<UsageAnomalyItemDto | null>(null);
  const [resolveAction, setResolveAction] = useState<'Confirm' | 'Adjust'>('Confirm');
  const [resolveValue, setResolveValue] = useState<string>('');
  const [resolveNote, setResolveNote] = useState<string>('');
  const [resolveLoading, setResolveLoading] = useState(false);
  const [anomalyMsg, setAnomalyMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [calcLoading, setCalcLoading] = useState(false);

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
  const [dormOptions, setDormOptions] = useState<string[]>(['Dom A', 'Dom B']);
  const [usageFilter, setUsageFilter] = useState({
    dormName: '',
    termId: '',
    billingPeriodId: '',
    blockCode: '',
    meterType: '',
    month: '',
    year: '',
  });

  useEffect(() => {
    dormApi
      .getAll({ status: 'Active', pageSize: 50 })
      .then((res) => {
        if (res?.items && res.items.length > 0) {
          const names = Array.from(new Set(res.items.map((d) => d.name).filter(Boolean)));
          if (names.length > 0) setDormOptions(names);
        }
      })
      .catch(() => {});
  }, []);

  const availableDorms = React.useMemo(() => {
    const set = new Set<string>(dormOptions);
    for (const r of usageRows) {
      if (r.dormName) set.add(r.dormName);
    }
    return Array.from(set).sort();
  }, [dormOptions, usageRows]);

  const filteredUsageRows = React.useMemo(() => {
    if (!usageFilter.dormName) return usageRows;
    const target = usageFilter.dormName.trim().toLowerCase();
    return usageRows.filter((r) => (r.dormName || '').trim().toLowerCase() === target);
  }, [usageRows, usageFilter.dormName]);

  // ── Pagination ──
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);

  useEffect(() => {
    setPage(1);
  }, [usageFilter]);

  const totalCount = filteredUsageRows.length;
  const paginatedUsageRows = React.useMemo(() => {
    const startIndex = (page - 1) * pageSize;
    return filteredUsageRows.slice(startIndex, startIndex + pageSize);
  }, [filteredUsageRows, page, pageSize]);

  // ── Modal Detail ──
  const [detailGroup, setDetailGroup] = useState<BlockUsageGroup | null>(null);

  const openDetailModal = (row: UsageTableRow) => {
    setDetailGroup({
      dormName: row.dormName,
      block: row.block,
      blockName: row.blockName,
      blockCode: row.blockCode,
      roomNumber: row.roomNumber,
      monthYear: row.hocKy,
      monthSubgroups: [],
      rows: [row],
    });
  };

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
          const diff = val - m.currentValue;
          const newMonthCons = (r.currentMonthConsumption ?? 0) + diff;
          const newTotal = (r.totalConsumption ?? r.usage) + diff;
          return {
            ...r,
            readings: newReadings,
            usage: newTotal,
            currentMonthConsumption: newMonthCons,
            totalConsumption: newTotal,
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

    // Re-evaluate validity (bao gồm kiểm tra vượt quá 1000 số điện / 50 số nước sau khi trừ định mức)
    const isElecRow = (row.meterType || '').toLowerCase().includes('elec') || (row.meterType || '').toLowerCase().includes('điện');
    const rowQuota = isElecRow ? 200 : 12;
    const rowSpikeThreshold = isElecRow ? 1000 : 50;

    const leftBelow = row.leftValue !== undefined && row.leftValue < row.leftOldReading;
    const rightBelow = row.rightValue !== undefined && row.rightValue < row.rightOldReading;
    const leftZero = row.leftValue !== undefined && row.leftValue === row.leftOldReading;
    const rightZero = row.rightValue !== undefined && row.rightValue === row.rightOldReading;
    const leftSpike = row.leftValue !== undefined && (row.leftConsumption - rowQuota) > rowSpikeThreshold;
    const rightSpike = row.rightValue !== undefined && (row.rightConsumption - rowQuota) > rowSpikeThreshold;

    if (leftBelow || rightBelow) {
      row.status = 'Warning';
      row.flagReason = `Chỉ số mới nhỏ hơn chỉ số cũ (${leftBelow ? 'Phòng L' : ''} ${rightBelow ? 'Phòng R' : ''})`;
    } else if (leftZero || rightZero) {
      row.status = 'Warning';
      row.flagReason = 'Mức tiêu thụ bằng 0';
    } else if (leftSpike || rightSpike) {
      row.status = 'Warning';
      row.flagReason = isElecRow
        ? `Tiêu thụ điện vượt > 1,000 kWh sau khi trừ định mức (${leftSpike ? 'Phòng L' : ''} ${rightSpike ? 'Phòng R' : ''}), nghi vấn dùng thiết bị cấm (bếp từ...)`
        : `Tiêu thụ nước vượt > 50 m³ sau khi trừ định mức (${leftSpike ? 'Phòng L' : ''} ${rightSpike ? 'Phòng R' : ''}), nghi vấn rò rỉ nước`;
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
      if (isManager) loadAnomalies();
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

  // ── Load anomalies (Manager) ──
  const loadAnomalies = useCallback(async () => {
    if (!isManager) return;
    setAnomaliesLoading(true);
    try {
      const data = await ewApi.getAnomalies({
        billingPeriodId: selectedPeriodId ? Number(selectedPeriodId) : undefined,
        status: anomalyStatusFilter || undefined,
      });
      setAnomalies(data);
    } catch {
      setAnomalies([]);
    } finally {
      setAnomaliesLoading(false);
    }
  }, [isManager, selectedPeriodId, anomalyStatusFilter]);

  useEffect(() => {
    if (isManager) {
      loadAnomalies();
    }
  }, [isManager, loadAnomalies]);

  const handleOpenResolveModal = (item: UsageAnomalyItemDto, defaultAction: 'Confirm' | 'Adjust') => {
    setResolvingAnomaly(item);
    setResolveAction(defaultAction);
    setResolveValue(item.currentValue.toString());
    setResolveNote(item.managerNote || '');
    setAnomalyMsg(null);
  };

  const handleSubmitResolveAnomaly = async () => {
    if (!resolvingAnomaly) return;
    setResolveLoading(true);
    setAnomalyMsg(null);
    try {
      let adjVal: number | undefined = undefined;
      if (resolveAction === 'Adjust') {
        adjVal = parseFloat(resolveValue);
        if (isNaN(adjVal) || adjVal < 0) {
          setAnomalyMsg({ type: 'error', text: 'Vui lòng nhập chỉ số điều chỉnh hợp lệ (>= 0)' });
          setResolveLoading(false);
          return;
        }
      }
      const res = await ewApi.resolveAnomaly(resolvingAnomaly.anomalyId, {
        action: resolveAction,
        adjustedValue: adjVal,
        managerNote: resolveNote,
      });
      setAnomalyMsg({ type: 'success', text: res.message });
      setResolvingAnomaly(null);
      await loadAnomalies();
      loadUsage();
    } catch (err: any) {
      setAnomalyMsg({ type: 'error', text: err.message || 'Lỗi khi xử lý cảnh báo bất thường' });
    } finally {
      setResolveLoading(false);
    }
  };

  const handleCalculateSurcharges = async () => {
    if (!selectedPeriodId) return;
    setCalcLoading(true);
    setAnomalyMsg(null);
    try {
      const res = await ewApi.calculatePeriodSurcharges(Number(selectedPeriodId));
      setAnomalyMsg({ type: 'success', text: res.message });
      loadUsage();
    } catch (err: any) {
      setAnomalyMsg({ type: 'error', text: err.message || 'Lỗi khi tính phụ trội điện nước' });
    } finally {
      setCalcLoading(false);
    }
  };

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

  // ── Load block progress & block form for AI capture tab ──
  const isBlockComplete = useCallback((b: BlockProgressResponseDto['blocks'][number]) => {
    const leftDone = Boolean(b.electricityLeft?.hasReading && b.waterLeft?.hasReading);
    const hasRight = Boolean(b.electricityRight || b.waterRight);
    const rightDone = !hasRight || Boolean(b.electricityRight?.hasReading && b.waterRight?.hasReading);
    return leftDone && rightDone;
  }, []);

  const loadBlockForm = useCallback(async (blockCode: string, periodId: number) => {
    setSelectedBlockCode(blockCode);
    setBlockFormLoading(true);
    setCaptureMsg(null);
    try {
      const form = await ewApi.getBlockFormData(blockCode, periodId);
      setBlockFormData(form);
      const initSlot = (slot?: RoomOldReadingDto): CaptureSlotState => ({
        value: slot?.existingCurrentValue !== undefined ? String(slot.existingCurrentValue) : '',
        evidenceUrl: slot?.existingEvidenceImageUrl,
        source: slot?.existingSource === 'PhotoOCR' ? 'PhotoOCR' : slot?.existingSource ? 'Manual' : undefined,
        editedManually: false,
      });
      setCaptureSlots({
        electricityLeft: initSlot(form.electricityLeft),
        electricityRight: initSlot(form.electricityRight),
        waterLeft: initSlot(form.waterLeft),
        waterRight: initSlot(form.waterRight),
      });
      setCaptureNote('');
    } catch (err: unknown) {
      setBlockFormData(null);
      setCaptureMsg({
        type: 'error',
        text: err instanceof Error ? err.message : 'Không thể tải dữ liệu công tơ của block',
      });
    } finally {
      setBlockFormLoading(false);
    }
  }, []);

  const loadBlockProgress = useCallback(
    async (periodId: number, autoSelectBlock?: string) => {
      setProgressLoading(true);
      try {
        const data = await ewApi.getBlockProgress(periodId);
        setBlockProgress(data);
        const targetCode =
          autoSelectBlock ||
          selectedBlockCode ||
          data.blocks.find((b) => !isBlockComplete(b))?.blockCode ||
          data.blocks[0]?.blockCode ||
          '';
        if (targetCode && data.blocks.some((b) => b.blockCode === targetCode)) {
          await loadBlockForm(targetCode, periodId);
        } else {
          setSelectedBlockCode('');
          setBlockFormData(null);
        }
      } catch (err: unknown) {
        setBlockProgress(null);
        setCaptureMsg({
          type: 'error',
          text: err instanceof Error ? err.message : 'Không thể tải tiến độ các block',
        });
      } finally {
        setProgressLoading(false);
      }
    },
    [selectedBlockCode, isBlockComplete, loadBlockForm]
  );

  useEffect(() => {
    if (activeSection === 'capture' && isGuard && selectedPeriodId) {
      void loadBlockProgress(Number(selectedPeriodId));
    }
  }, [activeSection, isGuard, selectedPeriodId, loadBlockProgress]);

  const filteredProgressBlocks = React.useMemo(() => {
    if (!blockProgress) return [];
    const q = blockSearch.trim().toLowerCase();
    return blockProgress.blocks.filter((b) => {
      const done = isBlockComplete(b);
      if (blockFilter === 'completed' && !done) return false;
      if (blockFilter === 'incomplete' && done) return false;
      if (!q) return true;
      return (
        b.blockCode.toLowerCase().includes(q) ||
        b.blockName.toLowerCase().includes(q) ||
        b.dormName.toLowerCase().includes(q)
      );
    });
  }, [blockProgress, blockSearch, blockFilter, isBlockComplete]);

  const handleSlotScanned = (slotKey: CaptureSlotKey, res: MeterPhotoScanResponseDto) => {
    setCaptureSlots((prev) => {
      const current = prev[slotKey];
      return {
        ...prev,
        [slotKey]: {
          value: res.readingValue !== undefined ? String(res.readingValue) : current.value,
          evidenceUrl: res.evidenceImageUrl || current.evidenceUrl,
          source: res.evidenceImageUrl ? 'PhotoOCR' : current.source,
          editedManually: false,
        },
      };
    });
  };

  const handleSlotValueChange = (slotKey: CaptureSlotKey, nextValue: string) => {
    setCaptureSlots((prev) => {
      const current = prev[slotKey];
      return {
        ...prev,
        [slotKey]: {
          ...current,
          value: nextValue,
          source: current.evidenceUrl ? current.source : 'Manual',
          editedManually: Boolean(current.evidenceUrl),
        },
      };
    });
  };

  const handleConfirmBlockCapture = async (advanceToNext: boolean) => {
    if (!selectedPeriodId || !selectedBlockCode || !blockFormData) return;

    const parseOptional = (raw: string): number | undefined => {
      if (raw.trim() === '') return undefined;
      const num = parseFloat(raw);
      return Number.isNaN(num) ? undefined : num;
    };

    const payload: ManualConfirmRequestDto = {
      billingPeriodId: Number(selectedPeriodId),
      blockCode: selectedBlockCode,
      electricityLeftNewValue: blockFormData.electricityLeft ? parseOptional(captureSlots.electricityLeft.value) : undefined,
      electricityRightNewValue: blockFormData.electricityRight ? parseOptional(captureSlots.electricityRight.value) : undefined,
      waterLeftNewValue: blockFormData.waterLeft ? parseOptional(captureSlots.waterLeft.value) : undefined,
      waterRightNewValue: blockFormData.waterRight ? parseOptional(captureSlots.waterRight.value) : undefined,
      electricityLeftEvidenceUrl: captureSlots.electricityLeft.evidenceUrl,
      electricityRightEvidenceUrl: captureSlots.electricityRight.evidenceUrl,
      waterLeftEvidenceUrl: captureSlots.waterLeft.evidenceUrl,
      waterRightEvidenceUrl: captureSlots.waterRight.evidenceUrl,
      electricityLeftSource: captureSlots.electricityLeft.evidenceUrl ? 'PhotoOCR' : 'Manual',
      electricityRightSource: captureSlots.electricityRight.evidenceUrl ? 'PhotoOCR' : 'Manual',
      waterLeftSource: captureSlots.waterLeft.evidenceUrl ? 'PhotoOCR' : 'Manual',
      waterRightSource: captureSlots.waterRight.evidenceUrl ? 'PhotoOCR' : 'Manual',
      note: captureNote.trim() || undefined,
      importOnlyValid: false,
    };

    const hasAnyValue =
      payload.electricityLeftNewValue !== undefined ||
      payload.electricityRightNewValue !== undefined ||
      payload.waterLeftNewValue !== undefined ||
      payload.waterRightNewValue !== undefined;

    if (!hasAnyValue) {
      setCaptureMsg({ type: 'error', text: 'Vui lòng chụp ảnh hoặc nhập ít nhất 1 chỉ số trước khi lưu.' });
      return;
    }

    setSavingBlock(true);
    setCaptureMsg(null);
    try {
      const res = await ewApi.confirmManualReading(payload);
      const summary = res.managerAlerted
        ? `Đã lưu ${res.saved} chỉ số cho block ${selectedBlockCode} (${res.flagged} chỉ số bất thường đã gửi cảnh báo Quản lý).`
        : `Đã lưu thành công ${res.saved} chỉ số cho block ${selectedBlockCode}!`;

      loadUsage();

      const updatedProgress = await ewApi.getBlockProgress(Number(selectedPeriodId));
      setBlockProgress(updatedProgress);

      if (advanceToNext) {
        const nextBlock =
          updatedProgress.blocks.find((b) => b.blockCode !== selectedBlockCode && !isBlockComplete(b)) ||
          updatedProgress.blocks.find((b) => b.blockCode !== selectedBlockCode);
        if (nextBlock) {
          await loadBlockForm(nextBlock.blockCode, Number(selectedPeriodId));
          setCaptureMsg({ type: 'success', text: `${summary} Đã chuyển sang block ${nextBlock.blockCode}.` });
        } else {
          await loadBlockForm(selectedBlockCode, Number(selectedPeriodId));
          setCaptureMsg({ type: 'success', text: `${summary} Đã hoàn tất toàn bộ block!` });
        }
      } else {
        await loadBlockForm(selectedBlockCode, Number(selectedPeriodId));
        setCaptureMsg({ type: 'success', text: summary });
      }
    } catch (err: unknown) {
      setCaptureMsg({
        type: 'error',
        text: err instanceof Error ? err.message : 'Không thể lưu chỉ số của block',
      });
    } finally {
      setSavingBlock(false);
    }
  };

  const flaggedAnomaliesCount = anomalies.filter((a) => a.status === 'Flagged').length;

  const availableAnomalyDorms = React.useMemo(() => {
    const set = new Set<string>(dormOptions);
    for (const a of anomalies) {
      if (a.dormName) set.add(a.dormName);
    }
    return Array.from(set).sort();
  }, [dormOptions, anomalies]);

  const groupedAnomalies = React.useMemo(() => {
    const isElecItem = (item: UsageAnomalyItemDto) =>
      (item.meterType || '').toLowerCase().includes('elec') ||
      (item.meterType || '').toLowerCase().includes('điện');

    const map = new Map<
      string,
      {
        key: string;
        roomId: number;
        roomNumber: string;
        blockCode: string;
        dormName: string;
        billingPeriodId?: number;
        billingPeriodDisplay: string;
        guardName: string;
        guardPhone?: string;
        items: UsageAnomalyItemDto[];
        hasElec: boolean;
        hasWater: boolean;
      }
    >();

    for (const item of anomalies) {
      const key = `${item.roomId}-${item.billingPeriodId ?? item.billingPeriodDisplay}`;
      let group = map.get(key);
      if (!group) {
        group = {
          key,
          roomId: item.roomId,
          roomNumber: item.roomNumber,
          blockCode: item.blockCode,
          dormName: item.dormName || 'Dom A',
          billingPeriodId: item.billingPeriodId,
          billingPeriodDisplay: item.billingPeriodDisplay || '—',
          guardName: item.guardName || 'Bảo vệ',
          guardPhone: item.guardPhone,
          items: [],
          hasElec: false,
          hasWater: false,
        };
        map.set(key, group);
      }
      group.items.push(item);
      if (isElecItem(item)) group.hasElec = true;
      else group.hasWater = true;
    }

    const q = anomalyRoomSearch.trim().toLowerCase();
    const dormTarget = anomalyDormFilter.trim().toLowerCase();

    return Array.from(map.values())
      .map((g) => ({
        ...g,
        // Sắp xếp Điện lên trước, Nước sau cho đồng nhất
        items: [...g.items].sort((a, b) => (isElecItem(a) ? 0 : 1) - (isElecItem(b) ? 0 : 1)),
      }))
      .filter((g) => {
        if (dormTarget && g.dormName.trim().toLowerCase() !== dormTarget) return false;
        if (q) {
          const matchRoom =
            g.roomNumber.toLowerCase().includes(q) ||
            g.blockCode.toLowerCase().includes(q) ||
            g.dormName.toLowerCase().includes(q);
          if (!matchRoom) return false;
        }
        if (anomalyMeterFilter === 'Electricity' && !g.hasElec) return false;
        if (anomalyMeterFilter === 'Water' && !g.hasWater) return false;
        if (anomalyMeterFilter === 'Both' && !(g.hasElec && g.hasWater)) return false;
        return true;
      });
  }, [anomalies, anomalyDormFilter, anomalyRoomSearch, anomalyMeterFilter]);

  useEffect(() => {
    setAnomalyPage(1);
  }, [selectedPeriodId, anomalyStatusFilter, anomalyDormFilter, anomalyRoomSearch, anomalyMeterFilter]);

  const paginatedAnomalyGroups = React.useMemo(() => {
    const start = (anomalyPage - 1) * anomalyPageSize;
    return groupedAnomalies.slice(start, start + anomalyPageSize);
  }, [groupedAnomalies, anomalyPage, anomalyPageSize]);

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
      <div style={{ display: 'flex', gap: 0, borderBottom: '2px solid #E2E8F0', marginBottom: 24, flexWrap: 'wrap' }}>
        {(
          isManager
            ? ([
                { key: 'usage', label: 'Bảng theo dõi sử dụng' },
                ...(isGuard ? [{ key: 'capture' as const, label: 'Chụp ảnh công tơ (AI)' }] : []),
                {
                  key: 'anomalies',
                  label: `Cảnh báo bất thường${flaggedAnomaliesCount > 0 ? ` (${flaggedAnomaliesCount})` : ''}`,
                },
                { key: 'import', label: 'Import chỉ số Excel' },
                { key: 'history', label: 'Lịch sử import' },
              ] as const)
            : ([
                { key: 'usage', label: 'Bảng theo dõi sử dụng' },
                ...(isGuard ? [{ key: 'capture' as const, label: 'Chụp ảnh công tơ (AI)' }] : []),
                { key: 'import', label: 'Import chỉ số Excel' },
                { key: 'history', label: 'Lịch sử import' },
              ] as const)
        ).map((tab) => (
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
          SECTION: CHỤP ẢNH CÔNG TƠ (AI) — GUARD
          ════════════════════════════════════════════════ */}
      {activeSection === 'capture' && isGuard && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
          {/* Period & Progress header */}
          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              padding: '16px 20px',
              display: 'flex',
              flexWrap: 'wrap',
              gap: 16,
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap', flex: 1, minWidth: 260 }}>
              <div style={{ minWidth: 240, flex: 1, maxWidth: 360 }}>
                <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#374151', marginBottom: 4 }}>
                  Kỳ thanh toán
                </label>
                <select
                  value={selectedPeriodId}
                  onChange={(e) => {
                    const nextId = e.target.value ? Number(e.target.value) : '';
                    setSelectedPeriodId(nextId);
                    setSelectedBlockCode('');
                    setBlockFormData(null);
                  }}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    borderRadius: 8,
                    border: '1px solid #CBD5E1',
                    fontSize: 13,
                    background: '#fff',
                  }}
                >
                  <option value="">-- Chọn kỳ thanh toán --</option>
                  {periods.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.displayName} ({p.termName}) — {p.status === 'Open' ? 'Đang mở' : 'Đã đóng'}
                    </option>
                  ))}
                </select>
              </div>
              {selectedPeriod && (
                <span
                  style={{
                    padding: '5px 12px',
                    borderRadius: 20,
                    fontSize: 12,
                    fontWeight: 700,
                    background: selectedPeriod.status === 'Open' ? '#DCFCE7' : '#FEE2E2',
                    color: selectedPeriod.status === 'Open' ? '#15803D' : '#DC2626',
                    marginTop: 18,
                  }}
                >
                  {selectedPeriod.status === 'Open' ? 'Kỳ đang mở' : 'Kỳ đã đóng'}
                </span>
              )}
            </div>

            {blockProgress && (
              <div style={{ minWidth: 230 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 12, fontWeight: 700, color: '#0F172A', marginBottom: 6 }}>
                  <span>Tiến độ ghi chỉ số</span>
                  <span style={{ color: '#D97026' }}>
                    {blockProgress.completedBlocks}/{blockProgress.totalBlocks} block
                  </span>
                </div>
                <div style={{ width: '100%', height: 8, background: '#E2E8F0', borderRadius: 99, overflow: 'hidden' }}>
                  <div
                    style={{
                      width: `${blockProgress.totalBlocks > 0 ? Math.round((blockProgress.completedBlocks / blockProgress.totalBlocks) * 100) : 0}%`,
                      height: '100%',
                      background: '#D97026',
                      transition: 'width 0.2s ease',
                    }}
                  />
                </div>
              </div>
            )}
          </div>

          {isClosed && selectedPeriod && (
            <div
              style={{
                padding: '12px 16px',
                borderRadius: 10,
                background: '#FEF2F2',
                border: '1px solid #FECACA',
                color: '#DC2626',
                fontWeight: 600,
                fontSize: 13,
              }}
            >
              Kỳ thanh toán này đã đóng — chức năng chụp ảnh &amp; lưu chỉ số đã được khóa.
            </div>
          )}

          {captureMsg && (
            <div
              style={{
                padding: '12px 16px',
                borderRadius: 8,
                background: captureMsg.type === 'success' ? '#DCFCE7' : '#FEF2F2',
                border: `1px solid ${captureMsg.type === 'success' ? '#86EFAC' : '#FECACA'}`,
                color: captureMsg.type === 'success' ? '#15803D' : '#DC2626',
                fontSize: 13,
                fontWeight: 600,
              }}
            >
              {captureMsg.text}
            </div>
          )}

          {/* Main 2-column capture workspace */}
          <div style={{ display: 'grid', gridTemplateColumns: 'minmax(240px, 300px) minmax(0, 1fr)', gap: 18, alignItems: 'start' }}>
            {/* Left: Block list */}
            <div
              style={{
                background: '#fff',
                border: '1px solid #E2E8F0',
                borderRadius: 12,
                padding: 14,
                display: 'flex',
                flexDirection: 'column',
                gap: 10,
              }}
            >
              <input
                type="text"
                value={blockSearch}
                onChange={(e) => setBlockSearch(e.target.value)}
                placeholder="Tìm mã Block / KTX..."
                style={{
                  width: '100%',
                  padding: '8px 10px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  fontSize: 13,
                  boxSizing: 'border-box',
                }}
              />

              <div style={{ display: 'flex', gap: 6 }}>
                {(
                  [
                    { id: 'all', label: 'Tất cả' },
                    { id: 'incomplete', label: 'Chưa đủ' },
                    { id: 'completed', label: 'Đã đủ' },
                  ] as const
                ).map((f) => (
                  <button
                    key={f.id}
                    type="button"
                    onClick={() => setBlockFilter(f.id)}
                    style={{
                      flex: 1,
                      padding: '5px 6px',
                      borderRadius: 6,
                      border: blockFilter === f.id ? '1px solid #D97026' : '1px solid #E2E8F0',
                      background: blockFilter === f.id ? '#FFF7ED' : '#F8FAFC',
                      color: blockFilter === f.id ? '#C2410C' : '#475569',
                      fontSize: 11,
                      fontWeight: 700,
                      cursor: 'pointer',
                    }}
                  >
                    {f.label}
                  </button>
                ))}
              </div>

              <div style={{ maxHeight: 520, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 6 }}>
                {progressLoading ? (
                  <div style={{ padding: 20, textAlign: 'center', fontSize: 12, color: '#94A3B8' }}>
                    Đang tải danh sách block...
                  </div>
                ) : filteredProgressBlocks.length === 0 ? (
                  <div style={{ padding: 20, textAlign: 'center', fontSize: 12, color: '#94A3B8' }}>
                    Không có block phù hợp.
                  </div>
                ) : (
                  filteredProgressBlocks.map((b) => {
                    const done = isBlockComplete(b);
                    const isSelected = b.blockCode === selectedBlockCode;
                    return (
                      <button
                        key={`${b.dormName}-${b.blockCode}`}
                        type="button"
                        onClick={() => {
                          if (selectedPeriodId) {
                            void loadBlockForm(b.blockCode, Number(selectedPeriodId));
                          }
                        }}
                        style={{
                          width: '100%',
                          textAlign: 'left',
                          padding: '10px 12px',
                          borderRadius: 8,
                          border: isSelected ? '2px solid #D97026' : '1px solid #E2E8F0',
                          background: isSelected ? '#FFF7ED' : '#FFFFFF',
                          cursor: 'pointer',
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          gap: 8,
                        }}
                      >
                        <div>
                          <div style={{ fontWeight: 700, fontSize: 13, color: '#0F172A' }}>
                            Block {b.blockCode}
                          </div>
                          <div style={{ fontSize: 11, color: '#64748B' }}>{b.dormName || b.blockName}</div>
                        </div>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: 10,
                            fontSize: 10,
                            fontWeight: 700,
                            background: done ? '#DCFCE7' : '#FEF3C7',
                            color: done ? '#15803D' : '#B45309',
                          }}
                        >
                          {done ? 'Đủ' : 'Thiếu'}
                        </span>
                      </button>
                    );
                  })
                )}
              </div>
            </div>

            {/* Right: 4 meter cards for selected block */}
            <div
              style={{
                background: '#fff',
                border: '1px solid #E2E8F0',
                borderRadius: 12,
                padding: 20,
                display: 'flex',
                flexDirection: 'column',
                gap: 16,
              }}
            >
              {blockFormLoading ? (
                <div style={{ padding: 40, textAlign: 'center', color: '#94A3B8', fontSize: 14 }}>
                  Đang tải công tơ của block {selectedBlockCode}...
                </div>
              ) : !blockFormData ? (
                <div style={{ padding: 40, textAlign: 'center', color: '#94A3B8', fontSize: 14 }}>
                  Chọn một Block ở cột bên trái để bắt đầu chụp ảnh công tơ.
                </div>
              ) : (
                <>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 8 }}>
                    <div>
                      <h3 style={{ margin: 0, fontSize: 17, fontWeight: 700, color: '#0F172A' }}>
                        Block {blockFormData.blockCode} — {blockFormData.displayPeriod}
                      </h3>
                      <div style={{ fontSize: 12, color: '#64748B', marginTop: 2 }}>
                        Chụp ảnh công tơ để AI tự điền chỉ số, kiểm tra lại rồi bấm Xác nhận lưu.
                      </div>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 14 }}>
                    {(
                      [
                        { key: 'electricityLeft' as const, title: 'Điện phòng Trái', meterType: 'Electricity' as const, unit: 'kWh', slot: blockFormData.electricityLeft },
                        { key: 'electricityRight' as const, title: 'Điện phòng Phải', meterType: 'Electricity' as const, unit: 'kWh', slot: blockFormData.electricityRight },
                        { key: 'waterLeft' as const, title: 'Nước phòng Trái', meterType: 'Water' as const, unit: 'm³', slot: blockFormData.waterLeft },
                        { key: 'waterRight' as const, title: 'Nước phòng Phải', meterType: 'Water' as const, unit: 'm³', slot: blockFormData.waterRight },
                      ]
                    )
                      .filter((item): item is typeof item & { slot: RoomOldReadingDto } => Boolean(item.slot))
                      .map(({ key, title, meterType, unit, slot }) => {
                        const state = captureSlots[key];
                        const isElec = meterType === 'Electricity';
                        const parsedNew = state.value.trim() === '' ? undefined : parseFloat(state.value);
                        const consumption = parsedNew !== undefined && !Number.isNaN(parsedNew) ? parsedNew - slot.oldReading : undefined;
                        const isNegative = consumption !== undefined && consumption < 0;
                        const isZero = consumption !== undefined && consumption === 0;

                        return (
                          <div
                            key={`${blockFormData.blockCode}-${key}`}
                            style={{
                              border: `1px solid ${isElec ? '#FED7AA' : '#BFDBFE'}`,
                              background: isElec ? '#FFFBF5' : '#F8FBFF',
                              borderRadius: 10,
                              padding: 14,
                              display: 'flex',
                              flexDirection: 'column',
                              gap: 10,
                            }}
                          >
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 6 }}>
                              <div>
                                <div style={{ fontWeight: 700, fontSize: 14, color: isElec ? '#C2410C' : '#1D4ED8' }}>
                                  {title} ({slot.roomNumber})
                                </div>
                                <div style={{ fontSize: 12, color: '#64748B' }}>
                                  Chỉ số cũ: <strong>{slot.oldReading} {unit}</strong>
                                </div>
                              </div>
                              <span
                                style={{
                                  padding: '2px 8px',
                                  borderRadius: 10,
                                  fontSize: 10,
                                  fontWeight: 700,
                                  background: state.evidenceUrl ? '#ECFDF5' : slot.alreadyRecorded ? '#EFF6FF' : '#F1F5F9',
                                  color: state.evidenceUrl ? '#047857' : slot.alreadyRecorded ? '#1D4ED8' : '#64748B',
                                }}
                              >
                                {state.evidenceUrl
                                  ? state.editedManually
                                    ? 'AI + Đã sửa tay'
                                    : 'AI Camera'
                                  : slot.alreadyRecorded
                                    ? 'Đã ghi trước đó'
                                    : 'Chưa ghi'}
                              </span>
                            </div>

                            <MeterPhotoScanner
                              key={`${blockFormData.blockCode}-${key}-${slot.roomId}`}
                              expectedMeterType={meterType}
                              previousValue={slot.oldReading}
                              saveEvidence
                              disabled={isClosed || savingBlock}
                              label={slot.roomNumber}
                              existingImageUrl={state.evidenceUrl || slot.existingEvidenceImageUrl}
                              onScanned={(res) => handleSlotScanned(key, res)}
                            />

                            <div>
                              <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#334155', marginBottom: 4 }}>
                                Chỉ số mới ({unit})
                              </label>
                              <input
                                type="number"
                                step="0.01"
                                min="0"
                                disabled={isClosed || savingBlock}
                                value={state.value}
                                onChange={(e) => handleSlotValueChange(key, e.target.value)}
                                placeholder={`Nhập >= ${slot.oldReading}`}
                                style={{
                                  width: '100%',
                                  padding: '8px 10px',
                                  borderRadius: 7,
                                  border: isNegative ? '1px solid #EF4444' : isZero ? '1px solid #F59E0B' : '1px solid #CBD5E1',
                                  background: isNegative ? '#FEF2F2' : '#FFFFFF',
                                  fontSize: 14,
                                  fontWeight: 700,
                                  color: '#0F172A',
                                  boxSizing: 'border-box',
                                }}
                              />
                              {consumption !== undefined && (
                                <div
                                  style={{
                                    marginTop: 4,
                                    fontSize: 12,
                                    fontWeight: 600,
                                    color: isNegative ? '#DC2626' : isZero ? '#B45309' : '#047857',
                                  }}
                                >
                                  Tiêu thụ dự kiến: {consumption.toFixed(2)} {unit}
                                  {isNegative ? ' (Nhỏ hơn chỉ số cũ!)' : isZero ? ' (Bằng 0 — kiểm tra lại)' : ''}
                                </div>
                              )}
                            </div>
                          </div>
                        );
                      })}
                  </div>

                  {/* Shared note & Save buttons */}
                  <div style={{ borderTop: '1px solid #E2E8F0', paddingTop: 14, display: 'flex', flexDirection: 'column', gap: 12 }}>
                    <div>
                      <label style={{ display: 'block', fontSize: 12, fontWeight: 600, color: '#374151', marginBottom: 4 }}>
                        Ghi chú chung cho Block (tùy chọn)
                      </label>
                      <input
                        type="text"
                        disabled={isClosed || savingBlock}
                        value={captureNote}
                        onChange={(e) => setCaptureNote(e.target.value)}
                        placeholder="VD: Đồng hồ kính hơi mờ, đã đối chiếu trực tiếp..."
                        style={{
                          width: '100%',
                          padding: '8px 12px',
                          borderRadius: 8,
                          border: '1px solid #CBD5E1',
                          fontSize: 13,
                          boxSizing: 'border-box',
                        }}
                      />
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, flexWrap: 'wrap' }}>
                      <button
                        type="button"
                        disabled={isClosed || savingBlock}
                        onClick={() => void handleConfirmBlockCapture(false)}
                        style={{
                          padding: '9px 18px',
                          borderRadius: 8,
                          border: '1px solid #D97026',
                          background: '#FFF7ED',
                          color: '#C2410C',
                          fontWeight: 700,
                          fontSize: 13,
                          cursor: isClosed || savingBlock ? 'not-allowed' : 'pointer',
                        }}
                      >
                        {savingBlock ? 'Đang lưu...' : 'Xác nhận lưu Block'}
                      </button>
                      <button
                        type="button"
                        disabled={isClosed || savingBlock}
                        onClick={() => void handleConfirmBlockCapture(true)}
                        style={{
                          padding: '9px 20px',
                          borderRadius: 8,
                          border: 'none',
                          background: isClosed || savingBlock ? '#94A3B8' : '#D97026',
                          color: '#FFFFFF',
                          fontWeight: 700,
                          fontSize: 13,
                          cursor: isClosed || savingBlock ? 'not-allowed' : 'pointer',
                        }}
                      >
                        {savingBlock ? 'Đang lưu...' : 'Lưu & sang Block tiếp theo →'}
                      </button>
                    </div>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

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
            {/* 0. Ký túc xá (Dorm) */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 12, fontWeight: 600, color: '#374151' }}>Dorm</label>
              <select
                value={usageFilter.dormName}
                onChange={(e) => setUsageFilter((prev) => ({ ...prev, dormName: e.target.value }))}
                style={{
                  padding: '8px 12px',
                  border: '1px solid #CBD5E1',
                  borderRadius: 8,
                  fontSize: 13,
                  minWidth: 145,
                  background: '#fff',
                }}
              >
                <option value="">Tất cả Dorm</option>
                {availableDorms.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            </div>

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
            {(usageFilter.dormName || usageFilter.termId || usageFilter.billingPeriodId || usageFilter.blockCode || usageFilter.meterType || usageFilter.month || usageFilter.year) && (
              <button
                onClick={() => {
                  setUsageFilter({ dormName: '', termId: '', billingPeriodId: '', blockCode: '', meterType: '', month: '', year: '' });
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

          {/* Kết quả banner */}
          <div style={{ fontSize: '18px', fontWeight: 700, color: '#1D4ED8', margin: '4px 0 0 2px' }}>
            Kết quả: <span style={{ fontWeight: 800 }}>{filteredUsageRows.length}</span>
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
            ) : filteredUsageRows.length === 0 ? (
              <div style={{ padding: 40, color: '#94a3b8', textAlign: 'center' }}>
                Không tìm thấy dữ liệu phù hợp. Hãy điều chỉnh bộ lọc hoặc chọn kỳ thanh toán khác.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
                <thead>
                  <tr style={{ background: '#F8FAFC', borderBottom: '1px solid #E2E8F0' }}>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '11%' }}>
                      Tên Dorm
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '9%' }}>
                      Tên block
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '10%' }}>
                      Loại sử dụng
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '11%' }}>
                      Ngày Tạo
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '13%' }}>
                      Học Kỳ
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '13%' }}>
                      Số công tơ (L - R)
                    </th>
                    <th style={{ padding: '12px 12px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '11%' }}>
                      Tiêu thụ tháng
                    </th>
                    <th style={{ padding: '12px 12px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '11%' }}>
                      Tổng tiêu thụ kỳ
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '6%' }}>
                      Đơn vị
                    </th>
                    <th style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#475569', width: '11%' }}>
                      Thao tác
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {paginatedUsageRows.map((row, idx) => {
                    const isElec = row.typeLabel === 'Điện';
                    return (
                      <tr
                        key={`${row.id}-${row.type}-${idx}`}
                        style={{
                          borderBottom: '1px solid #F1F5F9',
                          background: '#FFFFFF',
                          transition: 'background 0.15s ease',
                        }}
                        onMouseEnter={(e) => (e.currentTarget.style.background = '#F8FAFC')}
                        onMouseLeave={(e) => (e.currentTarget.style.background = '#FFFFFF')}
                      >
                        <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#1E293B' }}>
                          {row.dormName || 'Dom A'}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 600, color: '#1E293B' }}>
                          {row.block}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          {isElec ? (
                            <span
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 4,
                                padding: '2px 8px',
                                borderRadius: 6,
                                background: '#FFF7ED',
                                color: '#C2410C',
                                border: '1px solid #FED7AA',
                                fontWeight: 700,
                                fontSize: '12px',
                              }}
                            >
                              Điện
                            </span>
                          ) : (
                            <span
                              style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: 4,
                                padding: '2px 8px',
                                borderRadius: 6,
                                background: '#EFF6FF',
                                color: '#1D4ED8',
                                border: '1px solid #BFDBFE',
                                fontWeight: 700,
                                fontSize: '12px',
                              }}
                            >
                              Nước
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center', color: '#475569' }}>
                          {row.ngayTao}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center', color: '#475569' }}>
                          {row.hocKy}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center', fontWeight: 500, color: '#1E293B' }}>
                          {row.meterNumberDisplay}
                        </td>
                        <td
                          style={{
                            padding: '12px 12px',
                            textAlign: 'center',
                            fontWeight: 700,
                            fontSize: '13px',
                            color: isElec ? '#C2410C' : '#1D4ED8',
                          }}
                        >
                          {row.currentMonthConsumption}
                        </td>
                        <td
                          style={{
                            padding: '12px 12px',
                            textAlign: 'center',
                            fontWeight: 700,
                            fontSize: '13px',
                            color: '#0F172A',
                          }}
                        >
                          {row.totalConsumption}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center', color: '#64748B' }}>
                          {row.donvi}
                        </td>
                        <td style={{ padding: '12px 14px', textAlign: 'center' }}>
                          <button
                            onClick={() => openDetailModal(row)}
                            style={{
                              background: '#EFF6FF',
                              border: '1px solid #BFDBFE',
                              borderRadius: 6,
                              color: '#1D4ED8',
                              fontWeight: 600,
                              fontSize: '12px',
                              cursor: 'pointer',
                              padding: '4px 12px',
                              transition: 'all 0.15s ease',
                            }}
                            onMouseEnter={(e) => {
                              e.currentTarget.style.background = '#DBEAFE';
                              e.currentTarget.style.color = '#1E40AF';
                            }}
                            onMouseLeave={(e) => {
                              e.currentTarget.style.background = '#EFF6FF';
                              e.currentTarget.style.color = '#1D4ED8';
                            }}
                          >
                            Chỉnh
                          </button>
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
            itemName="bản ghi"
          />

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
                        <strong>Chỉnh sửa trực tiếp:</strong> Bạn có thể gõ sửa trực tiếp chỉ số tại cột <strong>Giá trị L</strong> và <strong>Giá trị R</strong> bên dưới. Mức tiêu thụ sẽ được tự động tính toán lại tức thì trước khi bạn bấm <strong>Xác nhận lưu</strong>.
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
          SECTION: CẢNH BÁO BẤT THƯỜNG (MANAGER ADJUST / CONFIRM)
          ════════════════════════════════════════════════ */}
      {activeSection === 'anomalies' && isManager && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Workflow Banner */}
          <div
            style={{
              background: '#FFF7ED',
              border: '1px solid #FDBA74',
              borderRadius: 12,
              padding: '14px 18px',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: 12,
            }}
          >
            <div style={{ flex: 1, minWidth: 280 }}>
              <div style={{ fontWeight: 700, fontSize: 14, color: '#9A3412', marginBottom: 4 }}>
                Quy trình xử lý cảnh báo bất thường (Điện vượt &gt; 1,000 kWh / Nước vượt &gt; 50 m³ sau định mức)
              </div>
              <div style={{ fontSize: 12.5, color: '#C2410C', lineHeight: 1.55 }}>
                1. Liên hệ Bảo vệ tòa nhà để kiểm tra phòng (rò rỉ nước / thiết bị cấm như bếp từ).
                <br />
                2. Bấm <strong>Xác nhận (Confirm)</strong> nếu sinh viên dùng thật, hoặc <strong>Điều chỉnh (Adjust)</strong> nếu do rò rỉ/nhập sai. Hệ thống sẽ tự động tính phụ trội, tạo hóa đơn và gửi thông báo công nợ ngay khi xử lý xong.
              </div>
            </div>

            {selectedPeriodId && (
              <button
                onClick={handleCalculateSurcharges}
                disabled={calcLoading}
                title="Chạy đồng bộ / tính lại phụ trội và tạo hóa đơn còn thiếu cho toàn bộ phòng hợp lệ (không bị Flagged) trong kỳ này"
                style={{
                  padding: '8px 14px',
                  borderRadius: 8,
                  border: 'none',
                  background: '#D97026',
                  color: '#fff',
                  fontWeight: 700,
                  fontSize: 12.5,
                  cursor: calcLoading ? 'not-allowed' : 'pointer',
                  whiteSpace: 'nowrap',
                }}
              >
                {calcLoading ? 'Đang đồng bộ...' : 'Đồng bộ / Tính lại phụ trội kỳ này'}
              </button>
            )}
          </div>

          {/* Filter Bar */}
          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              padding: '14px 16px',
              display: 'flex',
              flexWrap: 'wrap',
              gap: 10,
              alignItems: 'flex-end',
            }}
          >
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 11.5, fontWeight: 600, color: '#475569' }}>Kỳ thanh toán</label>
              <select
                value={selectedPeriodId}
                onChange={(e) => setSelectedPeriodId(e.target.value ? Number(e.target.value) : '')}
                style={{
                  padding: '7px 10px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#fff',
                  fontSize: 13,
                  fontWeight: 600,
                  minWidth: 190,
                }}
              >
                <option value="">Tất cả kỳ thanh toán</option>
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.displayName} ({p.termName})
                  </option>
                ))}
              </select>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 11.5, fontWeight: 600, color: '#475569' }}>Khu KTX (Dorm)</label>
              <select
                value={anomalyDormFilter}
                onChange={(e) => setAnomalyDormFilter(e.target.value)}
                style={{
                  padding: '7px 10px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#fff',
                  fontSize: 13,
                  minWidth: 145,
                }}
              >
                <option value="">Tất cả Dorm</option>
                {availableAnomalyDorms.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 11.5, fontWeight: 600, color: '#475569' }}>Phòng / Block</label>
              <input
                type="text"
                value={anomalyRoomSearch}
                onChange={(e) => setAnomalyRoomSearch(e.target.value)}
                placeholder="VD: 202R, 101..."
                style={{
                  padding: '7px 10px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#fff',
                  fontSize: 13,
                  width: 140,
                }}
              />
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 11.5, fontWeight: 600, color: '#475569' }}>Loại bất thường</label>
              <select
                value={anomalyMeterFilter}
                onChange={(e) => setAnomalyMeterFilter(e.target.value)}
                style={{
                  padding: '7px 10px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#fff',
                  fontSize: 13,
                  minWidth: 145,
                }}
              >
                <option value="">Tất cả (Điện & Nước)</option>
                <option value="Electricity">Có Điện bất thường</option>
                <option value="Water">Có Nước bất thường</option>
                <option value="Both">Cả Điện & Nước</option>
              </select>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label style={{ fontSize: 11.5, fontWeight: 600, color: '#475569' }}>Trạng thái</label>
              <select
                value={anomalyStatusFilter}
                onChange={(e) => setAnomalyStatusFilter(e.target.value)}
                style={{
                  padding: '7px 10px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#fff',
                  fontSize: 13,
                  fontWeight: 600,
                  minWidth: 165,
                }}
              >
                <option value="">Tất cả trạng thái</option>
                <option value="Flagged">Đang chờ xử lý (Flagged)</option>
                <option value="Resolved">Đã xử lý (Resolved)</option>
              </select>
            </div>

            <div style={{ display: 'flex', gap: 8, marginLeft: 'auto' }}>
              {(anomalyDormFilter || anomalyRoomSearch || anomalyMeterFilter || anomalyStatusFilter !== 'Flagged') && (
                <button
                  onClick={() => {
                    setAnomalyDormFilter('');
                    setAnomalyRoomSearch('');
                    setAnomalyMeterFilter('');
                    setAnomalyStatusFilter('Flagged');
                  }}
                  style={{
                    padding: '7px 12px',
                    borderRadius: 8,
                    border: '1px solid #CBD5E1',
                    background: '#F8FAFC',
                    color: '#64748B',
                    fontWeight: 600,
                    fontSize: 12.5,
                    cursor: 'pointer',
                  }}
                >
                  Xóa lọc
                </button>
              )}
              <button
                onClick={loadAnomalies}
                style={{
                  padding: '7px 14px',
                  borderRadius: 8,
                  border: '1px solid #CBD5E1',
                  background: '#fff',
                  color: '#374151',
                  fontWeight: 600,
                  fontSize: 12.5,
                  cursor: 'pointer',
                }}
              >
                Làm mới
              </button>
            </div>
          </div>

          {/* Summary count */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: 13, color: '#475569', padding: '0 2px' }}>
            <span>
              Hiển thị <strong style={{ color: '#0F172A' }}>{groupedAnomalies.length}</strong> phòng có cảnh báo
              {anomalies.length > 0 && (
                <span> ({anomalies.length} chỉ số bất thường)</span>
              )}
            </span>
          </div>

          {anomalyMsg && (
            <div
              style={{
                padding: '12px 16px',
                borderRadius: 10,
                background: anomalyMsg.type === 'success' ? '#F0FDF4' : '#FEF2F2',
                border: `1px solid ${anomalyMsg.type === 'success' ? '#86EFAC' : '#FCA5A5'}`,
                color: anomalyMsg.type === 'success' ? '#166534' : '#991B1B',
                fontWeight: 600,
                fontSize: 13,
              }}
            >
              {anomalyMsg.text}
            </div>
          )}

          {/* Anomalies Table (Grouped by Room, 100% width fit) */}
          <div
            style={{
              background: '#fff',
              border: '1px solid #E2E8F0',
              borderRadius: 12,
              overflow: 'hidden',
              boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
            }}
          >
            {anomaliesLoading ? (
              <div style={{ padding: 28, textAlign: 'center', color: '#64748B' }}>
                Đang tải danh sách cảnh báo bất thường...
              </div>
            ) : groupedAnomalies.length === 0 ? (
              <div style={{ padding: 36, textAlign: 'center', color: '#64748B' }}>
                Không có phòng nào có cảnh báo bất thường theo bộ lọc hiện tại.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed', fontSize: 12.5 }}>
                <thead>
                  <tr style={{ background: '#F8FAFC', borderBottom: '2px solid #E2E8F0' }}>
                    <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 700, color: '#374151', width: '19%' }}>
                      Phòng / KTX &amp; Bảo vệ
                    </th>
                    <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 700, color: '#374151', width: '24%' }}>
                      Loại &amp; Chỉ số tiêu thụ
                    </th>
                    <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 700, color: '#374151', width: '33%' }}>
                      Lý do cảnh báo
                    </th>
                    <th style={{ padding: '10px 12px', textAlign: 'left', fontWeight: 700, color: '#374151', width: '24%' }}>
                      Trạng thái &amp; Thao tác
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {paginatedAnomalyGroups.map((group, gIdx) => {
                    const rowBg = gIdx % 2 === 0 ? '#FFFFFF' : '#F8FAFC';
                    return group.items.map((item, itemIdx) => {
                      const isElec =
                        (item.meterType || '').toLowerCase().includes('elec') ||
                        (item.meterType || '').toLowerCase().includes('điện');
                      const unit = isElec ? 'kWh' : 'm³';
                      const isFlagged = item.status === 'Flagged';
                      const isLastInGroup = itemIdx === group.items.length - 1;

                      return (
                        <tr
                          key={item.anomalyId}
                          style={{
                            background: rowBg,
                            borderBottom: isLastInGroup ? '2px solid #E2E8F0' : '1px dashed #CBD5E1',
                          }}
                        >
                          {itemIdx === 0 && (
                            <td
                              rowSpan={group.items.length}
                              style={{
                                padding: '12px',
                                verticalAlign: 'top',
                                borderRight: '1px solid #F1F5F9',
                                wordBreak: 'break-word',
                              }}
                            >
                              <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                                <span style={{ fontWeight: 800, fontSize: 14, color: '#0F172A' }}>
                                  Phòng {group.roomNumber}
                                </span>
                                {group.hasElec && group.hasWater && (
                                  <span
                                    style={{
                                      padding: '1px 6px',
                                      borderRadius: 6,
                                      fontSize: 10,
                                      fontWeight: 700,
                                      background: '#FEF2F2',
                                      color: '#B91C1C',
                                      border: '1px solid #FECACA',
                                    }}
                                  >
                                    Điện + Nước
                                  </span>
                                )}
                              </div>
                              <div style={{ fontSize: 12, color: '#475569', marginTop: 2 }}>
                                {group.dormName} • {group.billingPeriodDisplay}
                              </div>
                              <div style={{ marginTop: 8, paddingTop: 6, borderTop: '1px solid #F1F5F9', fontSize: 11.5 }}>
                                <div style={{ fontWeight: 600, color: '#334155' }}>BV: {group.guardName}</div>
                                {group.guardPhone && (
                                  <div style={{ color: '#0284C7', fontWeight: 600 }}>SĐT: {group.guardPhone}</div>
                                )}
                              </div>
                            </td>
                          )}

                          <td style={{ padding: '10px 12px', verticalAlign: 'top', wordBreak: 'break-word' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4, flexWrap: 'wrap' }}>
                              <span
                                style={{
                                  padding: '2px 8px',
                                  borderRadius: 10,
                                  fontSize: 11,
                                  fontWeight: 700,
                                  background: isElec ? '#FEF3C7' : '#DBEAFE',
                                  color: isElec ? '#B45309' : '#1D4ED8',
                                }}
                              >
                                {isElec ? 'Điện' : 'Nước'}
                              </span>
                              <span style={{ fontWeight: 600, color: '#334155' }}>
                                {item.previousValue} → <span style={{ color: '#DC2626' }}>{item.currentValue}</span>
                              </span>
                            </div>
                            <div style={{ fontWeight: 700, color: '#0F172A' }}>
                              Tiêu thụ: {item.consumption.toLocaleString('vi-VN')} {unit}
                            </div>
                            <div style={{ fontSize: 11, color: item.overUsage > 0 ? '#DC2626' : '#64748B', marginTop: 2 }}>
                              Định mức: {item.quota} {unit} | Vượt: {item.overUsage > 0 ? `+${item.overUsage.toLocaleString('vi-VN')}` : 0} {unit}
                            </div>
                          </td>

                          <td style={{ padding: '10px 12px', verticalAlign: 'top', color: '#991B1B', lineHeight: 1.45, wordBreak: 'break-word' }}>
                            <div style={{ fontWeight: 500 }}>{item.flagReason}</div>
                            {item.managerNote && (
                              <div style={{ fontSize: 11, color: '#475569', marginTop: 4, fontStyle: 'italic' }}>
                                Ghi chú QL: {item.managerNote}
                              </div>
                            )}
                          </td>

                          <td style={{ padding: '10px 12px', verticalAlign: 'top' }}>
                            <div style={{ marginBottom: 8 }}>
                              <span
                                style={{
                                  display: 'inline-block',
                                  padding: '3px 8px',
                                  borderRadius: 10,
                                  fontSize: 11,
                                  fontWeight: 700,
                                  background: isFlagged ? '#FEE2E2' : '#DCFCE7',
                                  color: isFlagged ? '#991B1B' : '#166534',
                                }}
                              >
                                {isFlagged
                                  ? 'Chờ kiểm tra (Flagged)'
                                  : item.resolution === 'Corrected'
                                  ? 'Đã điều chỉnh'
                                  : 'Đã xác nhận'}
                              </span>
                            </div>
                            {isFlagged ? (
                              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                                <button
                                  onClick={() => handleOpenResolveModal(item, 'Confirm')}
                                  style={{
                                    padding: '5px 10px',
                                    borderRadius: 6,
                                    border: '1px solid #16A34A',
                                    background: '#F0FDF4',
                                    color: '#15803D',
                                    fontWeight: 700,
                                    fontSize: 11.5,
                                    cursor: 'pointer',
                                  }}
                                  title="Xác nhận chỉ số đúng (sinh viên dùng bếp từ / dùng nhiều thật) và tính phụ trội"
                                >
                                  Xác nhận
                                </button>
                                <button
                                  onClick={() => handleOpenResolveModal(item, 'Adjust')}
                                  style={{
                                    padding: '5px 10px',
                                    borderRadius: 6,
                                    border: '1px solid #D97026',
                                    background: '#FFF7ED',
                                    color: '#C2410C',
                                    fontWeight: 700,
                                    fontSize: 11.5,
                                    cursor: 'pointer',
                                  }}
                                  title="Điều chỉnh lại chỉ số (do rò rỉ nước hoặc bảo vệ ghi nhầm) rồi tính phụ trội"
                                >
                                  Điều chỉnh
                                </button>
                              </div>
                            ) : (
                              <div style={{ fontSize: 11.5, color: '#64748B' }}>
                                Đã chốt {item.resolvedByName ? `bởi ${item.resolvedByName}` : ''}
                              </div>
                            )}
                          </td>
                        </tr>
                      );
                    });
                  })}
                </tbody>
              </table>
            )}
          </div>

          {/* Pagination */}
          {groupedAnomalies.length > 0 && (
            <Pagination
              currentPage={anomalyPage}
              totalItems={groupedAnomalies.length}
              pageSize={anomalyPageSize}
              onPageChange={setAnomalyPage}
              itemName="phòng"
            />
          )}

          {/* Modal Adjust or Confirm Utility Data */}
          {resolvingAnomaly && (
            <div
              style={{
                position: 'fixed',
                inset: 0,
                background: 'rgba(15, 23, 42, 0.5)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                zIndex: 1100,
                padding: 16,
              }}
            >
              <div
                style={{
                  background: '#fff',
                  borderRadius: 14,
                  maxWidth: 520,
                  width: '100%',
                  padding: '24px 26px',
                  boxShadow: '0 20px 25px -5px rgba(0,0,0,0.15)',
                }}
              >
                <h3 style={{ margin: '0 0 8px', fontSize: 18, fontWeight: 800, color: '#0F172A' }}>
                  {resolveAction === 'Confirm'
                    ? 'Xác nhận chỉ số bất thường (Confirm Utility Data)'
                    : 'Điều chỉnh chỉ số điện/nước (Adjust Utility Data)'}
                </h3>
                <p style={{ margin: '0 0 16px', fontSize: 13, color: '#475569', lineHeight: 1.5 }}>
                  Phòng <strong>{resolvingAnomaly.roomNumber}</strong> ({resolvingAnomaly.dormName}) —{' '}
                  <strong>{resolvingAnomaly.meterType === 'Electricity' ? 'Điện' : 'Nước'}</strong> ({resolvingAnomaly.billingPeriodDisplay})
                  <br />
                  <span style={{ color: '#B91C1C' }}>Cảnh báo: {resolvingAnomaly.flagReason}</span>
                </p>

                <div style={{ display: 'flex', gap: 10, marginBottom: 16 }}>
                  <button
                    type="button"
                    onClick={() => setResolveAction('Confirm')}
                    style={{
                      flex: 1,
                      padding: '10px 12px',
                      borderRadius: 8,
                      border: resolveAction === 'Confirm' ? '2px solid #16A34A' : '1px solid #CBD5E1',
                      background: resolveAction === 'Confirm' ? '#F0FDF4' : '#fff',
                      color: resolveAction === 'Confirm' ? '#15803D' : '#475569',
                      fontWeight: 700,
                      fontSize: 13,
                      cursor: 'pointer',
                    }}
                  >
                    Giữ nguyên chỉ số (Confirm)
                    <div style={{ fontSize: 11, fontWeight: 500, marginTop: 2 }}>
                      SV dùng nhiều thật / dùng bếp từ
                    </div>
                  </button>
                  <button
                    type="button"
                    onClick={() => setResolveAction('Adjust')}
                    style={{
                      flex: 1,
                      padding: '10px 12px',
                      borderRadius: 8,
                      border: resolveAction === 'Adjust' ? '2px solid #D97026' : '1px solid #CBD5E1',
                      background: resolveAction === 'Adjust' ? '#FFF7ED' : '#fff',
                      color: resolveAction === 'Adjust' ? '#C2410C' : '#475569',
                      fontWeight: 700,
                      fontSize: 13,
                      cursor: 'pointer',
                    }}
                  >
                    Sửa lại chỉ số (Adjust)
                    <div style={{ fontSize: 11, fontWeight: 500, marginTop: 2 }}>
                      Do rò rỉ nước / bảo vệ ghi nhầm
                    </div>
                  </button>
                </div>

                <div style={{ marginBottom: 14, fontSize: 13, color: '#334155' }}>
                  Chỉ số kỳ trước: <strong>{resolvingAnomaly.previousValue}</strong> | Chỉ số bảo vệ nhập:{' '}
                  <strong>{resolvingAnomaly.currentValue}</strong> (Tiêu thụ: <strong>{resolvingAnomaly.consumption}</strong>)
                </div>

                {resolveAction === 'Adjust' && (
                  <div style={{ marginBottom: 14 }}>
                    <label style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#374151', marginBottom: 6 }}>
                      Chỉ số mới sau khi kiểm tra (&ge; {resolvingAnomaly.previousValue}) *
                    </label>
                    <input
                      type="number"
                      value={resolveValue}
                      onChange={(e) => setResolveValue(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '9px 12px',
                        borderRadius: 8,
                        border: '1px solid #CBD5E1',
                        fontSize: 14,
                        fontWeight: 600,
                        boxSizing: 'border-box',
                      }}
                    />
                  </div>
                )}

                <div style={{ marginBottom: 18 }}>
                  <label style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#374151', marginBottom: 6 }}>
                    Ghi chú kết quả kiểm tra từ Bảo vệ
                  </label>
                  <textarea
                    rows={3}
                    value={resolveNote}
                    onChange={(e) => setResolveNote(e.target.value)}
                    placeholder={
                      resolveAction === 'Confirm'
                        ? 'VD: Đã gọi bảo vệ kiểm tra phòng, xác nhận sinh viên sử dụng bếp từ nấu ăn...'
                        : 'VD: Bảo vệ kiểm tra phát hiện rò rỉ van nước nhà vệ sinh, điều chỉnh lại chỉ số...'
                    }
                    style={{
                      width: '100%',
                      padding: '9px 12px',
                      borderRadius: 8,
                      border: '1px solid #CBD5E1',
                      fontSize: 13,
                      boxSizing: 'border-box',
                    }}
                  />
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                  <button
                    type="button"
                    onClick={() => setResolvingAnomaly(null)}
                    disabled={resolveLoading}
                    style={{
                      padding: '8px 16px',
                      borderRadius: 8,
                      border: '1px solid #CBD5E1',
                      background: '#fff',
                      color: '#475569',
                      fontWeight: 600,
                      fontSize: 13,
                      cursor: 'pointer',
                    }}
                  >
                    Hủy
                  </button>
                  <button
                    type="button"
                    onClick={handleSubmitResolveAnomaly}
                    disabled={resolveLoading}
                    style={{
                      padding: '8px 18px',
                      borderRadius: 8,
                      border: 'none',
                      background: resolveAction === 'Confirm' ? '#16A34A' : '#D97026',
                      color: '#fff',
                      fontWeight: 700,
                      fontSize: 13,
                      cursor: resolveLoading ? 'not-allowed' : 'pointer',
                    }}
                  >
                    {resolveLoading
                      ? 'Đang xử lý...'
                      : resolveAction === 'Confirm'
                      ? 'Xác nhận & Tính phụ trội'
                      : 'Lưu điều chỉnh & Tính phụ trội'}
                  </button>
                </div>
              </div>
            </div>
          )}
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
                  Block {detailGroup.block} ({detailGroup.block} - {detailGroup.dormName || 'Dom A'})
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
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16, marginBottom: 24 }}>
              {detailGroup.rows.map((r, ri) => {
                const isElec = r.typeLabel === 'Điện' || r.type.toLowerCase().includes('electric') || r.type.toLowerCase().includes('điện');
                const unitStr = isElec ? 'kWh' : 'm³';
                const isOver = r.overUsage > 0;
                return (
                  <div
                    key={`${r.type}-${r.monthYear}-${ri}`}
                    style={{
                      background: isElec ? '#FFF7ED' : '#EFF6FF',
                      border: `1px solid ${isElec ? '#FED7AA' : '#BFDBFE'}`,
                      borderRadius: 12,
                      padding: 18,
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
                      <span style={{ fontSize: 14, fontWeight: 700, color: isElec ? '#C2410C' : '#1D4ED8' }}>
                        Đồng hồ {r.typeLabel} ({r.monthYear})
                      </span>
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          padding: '3px 10px',
                          borderRadius: 12,
                          background: isOver ? '#FEE2E2' : '#DCFCE7',
                          color: isOver ? '#DC2626' : '#15803D',
                        }}
                      >
                        {isOver ? 'Vượt định mức' : 'Trong định mức'}
                      </span>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12, marginBottom: 12 }}>
                      <div style={{ background: '#FFFFFF', padding: '10px 14px', borderRadius: 8, border: '1px solid #E2E8F0' }}>
                        <div style={{ fontSize: 11, color: '#64748B', fontWeight: 600, textTransform: 'uppercase' }}>
                          Tiêu thụ tháng này
                        </div>
                        <div style={{ fontSize: 20, fontWeight: 800, color: isElec ? '#C2410C' : '#1D4ED8', marginTop: 4 }}>
                          {(r.currentMonthConsumption ?? 0).toFixed(2)}{' '}
                          <span style={{ fontSize: 12, fontWeight: 500, color: '#64748B' }}>{unitStr}</span>
                        </div>
                        <div style={{ fontSize: 10, color: '#94A3B8', marginTop: 2 }}>
                          (Tổng công tơ mới nhất)
                        </div>
                      </div>

                      <div style={{ background: '#FFFFFF', padding: '10px 14px', borderRadius: 8, border: '1px solid #E2E8F0' }}>
                        <div style={{ fontSize: 11, color: '#64748B', fontWeight: 600, textTransform: 'uppercase' }}>
                          Tổng tiêu thụ cả kỳ
                        </div>
                        <div style={{ fontSize: 20, fontWeight: 800, color: '#0F172A', marginTop: 4 }}>
                          {r.totalConsumption.toFixed(2)}{' '}
                          <span style={{ fontSize: 12, fontWeight: 500, color: '#64748B' }}>{unitStr}</span>
                        </div>
                        <div style={{ fontSize: 10, color: '#94A3B8', marginTop: 2 }}>
                          (Cộng dồn tất cả tháng)
                        </div>
                      </div>
                    </div>

                    <div style={{ fontSize: 12, color: '#475569', display: 'flex', justifyContent: 'space-between', borderTop: '1px dashed #CBD5E1', paddingTop: 8 }}>
                      <span>Định mức miễn phí:</span>
                      <strong>{r.quota} {unitStr}</strong>
                    </div>
                    <div style={{ fontSize: 12, color: isOver ? '#DC2626' : '#15803D', display: 'flex', justifyContent: 'space-between', marginTop: 4, fontWeight: 700 }}>
                      <span>Chênh lệch:</span>
                      <span>{isOver ? `+${r.overUsage.toFixed(2)}` : r.overUsage.toFixed(2)} {unitStr}</span>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Meter Readings Table */}
            <div style={{ marginBottom: 20 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}>
                <h4 style={{ fontSize: 14, fontWeight: 700, color: '#0F172A', margin: 0 }}>
                  Chỉ số công tơ mới nhất từng phòng (Phòng Trái & Phải)
                </h4>
                <span style={{ fontSize: 12, color: '#64748B' }}>
                  Chỉ hiển thị và cho phép <strong>"Sửa"</strong> chỉ số công tơ mới nhất đã nhập
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
                const rawReadings = detailGroup.rows.flatMap((r) => r.readings || []);
                if (rawReadings.length === 0) {
                  return (
                    <div style={{ padding: 16, background: '#F8FAFC', borderRadius: 8, textAlign: 'center', color: '#94A3B8', fontSize: 13 }}>
                      Không có bản ghi công tơ chi tiết riêng rẽ cho block này.
                    </div>
                  );
                }

                // Chỉ hiển thị chỉ số mới nhất (lần ghi cuối cùng) của từng phòng
                const roomLatestMap = new Map<string, EwReadingDetailItem>();
                const sorted = [...rawReadings].sort((a, b) => {
                  const tA = a.recordedTime ? new Date(a.recordedTime).getTime() : 0;
                  const tB = b.recordedTime ? new Date(b.recordedTime).getTime() : 0;
                  if (tA !== tB) return tA - tB;
                  return a.readingId - b.readingId;
                });

                for (const item of sorted) {
                  const key = `${item.roomId || item.roomNumber}-${item.meterType}`;
                  roomLatestMap.set(key, item);
                }

                const latestReadings = Array.from(roomLatestMap.values()).sort((a, b) =>
                  a.roomNumber.localeCompare(b.roomNumber)
                );

                return (
                  <div style={{ border: '1px solid #E2E8F0', borderRadius: 8, overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}>
                      <thead>
                        <tr style={{ background: '#F8FAFC' }}>
                          <th style={{ padding: '8px 10px', textAlign: 'left', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Phòng</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Loại</th>
                          <th style={{ padding: '8px 10px', textAlign: 'right', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Chỉ số cũ</th>
                          <th style={{ padding: '8px 10px', textAlign: 'right', color: '#475569', borderBottom: '1px solid #E2E8F0', width: 110 }}>Chỉ số mới</th>
                          <th style={{ padding: '8px 10px', textAlign: 'right', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Tiêu thụ</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Nguồn</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Ảnh công tơ</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Trạng thái</th>
                          <th style={{ padding: '8px 10px', textAlign: 'left', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Người ghi</th>
                          <th style={{ padding: '8px 10px', textAlign: 'left', color: '#475569', borderBottom: '1px solid #E2E8F0' }}>Thời gian</th>
                          <th style={{ padding: '8px 10px', textAlign: 'center', color: '#475569', borderBottom: '1px solid #E2E8F0', width: 100 }}>Thao tác</th>
                        </tr>
                      </thead>
                      <tbody>
                        {latestReadings.map((m, idx) => {
                          const isEditing = editingReadingId === m.readingId;

                          return (
                            <tr key={idx} style={{ background: idx % 2 === 0 ? '#fff' : '#FAFAFA', borderBottom: '1px solid #F1F5F9' }}>
                              <td style={{ padding: '8px 10px', fontWeight: 700, color: '#0F172A' }}>{m.roomNumber}</td>
                              <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                                <span
                                  style={{
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    gap: 3,
                                    padding: '2px 8px',
                                    borderRadius: 6,
                                    fontSize: 11,
                                    fontWeight: 700,
                                    background: (m.meterType || '').toLowerCase().includes('electric') || (m.meterType || '').toLowerCase().includes('điện') ? '#FFF7ED' : '#EFF6FF',
                                    color: (m.meterType || '').toLowerCase().includes('electric') || (m.meterType || '').toLowerCase().includes('điện') ? '#C2410C' : '#1D4ED8',
                                    border: (m.meterType || '').toLowerCase().includes('electric') || (m.meterType || '').toLowerCase().includes('điện') ? '1px solid #FED7AA' : '1px solid #BFDBFE',
                                  }}
                                >
                                  {(m.meterType || '').toLowerCase().includes('electric') || (m.meterType || '').toLowerCase().includes('điện') ? 'Điện' : 'Nước'}
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

                              <td
                                style={{
                                  padding: '8px 10px',
                                  textAlign: 'right',
                                  fontWeight: 800,
                                  color: (m.meterType || '').toLowerCase().includes('electric') || (m.meterType || '').toLowerCase().includes('điện') ? '#C2410C' : '#1D4ED8',
                                }}
                              >
                                {isEditing && !isNaN(parseFloat(editReadingValue))
                                  ? (parseFloat(editReadingValue) - m.previousValue).toFixed(2)
                                  : m.consumption.toFixed(2)}
                              </td>
                              <td style={{ padding: '8px 10px', textAlign: 'center', whiteSpace: 'nowrap' }}>
                                <span
                                  style={{
                                    display: 'inline-block',
                                    padding: '2px 7px',
                                    borderRadius: 6,
                                    fontSize: 10,
                                    fontWeight: 700,
                                    background: m.source === 'PhotoOCR' ? '#ECFDF5' : m.source === 'Import' ? '#EFF6FF' : '#F1F5F9',
                                    color: m.source === 'PhotoOCR' ? '#047857' : m.source === 'Import' ? '#1D4ED8' : '#475569',
                                  }}
                                >
                                  {m.source === 'PhotoOCR'
                                    ? 'AI Camera'
                                    : m.source === 'Import'
                                      ? 'Excel'
                                      : m.source === 'Handover'
                                        ? 'Bàn giao'
                                        : 'Nhập tay'}
                                </span>
                              </td>
                              <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                                {m.evidenceImageUrl ? (
                                  <button
                                    type="button"
                                    onClick={() =>
                                      setLightboxImage({
                                        url: m.evidenceImageUrl!,
                                        title: `Ảnh công tơ phòng ${m.roomNumber} (${(m.meterType || '').toLowerCase().includes('electric') ? 'Điện' : 'Nước'})`,
                                        subtitle: `Chỉ số: ${m.previousValue.toFixed(2)} → ${m.currentValue.toFixed(2)}`,
                                      })
                                    }
                                    style={{
                                      display: 'inline-flex',
                                      alignItems: 'center',
                                      gap: 4,
                                      padding: '3px 8px',
                                      borderRadius: 6,
                                      border: '1px solid #BFDBFE',
                                      background: '#EFF6FF',
                                      color: '#1D4ED8',
                                      fontSize: 11,
                                      fontWeight: 600,
                                      cursor: 'pointer',
                                      whiteSpace: 'nowrap',
                                    }}
                                  >
                                    Xem ảnh
                                  </button>
                                ) : (
                                  <span style={{ color: '#94A3B8' }}>—</span>
                                )}
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

      <ImageLightboxModal
        imageUrl={lightboxImage?.url ?? null}
        title={lightboxImage?.title}
        subtitle={lightboxImage?.subtitle}
        onClose={() => setLightboxImage(null)}
      />
    </div>
  );
};
