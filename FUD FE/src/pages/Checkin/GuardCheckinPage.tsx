import React, { useEffect, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import {
  checkinApi,
  facilityCommonApi,
  type BlockItem,
  type RoomItem,
  type PendingCheckinStudent,
  type RoomEquipmentCheckin,
  type CheckinHandoverDetail,
} from '../../services/api';
import '../../styles/checkin.css';
import '../../styles/violation.css';

interface GuardCheckinPageProps {
  mode: 'create' | 'history';
  onNavigateHistory?: () => void;
}

interface EditableEquipItem extends RoomEquipmentCheckin {
  itemCondition: 'Good' | 'Damaged' | 'Missing';
  handoverNote: string;
}

const CONDITION_LABELS: Record<'Good' | 'Damaged' | 'Missing', string> = {
  Good: 'Bình thường / Tốt',
  Damaged: 'Hư hỏng',
  Missing: 'Thiếu',
};

export const GuardCheckinPage: React.FC<GuardCheckinPageProps> = ({ mode, onNavigateHistory }) => {
  const { currentUser } = useAuth();

  // Filter states
  const [blocks, setBlocks] = useState<BlockItem[]>([]);
  const [rooms, setRooms] = useState<RoomItem[]>([]);
  const [selectedBlockId, setSelectedBlockId] = useState<string>('');
  const [selectedRoomId, setSelectedRoomId] = useState<string>('');
  const [searchKeyword, setSearchKeyword] = useState<string>('');

  // Pending students & selected student (Create mode)
  const [pendingStudents, setPendingStudents] = useState<PendingCheckinStudent[]>([]);
  const [selectedStudent, setSelectedStudent] = useState<PendingCheckinStudent | null>(null);
  const [equipItems, setEquipItems] = useState<EditableEquipItem[]>([]);

  // Extra fields on paper form (Section 2 & Row 17)
  const [studentTitle, setStudentTitle] = useState<string>('Sinh viên');
  const [otherItems, setOtherItems] = useState<string>('');
  const [electricityL, setElectricityL] = useState<string>('');
  const [electricityR, setElectricityR] = useState<string>('');
  const [waterL, setWaterL] = useState<string>('');
  const [waterR, setWaterR] = useState<string>('');

  // Completed / History states
  const [completedHandover, setCompletedHandover] = useState<CheckinHandoverDetail | null>(null);
  const [historyList, setHistoryList] = useState<CheckinHandoverDetail[]>([]);
  const [viewingHandover, setViewingHandover] = useState<CheckinHandoverDetail | null>(null);

  // UI feedback
  const [loading, setLoading] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState<string>('');

  // Load Blocks on mount
  useEffect(() => {
    facilityCommonApi
      .getBlocks()
      .then(data => setBlocks(data || []))
      .catch(() => setBlocks([]));
  }, []);

  // Load Rooms when Block changes
  useEffect(() => {
    if (!selectedBlockId) {
      setRooms([]);
      setSelectedRoomId('');
      return;
    }
    facilityCommonApi
      .getRoomsByBlock(parseInt(selectedBlockId, 10))
      .then(data => {
        setRooms(data || []);
        setSelectedRoomId('');
      })
      .catch(() => {
        setRooms([]);
        setSelectedRoomId('');
      });
  }, [selectedBlockId]);

  // Load Pending Students or History when filters or mode change
  const loadData = async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      const bId = selectedBlockId ? parseInt(selectedBlockId, 10) : undefined;
      const rId = selectedRoomId ? parseInt(selectedRoomId, 10) : undefined;
      const kw = searchKeyword.trim() || undefined;

      if (mode === 'create') {
        const list = await checkinApi.getPendingStudents({ blockId: bId, roomId: rId, search: kw });
        setPendingStudents(list);
        if (list.length > 0) {
          const stillExists = selectedStudent && list.find(s => s.studentId === selectedStudent.studentId && s.bedId === selectedStudent.bedId);
          handleSelectStudent(stillExists || list[0]);
        } else {
          setSelectedStudent(null);
          setEquipItems([]);
        }
      } else {
        const list = await checkinApi.getHistory({ blockId: bId, roomId: rId, search: kw });
        setHistoryList(list);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Không thể tải dữ liệu Check-in.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setCompletedHandover(null);
    setViewingHandover(null);
    setErrorMsg('');
    setSuccessMsg('');
    loadData();
  }, [mode, selectedBlockId, selectedRoomId]);

  const handleSelectStudent = async (student: PendingCheckinStudent) => {
    setSelectedStudent(student);
    setCompletedHandover(null);
    setStudentTitle('Sinh viên');
    setOtherItems('');
    setElectricityL('');
    setElectricityR('');
    setWaterL('');
    setWaterR('');
    setErrorMsg('');

    try {
      const eqs = await checkinApi.getRoomEquipments(student.roomId, student.bedId);
      setEquipItems(
        (eqs || []).map(e => ({
          ...e,
          itemCondition: e.currentCondition || 'Good',
          handoverNote: e.note || 'Bình thường',
        }))
      );
    } catch (err: any) {
      setEquipItems([]);
      setErrorMsg(err.message || 'Không thể tải danh sách thiết bị của phòng.');
    }
  };

  const handleConditionChange = (equipmentId: number, cond: 'Good' | 'Damaged' | 'Missing') => {
    setEquipItems(prev =>
      prev.map(item =>
        item.equipmentId === equipmentId
          ? {
            ...item,
            itemCondition: cond,
            handoverNote:
              cond === 'Good'
                ? 'Bình thường'
                : cond === 'Damaged'
                  ? 'Hư hỏng'
                  : 'Thiếu',
          }
          : item
      )
    );
  };

  const handleNoteChange = (equipmentId: number, note: string) => {
    setEquipItems(prev =>
      prev.map(item => (item.equipmentId === equipmentId ? { ...item, handoverNote: note } : item))
    );
  };

  const handleCompleteCheckin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedStudent) {
      setErrorMsg('Vui lòng chọn một sinh viên trong danh sách chờ nhận phòng.');
      return;
    }

    setSubmitting(true);
    setErrorMsg('');
    setSuccessMsg('');

    try {
      const result = await checkinApi.completeCheckin({
        bookingRequestId: selectedStudent.bookingRequestId,
        residencyId: selectedStudent.residencyId,
        studentId: selectedStudent.studentId,
        roomId: selectedStudent.roomId,
        bedId: selectedStudent.bedId,
        studentTitle: studentTitle.trim() || 'Sinh viên',
        otherItems: otherItems.trim() || undefined,
        electricityL: electricityL.trim() || undefined,
        electricityR: electricityR.trim() || undefined,
        waterL: waterL.trim() || undefined,
        waterR: waterR.trim() || undefined,
        items: equipItems.map(i => ({
          equipmentId: i.equipmentId,
          itemCondition: i.itemCondition,
          note: i.handoverNote.trim() || CONDITION_LABELS[i.itemCondition],
        })),
      });

      setCompletedHandover(result);
      setSuccessMsg(
        `Đã hoàn tất Check-in nhận phòng cho sinh viên ${result.studentName} (${result.studentCode}) tại Phòng ${result.roomNumber} - Giường ${result.bedCode}!`
      );
      // Cập nhật lại danh sách chờ
      const updatedPending = await checkinApi.getPendingStudents({
        blockId: selectedBlockId ? parseInt(selectedBlockId, 10) : undefined,
        roomId: selectedRoomId ? parseInt(selectedRoomId, 10) : undefined,
        search: searchKeyword.trim() || undefined,
      });
      setPendingStudents(updatedPending);
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi hoàn tất biên bản Check-in.');
    } finally {
      setSubmitting(false);
    }
  };

  // Render Readonly/Printable Paper View for a completed Handover
  const renderPrintablePaper = (handover: CheckinHandoverDetail) => {
    const dt = new Date(handover.createdTime);
    const day = String(dt.getDate()).padStart(2, '0');
    const month = String(dt.getMonth() + 1).padStart(2, '0');
    const year = dt.getFullYear();

    return (
      <div className="fpt-handover-paper">
        <div className="fpt-paper-top">
          <div className="fpt-paper-top-left">
            <div className="fpt-logo-text">FPT Education</div>
            <div className="fpt-org-title">TỔ CHỨC GIÁO DỤC FPT</div>
          </div>
          <div className="fpt-paper-top-right">
            <div className="fpt-nation-title">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
            <div className="fpt-nation-sub">Độc lập - Tự do - Hạnh phúc</div>
          </div>
        </div>

        <div className="fpt-paper-banner">
          BIÊN BẢN BÀN GIAO TÀI SẢN BLOCK: {handover.blockCode || handover.blockName} — PHÒNG {handover.roomNumber} (GIƯỜNG {handover.bedCode})
        </div>

        <div className="fpt-paper-parties">
          <div className="fpt-party-heading">1 - Bên bàn giao:</div>
          <div>Ông/Bà: <strong>{handover.guardName}</strong> ({handover.guardCode})</div>
          <div>Bộ phận: An ninh - Phòng Quản lý ký túc xá.</div>

          <div className="fpt-party-heading" style={{ marginTop: 6 }}>2 – Bên nhận bàn giao:</div>
          <div>Anh/Chị: <strong>{handover.studentName}</strong></div>
          <div>MSSV(CBGV): <strong>{handover.studentCode}</strong></div>
          <div>Chức danh: <strong>{handover.studentTitle}</strong> (Phòng {handover.roomNumber} - Giường {handover.bedCode})</div>

          <div className="fpt-party-heading" style={{ marginTop: 6 }}>3 – Loại thiết bị bàn giao:</div>
        </div>

        <table className="fpt-equip-table">
          <thead>
            <tr>
              <th style={{ width: 48 }}>TT</th>
              <th>Loại thiết bị</th>
              <th style={{ width: 180 }}>Số lượng</th>
              <th style={{ width: 240 }}>Tình trạng</th>
            </tr>
          </thead>
          <tbody>
            {handover.items.length === 0 ? (
              <tr>
                <td colSpan={4} style={{ textAlign: 'center', fontStyle: 'italic', padding: '12px' }}>
                  Chưa có thiết bị nào được khai báo trong phòng {handover.roomNumber}
                </td>
              </tr>
            ) : (
              handover.items.map((item, idx) => (
                <tr key={item.id}>
                  <td style={{ textAlign: 'center' }}>{idx + 1}</td>
                  <td>
                    <strong>{item.catalogName}</strong>{' '}
                    <span style={{ color: '#475569', fontSize: '12.5px' }}>
                      ({item.assetCode}{item.bedCode ? ` · Giường ${item.bedCode}` : ''})
                    </span>
                  </td>
                  <td style={{ textAlign: 'center' }}>{item.quantity}</td>
                  <td>
                    {CONDITION_LABELS[item.itemCondition] || item.itemCondition}
                    {item.note && item.note !== CONDITION_LABELS[item.itemCondition] ? ` - ${item.note}` : ''}
                  </td>
                </tr>
              ))
            )}

            {/* Dòng cuối: Vật dụng khác + Số điện (L/R) + Số nước (L/R) */}
            <tr>
              <td style={{ textAlign: 'center' }}>{handover.items.length + 1}</td>
              <td>
                <div><strong>Vật dụng khác:</strong> {handover.otherItems || 'Không'}</div>
              </td>
              <td style={{ padding: '4px 10px' }}>
                <div style={{ textAlign: 'center', fontWeight: 700, fontStyle: 'italic' }}>Số điện</div>
                <div className="fpt-meter-split">
                  <div className="fpt-meter-cell">L: <strong>{handover.electricityL || '0'}</strong></div>
                  <div className="fpt-meter-cell">R: <strong>{handover.electricityR || '0'}</strong></div>
                </div>
              </td>
              <td style={{ padding: '4px 10px' }}>
                <div style={{ textAlign: 'center', fontWeight: 700, fontStyle: 'italic' }}>Số nước</div>
                <div className="fpt-meter-split">
                  <div className="fpt-meter-cell">L: <strong>{handover.waterL || '0'}</strong></div>
                  <div className="fpt-meter-cell">R: <strong>{handover.waterR || '0'}</strong></div>
                </div>
              </td>
            </tr>
          </tbody>
        </table>

        <div className="fpt-paper-footer">
          <div className="fpt-date-line">
            Hòa Lạc, ngày {day} tháng {month} năm {year}
          </div>
          <div className="fpt-signatures-grid">
            <div>
              <div>Người nhận bàn giao</div>
              <div style={{ fontWeight: 400, fontSize: 12, fontStyle: 'italic' }}></div>
              <div className="fpt-sig-name">{handover.studentName}</div>
            </div>
            <div>
              <div>Người bàn giao</div>
              <div style={{ fontWeight: 400, fontSize: 12, fontStyle: 'italic' }}></div>
              <div className="fpt-sig-name">{handover.guardName}</div>
            </div>
          </div>
        </div>
      </div>
    );
  };

  const now = new Date();

  return (
    <div className="checkin-page-wrapper">
      <div className="checkin-header no-print">
        <div>
          <h1>
            {mode === 'create'
              ? 'Check-in Nhận phòng & Bàn giao tài sản'
              : 'Danh sách Biên bản Bàn giao (Check-in)'}
          </h1>
          <p>
            {mode === 'create'
              ? 'Kiểm tra thiết bị phòng thực tế cùng sinh viên đã có đơn đặt phòng và lập biên bản bàn giao trực tuyến'
              : 'Tra cứu và in lại biên bản bàn giao tài sản nhận phòng Ký túc xá'}
          </p>
        </div>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error no-print">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success no-print">{successMsg}</div>}

      {/* Bộ lọc tìm kiếm */}
      <div className="checkin-filter-card no-print">
        <div className="checkin-filter-row">
          <div>
            <label className="violation-label">Tòa (Block)</label>
            <select
              className="violation-select"
              value={selectedBlockId}
              onChange={e => setSelectedBlockId(e.target.value)}
            >
              <option value="">-- Tất cả các Tòa --</option>
              {blocks.map(b => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="violation-label">Phòng</label>
            <select
              className="violation-select"
              value={selectedRoomId}
              onChange={e => setSelectedRoomId(e.target.value)}
              disabled={!selectedBlockId}
            >
              <option value="">-- Tất cả Phòng --</option>
              {rooms.map(r => (
                <option key={r.id} value={r.id}>
                  Phòng {r.roomNumber}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="violation-label">Tìm theo MSSV, Họ tên hoặc Số phòng</label>
            <input
              type="text"
              className="violation-input"
              placeholder="VD: SE17002, Hoàng Nữ Sinh, 202..."
              value={searchKeyword}
              onChange={e => setSearchKeyword(e.target.value)}
              onKeyDown={e => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  loadData();
                }
              }}
            />
          </div>

          <button type="button" className="btn-secondary" onClick={loadData}>
            {loading ? 'Đang tải...' : 'Tìm kiếm'}
          </button>
        </div>

        {/* Danh sách sinh viên chờ Check-in (chế độ Create) */}
        {mode === 'create' && (
          <div style={{ marginTop: 16 }}>
            <div className="violation-label">
              Danh sách sinh viên có đơn Đặt phòng (Booking/Residency) chờ nhận phòng ({pendingStudents.length})
            </div>
            <div className="checkin-pending-table-wrap">
              <table className="checkin-pending-table">
                <thead>
                  <tr>
                    <th>MSSV</th>
                    <th>Họ và tên</th>
                    <th>Tòa / Phòng / Giường</th>
                    <th>Học kỳ</th>
                    <th>Trạng thái đơn</th>
                    <th style={{ width: 130, textAlign: 'right' }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {pendingStudents.length === 0 ? (
                    <tr>
                      <td colSpan={6} style={{ textAlign: 'center', color: '#64748b', padding: '18px' }}>
                        Không có sinh viên nào đang chờ Check-in theo bộ lọc hiện tại.
                      </td>
                    </tr>
                  ) : (
                    pendingStudents.map(st => {
                      const isSelected =
                        selectedStudent?.studentId === st.studentId && selectedStudent?.bedId === st.bedId;
                      return (
                        <tr
                          key={`${st.studentId}-${st.bedId}`}
                          className={isSelected ? 'selected-row' : ''}
                          onClick={() => handleSelectStudent(st)}
                          style={{ cursor: 'pointer' }}
                        >
                          <td style={{ fontWeight: 700 }}>{st.studentCode}</td>
                          <td>{st.studentName}</td>
                          <td>
                            <strong>{st.blockCode || st.blockName}</strong> · P.{st.roomNumber} · Giường{' '}
                            <strong>{st.bedCode}</strong>
                          </td>
                          <td>{st.termName}</td>
                          <td>
                            <span className="checkin-status-badge">{st.bookingStatus}</span>
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <button
                              type="button"
                              className={isSelected ? 'btn-submit' : 'btn-secondary'}
                              style={{ padding: '5px 12px', fontSize: 12 }}
                              onClick={e => {
                                e.stopPropagation();
                                handleSelectStudent(st);
                              }}
                            >
                              {isSelected ? 'Đang chọn' : 'Chọn Check-in'}
                            </button>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {/* CHẾ ĐỘ 1: LẬP BIÊN BẢN CHECK-IN */}
      {mode === 'create' && completedHandover && (
        <div>
          <div className="checkin-actions-bar no-print" style={{ marginBottom: 16 }}>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => {
                setCompletedHandover(null);
                if (pendingStudents.length > 0) {
                  handleSelectStudent(pendingStudents[0]);
                }
              }}
            >
              Check-in sinh viên tiếp theo
            </button>
            {onNavigateHistory && (
              <button type="button" className="btn-secondary" onClick={onNavigateHistory}>
                Xem danh sách biên bản
              </button>
            )}
            <button type="button" className="btn-submit" onClick={() => window.print()}>
              In biên bản bàn giao (A4)
            </button>
          </div>

          {renderPrintablePaper(completedHandover)}
        </div>
      )}

      {mode === 'create' && !completedHandover && selectedStudent && (
        <form onSubmit={handleCompleteCheckin}>
          <div className="fpt-handover-paper">
            {/* Quốc hiệu & Logo */}
            <div className="fpt-paper-top">
              <div className="fpt-paper-top-left">
                <div className="fpt-logo-text">FPT Education</div>
                <div className="fpt-org-title">TỔ CHỨC GIÁO DỤC FPT</div>
              </div>
              <div className="fpt-paper-top-right">
                <div className="fpt-nation-title">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
                <div className="fpt-nation-sub">Độc lập - Tự do - Hạnh phúc</div>
              </div>
            </div>

            {/* Tiêu đề biên bản */}
            <div className="fpt-paper-banner">
              BIÊN BẢN BÀN GIAO TÀI SẢN BLOCK: {selectedStudent.blockCode || selectedStudent.blockName} — PHÒNG{' '}
              {selectedStudent.roomNumber} (GIƯỜNG {selectedStudent.bedCode})
            </div>

            {/* Mục 1 & Mục 2 */}
            <div className="fpt-paper-parties">
              <div className="fpt-party-heading">1 - Bên bàn giao:</div>
              <div>
                Ông/Bà: <strong>{currentUser?.fullName || 'Cán bộ Bảo vệ'}</strong> ({currentUser?.code || 'GRD01'})
              </div>
              <div>Bộ phận: An ninh - Phòng Quản lý ký túc xá.</div>

              <div className="fpt-party-heading" style={{ marginTop: 8 }}>
                2 – Bên nhận bàn giao:
              </div>
              <div>
                Anh/Chị: <strong>{selectedStudent.studentName}</strong>
              </div>
              <div>
                MSSV(CBGV): <strong>{selectedStudent.studentCode}</strong>
              </div>
              <div className="fpt-party-line">
                <span>Chức danh:</span>
                <input
                  type="text"
                  className="fpt-inline-input"
                  value={studentTitle}
                  onChange={e => setStudentTitle(e.target.value)}
                  placeholder="Sinh viên"
                />
                <span style={{ color: '#475569', fontSize: 13 }}>
                  (Phòng {selectedStudent.roomNumber} · Giường {selectedStudent.bedCode} · Kỳ {selectedStudent.termName})
                </span>
              </div>

              <div className="fpt-party-heading" style={{ marginTop: 8 }}>
                3 – Loại thiết bị bàn giao:
              </div>
            </div>

            {/* Bảng thiết bị lấy động từ bảng Equipments của phòng */}
            <table className="fpt-equip-table">
              <thead>
                <tr>
                  <th style={{ width: 48 }}>TT</th>
                  <th>Loại thiết bị (Theo dữ liệu Phòng {selectedStudent.roomNumber})</th>
                  <th style={{ width: 170 }}>Số lượng</th>
                  <th style={{ width: 320 }}>Tình trạng</th>
                </tr>
              </thead>
              <tbody>
                {equipItems.length === 0 ? (
                  <tr>
                    <td colSpan={4} style={{ textAlign: 'center', color: '#64748b', padding: '14px' }}>
                      Phòng {selectedStudent.roomNumber} hiện chưa có thiết bị nào trong bảng Equipments. Bạn vẫn có thể ghi nhận Vật dụng khác, Số điện/nước và hoàn tất Check-in.
                    </td>
                  </tr>
                ) : (
                  equipItems.map((item, idx) => (
                    <tr key={item.equipmentId}>
                      <td style={{ textAlign: 'center' }}>{idx + 1}</td>
                      <td>
                        <div style={{ fontWeight: 700 }}>{item.catalogName}</div>
                        <div style={{ fontSize: 12, color: '#64748b' }}>
                          Mã TS: <strong>{item.assetCode}</strong> · Danh mục: {item.category}
                          {item.bedCode ? ` · Giường ${item.bedCode}` : ' · Dùng chung phòng'}
                        </div>
                      </td>
                      <td style={{ textAlign: 'center', fontWeight: 600 }}>01</td>
                      <td>
                        <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                          <select
                            className="fpt-equip-select"
                            value={item.itemCondition}
                            onChange={e =>
                              handleConditionChange(
                                item.equipmentId,
                                e.target.value as 'Good' | 'Damaged' | 'Missing'
                              )
                            }
                          >
                            <option value="Good">Tốt / Bình thường</option>
                            <option value="Damaged">Hư hỏng</option>
                            <option value="Missing">Thiếu</option>
                          </select>
                          <input
                            type="text"
                            className="fpt-equip-note-input"
                            value={item.handoverNote}
                            onChange={e => handleNoteChange(item.equipmentId, e.target.value)}
                            placeholder="Ghi chú tình trạng..."
                          />
                        </div>
                      </td>
                    </tr>
                  ))
                )}

                {/* Dòng cuối giống mẫu giấy: Vật dụng khác | Số điện (L/R) | Số nước (L/R) */}
                <tr>
                  <td style={{ textAlign: 'center' }}>{equipItems.length + 1}</td>
                  <td>
                    <div style={{ fontWeight: 700, marginBottom: 4 }}>Vật dụng khác:</div>
                    <input
                      type="text"
                      className="fpt-equip-note-input"
                      placeholder="Nhập các vật dụng bàn giao thêm (nếu có)..."
                      value={otherItems}
                      onChange={e => setOtherItems(e.target.value)}
                    />
                  </td>
                  <td style={{ padding: '4px 10px' }}>
                    <div style={{ textAlign: 'center', fontWeight: 700, fontStyle: 'italic' }}>Số điện</div>
                    <div className="fpt-meter-split">
                      <div className="fpt-meter-cell">
                        <span>L:</span>
                        <input
                          type="text"
                          className="fpt-meter-input"
                          placeholder="0"
                          value={electricityL}
                          onChange={e => setElectricityL(e.target.value)}
                        />
                      </div>
                      <div className="fpt-meter-cell">
                        <span>R:</span>
                        <input
                          type="text"
                          className="fpt-meter-input"
                          placeholder="0"
                          value={electricityR}
                          onChange={e => setElectricityR(e.target.value)}
                        />
                      </div>
                    </div>
                  </td>
                  <td style={{ padding: '4px 10px' }}>
                    <div style={{ textAlign: 'center', fontWeight: 700, fontStyle: 'italic' }}>Số nước</div>
                    <div className="fpt-meter-split">
                      <div className="fpt-meter-cell">
                        <span>L:</span>
                        <input
                          type="text"
                          className="fpt-meter-input"
                          placeholder="0"
                          value={waterL}
                          onChange={e => setWaterL(e.target.value)}
                        />
                      </div>
                      <div className="fpt-meter-cell">
                        <span>R:</span>
                        <input
                          type="text"
                          className="fpt-meter-input"
                          placeholder="0"
                          value={waterR}
                          onChange={e => setWaterR(e.target.value)}
                        />
                      </div>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>

            {/* Chân trang chữ ký */}
            <div className="fpt-paper-footer">
              <div className="fpt-date-line">
                Hòa Lạc, ngày {String(now.getDate()).padStart(2, '0')} tháng{' '}
                {String(now.getMonth() + 1).padStart(2, '0')} năm {now.getFullYear()}
              </div>
              <div className="fpt-signatures-grid">
                <div>
                  <div>Người nhận bàn giao</div>
                  <div style={{ fontWeight: 400, fontSize: 12, fontStyle: 'italic' }}>
                    (Sinh viên kiểm tra & ký xác nhận)
                  </div>
                  <div className="fpt-sig-name">{selectedStudent.studentName}</div>
                </div>
                <div>
                  <div>Người bàn giao</div>
                  <div style={{ fontWeight: 400, fontSize: 12, fontStyle: 'italic' }}>
                    (Cán bộ Bảo vệ lập biên bản)
                  </div>
                  <div className="fpt-sig-name">{currentUser?.fullName || 'Cán bộ Bảo vệ'}</div>
                </div>
              </div>
            </div>
          </div>

          <div className="checkin-actions-bar no-print">
            <button type="button" className="btn-secondary" onClick={() => window.print()}>
              In phôi biên bản
            </button>
            <button type="submit" className="btn-submit" disabled={submitting}>
              {submitting ? 'Đang lưu & kích hoạt phòng...' : 'Hoàn tất Check-in'}
            </button>
          </div>
        </form>
      )}

      {/* CHẾ ĐỘ 2: LỊCH SỬ BIÊN BẢN ĐÃ CHECK-IN */}
      {mode === 'history' && (
        <div>
          <div className="checkin-filter-card no-print">
            <div className="violation-label" style={{ marginBottom: 10 }}>
              Danh sách biên bản bàn giao Check-in đã hoàn tất ({historyList.length})
            </div>
            <div className="checkin-pending-table-wrap">
              <table className="checkin-pending-table">
                <thead>
                  <tr>
                    <th>Mã BB</th>
                    <th>MSSV</th>
                    <th>Họ tên sinh viên</th>
                    <th>Tòa / Phòng / Giường</th>
                    <th>Bảo vệ bàn giao</th>
                    <th>Ngày Check-in</th>
                    <th>Tình trạng TS</th>
                    <th style={{ textAlign: 'right' }}>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {historyList.length === 0 ? (
                    <tr>
                      <td colSpan={8} style={{ textAlign: 'center', color: '#64748b', padding: '18px' }}>
                        Chưa có biên bản bàn giao Check-in nào.
                      </td>
                    </tr>
                  ) : (
                    historyList.map(h => (
                      <tr
                        key={h.id}
                        className={viewingHandover?.id === h.id ? 'selected-row' : ''}
                        onClick={() => setViewingHandover(h)}
                        style={{ cursor: 'pointer' }}
                      >
                        <td style={{ fontWeight: 700 }}>#BB-{String(h.id).padStart(4, '0')}</td>
                        <td style={{ fontWeight: 600 }}>{h.studentCode}</td>
                        <td>{h.studentName}</td>
                        <td>
                          {h.blockCode} · P.{h.roomNumber} · Giường {h.bedCode}
                        </td>
                        <td>{h.guardName}</td>
                        <td>{new Date(h.createdTime).toLocaleString('vi-VN')}</td>
                        <td>
                          <span className={`checkin-status-badge ${h.hasDamageOrMissingFlag ? '' : 'completed'}`}>
                            {h.hasDamageOrMissingFlag ? 'Có ghi nhận hư hỏng/thiếu' : 'Đầy đủ / Tốt'}
                          </span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <button
                            type="button"
                            className="btn-secondary"
                            style={{ padding: '5px 12px', fontSize: 12 }}
                            onClick={e => {
                              e.stopPropagation();
                              setViewingHandover(h);
                            }}
                          >
                            Xem / In biên bản
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {viewingHandover && (
            <div>
              <div className="checkin-actions-bar no-print" style={{ marginBottom: 16 }}>
                <button type="button" className="btn-secondary" onClick={() => setViewingHandover(null)}>
                  Đóng xem chi tiết
                </button>
                <button type="button" className="btn-submit" onClick={() => window.print()}>
                  In biên bản bàn giao (A4)
                </button>
              </div>
              {renderPrintablePaper(viewingHandover)}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
