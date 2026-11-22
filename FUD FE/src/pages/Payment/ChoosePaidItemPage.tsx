import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  paymentApi,
  type StudentInvoiceDto,
  type PaymentTransactionDto,
  type PaymentSuccessDto,
  type PaymentHistoryItemDto,
  type PaymentNavigationParams,
} from '../../services/api';
import { formatDate, formatDateTime } from '../../utils/dateUtils';
import { usePopup } from '../../components/PopupDialog';
import '../../styles/violation.css';

type FilterTab = 'All' | 'RoomFee' | 'Utility' | 'BedTransfer' | 'Compensation';

const INVOICE_TYPE_TABS: { id: FilterTab; label: string }[] = [
  { id: 'All', label: 'Tất cả' },
  { id: 'RoomFee', label: 'Tiền phòng (RoomFee)' },
  { id: 'Utility', label: 'Điện nước (Utility)' },
  { id: 'BedTransfer', label: 'Chuyển giường (BedTransfer)' },
  { id: 'Compensation', label: 'Bồi thường / Trả phòng (Compensation)' },
];

export const INVOICE_TYPE_META: Record<string, { label: string; bg: string; color: string; border: string }> = {
  RoomFee: { label: 'Tiền phòng nội trú', bg: '#FFF7ED', color: '#C2410C', border: '#FED7AA' },
  Utility: { label: 'Tiền điện / nước', bg: '#EFF6FF', color: '#1D4ED8', border: '#BFDBFE' },
  BedTransfer: { label: 'Phí chuyển giường', bg: '#F0FDF4', color: '#15803D', border: '#BBF7D0' },
  Compensation: { label: 'Bồi thường / Trả phòng', bg: '#FEF2F2', color: '#B91C1C', border: '#FECACA' },
};

export const INVOICE_STATUS_META: Record<string, { label: string; bg: string; color: string }> = {
  Unpaid: { label: 'Chưa thanh toán', bg: '#FEF3C7', color: '#92400E' },
  Overdue: { label: 'Quá hạn thanh toán', bg: '#FEE2E2', color: '#B91C1C' },
  Processing: { label: 'Đang chờ quét QR', bg: '#DBEAFE', color: '#1D4ED8' },
  Paid: { label: 'Đã thanh toán', bg: '#DCFCE7', color: '#15803D' },
  Cancelled: { label: 'Đã hủy', bg: '#F1F5F9', color: '#475569' },
};

const SOURCE_TYPE_LABELS: Record<string, string> = {
  Booking: 'Đơn đặt phòng',
  Keeping: 'Đơn giữ phòng',
  Utility: 'Hóa đơn điện nước',
  BedTransfer: 'Đơn chuyển giường',
  Checkout: 'Bàn giao / Trả phòng',
};

const TX_CACHE_KEY_PREFIX = 'fud_active_qr_tx_';

function saveTxToSession(tx: PaymentTransactionDto) {
  try {
    sessionStorage.setItem(`${TX_CACHE_KEY_PREFIX}${tx.id}`, JSON.stringify(tx));
  } catch {
    // ignore storage errors
  }
}

function loadTxFromSession(txId: number): PaymentTransactionDto | null {
  try {
    const raw = sessionStorage.getItem(`${TX_CACHE_KEY_PREFIX}${txId}`);
    if (!raw) return null;
    return JSON.parse(raw) as PaymentTransactionDto;
  } catch {
    return null;
  }
}

function removeTxFromSession(txId: number) {
  try {
    sessionStorage.removeItem(`${TX_CACHE_KEY_PREFIX}${txId}`);
  } catch {
    // ignore
  }
}

function formatVnd(amount: number): string {
  return `${Number(amount || 0).toLocaleString('vi-VN')} đ`;
}

function formatCountdown(seconds: number): string {
  const safe = Math.max(0, Math.floor(seconds));
  const mins = Math.floor(safe / 60);
  const secs = safe % 60;
  return `${String(mins).padStart(2, '0')}:${String(secs).padStart(2, '0')}`;
}

interface ChoosePaidItemPageProps {
  navigationParams?: PaymentNavigationParams | null;
  onClearNavigationParams?: () => void;
  onGoToResidencyHistory?: () => void;
  onGoToRequests?: () => void;
  onInvoicesUpdated?: () => void;
}

