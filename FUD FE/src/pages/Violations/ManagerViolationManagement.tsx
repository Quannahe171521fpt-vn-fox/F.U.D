import React, { useState, useEffect } from 'react';
import { violationApi, type ViolationItem } from '../../services/api';
import { Pagination } from '../../components/Pagination';
import { formatDateTime, formatDate } from '../../utils/dateUtils';
import '../../styles/violation.css';

const PAGE_SIZE = 10;

// Mapping trạng thái theo CONSTRAINT CHK_Violation_Status CHECK (Status IN ('Recorded','Finalized','Dismissed'))
export const VIOLATION_STATUS_VI: Record<string, string> = {
  'Recorded': 'Đã ghi nhận',
  'Finalized': 'Đã có kết luận',
  'Dismissed': 'Đã bác bỏ',
};

const STATUS_COLOR: Record<string, { bg: string; color: string }> = {
  'Recorded': { bg: '#FEF3C7', color: '#92400E' },
  'Finalized': { bg: '#FEE2E2', color: '#B91C1C' },
  'Dismissed': { bg: '#ECFDF5', color: '#047857' },
};

function renderStatusBadge(status: string) {
  const meta = STATUS_COLOR[status] || { bg: '#F1F5F9', color: '#475569' };
  return (
    <span
      style={{
        display: 'inline-block',
        padding: '3px 8px',
        borderRadius: '9999px',
        fontSize: '11px',
        fontWeight: 600,
        lineHeight: 1.25,
        backgroundColor: meta.bg,
        color: meta.color,
        textAlign: 'center',
        wordBreak: 'break-word',
      }}
    >
      {VIOLATION_STATUS_VI[status] || status}
    </span>
  );
}

