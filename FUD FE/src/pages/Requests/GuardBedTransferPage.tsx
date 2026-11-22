import React, { useEffect, useState } from 'react';
import {
  checkinApi,
  requestApi,
  type RoomEquipmentCheckin,
  type TransferInspectionTaskDto,
} from '../../services/api';
import { formatDateTime } from '../../utils/dateUtils';
import { MeterPhotoScanner } from '../../components/MeterPhotoScanner';
import { ImageLightboxModal } from '../../components/ImageLightboxModal';
import '../../styles/checkin.css';
import '../../styles/violation.css';

interface EditableTransferEquipment extends RoomEquipmentCheckin {
  itemCondition: 'Good' | 'Damaged' | 'Missing';
  description: string;
  compensationAmount: number;
}

const CONDITION_LABELS: Record<'Good' | 'Damaged' | 'Missing', string> = {
  Good: 'Bình thường / Tốt',
  Damaged: 'Hư hỏng',
  Missing: 'Thiếu / Mất',
};

const formatVnd = (amount?: number) =>
  amount !== undefined && amount !== null
    ? `${Number(amount).toLocaleString('vi-VN')} đ`
    : '0 đ';

interface GuardBedTransferPageProps {
  mode?: 'pending' | 'history';
}

export const GuardBedTransferPage: React.FC<GuardBedTransferPageProps> = ({ mode = 'pending' }) => {
  const [tasks, setTasks] = useState<TransferInspectionTaskDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedTask, setSelectedTask] = useState<TransferInspectionTaskDto | null>(null);

  const [equipments, setEquipments] = useState<EditableTransferEquipment[]>([]);
  const [loadingEquip, setLoadingEquip] = useState(false);

  const [currElectricity, setCurrElectricity] = useState<string>('0');
  const [currWater, setCurrWater] = useState<string>('0');
  const [elecEvidenceUrl, setElecEvidenceUrl] = useState<string>('');
  const [waterEvidenceUrl, setWaterEvidenceUrl] = useState<string>('');
  const [lightboxImage, setLightboxImage] = useState<{ url: string; title: string; subtitle?: string } | null>(null);
  const [note, setNote] = useState('');
  const [witnessConfirmed, setWitnessConfirmed] = useState(false);

  const [submitting, setSubmitting] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [modalError, setModalError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');

  const loadTasks = async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      const data = await requestApi.getGuardTransferTasks();
      setTasks(data);
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Không thể tải danh sách nhiệm vụ kiểm tra chuyển giường.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadTasks();
  }, []);

  useEffect(() => {
    setSelectedTask(null);
    setModalError('');
  }, [mode]);

  const closeModal = () => {
    if (submitting) return;
    setSelectedTask(null);
    setModalError('');
    setElecEvidenceUrl('');
    setWaterEvidenceUrl('');
  };

  useEffect(() => {
    if (!selectedTask) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !submitting) {
        setSelectedTask(null);
        setModalError('');
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [selectedTask, submitting]);

  const handleSelectTask = async (task: TransferInspectionTaskDto) => {
    setSelectedTask(task);
    setModalError('');
    setSuccessMsg('');
    setNote('');
    setWitnessConfirmed(false);
    setCurrElectricity(String(task.previousElectricityReading ?? 0));
    setCurrWater(String(task.previousWaterReading ?? 0));
    setElecEvidenceUrl('');
    setWaterEvidenceUrl('');

    if (!task.handoverCompleted) {
      setLoadingEquip(true);
      try {
        const eqList = await checkinApi.getRoomEquipments(task.fromRoomId, task.fromBedId);
        setEquipments(
          eqList.map((eq) => {
            const cond = eq.currentCondition as string;
            const mapped: 'Good' | 'Damaged' | 'Missing' =
              cond === 'Broken' || cond === 'Damaged'
                ? 'Damaged'
                : cond === 'Missing'
                  ? 'Missing'
                  : 'Good';
            return {
              ...eq,
              itemCondition: mapped,
              description: '',
              compensationAmount: 0,
            };
          })
        );
      } catch (err: unknown) {
        setModalError(err instanceof Error ? err.message : 'Không thể tải danh mục thiết bị phòng cũ.');
        setEquipments([]);
      } finally {
        setLoadingEquip(false);
      }
    } else {
      setEquipments([]);
    }
  };

  const updateEquipField = (
    equipmentId: number,
    patch: Partial<Pick<EditableTransferEquipment, 'itemCondition' | 'description' | 'compensationAmount'>>
  ) => {
    setEquipments((prev) =>
      prev.map((item) => {
        if (item.equipmentId !== equipmentId) return item;
        const next = { ...item, ...patch };
        if (next.itemCondition === 'Good') {
          next.compensationAmount = 0;
        }
        return next;
      })
    );
  };

  const handleSubmitInspection = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTask) return;

    const elecVal = Number(currElectricity);
    const waterVal = Number(currWater);

    if (Number.isNaN(elecVal) || elecVal < selectedTask.previousElectricityReading) {
      setModalError(`Chỉ số điện hiện tại phải >= chỉ số cũ (${selectedTask.previousElectricityReading}).`);
      return;
    }
    if (Number.isNaN(waterVal) || waterVal < selectedTask.previousWaterReading) {
      setModalError(`Chỉ số nước hiện tại phải >= chỉ số cũ (${selectedTask.previousWaterReading}).`);
      return;
    }
    if (!witnessConfirmed) {
      setModalError('Vui lòng xác nhận biên bản đã có sự chứng kiến của 3 bên (Sinh viên - Bảo vệ - Bạn cùng phòng).');
      return;
    }

    setSubmitting(true);
    setModalError('');
    setSuccessMsg('');
    try {
      const updated = await requestApi.submitTransferInspection(selectedTask.requestId, {
        currentElectricityReading: elecVal,
        currentWaterReading: waterVal,
        electricityEvidenceImageUrl: elecEvidenceUrl || undefined,
        waterEvidenceImageUrl: waterEvidenceUrl || undefined,
        note: note.trim() || undefined,
        items: equipments.map((eq) => ({
          equipmentId: eq.equipmentId,
          itemCondition: eq.itemCondition,
          description: eq.description.trim() || undefined,
          compensationAmount: eq.itemCondition === 'Good' ? 0 : Math.max(0, Number(eq.compensationAmount) || 0),
        })),
      });

      setSuccessMsg(
        `Đã lập biên bản kiểm tra và chốt điện/nước cho đơn ${updated.requestCode}. Dữ liệu đã gửi tới Quản lý để phát hành hóa đơn và chuyển giường.`
      );
      setSelectedTask(null);
      setModalError('');
      await loadTasks();
    } catch (err: unknown) {
      setModalError(err instanceof Error ? err.message : 'Lỗi khi gửi biên bản kiểm tra.');
    } finally {
      setSubmitting(false);
    }
  };

  const visibleTasks = tasks.filter((t) => (mode === 'pending' ? !t.handoverCompleted : t.handoverCompleted));

  const elecDelta = selectedTask
    ? Math.max(0, (Number(currElectricity) || 0) - selectedTask.previousElectricityReading)
    : 0;
  const waterDelta = selectedTask
    ? Math.max(0, (Number(currWater) || 0) - selectedTask.previousWaterReading)
    : 0;
  const residentDivisor = Math.max(1, selectedTask?.activeResidentCount || 1);
  const estElecShare = selectedTask
    ? Math.round((elecDelta * (selectedTask.electricityUnitPrice || 0)) / residentDivisor)
    : 0;
  const estWaterShare = selectedTask
    ? Math.round((waterDelta * (selectedTask.waterUnitPrice || 0)) / residentDivisor)
    : 0;
  const estCompensation = equipments.reduce(
    (sum, item) => sum + (item.itemCondition === 'Good' ? 0 : Math.max(0, Number(item.compensationAmount) || 0)),
    0
  );

  return (
    <div className="checkin-page-wrapper">
      <div className="checkin-header">
        <div>
          <h1>
            {mode === 'pending'
              ? 'Trả phòng'
              : 'Biên bản Trả phòng'}
          </h1>
          <p>
            {mode === 'pending'
              ? 'Kiểm tra thiết bị phòng/giường cũ, chốt chỉ số điện nước cuối với sự chứng kiến 3 bên (Sinh viên, Bảo vệ, Bạn cùng phòng) trước khi Quản lý xuất hóa đơn.'
              : 'Tra cứu các biên bản kiểm tra thiết bị và chốt chỉ số điện nước chuyển giường giữa kỳ đã lập.'}
          </p>
        </div>
        <button
          type="button"
          className="btn-secondary"
          onClick={loadTasks}
          disabled={loading}
          style={{ padding: '8px 14px', borderRadius: 8, cursor: 'pointer' }}
        >
          {loading ? 'Đang tải...' : 'Làm mới'}
        </button>
      </div>

      {errorMsg && (
        <div
          style={{
            background: '#FEF2F2',
            border: '1px solid #FECACA',
            color: '#991B1B',
            padding: '10px 14px',
            borderRadius: 8,
            marginBottom: 14,
            fontSize: 13,
          }}
        >
          {errorMsg}
        </div>
      )}

      {successMsg && (
        <div
          style={{
            background: '#ECFDF5',
            border: '1px solid #A7F3D0',
            color: '#065F46',
            padding: '10px 14px',
            borderRadius: 8,
            marginBottom: 14,
            fontSize: 13,
          }}
        >
          {successMsg}
        </div>
      )}

      <div className="checkin-filter-card">
        {loading ? (
          <p style={{ color: '#64748B', fontSize: 13 }}>Đang tải danh sách đơn được phân công...</p>
        ) : visibleTasks.length === 0 ? (
          <p style={{ color: '#64748B', fontSize: 13, margin: 0 }}>
            {mode === 'pending'
              ? 'Hiện không có đơn chuyển giường nào đang chờ bạn kiểm tra.'
              : 'Chưa có biên bản kiểm tra chuyển giường nào được ghi nhận.'}
          </p>
        ) : (
          <div className="checkin-pending-table-wrap">
            <table className="checkin-pending-table">
              <thead>
                <tr>
                  <th>Mã đơn</th>
                  <th>Sinh viên</th>
                  <th>Phòng / Giường cũ</th>
                  <th>Giường đích</th>
                  <th>Ngày chuyển dự kiến</th>
                  <th>Trạng thái kiểm tra</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {visibleTasks.map((t) => {
                  const isSelected = selectedTask?.requestId === t.requestId;
                  return (
                    <tr
                      key={t.requestId}
                      className={isSelected ? 'selected-row' : undefined}
                      onClick={() => handleSelectTask(t)}
                      style={{ cursor: 'pointer' }}
                    >
                      <td style={{ fontWeight: 600 }}>{t.requestCode}</td>
                      <td>
                        <div style={{ fontWeight: 600 }}>{t.studentName}</div>
                        <div style={{ fontSize: 12, color: '#64748B' }}>
                          {t.studentCode} {t.studentPhone ? `· ${t.studentPhone}` : ''}
                        </div>
                      </td>
                      <td>
                        <div>
                          {t.fromDormName} · {t.fromBlockName}
                        </div>
                        <div style={{ fontSize: 12, color: '#0369A1', fontWeight: 600 }}>
                          P.{t.fromRoomNumber} - Giường {t.fromBedCode}
                        </div>
                      </td>
                      <td>
                        <div>{t.toDormName} · {t.toBlockName}</div>
                        <div style={{ fontSize: 12, color: '#047857', fontWeight: 600 }}>
                          P.{t.toRoomNumber} - Giường {t.toBedCode}
                        </div>
                      </td>
                      <td>{t.transferDate || '—'}</td>
                      <td>
                        <span className={`checkin-status-badge ${t.handoverCompleted ? 'completed' : ''}`}>
                          {t.handoverCompleted ? 'Đã kiểm tra & chốt sổ' : 'Chờ Bảo vệ kiểm tra'}
                        </span>
                      </td>
                      <td>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            handleSelectTask(t);
                          }}
                          style={{
                            padding: '6px 12px',
                            borderRadius: 6,
                            border: 'none',
                            background: t.handoverCompleted ? '#E2E8F0' : '#047857',
                            color: t.handoverCompleted ? '#1E293B' : '#FFFFFF',
                            fontWeight: 600,
                            fontSize: 12,
                            cursor: 'pointer',
                          }}
                        >
                          {t.handoverCompleted ? 'Xem biên bản' : 'Kiểm tra phòng cũ'}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {selectedTask && (
        <div className="checkin-modal-overlay" onClick={closeModal}>
          <div className="checkin-modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="checkin-modal-header">
              <div>
                <h2 className="checkin-modal-title">
                  {selectedTask.handoverCompleted
                    ? 'Chi tiết Biên bản Kiểm tra Chuyển giường'
                    : 'Lập Biên bản Kiểm tra Phòng cũ & Chốt Điện Nước'}{' '}
                  — {selectedTask.requestCode}
                </h2>
                <p className="checkin-modal-subtitle">
                  Sinh viên: <strong>{selectedTask.studentName} ({selectedTask.studentCode})</strong> · Rời P.{selectedTask.fromRoomNumber} (Giường {selectedTask.fromBedCode}) sang P.{selectedTask.toRoomNumber} (Giường {selectedTask.toBedCode})
                </p>
              </div>
              <button
                type="button"
                className="checkin-modal-close"
                onClick={closeModal}
                disabled={submitting}
                aria-label="Đóng"
              >
                ✕
              </button>
            </div>

            {selectedTask.handoverCompleted && selectedTask.handover ? (
              <>
                <div className="checkin-modal-body" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                  {modalError && <div className="violation-alert violation-alert-error">{modalError}</div>}
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, minmax(0, 1fr))', gap: 12, background: '#F8FAFC', padding: 14, borderRadius: 8, border: '1px solid #E2E8F0', fontSize: 13 }}>
                    <div>
                      <div style={{ color: '#64748B' }}>Mã biên bản</div>
                      <div style={{ fontWeight: 700 }}>{selectedTask.handover.handoverCode}</div>
                    </div>
                    <div>
                      <div style={{ color: '#64748B' }}>Xác nhận 3 bên</div>
                      <div style={{ fontWeight: 700, color: '#047857' }}>
                        {selectedTask.handover.studentAcceptedFlag ? 'Đã đồng thuận tại chỗ' : 'Chưa xác nhận'}
                      </div>
                    </div>
                    <div>
                      <div style={{ color: '#64748B' }}>Thời gian hoàn tất</div>
                      <div style={{ fontWeight: 600 }}>{formatDateTime(selectedTask.handover.completedTime || '')}</div>
                    </div>
                    <div>
                      <div style={{ color: '#64748B' }}>Tổng tiền bồi thường</div>
                      <div style={{ fontWeight: 700, color: '#B45309' }}>{formatVnd(selectedTask.handover.totalCompensation)}</div>
                    </div>
                  </div>

                  <div>
                    <h3 style={{ fontSize: 14, marginBottom: 8 }}>Chỉ số Điện & Nước đã chốt</h3>
                    <div className="checkin-pending-table-wrap">
                      <table className="checkin-pending-table">
                        <thead>
                          <tr>
                            <th>Loại đồng hồ</th>
                            <th>Chỉ số cũ</th>
                            <th>Chỉ số chốt</th>
                            <th>Tiêu thụ phòng</th>
                            <th>Đơn giá</th>
                            <th>Chia {selectedTask.activeResidentCount} SV trong phòng</th>
                            <th>Ảnh công tơ</th>
                          </tr>
                        </thead>
                        <tbody>
                          {selectedTask.meterReadings.map((mr) => (
                            <tr key={mr.id}>
                              <td style={{ fontWeight: 600 }}>{mr.meterType === 'Electricity' ? 'Điện (kWh)' : 'Nước (m³)'}</td>
                              <td>{mr.previousValue}</td>
                              <td style={{ fontWeight: 600 }}>{mr.currentValue}</td>
                              <td>{mr.consumption}</td>
                              <td>{formatVnd(mr.unitPrice)}</td>
                              <td style={{ fontWeight: 700, color: '#0369A1' }}>{formatVnd(mr.studentShareCost)}</td>
                              <td>
                                {mr.evidenceImageUrl ? (
                                  <button
                                    type="button"
                                    onClick={() =>
                                      setLightboxImage({
                                        url: mr.evidenceImageUrl!,
                                        title: `Ảnh công tơ ${mr.meterType === 'Electricity' ? 'Điện' : 'Nước'} — P.${selectedTask.fromRoomNumber}`,
                                        subtitle: `Chỉ số: ${mr.previousValue} → ${mr.currentValue}`,
                                      })
                                    }
                                    style={{
                                      padding: '3px 9px',
                                      borderRadius: 6,
                                      border: '1px solid #BFDBFE',
                                      background: '#EFF6FF',
                                      color: '#1D4ED8',
                                      fontSize: 11,
                                      fontWeight: 600,
                                      cursor: 'pointer',
                                    }}
                                  >
                                    Xem ảnh
                                  </button>
                                ) : (
                                  <span style={{ color: '#94A3B8' }}>—</span>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>

                  <div>
                    <h3 style={{ fontSize: 14, marginBottom: 8 }}>Kết quả kiểm tra thiết bị</h3>
                    <div className="checkin-pending-table-wrap">
                      <table className="checkin-pending-table">
                        <thead>
                          <tr>
                            <th>Mã TS</th>
                            <th>Tên thiết bị</th>
                            <th>Tình trạng</th>
                            <th>Ghi chú</th>
                            <th>Bồi thường</th>
                          </tr>
                        </thead>
                        <tbody>
                          {selectedTask.handover.items.map((item) => (
                            <tr key={item.id}>
                              <td>{item.equipmentCode}</td>
                              <td>{item.equipmentName} ({item.category})</td>
                              <td>{CONDITION_LABELS[item.itemCondition as keyof typeof CONDITION_LABELS] || item.itemCondition}</td>
                              <td>{item.description || '—'}</td>
                              <td style={{ fontWeight: 600, color: item.compensationAmount > 0 ? '#DC2626' : '#0F172A' }}>
                                {formatVnd(item.compensationAmount)}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>

                  {selectedTask.handover.inspectionNote && (
                    <div style={{ fontSize: 13, background: '#F8FAFC', padding: 12, borderRadius: 8 }}>
                      <strong>Ghi chú bàn giao:</strong> {selectedTask.handover.inspectionNote}
                    </div>
                  )}
                </div>

                <div className="checkin-modal-footer">
                  <button
                    type="button"
                    onClick={closeModal}
                    style={{ padding: '9px 16px', borderRadius: 8, border: '1px solid #CBD5E1', background: '#FFFFFF', cursor: 'pointer' }}
                  >
                    Đóng
                  </button>
                </div>
              </>
            ) : (
              <form className="checkin-modal-form" onSubmit={handleSubmitInspection}>
                <div className="checkin-modal-body" style={{ display: 'flex', flexDirection: 'column', gap: 18 }}>
                  {modalError && (
                    <div
                      style={{
                        background: '#FEF2F2',
                        border: '1px solid #FECACA',
                        color: '#991B1B',
                        padding: '10px 14px',
                        borderRadius: 8,
                        fontSize: 13,
                      }}
                    >
                      {modalError}
                    </div>
                  )}

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 16 }}>
                    <div style={{ background: '#F8FAFC', border: '1px solid #E2E8F0', borderRadius: 10, padding: 14 }}>
                      <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 10, color: '#0F172A' }}>
                        Chốt chỉ số Điện (P.{selectedTask.fromRoomNumber})
                      </div>
                      <div style={{ fontSize: 12, color: '#64748B', marginBottom: 8 }}>
                        Chỉ số gần nhất: <strong>{selectedTask.previousElectricityReading} kWh</strong> · Đơn giá: <strong>{formatVnd(selectedTask.electricityUnitPrice)}/kWh</strong>
                      </div>
                      <div style={{ marginBottom: 10 }}>
                        <MeterPhotoScanner
                          expectedMeterType="Electricity"
                          previousValue={selectedTask.previousElectricityReading}
                          saveEvidence
                          disabled={submitting}
                          label={`P.${selectedTask.fromRoomNumber}`}
                          existingImageUrl={elecEvidenceUrl || undefined}
                          onScanned={(res) => {
                            if (res.readingValue !== undefined) {
                              setCurrElectricity(String(res.readingValue));
                            }
                            if (res.evidenceImageUrl) {
                              setElecEvidenceUrl(res.evidenceImageUrl);
                            }
                          }}
                        />
                      </div>
                      <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>
                        Chỉ số điện hiện tại (kWh) <span style={{ color: '#DC2626' }}>*</span>
                      </label>
                      <input
                        type="number"
                        step="0.01"
                        min={selectedTask.previousElectricityReading}
                        value={currElectricity}
                        onChange={(e) => setCurrElectricity(e.target.value)}
                        required
                        style={{ width: '100%', padding: '8px 10px', borderRadius: 6, border: '1px solid #CBD5E1' }}
                      />
                      <div style={{ fontSize: 12, marginTop: 8, color: '#0369A1' }}>
                        Tiêu thụ: <strong>{elecDelta.toFixed(2)} kWh</strong> · Chia {residentDivisor} SV: <strong>{formatVnd(estElecShare)}</strong>
                      </div>
                    </div>

                    <div style={{ background: '#F8FAFC', border: '1px solid #E2E8F0', borderRadius: 10, padding: 14 }}>
                      <div style={{ fontWeight: 700, fontSize: 14, marginBottom: 10, color: '#0F172A' }}>
                        Chốt chỉ số Nước (P.{selectedTask.fromRoomNumber})
                      </div>
                      <div style={{ fontSize: 12, color: '#64748B', marginBottom: 8 }}>
                        Chỉ số gần nhất: <strong>{selectedTask.previousWaterReading} m³</strong> · Đơn giá: <strong>{formatVnd(selectedTask.waterUnitPrice)}/m³</strong>
                      </div>
                      <div style={{ marginBottom: 10 }}>
                        <MeterPhotoScanner
                          expectedMeterType="Water"
                          previousValue={selectedTask.previousWaterReading}
                          saveEvidence
                          disabled={submitting}
                          label={`P.${selectedTask.fromRoomNumber}`}
                          existingImageUrl={waterEvidenceUrl || undefined}
                          onScanned={(res) => {
                            if (res.readingValue !== undefined) {
                              setCurrWater(String(res.readingValue));
                            }
                            if (res.evidenceImageUrl) {
                              setWaterEvidenceUrl(res.evidenceImageUrl);
                            }
                          }}
                        />
                      </div>
                      <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 4 }}>
                        Chỉ số nước hiện tại (m³) <span style={{ color: '#DC2626' }}>*</span>
                      </label>
                      <input
                        type="number"
                        step="0.01"
                        min={selectedTask.previousWaterReading}
                        value={currWater}
                        onChange={(e) => setCurrWater(e.target.value)}
                        required
                        style={{ width: '100%', padding: '8px 10px', borderRadius: 6, border: '1px solid #CBD5E1' }}
                      />
                      <div style={{ fontSize: 12, marginTop: 8, color: '#0369A1' }}>
                        Tiêu thụ: <strong>{waterDelta.toFixed(2)} m³</strong> · Chia {residentDivisor} SV: <strong>{formatVnd(estWaterShare)}</strong>
                      </div>
                    </div>
                  </div>

                  <div>
                    <h3 style={{ fontSize: 14, margin: '0 0 8px' }}>
                      Kiểm tra danh mục Thiết bị Phòng & Giường cũ
                    </h3>
                    {loadingEquip ? (
                      <p style={{ fontSize: 13, color: '#64748B' }}>Đang tải danh sách thiết bị...</p>
                    ) : equipments.length === 0 ? (
                      <p style={{ fontSize: 13, color: '#64748B' }}>Không có thiết bị nào gắn cho phòng/giường này.</p>
                    ) : (
                      <div className="checkin-pending-table-wrap">
                        <table className="checkin-pending-table">
                          <thead>
                            <tr>
                              <th>Mã TS</th>
                              <th>Thiết bị</th>
                              <th>Vị trí</th>
                              <th>Tình trạng kiểm tra</th>
                              <th>Tiền bồi thường (VNĐ)</th>
                              <th>Ghi chú hư hỏng / mất</th>
                            </tr>
                          </thead>
                          <tbody>
                            {equipments.map((eq) => (
                              <tr key={eq.equipmentId}>
                                <td style={{ fontWeight: 600 }}>{eq.assetCode}</td>
                                <td>
                                  <div>{eq.catalogName}</div>
                                  <div style={{ fontSize: 11, color: '#64748B' }}>{eq.category}</div>
                                </td>
                                <td>{eq.bedCode ? `Giường ${eq.bedCode}` : 'Chung phòng'}</td>
                                <td>
                                  <select
                                    value={eq.itemCondition}
                                    onChange={(e) =>
                                      updateEquipField(eq.equipmentId, {
                                        itemCondition: e.target.value as 'Good' | 'Damaged' | 'Missing',
                                      })
                                    }
                                    style={{ padding: '6px 8px', borderRadius: 6, border: '1px solid #CBD5E1' }}
                                  >
                                    <option value="Good">Bình thường / Tốt</option>
                                    <option value="Damaged">Hư hỏng</option>
                                    <option value="Missing">Thiếu / Mất</option>
                                  </select>
                                </td>
                                <td>
                                  <input
                                    type="number"
                                    min={0}
                                    step={1000}
                                    disabled={eq.itemCondition === 'Good'}
                                    value={eq.itemCondition === 'Good' ? 0 : eq.compensationAmount}
                                    onChange={(e) =>
                                      updateEquipField(eq.equipmentId, {
                                        compensationAmount: Number(e.target.value) || 0,
                                      })
                                    }
                                    style={{ width: 120, padding: '6px 8px', borderRadius: 6, border: '1px solid #CBD5E1' }}
                                  />
                                </td>
                                <td>
                                  <input
                                    type="text"
                                    placeholder="Mô tả tình trạng nếu có..."
                                    value={eq.description}
                                    onChange={(e) =>
                                      updateEquipField(eq.equipmentId, { description: e.target.value })
                                    }
                                    style={{ width: '100%', padding: '6px 8px', borderRadius: 6, border: '1px solid #CBD5E1' }}
                                  />
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: 13, fontWeight: 600, marginBottom: 6 }}>
                      Ghi chú biên bản bàn giao
                    </label>
                    <textarea
                      rows={2}
                      value={note}
                      onChange={(e) => setNote(e.target.value)}
                      placeholder="Ghi chú thêm về hiện trạng phòng, vật dụng hoặc ý kiến các bên..."
                      style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: '1px solid #CBD5E1', fontSize: 13 }}
                    />
                  </div>

                  <div
                    style={{
                      background: '#ECFDF5',
                      border: '1px solid #A7F3D0',
                      borderRadius: 8,
                      padding: 12,
                      display: 'flex',
                      flexDirection: 'column',
                      gap: 8,
                    }}
                  >
                    <div style={{ fontSize: 13, color: '#065F46' }}>
                      <strong>Tạm tính công nợ phát sinh tại phòng cũ:</strong> Điện ({formatVnd(estElecShare)}) + Nước ({formatVnd(estWaterShare)}) + Bồi thường ({formatVnd(estCompensation)}) ={' '}
                      <strong>{formatVnd(estElecShare + estWaterShare + estCompensation)}</strong>
                    </div>
                    <label style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 13, fontWeight: 600, color: '#065F46', cursor: 'pointer' }}>
                      <input
                        type="checkbox"
                        checked={witnessConfirmed}
                        onChange={(e) => setWitnessConfirmed(e.target.checked)}
                      />
                      Xác nhận đã kiểm tra thực tế có sự chứng kiến của 3 bên (Sinh viên chuyển giường, Bảo vệ, Bạn cùng phòng).
                    </label>
                  </div>
                </div>

                <div className="checkin-modal-footer">
                  <button
                    type="button"
                    onClick={closeModal}
                    disabled={submitting}
                    style={{ padding: '9px 16px', borderRadius: 8, border: '1px solid #CBD5E1', background: '#FFFFFF', cursor: 'pointer' }}
                  >
                    Đóng
                  </button>
                  <button
                    type="submit"
                    disabled={submitting || loadingEquip}
                    style={{
                      padding: '9px 18px',
                      borderRadius: 8,
                      border: 'none',
                      background: '#047857',
                      color: '#FFFFFF',
                      fontWeight: 700,
                      cursor: submitting || loadingEquip ? 'not-allowed' : 'pointer',
                    }}
                  >
                    {submitting ? 'Đang lưu biên bản...' : 'Hoàn tất Kiểm tra & Gửi Quản lý'}
                  </button>
                </div>
              </form>
            )}
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