export const ChoosePaidItemPage: React.FC<ChoosePaidItemPageProps> = ({
  navigationParams,
  onClearNavigationParams,
  onInvoicesUpdated,
}) => {
  const { showConfirm, popup } = usePopup();
  const [invoices, setInvoices] = useState<StudentInvoiceDto[]>([]);
  const [totalOutstanding, setTotalOutstanding] = useState<number>(0);
  const [overdueCount, setOverdueCount] = useState<number>(0);
  const [loading, setLoading] = useState<boolean>(true);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [infoBanner, setInfoBanner] = useState<string>('');

  // Tab lọc theo InvoiceType
  const [activeTab, setActiveTab] = useState<FilterTab>('All');

  // Danh sách ID hóa đơn được tick chọn
  const [selectedIds, setSelectedIds] = useState<number[]>([]);

  // Modal xác nhận trước khi tạo mã QR
  const [showConfirmModal, setShowConfirmModal] = useState<boolean>(false);
  const [creatingQr, setCreatingQr] = useState<boolean>(false);

  // Màn hình Quét mã QR (Step 3)
  const [activeTx, setActiveTx] = useState<PaymentTransactionDto | null>(null);
  const [remainingSeconds, setRemainingSeconds] = useState<number>(0);
  const [cancellingTxId, setCancellingTxId] = useState<number | null>(null);
  const [copiedRef, setCopiedRef] = useState<boolean>(false);

  // Màn hình Biên lai kết quả thanh toán (Step 3 - Success)
  const [paymentReceipt, setPaymentReceipt] = useState<PaymentSuccessDto | null>(null);

  // Màn hình Lịch sử thanh toán & giao dịch
  const [showHistory, setShowHistory] = useState<boolean>(false);
  const [historyList, setHistoryList] = useState<PaymentHistoryItemDto[]>([]);
  const [loadingHistory, setLoadingHistory] = useState<boolean>(false);
  const [historyTab, setHistoryTab] = useState<FilterTab>('All');
  const [historySearch, setHistorySearch] = useState<string>('');

  const loadPaymentHistory = useCallback(async () => {
    setLoadingHistory(true);
    try {
      const list = await paymentApi.getPaymentHistory();
      setHistoryList(list || []);
    } catch {
      // ignore
    } finally {
      setLoadingHistory(false);
    }
  }, []);

  // Đọc tham số từ URL (?invoiceId=... hoặc ?sourceType=...&sourceId=...) kết hợp với navigationParams
  const resolveTargetFromUrlOrProps = useCallback((): PaymentNavigationParams => {
    const search = new URLSearchParams(typeof window !== 'undefined' ? window.location.search : '');
    const urlInvoiceId = Number(search.get('invoiceId') || 0);
    const urlSourceType = search.get('sourceType') || undefined;
    const urlSourceId = Number(search.get('sourceId') || 0);
    const urlInvoiceType = search.get('invoiceType') || undefined;

    return {
      invoiceId: navigationParams?.invoiceId || (urlInvoiceId > 0 ? urlInvoiceId : undefined),
      sourceType: navigationParams?.sourceType || urlSourceType,
      sourceId: navigationParams?.sourceId || (urlSourceId > 0 ? urlSourceId : undefined),
      tabFilter: navigationParams?.tabFilter || urlInvoiceType,
    };
  }, [navigationParams]);

  // Nạp danh sách hóa đơn và xử lý pre-select
  const loadInvoices = useCallback(
    async (applyPreselect = false) => {
      setLoading(true);
      setErrorMsg('');
      try {
        const data = await paymentApi.getUnpaidInvoices();
        // Đảm bảo sắp xếp: Overdue lên đầu, sau đó theo DueDate tăng dần
        const sorted = [...(data.items || [])].sort((a, b) => {
          const aOver = a.status === 'Overdue' || a.isOverdue ? 1 : 0;
          const bOver = b.status === 'Overdue' || b.isOverdue ? 1 : 0;
          if (aOver !== bOver) return bOver - aOver;
          return String(a.dueDate).localeCompare(String(b.dueDate));
        });

        setInvoices(sorted);
        setTotalOutstanding(data.totalOutstanding || 0);
        setOverdueCount(data.overdueCount || 0);

        if (applyPreselect) {
          const target = resolveTargetFromUrlOrProps();

          if (
            target.tabFilter &&
            ['RoomFee', 'Utility', 'BedTransfer', 'Compensation'].includes(target.tabFilter)
          ) {
            setActiveTab(target.tabFilter as FilterTab);
          }

          let matchedInvoice: StudentInvoiceDto | undefined;

          // TH1: Có truyền invoiceId
          if (target.invoiceId) {
            matchedInvoice = sorted.find((inv) => inv.id === target.invoiceId);
          }
          // TH2: Có truyền sourceType + sourceId
          else if (target.sourceType && target.sourceId) {
            const normSource = target.sourceType.trim().toLowerCase();
            matchedInvoice = sorted.find(
              (inv) =>
                inv.sourceId === target.sourceId &&
                (inv.sourceType || '').toLowerCase() === normSource
            );

            // Nếu chưa khớp trực tiếp trên list, gọi thêm API by-source để tra cứu chính xác
            if (!matchedInvoice) {
              try {
                const bySource = await paymentApi.getInvoiceBySource(
                  target.sourceType,
                  target.sourceId
                );
                if (bySource) {
                  matchedInvoice = sorted.find((inv) => inv.id === bySource.id) || bySource;
                }
              } catch {
                // Nếu không có hóa đơn tương ứng thì không tick sẵn
              }
            }
          }

          if (matchedInvoice) {
            if (matchedInvoice.status === 'Unpaid' || matchedInvoice.status === 'Overdue') {
              setSelectedIds([matchedInvoice.id]);
              setInfoBanner(
                `Đã tự động chọn hóa đơn ${matchedInvoice.invoiceNumber} (${formatVnd(
                  matchedInvoice.amount
                )}). Bạn có thể chọn thêm các hóa đơn khác để thanh toán gộp.`
              );
            } else if (matchedInvoice.status === 'Processing') {
              setSelectedIds([]);
              setInfoBanner(
                `Hóa đơn ${matchedInvoice.invoiceNumber} đang có phiên mã QR chờ thanh toán. Bạn có thể bấm "Tiếp tục thanh toán" hoặc "Hủy phiên cũ" bên dưới.`
              );
            }
          } else if (!target.invoiceId && !target.sourceType) {
            // Nếu URL không có query param: Không tick sẵn ô nào
            setSelectedIds([]);
          }

          onClearNavigationParams?.();
        } else {
          // Giữ lại những ID vẫn còn ở trạng thái cho phép thanh toán (Unpaid / Overdue)
          setSelectedIds((prev) =>
            prev.filter((id) =>
              sorted.some(
                (inv) => inv.id === id && (inv.status === 'Unpaid' || inv.status === 'Overdue')
              )
            )
          );
        }
      } catch (err: any) {
        setErrorMsg(err.message || 'Không thể tải danh sách hóa đơn cần thanh toán.');
      } finally {
        setLoading(false);
      }
    },
    [resolveTargetFromUrlOrProps, onClearNavigationParams]
  );

  useEffect(() => {
    loadInvoices(true);
    void loadPaymentHistory();
  }, [loadInvoices, loadPaymentHistory]);

  // Lắng nghe sự kiện điều hướng từ các trang khác khi đang ở trong StudentPortal
  useEffect(() => {
    const onNavigatePayment = () => {
      setActiveTx(null);
      setPaymentReceipt(null);
      loadInvoices(true);
    };
    window.addEventListener('fud:navigate-payment', onNavigatePayment);
    return () => window.removeEventListener('fud:navigate-payment', onNavigatePayment);
  }, [loadInvoices]);

  // Đồng hồ đếm ngược 20 phút cho màn hình QR
  useEffect(() => {
    if (!activeTx) return;

    const expiryMs = new Date(activeTx.expiryTime).getTime();
    const updateTimer = () => {
      const diffSec = Math.max(0, Math.ceil((expiryMs - Date.now()) / 1000));
      setRemainingSeconds(diffSec);
    };

    updateTimer();
    const interval = setInterval(updateTimer, 1000);
    return () => clearInterval(interval);
  }, [activeTx]);

  // Tự động Polling chờ SePay Webhook xác nhận giao dịch (mỗi 3 giây khi đang mở màn hình QR)
  useEffect(() => {
    if (!activeTx) return;
    let cancelled = false;

    const checkSePayStatus = async () => {
      try {
        const statusRes = await paymentApi.getTransactionStatus(activeTx.id);
        if (cancelled) return;
        if (statusRes.status === 'Success' && statusRes.receipt) {
          removeTxFromSession(activeTx.id);
          setActiveTx(null);
          setPaymentReceipt(statusRes.receipt);
          await loadInvoices(false);
          void loadPaymentHistory();
          onInvoicesUpdated?.();
        }
      } catch {
        // Bỏ qua lỗi mạng tạm thời trong lúc polling
      }
    };

    const pollTimer = setInterval(() => {
      void checkSePayStatus();
    }, 3000);

    return () => {
      cancelled = true;
      clearInterval(pollTimer);
    };
  }, [activeTx, loadInvoices, onInvoicesUpdated]);

  // Lọc hóa đơn theo Tab
  const filteredInvoices = useMemo(() => {
    if (activeTab === 'All') return invoices;
    return invoices.filter((inv) => inv.invoiceType === activeTab);
  }, [invoices, activeTab]);

  // Các hóa đơn trong tab hiện tại có thể tick chọn (Unpaid / Overdue)
  const selectableInTab = useMemo(
    () =>
      filteredInvoices.filter((inv) => inv.status === 'Unpaid' || inv.status === 'Overdue'),
    [filteredInvoices]
  );

  const isAllInTabSelected =
    selectableInTab.length > 0 &&
    selectableInTab.every((inv) => selectedIds.includes(inv.id));

  // Danh sách các đối tượng hóa đơn đang được tick chọn (tính trên toàn bộ danh sách)
  const selectedInvoices = useMemo(
    () =>
      invoices.filter(
        (inv) =>
          selectedIds.includes(inv.id) &&
          (inv.status === 'Unpaid' || inv.status === 'Overdue')
      ),
    [invoices, selectedIds]
  );

  // Tổng tiền động theo thời gian thực
  const selectedTotalAmount = useMemo(
    () => selectedInvoices.reduce((sum, inv) => sum + Number(inv.amount || 0), 0),
    [selectedInvoices]
  );

  // Đếm số lượng theo từng tab
  const tabCounts = useMemo(() => {
    const counts: Record<FilterTab, number> = {
      All: invoices.length,
      RoomFee: 0,
      Utility: 0,
      BedTransfer: 0,
      Compensation: 0,
    };
    for (const inv of invoices) {
      if (inv.invoiceType in counts) {
        counts[inv.invoiceType as FilterTab] += 1;
      }
    }
    return counts;
  }, [invoices]);

  const toggleSelectOne = (invoice: StudentInvoiceDto) => {
    if (invoice.status !== 'Unpaid' && invoice.status !== 'Overdue') return;
    setSelectedIds((prev) =>
      prev.includes(invoice.id) ? prev.filter((id) => id !== invoice.id) : [...prev, invoice.id]
    );
  };

  const toggleSelectAllInTab = () => {
    if (selectableInTab.length === 0) return;
    if (isAllInTabSelected) {
      const tabIds = new Set(selectableInTab.map((i) => i.id));
      setSelectedIds((prev) => prev.filter((id) => !tabIds.has(id)));
    } else {
      const merged = new Set([...selectedIds, ...selectableInTab.map((i) => i.id)]);
      setSelectedIds(Array.from(merged));
    }
  };

  // Bước 2 -> Bước 3: Khởi tạo giao dịch VietQR
  const handleCreateQrTransaction = async () => {
    if (selectedInvoices.length === 0) return;
    setCreatingQr(true);
    setErrorMsg('');
    try {
      const tx = await paymentApi.createTransaction(selectedInvoices.map((i) => i.id));
      saveTxToSession(tx);
      setShowConfirmModal(false);
      setActiveTx(tx);
      setRemainingSeconds(tx.remainingSeconds || 1200);
      setSelectedIds([]);
      // Xóa query param trên URL sau khi đã tạo giao dịch để tránh tự tick lại khi quay về
      if (typeof window !== 'undefined' && window.location.search) {
        window.history.replaceState({}, '', window.location.pathname);
      }
      onInvoicesUpdated?.();
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể khởi tạo mã QR thanh toán.');
      setShowConfirmModal(false);
      await loadInvoices(false);
    } finally {
      setCreatingQr(false);
    }
  };

  // Tiếp tục phiên QR đang Processing
  const handleResumeTransaction = (invoice: StudentInvoiceDto) => {
    if (!invoice.activeTransactionId) return;
    const txId = invoice.activeTransactionId;

    // Ưu tiên lấy từ sessionStorage nếu vừa tạo trong phiên trình duyệt
    const cached = loadTxFromSession(txId);
    if (cached) {
      setActiveTx(cached);
      return;
    }

    // Nếu người dùng vừa tải lại trang (F5), tái tạo thông tin giao dịch từ các hóa đơn cùng ActiveTransactionId
    const relatedInvoices = invoices.filter((i) => i.activeTransactionId === txId);
    const listToUse = relatedInvoices.length > 0 ? relatedInvoices : [invoice];
    const totalAmount = listToUse.reduce((sum, i) => sum + Number(i.amount || 0), 0);
    const expiryTime =
      invoice.activeTransactionExpiryTime || new Date(Date.now() + 15 * 60 * 1000).toISOString();
    const fallbackRef = `VQR-TX${txId}`;

    const reconstructed: PaymentTransactionDto = {
      id: txId,
      studentId: invoice.studentId,
      method: 'VietQR',
      gatewayReference: fallbackRef,
      qrLink: `https://img.vietqr.io/image/970436-FUD-compact.png?amount=${Math.round(
        totalAmount
      )}&addInfo=${encodeURIComponent(fallbackRef)}`,
      totalAmount,
      status: 'Processing',
      createdTime: invoice.issuedTime,
      expiryTime,
      remainingSeconds: Math.max(
        0,
        Math.ceil((new Date(expiryTime).getTime() - Date.now()) / 1000)
      ),
      invoices: listToUse.map((i) => ({
        invoiceId: i.id,
        invoiceNumber: i.invoiceNumber,
        invoiceType: i.invoiceType,
        allocatedAmount: i.amount,
        dueDate: i.dueDate,
        status: i.status,
        items: i.items || [],
      })),
    };

    setActiveTx(reconstructed);
  };

  // Bước 4: Bấm [Hủy giao dịch] hoặc [Hủy phiên cũ]
  const handleCancelTransaction = async (txId: number, askConfirm = true) => {
    if (askConfirm) {
      const ok = await showConfirm(
        'Bạn có chắc chắn muốn hủy phiên giao dịch QR này? Các hóa đơn trong phiên sẽ được mở khóa để bạn chọn lại.',
        { title: 'Hủy giao dịch QR', confirmText: 'Hủy giao dịch', cancelText: 'Giữ lại', variant: 'danger' }
      );
      if (!ok) return;
    }

    setCancellingTxId(txId);
    setErrorMsg('');
    try {
      await paymentApi.cancelTransaction(txId, 'Sinh viên chủ động hủy giao dịch QR');
      removeTxFromSession(txId);
      if (activeTx?.id === txId) {
        setActiveTx(null);
      }
      setInfoBanner('Đã hủy phiên giao dịch QR và mở khóa lại các hóa đơn.');
      await loadInvoices(false);
      onInvoicesUpdated?.();
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể hủy phiên giao dịch.');
    } finally {
      setCancellingTxId(null);
    }
  };

  const handleCopyGatewayRef = async (ref: string) => {
    try {
      await navigator.clipboard.writeText(ref);
      setCopiedRef(true);
      setTimeout(() => setCopiedRef(false), 2000);
    } catch {
      // ignore
    }
  };

  // ============================================================================
  // RENDER 1: MÀN HÌNH BIÊN LAI THANH TOÁN THÀNH CÔNG
  // ============================================================================
  if (paymentReceipt) {
    return (
      <div style={{ padding: '24px 32px', maxWidth: '900px', margin: '0 auto' }}>
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '16px',
            border: '1px solid #86EFAC',
            padding: '32px',
            boxShadow: '0 10px 25px -5px rgba(22, 163, 74, 0.08)',
          }}
        >
          <div style={{ textAlign: 'center', marginBottom: 24 }}>
            <div
              style={{
                width: 64,
                height: 64,
                borderRadius: '50%',
                backgroundColor: '#DCFCE7',
                color: '#15803D',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 32,
                fontWeight: 800,
                margin: '0 auto 14px',
                border: '2px solid #86EFAC',
              }}
            >
              ✓
            </div>
            <span
              style={{
                display: 'inline-block',
                padding: '4px 12px',
                borderRadius: '9999px',
                backgroundColor: '#DCFCE7',
                color: '#166534',
                fontSize: '12px',
                fontWeight: 700,
                marginBottom: 8,
              }}
            >
              GIAO DỊCH THÀNH CÔNG
            </span>
            <h1 style={{ fontSize: '24px', fontWeight: 800, color: '#0F172A', margin: '0 0 6px' }}>
              Biên lai xác nhận thanh toán
            </h1>
            <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
              Cảm ơn bạn! Hệ thống đã ghi nhận thanh toán và tự động cập nhật trạng thái hồ sơ liên quan.
            </p>
          </div>

          {/* Thông tin biên lai */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
              gap: '12px',
              backgroundColor: '#F8FAFC',
              border: '1px solid #E2E8F0',
              borderRadius: '12px',
              padding: '16px 20px',
              marginBottom: 20,
              fontSize: '13px',
              lineHeight: 1.7,
            }}
          >
            <div>
              <div style={{ color: '#64748B', fontSize: '12px' }}>Số biên lai (Receipt No.)</div>
              <div style={{ fontWeight: 800, color: '#15803D', fontSize: '15px' }}>
                {paymentReceipt.receiptNumber}
              </div>
            </div>
            <div>
              <div style={{ color: '#64748B', fontSize: '12px' }}>Mã giao dịch VietQR</div>
              <div style={{ fontWeight: 700, color: '#0F172A' }}>
                {paymentReceipt.gatewayReference}
              </div>
            </div>
            <div>
              <div style={{ color: '#64748B', fontSize: '12px' }}>Thời gian thanh toán</div>
              <div style={{ fontWeight: 700, color: '#0F172A' }}>
                {formatDateTime(paymentReceipt.paidTime)}
              </div>
            </div>
            <div>
              <div style={{ color: '#64748B', fontSize: '12px' }}>Tổng số tiền đã thanh toán</div>
              <div style={{ fontWeight: 800, color: '#C2410C', fontSize: '16px' }}>
                {formatVnd(paymentReceipt.totalAmount)}
              </div>
            </div>
          </div>

          {/* Side-effect notes (nếu có) */}
          {paymentReceipt.sideEffectNotes && paymentReceipt.sideEffectNotes.length > 0 && (
            <div
              style={{
                backgroundColor: '#F0FDF4',
                border: '1px solid #BBF7D0',
                borderRadius: '10px',
                padding: '14px 18px',
                marginBottom: 20,
                fontSize: '13px',
                color: '#166534',
              }}
            >
              <div style={{ fontWeight: 700, marginBottom: 6 }}>
                Cập nhật tự động từ hệ thống:
              </div>
              <ul style={{ margin: 0, paddingLeft: 18, lineHeight: 1.7 }}>
                {paymentReceipt.sideEffectNotes.map((note, idx) => (
                  <li key={idx}>{note}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Danh sách hóa đơn đã thanh toán */}
          <div style={{ marginBottom: 24 }}>
            <div style={{ fontSize: '14px', fontWeight: 700, color: '#0F172A', marginBottom: 10 }}>
              Danh sách hóa đơn đã gạch nợ ({paymentReceipt.invoices.length})
            </div>
            <div className="fud-table-wrapper">
              <table className="fud-table">
                <thead>
                  <tr>
                    <th>Mã hóa đơn</th>
                    <th>Loại phí</th>
                    <th>Chi tiết khoản thu</th>
                    <th style={{ textAlign: 'right' }}>Số tiền</th>
                    <th style={{ textAlign: 'center' }}>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {paymentReceipt.invoices.map((inv) => {
                    const typeMeta = INVOICE_TYPE_META[inv.invoiceType] || {
                      label: inv.invoiceType,
                      bg: '#F1F5F9',
                      color: '#334155',
                      border: '#CBD5E1',
                    };
                    return (
                      <tr key={inv.invoiceId}>
                        <td style={{ fontWeight: 700, color: '#C2410C' }}>{inv.invoiceNumber}</td>
                        <td>
                          <span
                            style={{
                              display: 'inline-block',
                              padding: '3px 9px',
                              borderRadius: '9999px',
                              fontSize: '11px',
                              fontWeight: 700,
                              backgroundColor: typeMeta.bg,
                              color: typeMeta.color,
                              border: `1px solid ${typeMeta.border}`,
                            }}
                          >
                            {typeMeta.label}
                          </span>
                        </td>
                        <td style={{ fontSize: '12.5px', color: '#334155' }}>
                          {inv.items && inv.items.length > 0
                            ? inv.items.map((it) => it.description).join('; ')
                            : typeMeta.label}
                        </td>
                        <td style={{ textAlign: 'right', fontWeight: 700, color: '#0F172A' }}>
                          {formatVnd(inv.allocatedAmount)}
                        </td>
                        <td style={{ textAlign: 'center' }}>
                          <span
                            style={{
                              display: 'inline-block',
                              padding: '3px 10px',
                              borderRadius: '9999px',
                              fontSize: '11px',
                              fontWeight: 700,
                              backgroundColor: '#DCFCE7',
                              color: '#15803D',
                            }}
                          >
                            Đã thanh toán (Paid)
                          </span>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>

          {/* Nút điều hướng */}
          <div style={{ display: 'flex', justifyContent: 'center', gap: 12, flexWrap: 'wrap' }}>
            <button
              type="button"
              onClick={() => {
                setPaymentReceipt(null);
                setShowHistory(true);
                void loadPaymentHistory();
              }}
              style={{
                padding: '10px 20px',
                backgroundColor: '#16A34A',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '8px',
                fontWeight: 700,
                fontSize: '13px',
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
                boxShadow: '0 4px 10px rgba(22, 163, 74, 0.25)',
              }}
            >
              📜 Xem lịch sử giao dịch
            </button>
            <button
              type="button"
              onClick={() => {
                setPaymentReceipt(null);
                loadInvoices(false);
              }}
              style={{
                padding: '10px 20px',
                backgroundColor: '#EA580C',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '8px',
                fontWeight: 700,
                fontSize: '13px',
                cursor: 'pointer',
              }}
            >
              ← Quay lại danh sách hóa đơn
            </button>
          </div>
        </div>
      </div>
    );
  }

  // ============================================================================
  // RENDER 2: MÀN HÌNH QUÉT MÃ VIETQR (SEPAY) & GIẢ LẬP ĐÃ THANH TOÁN
  // ============================================================================
  if (activeTx) {
    const isExpired = remainingSeconds <= 0;
    const sePayBank = 'BIDV';
    const sePayAcc = '96247FUD26';
    const sePayName = 'LE ANH QUANG (TK: 4271030639)';
    const sePayQrUrl = `https://qr.sepay.vn/img?acc=${encodeURIComponent(
      sePayAcc
    )}&bank=${encodeURIComponent(sePayBank)}&amount=${Math.round(
      activeTx.totalAmount
    )}&des=${encodeURIComponent(activeTx.gatewayReference)}&template=compact`;

    return (
      <div style={{ padding: '24px 32px', maxWidth: '980px', margin: '0 auto' }}>
        {popup}
        <div style={{ marginBottom: 16, display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 10 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
              <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: '#0F172A' }}>
                Thanh toán VietQR qua cổng SePay
              </h1>
              <span
                style={{
                  padding: '3px 10px',
                  borderRadius: '9999px',
                  backgroundColor: '#DBEAFE',
                  color: '#1D4ED8',
                  fontSize: '11px',
                  fontWeight: 700,
                }}
              >
                SePay Auto-Webhook
              </span>
            </div>
            <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
              Quét mã QR hoặc dùng "Mô phỏng giao dịch" trên SePay Test Mode — hệ thống sẽ tự động gạch nợ ngay khi nhận Webhook
            </p>
          </div>
          <button
            type="button"
            onClick={() => {
              setActiveTx(null);
              loadInvoices(false);
            }}
            style={{
              padding: '8px 14px',
              backgroundColor: '#FFFFFF',
              color: '#475569',
              border: '1px solid #CBD5E1',
              borderRadius: '8px',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            ← Quay lại danh sách
          </button>
        </div>

        {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}

        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
            gap: '20px',
            alignItems: 'start',
          }}
        >
          {/* Cột trái: Khung mã QR SePay & Nút giả lập */}
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '14px',
              border: '1px solid #E2E8F0',
              padding: '24px',
              textAlign: 'center',
              boxShadow: '0 4px 12px rgba(15, 23, 42, 0.05)',
            }}
          >
            {/* Đồng hồ đếm ngược 20 phút */}
            <div
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                padding: '6px 14px',
                borderRadius: '9999px',
                backgroundColor: isExpired ? '#FEE2E2' : remainingSeconds <= 180 ? '#FEF3C7' : '#FFF7ED',
                color: isExpired ? '#B91C1C' : remainingSeconds <= 180 ? '#B45309' : '#C2410C',
                border: `1px solid ${isExpired ? '#FCA5A5' : '#FDBA74'}`,
                fontSize: '13px',
                fontWeight: 700,
                marginBottom: 12,
              }}
            >
              <span>⏱</span>
              {isExpired ? (
                <span>Phiên mã QR đã hết hạn</span>
              ) : (
                <span>Thời gian còn lại: {formatCountdown(remainingSeconds)}</span>
              )}
            </div>

            {/* Trạng thái lắng nghe SePay Webhook tự động */}
            {!isExpired && (
              <div
                style={{
                  backgroundColor: '#EFF6FF',
                  border: '1px solid #BFDBFE',
                  color: '#1D4ED8',
                  borderRadius: '8px',
                  padding: '8px 12px',
                  fontSize: '12px',
                  fontWeight: 600,
                  marginBottom: 14,
                }}
              >
                🔄 Đang lắng nghe Webhook tự động từ SePay (kiểm tra mỗi 3 giây)...
              </div>
            )}

            {/* Ảnh mã QR SePay */}
            <div
              style={{
                maxWidth: 280,
                margin: '0 auto 16px',
                padding: 14,
                borderRadius: 12,
                border: '2px solid #93C5FD',
                backgroundColor: '#FFFFFF',
              }}
            >
              <img
                src={sePayQrUrl}
                alt={`SePay VietQR ${activeTx.gatewayReference}`}
                onError={(e) => {
                  const img = e.currentTarget;
                  if (activeTx.qrLink && img.src !== activeTx.qrLink) {
                    img.src = activeTx.qrLink;
                  }
                }}
                style={{ width: '100%', height: 'auto', display: 'block', borderRadius: 8 }}
              />
            </div>

            {/* Thông tin tài khoản SePay & Nội dung chuyển khoản */}
            <div
              style={{
                backgroundColor: '#F8FAFC',
                borderRadius: '10px',
                border: '1px solid #E2E8F0',
                padding: '14px 16px',
                textAlign: 'left',
                fontSize: '13px',
                lineHeight: 1.85,
                marginBottom: 20,
              }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: '#64748B' }}>Cổng / Ngân hàng:</span>
                <strong>SePay · {sePayBank}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: '#64748B' }}>Số tài khoản SePay:</span>
                <strong style={{ fontFamily: 'monospace', fontSize: '14px', color: '#1D4ED8' }}>
                  {sePayAcc}
                </strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: '#64748B' }}>Chủ tài khoản:</span>
                <strong>{sePayName}</strong>
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ color: '#64748B' }}>Số tiền thanh toán:</span>
                <strong style={{ fontSize: '16px', color: '#C2410C' }}>
                  {formatVnd(activeTx.totalAmount)}
                </strong>
              </div>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  gap: 8,
                  marginTop: 4,
                  paddingTop: 6,
                  borderTop: '1px dashed #CBD5E1',
                }}
              >
                <div>
                  <div style={{ color: '#64748B', fontSize: '11px' }}>Nội dung chuyển khoản (Bắt buộc):</div>
                  <strong style={{ color: '#0F172A', fontFamily: 'monospace', fontSize: '13.5px' }}>
                    {activeTx.gatewayReference}
                  </strong>
                </div>
                <button
                  type="button"
                  onClick={() => handleCopyGatewayRef(activeTx.gatewayReference)}
                  style={{
                    padding: '4px 10px',
                    borderRadius: '6px',
                    border: '1px solid #CBD5E1',
                    backgroundColor: copiedRef ? '#DCFCE7' : '#FFFFFF',
                    color: copiedRef ? '#15803D' : '#334155',
                    fontSize: '11px',
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  {copiedRef ? 'Đã chép ✓' : 'Sao chép'}
                </button>
              </div>
            </div>

            {/* Nút Hủy giao dịch */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              <button
                type="button"
                disabled={cancellingTxId === activeTx.id}
                onClick={() => handleCancelTransaction(activeTx.id, true)}
                style={{
                  width: '100%',
                  padding: '10px 18px',
                  backgroundColor: '#FEF2F2',
                  color: '#DC2626',
                  border: '1px solid #FCA5A5',
                  borderRadius: '8px',
                  fontSize: '13px',
                  fontWeight: 600,
                  cursor: cancellingTxId === activeTx.id ? 'not-allowed' : 'pointer',
                }}
              >
                {cancellingTxId === activeTx.id ? 'Đang hủy giao dịch...' : '✕ Hủy giao dịch'}
              </button>
            </div>
          </div>

          {/* Cột phải: Chi tiết các hóa đơn trong giao dịch */}
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '14px',
              border: '1px solid #E2E8F0',
              padding: '24px',
            }}
          >
            <h3 style={{ fontSize: '16px', fontWeight: 700, color: '#0F172A', margin: '0 0 12px' }}>
              Hóa đơn trong giao dịch ({activeTx.invoices.length})
            </h3>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
              {activeTx.invoices.map((inv) => {
                const typeMeta = INVOICE_TYPE_META[inv.invoiceType] || {
                  label: inv.invoiceType,
                  bg: '#F1F5F9',
                  color: '#334155',
                  border: '#CBD5E1',
                };
                return (
                  <div
                    key={inv.invoiceId}
                    style={{
                      backgroundColor: '#F8FAFC',
                      border: '1px solid #E2E8F0',
                      borderRadius: '10px',
                      padding: '14px',
                      fontSize: '13px',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        flexWrap: 'wrap',
                        gap: 8,
                        marginBottom: 6,
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                        <strong style={{ color: '#C2410C' }}>{inv.invoiceNumber}</strong>
                        <span
                          style={{
                            padding: '2px 8px',
                            borderRadius: '9999px',
                            fontSize: '11px',
                            fontWeight: 700,
                            backgroundColor: typeMeta.bg,
                            color: typeMeta.color,
                            border: `1px solid ${typeMeta.border}`,
                          }}
                        >
                          {typeMeta.label}
                        </span>
                      </div>
                      <strong style={{ fontSize: '14px', color: '#0F172A' }}>
                        {formatVnd(inv.allocatedAmount)}
                      </strong>
                    </div>

                    <div style={{ fontSize: '12px', color: '#64748B', marginBottom: 6 }}>
                      Hạn thanh toán: {formatDate(inv.dueDate)}
                    </div>

                    {inv.items && inv.items.length > 0 && (
                      <div
                        style={{
                          borderTop: '1px solid #E2E8F0',
                          paddingTop: 8,
                          marginTop: 6,
                          display: 'flex',
                          flexDirection: 'column',
                          gap: 4,
                          fontSize: '12px',
                          color: '#334155',
                        }}
                      >
                        {inv.items.map((it) => (
                          <div
                            key={it.id}
                            style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}
                          >
                            <span>• {it.description}</span>
                            <span style={{ fontWeight: 600, whiteSpace: 'nowrap' }}>
                              {formatVnd(it.amount)}
                            </span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            <div
              style={{
                marginTop: 16,
                paddingTop: 14,
                borderTop: '2px solid #E2E8F0',
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                fontSize: '15px',
              }}
            >
              <strong style={{ color: '#0F172A' }}>Tổng thanh toán:</strong>
              <strong style={{ fontSize: '18px', color: '#C2410C' }}>
                {formatVnd(activeTx.totalAmount)}
              </strong>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // ============================================================================
  // RENDER 2.5: MÀN HÌNH LỊCH SỬ THANH TOÁN & GIAO DỊCH
  // ============================================================================
  if (showHistory) {
    const filteredHistory = historyList.filter((item) => {
      if (historyTab !== 'All') {
        const hasType = (item.invoices || []).some((inv) => inv.invoiceType === historyTab);
        if (!hasType) return false;
      }
      if (historySearch.trim()) {
        const q = historySearch.trim().toLowerCase();
        const inReceipt = (item.receiptNumber || '').toLowerCase().includes(q);
        const inRef = (item.gatewayReference || '').toLowerCase().includes(q);
        const inInv = (item.invoices || []).some(
          (inv) =>
            (inv.invoiceNumber || '').toLowerCase().includes(q) ||
            (inv.items || []).some((it) => (it.description || '').toLowerCase().includes(q))
        );
        if (!inReceipt && !inRef && !inInv) return false;
      }
      return true;
    });

    const totalPaidSuccess = historyList.reduce((s, h) => s + Number(h.totalAmount || 0), 0);

    return (
      <div style={{ padding: '24px 32px' }}>
        {popup}
        {/* Header Lịch sử */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'flex-start',
            flexWrap: 'wrap',
            gap: 12,
            marginBottom: 20,
          }}
        >
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
              📜 Lịch sử thanh toán & Giao dịch VietQR
            </h1>
            <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
              Tra cứu toàn bộ biên lai và các hóa đơn bạn đã thanh toán thành công qua cổng SePay VietQR
            </p>
          </div>
          <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
            <button
              type="button"
              onClick={() => void loadPaymentHistory()}
              disabled={loadingHistory}
              style={{
                padding: '8px 16px',
                backgroundColor: '#FFFFFF',
                color: '#334155',
                border: '1px solid #CBD5E1',
                borderRadius: '8px',
                fontWeight: 600,
                fontSize: '13px',
                cursor: loadingHistory ? 'not-allowed' : 'pointer',
              }}
            >
              Làm mới
            </button>
            <button
              type="button"
              onClick={() => setShowHistory(false)}
              style={{
                padding: '8px 18px',
                backgroundColor: '#EA580C',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '8px',
                fontWeight: 700,
                fontSize: '13px',
                cursor: 'pointer',
              }}
            >
              ← Quay lại Thanh toán hóa đơn
            </button>
          </div>
        </div>

        {/* Thẻ thống kê lịch sử */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))',
            gap: '14px',
            marginBottom: 18,
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              border: '1px solid #E2E8F0',
              padding: '16px 20px',
            }}
          >
            <div style={{ fontSize: '12px', fontWeight: 700, color: '#64748B', textTransform: 'uppercase' }}>
              Tổng số giao dịch thành công
            </div>
            <div style={{ fontSize: '22px', fontWeight: 800, color: '#15803D', marginTop: 4 }}>
              {historyList.length} giao dịch
            </div>
            <div style={{ fontSize: '12px', color: '#64748B', marginTop: 2 }}>
              Đã được hệ thống đối soát và cấp biên lai điện tử
            </div>
          </div>

          <div
            style={{
              backgroundColor: '#F0FDF4',
              borderRadius: '12px',
              border: '1px solid #86EFAC',
              padding: '16px 20px',
            }}
          >
            <div style={{ fontSize: '12px', fontWeight: 700, color: '#166534', textTransform: 'uppercase' }}>
              Tổng số tiền đã thanh toán
            </div>
            <div style={{ fontSize: '22px', fontWeight: 800, color: '#15803D', marginTop: 4 }}>
              {formatVnd(totalPaidSuccess)}
            </div>
            <div style={{ fontSize: '12px', color: '#166534', marginTop: 2 }}>
              Thanh toán qua cổng SePay · BIDV VietQR
            </div>
          </div>
        </div>

        {/* Bộ lọc & Tìm kiếm */}
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 12,
            flexWrap: 'wrap',
            marginBottom: 16,
            backgroundColor: '#FFFFFF',
            padding: '12px 14px',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
          }}
        >
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {INVOICE_TYPE_TABS.map((tab) => {
              const isActive = historyTab === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setHistoryTab(tab.id)}
                  style={{
                    padding: '7px 12px',
                    borderRadius: '8px',
                    border: isActive ? '1px solid #EA580C' : '1px solid transparent',
                    backgroundColor: isActive ? '#EA580C' : '#F8FAFC',
                    color: isActive ? '#FFFFFF' : '#334155',
                    fontSize: '12.5px',
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  {tab.label}
                </button>
              );
            })}
          </div>

          <input
            type="text"
            value={historySearch}
            onChange={(e) => setHistorySearch(e.target.value)}
            placeholder="Tìm theo số biên lai, mã giao dịch, mã hóa đơn..."
            style={{
              minWidth: '260px',
              padding: '8px 12px',
              borderRadius: '8px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
            }}
          />
        </div>

        {/* Bảng lịch sử giao dịch */}
        <div className="fud-table-wrapper">
          <table className="fud-table">
            <colgroup>
              <col style={{ width: '16%' }} />
              <col style={{ width: '18%' }} />
              <col style={{ width: '24%' }} />
              <col style={{ width: '14%' }} />
              <col style={{ width: '12%' }} />
              <col style={{ width: '16%' }} />
            </colgroup>
            <thead>
              <tr>
                <th>Số biên lai / Mã GD</th>
                <th>Hóa đơn đã trả</th>
                <th>Chi tiết khoản thu</th>
                <th>Thời gian thanh toán</th>
                <th style={{ textAlign: 'right' }}>Tổng tiền</th>
                <th style={{ textAlign: 'center' }}>Trạng thái & Biên lai</th>
              </tr>
            </thead>
            <tbody>
              {loadingHistory ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '36px', color: '#64748B' }}>
                    Đang tải lịch sử thanh toán...
                  </td>
                </tr>
              ) : filteredHistory.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '42px', color: '#64748B' }}>
                    <div style={{ fontWeight: 700, fontSize: '15px', color: '#0F172A', marginBottom: 4 }}>
                      Chưa có giao dịch thanh toán nào
                    </div>
                    <div style={{ fontSize: '13px' }}>
                      Các giao dịch thanh toán hóa đơn qua VietQR sẽ được lưu trữ đầy đủ tại đây.
                    </div>
                  </td>
                </tr>
              ) : (
                filteredHistory.map((item) => (
                  <tr key={`${item.receiptNumber}-${item.transactionId}`}>
                    <td>
                      <div style={{ fontWeight: 800, color: '#15803D', fontSize: '13px' }}>
                        {item.receiptNumber}
                      </div>
                      <div style={{ fontSize: '11.5px', color: '#64748B', fontFamily: 'monospace' }}>
                        {item.gatewayReference}
                      </div>
                      <div style={{ fontSize: '11px', color: '#94A3B8' }}>
                        Cổng: {item.method || 'VietQR · SePay'}
                      </div>
                    </td>

                    <td>
                      <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                        {(item.invoices || []).map((inv) => {
                          const typeMeta = INVOICE_TYPE_META[inv.invoiceType] || {
                            label: inv.invoiceType,
                            bg: '#F1F5F9',
                            color: '#334155',
                            border: '#CBD5E1',
                          };
                          return (
                            <div key={inv.invoiceId} style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
                              <strong style={{ color: '#C2410C', fontSize: '12.5px' }}>{inv.invoiceNumber}</strong>
                              <span
                                style={{
                                  padding: '2px 7px',
                                  borderRadius: '9999px',
                                  fontSize: '10.5px',
                                  fontWeight: 700,
                                  backgroundColor: typeMeta.bg,
                                  color: typeMeta.color,
                                  border: `1px solid ${typeMeta.border}`,
                                }}
                              >
                                {typeMeta.label}
                              </span>
                            </div>
                          );
                        })}
                      </div>
                    </td>

                    <td style={{ fontSize: '12.5px', color: '#334155' }}>
                      {(item.invoices || [])
                        .flatMap((inv) =>
                          inv.items && inv.items.length > 0
                            ? inv.items.map((it) => it.description)
                            : [INVOICE_TYPE_META[inv.invoiceType]?.label || inv.invoiceType]
                        )
                        .join('; ')}
                    </td>

                    <td style={{ fontSize: '12.5px', color: '#0F172A', fontWeight: 600 }}>
                      {formatDateTime(item.paidTime)}
                    </td>

                    <td style={{ textAlign: 'right', fontWeight: 800, color: '#0F172A', fontSize: '13.5px' }}>
                      {formatVnd(item.totalAmount)}
                    </td>

                    <td style={{ textAlign: 'center' }}>
                      <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6 }}>
                        <span
                          style={{
                            display: 'inline-block',
                            padding: '3px 10px',
                            borderRadius: '9999px',
                            fontSize: '11px',
                            fontWeight: 700,
                            backgroundColor: '#DCFCE7',
                            color: '#15803D',
                          }}
                        >
                          Đã thanh toán
                        </span>
                        <button
                          type="button"
                          onClick={() => {
                            setPaymentReceipt({
                              transactionId: item.transactionId,
                              receiptNumber: item.receiptNumber,
                              gatewayReference: item.gatewayReference,
                              totalAmount: item.totalAmount,
                              paidTime: item.paidTime,
                              callbackTime: item.paidTime,
                              invoices: item.invoices || [],
                              sideEffectNotes: [],
                            });
                          }}
                          style={{
                            padding: '4px 10px',
                            backgroundColor: '#EFF6FF',
                            color: '#1D4ED8',
                            border: '1px solid #BFDBFE',
                            borderRadius: '6px',
                            fontSize: '11.5px',
                            fontWeight: 700,
                            cursor: 'pointer',
                          }}
                        >
                          Xem biên lai
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  // ============================================================================
  // RENDER 3: MÀN HÌNH CHỌN KHOẢN THANH TOÁN (CHOOSE PAID ITEM)
  // ============================================================================
  return (
    <div style={{ padding: '24px 32px' }}>
      {popup}
      {/* Header */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: 12,
          marginBottom: 20,
        }}
      >
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
            Thanh toán hóa đơn & Công nợ KTX
          </h1>
          <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
            Chọn một hoặc nhiều hóa đơn để thanh toán gộp qua cổng VietQR
          </p>
        </div>
        <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
          <button
            type="button"
            onClick={() => {
              setShowHistory(true);
              void loadPaymentHistory();
            }}
            style={{
              padding: '8px 16px',
              backgroundColor: '#FFF7ED',
              color: '#C2410C',
              border: '1px solid #FDBA74',
              borderRadius: '8px',
              fontWeight: 700,
              fontSize: '13px',
              cursor: 'pointer',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
            }}
          >
            <span>📜</span> Lịch sử thanh toán
            {historyList.length > 0 && (
              <span
                style={{
                  padding: '1px 7px',
                  borderRadius: '9999px',
                  backgroundColor: '#EA580C',
                  color: '#FFFFFF',
                  fontSize: '11px',
                  fontWeight: 700,
                }}
              >
                {historyList.length}
              </span>
            )}
          </button>
          <button
            type="button"
            onClick={() => loadInvoices(false)}
            disabled={loading}
            style={{
              padding: '8px 16px',
              backgroundColor: '#FFFFFF',
              color: '#334155',
              border: '1px solid #CBD5E1',
              borderRadius: '8px',
              fontWeight: 600,
              fontSize: '13px',
              cursor: loading ? 'not-allowed' : 'pointer',
            }}
          >
            Làm mới danh sách
          </button>
        </div>
      </div>

      {/* Cảnh báo lỗi */}
      {errorMsg && (
        <div
          className="violation-alert violation-alert-error"
          style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}
        >
          <span>{errorMsg}</span>
          <button
            type="button"
            onClick={() => setErrorMsg('')}
            style={{ background: 'none', border: 'none', color: '#991B1B', cursor: 'pointer', fontWeight: 700 }}
          >
            ✕
          </button>
        </div>
      )}

      {/* Thông báo tự động chọn từ URL query / thao tác */}
      {infoBanner && (
        <div
          style={{
            backgroundColor: '#FFF7ED',
            border: '1px solid #FDBA74',
            color: '#9A3412',
            borderRadius: '10px',
            padding: '12px 16px',
            fontSize: '13px',
            marginBottom: 16,
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: 12,
          }}
        >
          <span>💡 {infoBanner}</span>
          <button
            type="button"
            onClick={() => setInfoBanner('')}
            style={{ background: 'none', border: 'none', color: '#9A3412', cursor: 'pointer', fontWeight: 700 }}
          >
            ✕
          </button>
        </div>
      )}

      {/* Banner cảnh báo nợ quá hạn (Overdue) */}
      {overdueCount > 0 && (
        <div
          style={{
            backgroundColor: '#FEF2F2',
            border: '1.5px solid #F87171',
            borderRadius: '12px',
            padding: '14px 18px',
            marginBottom: 18,
            color: '#991B1B',
            display: 'flex',
            alignItems: 'flex-start',
            gap: 12,
          }}
        >
          <span style={{ fontSize: '20px', lineHeight: 1 }}>⚠️</span>
          <div style={{ fontSize: '13px', lineHeight: 1.6 }}>
            <div style={{ fontWeight: 700, fontSize: '14px', marginBottom: 2 }}>
              Cảnh báo: Bạn đang có {overdueCount} hóa đơn quá hạn thanh toán!
            </div>
            <div>
              Theo quy định KTX, khi có hóa đơn quá hạn (<strong>Overdue</strong>), hệ thống sẽ tạm khóa quyền gửi đơn yêu cầu mới và đăng ký đặt/giữ phòng. Các hóa đơn quá hạn đã được ưu tiên đưa lên đầu danh sách — vui lòng thanh toán sớm.
            </div>
          </div>
        </div>
      )}

      {/* 3 Thẻ thống kê công nợ */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(230px, 1fr))',
          gap: '14px',
          marginBottom: 18,
        }}
      >
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '16px 20px',
          }}
        >
          <div style={{ fontSize: '12px', fontWeight: 700, color: '#64748B', textTransform: 'uppercase' }}>
            Tổng công nợ chưa thanh toán
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#0F172A', marginTop: 4 }}>
            {formatVnd(totalOutstanding)}
          </div>
          <div style={{ fontSize: '12px', color: '#64748B', marginTop: 2 }}>
            Bao gồm hóa đơn Chưa thanh toán & Quá hạn
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: `1px solid ${overdueCount > 0 ? '#FCA5A5' : '#E2E8F0'}`,
            padding: '16px 20px',
          }}
        >
          <div style={{ fontSize: '12px', fontWeight: 700, color: '#64748B', textTransform: 'uppercase' }}>
            Hóa đơn quá hạn
          </div>
          <div
            style={{
              fontSize: '22px',
              fontWeight: 800,
              color: overdueCount > 0 ? '#DC2626' : '#16A34A',
              marginTop: 4,
            }}
          >
            {overdueCount} hóa đơn
          </div>
          <div style={{ fontSize: '12px', color: '#64748B', marginTop: 2 }}>
            {overdueCount > 0 ? 'Cần ưu tiên thanh toán ngay' : 'Không có công nợ quá hạn'}
          </div>
        </div>

        <div
          style={{
            backgroundColor: '#FFF7ED',
            borderRadius: '12px',
            border: '1px solid #FDBA74',
            padding: '16px 20px',
          }}
        >
          <div style={{ fontSize: '12px', fontWeight: 700, color: '#9A3412', textTransform: 'uppercase' }}>
            Đang chọn thanh toán ({selectedInvoices.length})
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#C2410C', marginTop: 4 }}>
            {formatVnd(selectedTotalAmount)}
          </div>
          <div style={{ fontSize: '12px', color: '#9A3412', marginTop: 2 }}>
            Tích chọn ô vuông bên dưới để gộp hóa đơn
          </div>
        </div>
      </div>

      {/* Tabs lọc theo InvoiceType */}
      <div
        style={{
          display: 'flex',
          gap: '8px',
          flexWrap: 'wrap',
          marginBottom: 16,
          backgroundColor: '#FFFFFF',
          padding: '10px 12px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
        }}
      >
        {INVOICE_TYPE_TABS.map((tab) => {
          const isActive = activeTab === tab.id;
          const count = tabCounts[tab.id] || 0;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id)}
              style={{
                padding: '8px 14px',
                borderRadius: '8px',
                border: isActive ? '1px solid #EA580C' : '1px solid transparent',
                backgroundColor: isActive ? '#EA580C' : '#F8FAFC',
                color: isActive ? '#FFFFFF' : '#334155',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
                transition: 'all 0.15s ease',
              }}
            >
              <span>{tab.label}</span>
              <span
                style={{
                  padding: '1px 7px',
                  borderRadius: '9999px',
                  fontSize: '11px',
                  fontWeight: 700,
                  backgroundColor: isActive ? 'rgba(255,255,255,0.22)' : '#E2E8F0',
                  color: isActive ? '#FFFFFF' : '#475569',
                }}
              >
                {count}
              </span>
            </button>
          );
        })}
      </div>

      {/* Bảng danh sách hóa đơn */}
      <div className="fud-table-wrapper" style={{ marginBottom: 20 }}>
        <table className="fud-table">
          <colgroup>
            <col style={{ width: '5%' }} />
            <col style={{ width: '15%' }} />
            <col style={{ width: '15%' }} />
            <col style={{ width: '27%' }} />
            <col style={{ width: '14%' }} />
            <col style={{ width: '12%' }} />
            <col style={{ width: '12%' }} />
          </colgroup>
          <thead>
            <tr>
              <th style={{ textAlign: 'center' }}>
                <input
                  type="checkbox"
                  checked={isAllInTabSelected}
                  disabled={selectableInTab.length === 0}
                  onChange={toggleSelectAllInTab}
                  title="Chọn / bỏ chọn tất cả hóa đơn trong tab này"
                  style={{ width: 16, height: 16, cursor: selectableInTab.length > 0 ? 'pointer' : 'not-allowed' }}
                />
              </th>
              <th>Mã hóa đơn</th>
              <th>Loại hóa đơn</th>
              <th>Chi tiết khoản phí (Invoice Items)</th>
              <th>Hạn thanh toán</th>
              <th style={{ textAlign: 'right' }}>Số tiền</th>
              <th style={{ textAlign: 'center' }}>Trạng thái</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '36px', color: '#64748B' }}>
                  Đang tải danh sách hóa đơn...
                </td>
              </tr>
            ) : filteredInvoices.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ textAlign: 'center', padding: '42px', color: '#64748B' }}>
                  <div style={{ fontWeight: 700, fontSize: '15px', color: '#0F172A', marginBottom: 4 }}>
                    Không có hóa đơn nào đang chờ thanh toán
                  </div>
                  <div style={{ fontSize: '13px' }}>
                    {activeTab === 'All'
                      ? 'Bạn đã hoàn tất toàn bộ nghĩa vụ thanh toán hiện tại.'
                      : 'Không có hóa đơn nào thuộc phân loại này.'}
                  </div>
                </td>
              </tr>
            ) : (
              filteredInvoices.map((inv) => {
                const isSelectable = inv.status === 'Unpaid' || inv.status === 'Overdue';
                const isChecked = selectedIds.includes(inv.id);
                const isOverdueRow = inv.status === 'Overdue' || inv.isOverdue;
                const typeMeta = INVOICE_TYPE_META[inv.invoiceType] || {
                  label: inv.invoiceType,
                  bg: '#F1F5F9',
                  color: '#334155',
                  border: '#CBD5E1',
                };
                const statusMeta = INVOICE_STATUS_META[inv.status] || {
                  label: inv.status,
                  bg: '#F1F5F9',
                  color: '#475569',
                };

                return (
                  <tr
                    key={inv.id}
                    onClick={() => {
                      if (isSelectable) toggleSelectOne(inv);
                    }}
                    style={{
                      cursor: isSelectable ? 'pointer' : 'default',
                      backgroundColor: isChecked
                        ? '#FFF7ED'
                        : isOverdueRow
                        ? '#FFF5F5'
                        : inv.status === 'Processing'
                        ? '#F8FAFC'
                        : 'transparent',
                    }}
                  >
                    {/* Checkbox */}
                    <td
                      style={{ textAlign: 'center' }}
                      onClick={(e) => e.stopPropagation()}
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        disabled={!isSelectable}
                        onChange={() => toggleSelectOne(inv)}
                        title={
                          !isSelectable
                            ? 'Hóa đơn đang trong phiên xử lý QR, không thể chọn trùng'
                            : 'Chọn hóa đơn này để thanh toán'
                        }
                        style={{
                          width: 16,
                          height: 16,
                          cursor: isSelectable ? 'pointer' : 'not-allowed',
                        }}
                      />
                    </td>

                    {/* Mã hóa đơn & Nguồn */}
                    <td>
                      <div style={{ fontWeight: 700, color: '#C2410C', fontSize: '13px' }}>
                        {inv.invoiceNumber}
                      </div>
                      {inv.sourceType && (
                        <div style={{ fontSize: '11px', color: '#64748B', marginTop: 2 }}>
                          {SOURCE_TYPE_LABELS[inv.sourceType] || inv.sourceType}
                          {inv.sourceId ? ` #${inv.sourceId}` : ''}
                        </div>
                      )}
                      <div style={{ fontSize: '11px', color: '#94A3B8' }}>
                        Phát hành: {formatDate(inv.issuedTime)}
                      </div>
                    </td>

                    {/* Loại hóa đơn */}
                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '3px 9px',
                          borderRadius: '9999px',
                          fontSize: '11px',
                          fontWeight: 700,
                          backgroundColor: typeMeta.bg,
                          color: typeMeta.color,
                          border: `1px solid ${typeMeta.border}`,
                        }}
                      >
                        {typeMeta.label}
                      </span>
                    </td>

                    {/* Chi tiết khoản phí (Invoice_Items) */}
                    <td>
                      {inv.items && inv.items.length > 0 ? (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 4, fontSize: '12px' }}>
                          {inv.items.map((item) => (
                            <div
                              key={item.id}
                              style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                gap: 8,
                                color: '#334155',
                              }}
                            >
                              <span>
                                • {item.description}
                                {item.quantity > 1 ? ` (SL: ${item.quantity})` : ''}
                              </span>
                              <span style={{ fontWeight: 600, color: '#0F172A', whiteSpace: 'nowrap' }}>
                                {formatVnd(item.amount)}
                              </span>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <span style={{ fontSize: '12px', color: '#64748B' }}>
                          {typeMeta.label} ({inv.invoiceNumber})
                        </span>
                      )}
                    </td>

                    {/* Hạn thanh toán */}
                    <td>
                      <div style={{ fontWeight: 600, fontSize: '12.5px', color: isOverdueRow ? '#DC2626' : '#0F172A' }}>
                        {formatDate(inv.dueDate)}
                      </div>
                      {isOverdueRow && (
                        <span
                          style={{
                            display: 'inline-block',
                            marginTop: 3,
                            padding: '2px 7px',
                            borderRadius: '4px',
                            backgroundColor: '#FEE2E2',
                            color: '#B91C1C',
                            fontSize: '10.5px',
                            fontWeight: 700,
                          }}
                        >
                          Quá hạn {inv.overdueDays > 0 ? `${inv.overdueDays} ngày` : ''}
                        </span>
                      )}
                    </td>

                    {/* Số tiền */}
                    <td style={{ textAlign: 'right', fontWeight: 800, color: '#0F172A', fontSize: '13.5px' }}>
                      {formatVnd(inv.amount)}
                    </td>

                    {/* Trạng thái & Hành động với hóa đơn Processing */}
                    <td
                      style={{ textAlign: 'center' }}
                      onClick={(e) => e.stopPropagation()}
                    >
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '4px 10px',
                          borderRadius: '9999px',
                          fontSize: '11px',
                          fontWeight: 700,
                          backgroundColor: statusMeta.bg,
                          color: statusMeta.color,
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {statusMeta.label}
                      </span>

                      {inv.status === 'Processing' && inv.activeTransactionId && (
                        <div
                          style={{
                            display: 'flex',
                            flexDirection: 'column',
                            gap: 4,
                            marginTop: 6,
                            alignItems: 'center',
                          }}
                        >
                          {inv.activeTransactionNotExpired && (
                            <button
                              type="button"
                              onClick={() => handleResumeTransaction(inv)}
                              style={{
                                padding: '4px 8px',
                                backgroundColor: '#2563EB',
                                color: '#FFFFFF',
                                border: 'none',
                                borderRadius: '5px',
                                fontSize: '11px',
                                fontWeight: 600,
                                cursor: 'pointer',
                                whiteSpace: 'nowrap',
                              }}
                            >
                              Tiếp tục thanh toán
                            </button>
                          )}
                          <button
                            type="button"
                            disabled={cancellingTxId === inv.activeTransactionId}
                            onClick={() => handleCancelTransaction(inv.activeTransactionId!, true)}
                            style={{
                              padding: '3px 8px',
                              backgroundColor: '#FEF2F2',
                              color: '#DC2626',
                              border: '1px solid #FCA5A5',
                              borderRadius: '5px',
                              fontSize: '11px',
                              fontWeight: 600,
                              cursor: 'pointer',
                              whiteSpace: 'nowrap',
                            }}
                          >
                            {cancellingTxId === inv.activeTransactionId ? 'Đang hủy...' : 'Hủy phiên cũ'}
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Thanh tóm tắt & Nút [Thanh toán] */}
      <div
        style={{
          backgroundColor: '#FFFFFF',
          borderRadius: '12px',
          border: '1.5px solid #FED7AA',
          padding: '16px 22px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 16,
          boxShadow: '0 4px 12px rgba(234, 88, 12, 0.08)',
        }}
      >
        <div>
          <div style={{ fontSize: '13px', color: '#475569' }}>
            Đã chọn <strong>{selectedInvoices.length}</strong> hóa đơn cần thanh toán
          </div>
          <div style={{ fontSize: '20px', fontWeight: 800, color: '#C2410C', marginTop: 2 }}>
            Tổng cộng: {formatVnd(selectedTotalAmount)}
          </div>
        </div>

        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          {selectedInvoices.length > 0 && (
            <button
              type="button"
              onClick={() => setSelectedIds([])}
              style={{
                padding: '10px 16px',
                backgroundColor: '#F1F5F9',
                color: '#475569',
                border: '1px solid #CBD5E1',
                borderRadius: '8px',
                fontWeight: 600,
                fontSize: '13px',
                cursor: 'pointer',
              }}
            >
              Bỏ chọn tất cả
            </button>
          )}
          <button
            type="button"
            disabled={selectedInvoices.length === 0}
            onClick={() => setShowConfirmModal(true)}
            style={{
              padding: '11px 24px',
              backgroundColor: selectedInvoices.length === 0 ? '#CBD5E1' : '#EA580C',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: '8px',
              fontWeight: 700,
              fontSize: '14px',
              cursor: selectedInvoices.length === 0 ? 'not-allowed' : 'pointer',
              boxShadow:
                selectedInvoices.length === 0 ? 'none' : '0 4px 10px rgba(234, 88, 12, 0.3)',
            }}
          >
            💳 Thanh toán ({selectedInvoices.length})
          </button>
        </div>
      </div>

      {/* Modal xác nhận danh sách hóa đơn & tổng tiền trước khi tạo mã QR */}
      {showConfirmModal && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.55)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: 20,
          }}
          onClick={() => !creatingQr && setShowConfirmModal(false)}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '14px',
              maxWidth: '620px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              padding: '24px',
              boxShadow: '0 20px 30px -10px rgba(0,0,0,0.2)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 700, color: '#C2410C', textTransform: 'uppercase' }}>
                  Xác nhận giao dịch VietQR
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>
                  Kiểm tra danh sách hóa đơn thanh toán
                </h3>
              </div>
              <button
                type="button"
                disabled={creatingQr}
                onClick={() => setShowConfirmModal(false)}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#94A3B8' }}
              >
                ✕
              </button>
            </div>

            <div
              style={{
                backgroundColor: '#F8FAFC',
                border: '1px solid #E2E8F0',
                borderRadius: '10px',
                padding: '14px',
                marginBottom: 16,
                maxHeight: '300px',
                overflowY: 'auto',
                display: 'flex',
                flexDirection: 'column',
                gap: 10,
              }}
            >
              {selectedInvoices.map((inv) => {
                const typeMeta = INVOICE_TYPE_META[inv.invoiceType] || {
                  label: inv.invoiceType,
                  bg: '#F1F5F9',
                  color: '#334155',
                  border: '#CBD5E1',
                };
                return (
                  <div
                    key={inv.id}
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center',
                      backgroundColor: '#FFFFFF',
                      border: '1px solid #E2E8F0',
                      borderRadius: '8px',
                      padding: '10px 14px',
                      fontSize: '13px',
                    }}
                  >
                    <div>
                      <div style={{ fontWeight: 700, color: '#C2410C' }}>
                        {inv.invoiceNumber}{' '}
                        <span style={{ fontWeight: 600, color: '#0F172A' }}>— {typeMeta.label}</span>
                      </div>
                      <div style={{ fontSize: '11.5px', color: '#64748B', marginTop: 2 }}>
                        Hạn: {formatDate(inv.dueDate)}
                        {inv.items && inv.items.length > 0
                          ? ` · ${inv.items.map((i) => i.description).join(', ')}`
                          : ''}
                      </div>
                    </div>
                    <div style={{ fontWeight: 800, color: '#0F172A', whiteSpace: 'nowrap', marginLeft: 12 }}>
                      {formatVnd(inv.amount)}
                    </div>
                  </div>
                );
              })}
            </div>

            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                padding: '12px 16px',
                backgroundColor: '#FFF7ED',
                border: '1px solid #FDBA74',
                borderRadius: '10px',
                marginBottom: 16,
              }}
            >
              <span style={{ fontSize: '14px', fontWeight: 700, color: '#9A3412' }}>
                Tổng số tiền thanh toán ({selectedInvoices.length} hóa đơn):
              </span>
              <span style={{ fontSize: '18px', fontWeight: 800, color: '#C2410C' }}>
                {formatVnd(selectedTotalAmount)}
              </span>
            </div>

            <div style={{ fontSize: '12px', color: '#64748B', lineHeight: 1.6, marginBottom: 20 }}>
              Lưu ý: Khi tạo mã QR, các hóa đơn được chọn sẽ chuyển sang trạng thái{' '}
              <strong>Đang xử lý (Processing)</strong> trong vòng 20 phút để tránh thanh toán trùng lặp.
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
              <button
                type="button"
                disabled={creatingQr}
                onClick={() => setShowConfirmModal(false)}
                style={{
                  padding: '9px 16px',
                  backgroundColor: '#F1F5F9',
                  color: '#334155',
                  border: '1px solid #CBD5E1',
                  borderRadius: '8px',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Hủy
              </button>
              <button
                type="button"
                disabled={creatingQr}
                onClick={handleCreateQrTransaction}
                style={{
                  padding: '9px 20px',
                  backgroundColor: '#EA580C',
                  color: '#FFFFFF',
                  border: 'none',
                  borderRadius: '8px',
                  fontWeight: 700,
                  fontSize: '13px',
                  cursor: creatingQr ? 'not-allowed' : 'pointer',
                }}
              >
                {creatingQr ? 'Đang khởi tạo mã QR...' : 'Xác nhận tạo mã QR'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