export const ManagerViolationManagement: React.FC = () => {
  const [violations, setViolations] = useState<ViolationItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  // Bộ lọc
  const [filterStatus, setFilterStatus] = useState<string>('');
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [currentPage, setCurrentPage] = useState(1);

  // Modal xử lý (Định mức ban đầu hoặc Xem chi tiết)
  const [selectedViolation, setSelectedViolation] = useState<ViolationItem | null>(null);
  const [actionType, setActionType] = useState<'evaluate' | 'review' | 'detail' | null>(null);

  // Form states cho Modal Định mức
  const [initialLevel, setInitialLevel] = useState<string>('Nhắc nhở');
  const [managerNote, setManagerNote] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [unsuspending, setUnsuspending] = useState(false);

  useEffect(() => {
    loadViolations(true);

    // Tự động polling cập nhật danh sách vi phạm theo thời gian thực (mỗi 5 giây)
    const intervalId = setInterval(() => {
      loadViolations(false);
    }, 5000);

    return () => clearInterval(intervalId);
  }, [filterStatus]);

  const loadViolations = async (showLoadingIndicator = true) => {
    if (showLoadingIndicator) {
      setLoading(true);
      setErrorMsg('');
    }
    try {
      const data = await violationApi.getViolations({
        status: filterStatus || undefined,
      });
      setViolations(data);
    } catch (err: any) {
      if (showLoadingIndicator) {
        setErrorMsg(err.message || 'Không thể tải danh sách vi phạm kỷ luật');
      }
    } finally {
      if (showLoadingIndicator) {
        setLoading(false);
      }
    }
  };

  const handleOpenEvaluate = (v: ViolationItem) => {
    setSelectedViolation(v);
    setActionType('evaluate');
    setInitialLevel('Nhắc nhở');
    setManagerNote('');
    setErrorMsg('');
    setSuccessMsg('');
  };

  const handleOpenDetail = (v: ViolationItem) => {
    setSelectedViolation(v);
    setActionType('detail');
    setErrorMsg('');
    setSuccessMsg('');
  };

  const handleCloseModal = () => {
    setSelectedViolation(null);
    setActionType(null);
  };

  const handleUnsuspendStudentFromViolation = async (studentId: number) => {
    if (!window.confirm('Bạn có chắc chắn muốn mở khóa quyền đăng ký book phòng và giữ phòng cho sinh viên này không?')) return;
    setUnsuspending(true);
    try {
      await violationApi.unsuspendStudent(studentId, `Mở khóa qua hồ sơ vi phạm ${selectedViolation?.code}`);
      setSuccessMsg('Đã mở khóa quyền book phòng và giữ phòng cho sinh viên thành công!');
      await loadViolations(false);
      if (selectedViolation) {
        setSelectedViolation(prev => prev ? { ...prev, studentAccountStatus: 'Active', suspensionReason: undefined } : null);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi mở khóa tài khoản');
    } finally {
      setUnsuspending(false);
    }
  };

  // Submit Định mức ban đầu (Recorded)
  const handleSubmitEvaluate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedViolation) return;

    setSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');

    try {
      await violationApi.evaluateViolation(selectedViolation.id, {
        initialLevel,
        managerNote: managerNote.trim() || undefined,
      });

      setSuccessMsg(`Đã ban hành mức phạt "${initialLevel}" và thông báo tới sinh viên thành công.`);
      handleCloseModal();
      await loadViolations();
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi định mức vi phạm');
    } finally {
      setSubmitting(false);
    }
  };

  // Lọc dữ liệu client
  const filteredViolations = violations.filter(v => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      v.code.toLowerCase().includes(term) ||
      v.studentName.toLowerCase().includes(term) ||
      v.studentCode.toLowerCase().includes(term) ||
      v.roomNumber.toLowerCase().includes(term) ||
      v.violationTypeName.toLowerCase().includes(term)
    );
  });

  const paginatedViolations = filteredViolations.slice(
    (currentPage - 1) * PAGE_SIZE,
    currentPage * PAGE_SIZE
  );

  return (
    <div style={{ padding: '24px 32px' }}>
      {/* Header */}
      <div style={{ marginBottom: 20 }}>
        <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
          Quản lý & Xử lý vi phạm kỷ luật
        </h1>
        <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
          Thẩm định biên bản do Bảo vệ lập, đánh giá định mức phạt, ban hành thông báo tới sinh viên
        </p>
      </div>

      {/* Thông báo Alert */}
      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

      {/* Bộ lọc & Tìm kiếm */}
      <div
        style={{
          display: 'flex',
          gap: '12px',
          alignItems: 'center',
          backgroundColor: '#FFFFFF',
          padding: '16px 20px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
          marginBottom: 16,
          flexWrap: 'wrap',
        }}
      >
        <div style={{ minWidth: '220px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Lọc theo Trạng thái hồ sơ
          </label>
          <select
            value={filterStatus}
            onChange={e => {
              setFilterStatus(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
              backgroundColor: '#FFF',
            }}
          >
            <option value="">Tất cả trạng thái</option>
            <option value="Recorded">Đã ghi nhận</option>
            <option value="Finalized">Đã có kết luận</option>
            <option value="Dismissed">Đã bác bỏ</option>
          </select>
        </div>

        <div style={{ flex: 1, minWidth: '260px' }}>
          <label style={{ display: 'block', fontSize: '11px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>
            Tìm theo mã, MSSV, tên, phòng
          </label>
          <input
            type="text"
            placeholder="Nhập thông tin tìm kiếm..."
            value={searchTerm}
            onChange={e => {
              setSearchTerm(e.target.value);
              setCurrentPage(1);
            }}
            style={{
              width: '100%',
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #CBD5E1',
              fontSize: '13px',
            }}
          />
        </div>

        <div style={{ alignSelf: 'flex-end' }}>
          <button
            type="button"
            onClick={() => loadViolations(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#EA580C',
              color: '#FFF',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              fontSize: '13px',
              cursor: 'pointer',
            }}
          >
            Làm mới
          </button>
        </div>
      </div>

      {/* Bảng dữ liệu: Chuẩn không co, không trượt ngang */}
      <div className="fud-table-wrapper">
        <table className="fud-table">
          <colgroup>
            <col style={{ width: '9%' }} />
            <col style={{ width: '15%' }} />
            <col style={{ width: '13%' }} />
            <col style={{ width: '22%' }} />
            <col style={{ width: '9%' }} />
            <col style={{ width: '9%' }} />
            <col style={{ width: '14%' }} />
            <col style={{ width: '9%' }} />
          </colgroup>
          <thead>
            <tr>
              <th>Mã biên bản</th>
              <th>Sinh viên</th>
              <th>Phòng</th>
              <th>Hành vi vi phạm</th>
              <th>Thời điểm</th>
              <th>Mức độ</th>
              <th>Trạng thái</th>
              <th>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Đang tải danh sách vi phạm...
                </td>
              </tr>
            ) : paginatedViolations.length === 0 ? (
              <tr>
                <td colSpan={8} style={{ textAlign: 'center', padding: '30px', color: '#64748B' }}>
                  Không tìm thấy biên bản vi phạm nào
                </td>
              </tr>
            ) : (
              paginatedViolations.map(item => (
                <tr key={item.id}>
                  {/* Mã biên bản (1 dòng, hover xem full) */}
                  <td>
                    <span className="cell-text-1line" title={item.code} style={{ fontWeight: 700, color: '#C2410C' }}>
                      {item.code}
                    </span>
                  </td>

                  {/* Sinh viên (1 dòng, hover xem full) */}
                  <td>
                    <div className="cell-text-1line" title={`${item.studentName} (${item.studentCode})`}>
                      <span style={{ fontWeight: 600, color: item.studentAccountStatus === 'Suspended' ? '#DC2626' : undefined }}>
                        {item.studentName}
                        {item.studentAccountStatus === 'Suspended' && ' 🔒'}
                      </span>
                      <div style={{ fontSize: '11px', color: item.studentAccountStatus === 'Suspended' ? '#DC2626' : '#64748B' }}>
                        {item.studentCode}
                        {item.studentAccountStatus === 'Suspended' && ' • Đình chỉ'}
                      </div>
                    </div>
                  </td>

                  {/* Phòng (1 dòng, hover xem full) */}
                  <td>
                    <span className="cell-text-1line" title={`Phòng ${item.roomNumber} (${item.blockName})`}>
                      P.{item.roomNumber} ({item.blockName})
                    </span>
                  </td>

                  {/* Hành vi vi phạm (3 dòng, hover xem full) */}
                  <td>
                    <div className="cell-text-3line" title={`${item.violationTypeName} - ${item.description || ''}`}>
                      <span style={{ fontWeight: 600 }}>{item.violationTypeName}</span>
                      {item.description && (
                        <div style={{ fontSize: '12px', color: '#64748B' }}>- {item.description}</div>
                      )}
                    </div>
                  </td>

                  {/* Thời điểm (1 dòng, hover xem full) */}
                  <td>
                    <span className="cell-text-1line" title={formatDateTime(item.occurrenceTime)}>
                      {formatDate(item.occurrenceTime)}
                    </span>
                  </td>

                  {/* Mức độ (1 dòng, hover xem full) */}
                  <td>
                    <span
                      className="cell-text-1line"
                      title={item.finalLevel || item.initialLevel || 'Chưa định mức'}
                      style={{
                        fontWeight: 600,
                        color: item.finalLevel ? '#DC2626' : item.initialLevel ? '#EA580C' : '#94A3B8',
                      }}
                    >
                      {item.finalLevel || item.initialLevel || '—'}
                    </span>
                  </td>

                  {/* Trạng thái */}
                  <td>{renderStatusBadge(item.status)}</td>

                  {/* Thao tác */}
                  <td>
                    {item.status === 'Recorded' && !item.initialLevel ? (
                      <button
                        type="button"
                        onClick={() => handleOpenEvaluate(item)}
                        style={{
                          padding: '6px 10px',
                          backgroundColor: '#EA580C',
                          color: '#FFF',
                          border: 'none',
                          borderRadius: '6px',
                          fontSize: '12px',
                          fontWeight: 600,
                          cursor: 'pointer',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        Định mức
                      </button>
                    ) : (
                      <button
                        type="button"
                        onClick={() => handleOpenDetail(item)}
                        style={{
                          padding: '6px 10px',
                          backgroundColor: '#F1F5F9',
                          color: '#334155',
                          border: '1px solid #CBD5E1',
                          borderRadius: '6px',
                          fontSize: '12px',
                          fontWeight: 600,
                          cursor: 'pointer',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        Chi tiết
                      </button>
                    )}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Phân trang */}
      <Pagination
        currentPage={currentPage}
        totalItems={filteredViolations.length}
        pageSize={PAGE_SIZE}
        onPageChange={page => setCurrentPage(page)}
        itemName="biên bản"
      />

      {/* Modal Xử lý / Chi tiết */}
      {selectedViolation && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(15, 23, 42, 0.5)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: '20px',
          }}
        >
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              maxWidth: '650px',
              width: '100%',
              maxHeight: '90vh',
              overflowY: 'auto',
              padding: '24px',
              boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 700, color: '#C2410C', textTransform: 'uppercase' }}>
                  {actionType === 'evaluate' ? 'Định mức vi phạm & Ban hành' : 'Chi tiết hồ sơ vi phạm'}
                </span>
                <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '2px 0 0', color: '#0F172A' }}>
                  Biên bản {selectedViolation.code}
                </h3>
              </div>
              <button
                type="button"
                onClick={handleCloseModal}
                style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#94A3B8' }}
              >
                Đóng
              </button>
            </div>

            {/* Thông tin hồ sơ cơ bản */}
            <div style={{ backgroundColor: '#F8FAFC', borderRadius: '8px', padding: '14px', fontSize: '13px', lineHeight: 1.8, marginBottom: 16 }}>
              <div><strong>Sinh viên vi phạm:</strong> {selectedViolation.studentName} ({selectedViolation.studentCode})</div>
              <div><strong>Vị trí vi phạm:</strong> Phòng {selectedViolation.roomNumber} ({selectedViolation.blockName})</div>
              <div><strong>Hành vi vi phạm:</strong> <span style={{ color: '#B91C1C', fontWeight: 600 }}>{selectedViolation.violationTypeName}</span></div>
              <div><strong>Thời điểm:</strong> {formatDateTime(selectedViolation.occurrenceTime)}</div>
              <div><strong>Người lập biên bản:</strong> {selectedViolation.recordedByName || 'Bảo vệ ca trực'}</div>
              <div><strong>Trạng thái hiện tại:</strong> {VIOLATION_STATUS_VI[selectedViolation.status] || selectedViolation.status}</div>
              {selectedViolation.initialLevel && (
                <div><strong>Mức độ ban đầu:</strong> <span style={{ color: '#EA580C', fontWeight: 600 }}>{selectedViolation.initialLevel}</span></div>
              )}
              {selectedViolation.finalLevel && (
                <div><strong>Kết luận chính thức:</strong> <span style={{ color: '#DC2626', fontWeight: 700 }}>{selectedViolation.finalLevel}</span></div>
              )}
              {selectedViolation.priorViolationsCount !== undefined && selectedViolation.priorViolationsCount > 0 && (
                <div style={{ color: '#B45309', fontWeight: 600 }}>
                  Lưu ý: Sinh viên đã có {selectedViolation.priorViolationsCount} vi phạm trước đó trong học kỳ.
                </div>
              )}
              {/* Nút mở khóa sinh viên nếu bị đình chỉ VÀ hồ sơ này KHÔNG có request khiếu nại (nếu có request thì nút nằm ở trang Request) */}
              {selectedViolation.studentAccountStatus === 'Suspended' && (
                <div style={{ marginTop: 10, padding: '10px 14px', backgroundColor: '#FEF2F2', border: '1px solid #FCA5A5', borderRadius: '6px', color: '#991B1B' }}>
                  <div style={{ fontWeight: 700 }}>⚠️ Sinh viên đang bị ĐÌNH CHỈ (Khóa Book & Giữ phòng):</div>
                  <div style={{ fontSize: '12px', marginTop: 2 }}>{selectedViolation.suspensionReason || 'Đình chỉ nội trú do vi phạm quy chế.'}</div>
                  {!selectedViolation.linkedRequestId ? (
                    <button
                      type="button"
                      disabled={unsuspending}
                      onClick={() => handleUnsuspendStudentFromViolation(selectedViolation.studentId)}
                      style={{
                        marginTop: 8,
                        padding: '6px 14px',
                        backgroundColor: '#15803D',
                        color: '#FFF',
                        border: 'none',
                        borderRadius: '5px',
                        fontSize: '12px',
                        fontWeight: 600,
                        cursor: 'pointer',
                      }}
                    >
                      {unsuspending ? 'Đang mở khóa...' : '🔓 Mở khóa lại trạng thái (Khôi phục quyền Book & Giữ phòng)'}
                    </button>
                  ) : (
                    <div style={{ marginTop: 6, fontSize: '12px', color: '#1D4ED8', fontStyle: 'italic' }}>
                      ℹ️ Biên bản này đã được sinh viên gửi Đơn khiếu nại (Request #{selectedViolation.linkedRequestId}). Vui lòng xử lý mở khóa tại phân hệ Quản lý Đơn từ (Requests) theo quy trình.
                    </div>
                  )}
                </div>
              )}
            </div>

            <div style={{ marginBottom: 14 }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 4 }}>Mô tả chi tiết từ bảo vệ:</div>
              <div style={{ backgroundColor: '#F1F5F9', padding: '10px', borderRadius: '6px', fontSize: '13px', wordBreak: 'break-word' }}>
                {selectedViolation.description || 'Không có mô tả chi tiết'}
              </div>
            </div>

            {/* Ảnh minh chứng từ Bảo vệ */}
            {selectedViolation.evidences.length > 0 && (
              <div style={{ marginBottom: 16 }}>
                <div style={{ fontSize: '12px', fontWeight: 700, color: '#475569', marginBottom: 6 }}>Ảnh minh chứng:</div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(90px, 1fr))', gap: '8px' }}>
                  {selectedViolation.evidences.map((ev, i) => (
                    <a key={i} href={ev.fileUrl} target="_blank" rel="noreferrer">
                      <img src={ev.fileUrl} alt="evidence" style={{ width: '100%', height: '70px', objectFit: 'cover', borderRadius: '6px' }} />
                    </a>
                  ))}
                </div>
              </div>
            )}

            {/* Form Định mức vi phạm ban đầu (UC23) */}
            {actionType === 'evaluate' && selectedViolation.status === 'Recorded' && !selectedViolation.initialLevel && (
              <form onSubmit={handleSubmitEvaluate} style={{ borderTop: '1px solid #E2E8F0', paddingTop: 16 }}>
                <div style={{ marginBottom: 14 }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                    Định mức hình thức xử lý vi phạm ban đầu <span style={{ color: '#DC2626' }}>*</span>
                  </label>
                  <select
                    value={initialLevel}
                    onChange={e => setInitialLevel(e.target.value)}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', backgroundColor: '#FFF' }}
                    required
                  >
                    <option value="Nhắc nhở">Nhắc nhở (Vi phạm lần đầu / lỗi nhẹ)</option>
                    <option value="Khiển trách">Khiển trách</option>
                    <option value="Cảnh cáo">Cảnh cáo</option>
                    <option value="Đình chỉ nội trú">Đình chỉ nội trú / Buộc rời ký túc xá</option>
                  </select>
                </div>

                <div style={{ marginBottom: 16 }}>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#0F172A', marginBottom: 4 }}>
                    Ghi chú thẩm định & Hướng dẫn sinh viên (nếu có)
                  </label>
                  <textarea
                    rows={3}
                    value={managerNote}
                    onChange={e => setManagerNote(e.target.value)}
                    placeholder="Ghi chú căn cứ xác định mức phạt và lời nhắn tới sinh viên..."
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px', boxSizing: 'border-box' }}
                  />
                  <div style={{ fontSize: '12px', color: '#64748B', marginTop: 4 }}>
                    Sau khi định mức và ban hành, sinh viên sẽ có 24 giờ để gửi đơn khiếu nại (Request).
                  </div>
                </div>

                <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                  <button
                    type="button"
                    onClick={handleCloseModal}
                    style={{ padding: '8px 16px', background: '#F1F5F9', border: '1px solid #CBD5E1', borderRadius: '6px', fontSize: '13px', cursor: 'pointer' }}
                  >
                    Hủy bỏ
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    style={{ padding: '8px 18px', background: '#EA580C', color: '#FFF', border: 'none', borderRadius: '6px', fontSize: '13px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    {submitting ? 'Đang lưu...' : 'Xác nhận & Ban hành thông báo'}
                  </button>
                </div>
              </form>
            )}

            {/* Chi tiết phản hồi BQL nếu có */}
            {actionType === 'detail' && selectedViolation.managerNote && (
              <div style={{ backgroundColor: '#F0FDF4', border: '1px solid #86EFAC', borderRadius: '8px', padding: '12px', marginBottom: 14 }}>
                <strong style={{ color: '#166534', fontSize: '12px' }}>Ghi chú / Phản hồi từ BQL:</strong>
                <div style={{ fontSize: '13px', color: '#14532D', marginTop: 4, whiteSpace: 'pre-line' }}>
                  {selectedViolation.managerNote}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
