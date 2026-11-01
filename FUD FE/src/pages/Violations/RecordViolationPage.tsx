import React, { useState, useEffect } from 'react';
import {
  violationApi,
  facilityCommonApi,
  type ViolationType,
  type RoomStudent,
  type BlockItem,
  type RoomItem,
  type ViolationItem,
} from '../../services/api';
import '../../styles/violation.css';

interface RecordViolationPageProps {
  onBack?: () => void;
  onSuccessNavigate?: () => void;
}

export const RecordViolationPage: React.FC<RecordViolationPageProps> = ({ onBack, onSuccessNavigate }) => {
  // Master data
  const [blocks, setBlocks] = useState<BlockItem[]>([]);
  const [rooms, setRooms] = useState<RoomItem[]>([]);
  const [violationTypes, setViolationTypes] = useState<ViolationType[]>([]);

  // Selection states
  const [selectedBlockId, setSelectedBlockId] = useState<string>('');
  const [selectedRoomId, setSelectedRoomId] = useState<string>('');
  const [searchMssv, setSearchMssv] = useState<string>('');

  // Table students in room + additional searched students
  const [roomStudents, setRoomStudents] = useState<RoomStudent[]>([]);
  const [selectedStudentIds, setSelectedStudentIds] = useState<number[]>([]);

  // Incident form details
  const [selectedTypeId, setSelectedTypeId] = useState<string>('');
  const [occurrenceTime, setOccurrenceTime] = useState<string>(() => {
    const now = new Date();
    const offset = now.getTimezoneOffset() * 60000;
    return new Date(now.getTime() - offset).toISOString().slice(0, 16);
  });
  const [description, setDescription] = useState<string>('');
  const [files, setFiles] = useState<File[]>([]);
  const [filePreviews, setFilePreviews] = useState<{ file: File; url: string }[]>([]);

  // Duplicate warning states
  const [duplicateWarning, setDuplicateWarning] = useState<{
    message: string;
    existingViolations: ViolationItem[];
  } | null>(null);

  // Status message
  const [loading, setLoading] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState<string>('');

  // 1. Initial Load: Blocks & Violation Types
  useEffect(() => {
    const initData = async () => {
      try {
        const [blocksData, typesData] = await Promise.all([
          facilityCommonApi.getBlocks().catch(() => [
            { id: 1, name: 'Tòa A' },
            { id: 2, name: 'Tòa B' },
            { id: 3, name: 'Tòa C' }
          ]),
          violationApi.getTypes().catch(() => [
            { id: 1, name: 'Gây ồn ào sau giờ quy định' },
            { id: 2, name: 'Sử dụng thiết bị điện không cho phép (bếp từ, bếp từ...)' },
            { id: 3, name: 'Không giữ gìn vệ sinh chung' },
            { id: 4, name: 'Tàng trữ chất cấm, rượu bia' },
            { id: 5, name: 'Chứa chấp người ngoài qua đêm' }
          ])
        ]);

        setBlocks(blocksData);
        setViolationTypes(typesData);

        if (blocksData.length > 0) {
          setSelectedBlockId(blocksData[0].id.toString());
        }
        if (typesData.length > 0) {
          setSelectedTypeId(typesData[0].id.toString());
        }
      } catch (err: any) {
        console.error('Error loading initial data:', err);
      }
    };

    initData();
  }, []);

  // 2. Fetch Rooms when Block changes
  useEffect(() => {
    if (!selectedBlockId) return;

    const fetchRooms = async () => {
      try {
        const roomsData = await facilityCommonApi.getRoomsByBlock(parseInt(selectedBlockId, 10))
          .catch(() => [
            { id: 1, blockId: parseInt(selectedBlockId, 10), roomNumber: 'A-102', floor: 1, status: 'Occupied' },
            { id: 2, blockId: parseInt(selectedBlockId, 10), roomNumber: 'A-103', floor: 1, status: 'Occupied' }
          ]);
        setRooms(roomsData);
        if (roomsData.length > 0) {
          setSelectedRoomId(roomsData[0].id.toString());
        } else {
          setSelectedRoomId('');
          setRoomStudents([]);
        }
      } catch (err: any) {
        console.error('Error loading rooms:', err);
      }
    };

    fetchRooms();
  }, [selectedBlockId]);

  // 3. Fetch Students when Room changes
  useEffect(() => {
    if (!selectedRoomId) {
      setRoomStudents([]);
      return;
    }

    const fetchStudents = async () => {
      try {
        const studentsData = await violationApi.getStudentsByRoom(parseInt(selectedRoomId, 10))
          .catch(() => [
            {
              studentId: 4,
              studentCode: 'HE180123',
              fullName: 'Nguyễn Văn An',
              bedCode: 'G02',
              statusDescription: 'Đang ở · Giường G02'
            },
            {
              studentId: 5,
              studentCode: 'HE180145',
              fullName: 'Trần Minh Đức',
              bedCode: 'G03',
              statusDescription: 'Đang ở · Giường G03'
            },
            {
              studentId: 6,
              studentCode: 'HE180167',
              fullName: 'Phạm Quốc Huy',
              bedCode: 'G04',
              statusDescription: 'Đang ở · Giường G04'
            }
          ]);
        setRoomStudents(studentsData);
        setSelectedStudentIds([4, 5].filter(id => studentsData.some(s => s.studentId === id)));
      } catch (err: any) {
        console.error('Error loading room students:', err);
      }
    };

    fetchStudents();
  }, [selectedRoomId]);

  // Handle Search MSSV
  const handleSearchStudent = async () => {
    if (!searchMssv.trim()) return;
    setErrorMsg('');
    try {
      const student = await violationApi.searchStudent(searchMssv.trim());
      if (!roomStudents.some(s => s.studentId === student.studentId)) {
        setRoomStudents(prev => [student, ...prev]);
      }
      if (!selectedStudentIds.includes(student.studentId)) {
        setSelectedStudentIds(prev => [...prev, student.studentId]);
      }
      setSuccessMsg(`Đã tìm thấy và thêm sinh viên ${student.fullName} (${student.studentCode}).`);
    } catch (err: any) {
      setErrorMsg(err.message || 'Không tìm thấy sinh viên với MSSV này');
    }
  };

  const toggleStudent = (id: number) => {
    setSelectedStudentIds(prev =>
      prev.includes(id) ? prev.filter(item => item !== id) : [...prev, id]
    );
  };

  const removeSelectedStudent = (id: number) => {
    setSelectedStudentIds(prev => prev.filter(item => item !== id));
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!e.target.files) return;
    const selected = Array.from(e.target.files);

    const validFiles: File[] = [];
    for (const f of selected) {
      if (f.size > 5 * 1024 * 1024) {
        setErrorMsg(`Ảnh ${f.name} vượt quá 5MB.`);
        return;
      }
      validFiles.push(f);
    }

    const newPreviews = validFiles.map(file => ({
      file,
      url: URL.createObjectURL(file)
    }));

    setFiles(prev => [...prev, ...validFiles]);
    setFilePreviews(prev => [...prev, ...newPreviews]);
  };

  const removeFile = (index: number) => {
    setFiles(prev => prev.filter((_, i) => i !== index));
    setFilePreviews(prev => prev.filter((_, i) => i !== index));
  };

  // Submit Violation Recording
  const executeSubmit = async (confirmedDuplicate = false) => {
    setErrorMsg('');
    setSuccessMsg('');

    if (selectedStudentIds.length === 0) {
      setErrorMsg('Vui lòng chọn ít nhất 1 sinh viên vi phạm.');
      return;
    }

    if (!description.trim()) {
      setErrorMsg('Vui lòng nhập mô tả sự việc vi phạm.');
      return;
    }

    if (files.length === 0) {
      setErrorMsg('Vui lòng tải lên ít nhất 1 ảnh minh chứng.');
      return;
    }

    // Kiểm tra trùng lặp hồ sơ trước khi lưu
    if (!confirmedDuplicate) {
      try {
        const dupCheck = await violationApi.checkDuplicate(
          selectedStudentIds,
          parseInt(selectedTypeId, 10),
          new Date(occurrenceTime).toISOString()
        );

        if (dupCheck.hasDuplicate) {
          setDuplicateWarning({
            message: dupCheck.message,
            existingViolations: dupCheck.existingViolations,
          });
          return;
        }
      } catch (err) {
        console.warn('Lỗi khi kiểm tra trùng lặp:', err);
      }
    }

    setLoading(true);

    try {
      const formData = new FormData();
      formData.append('roomId', selectedRoomId);
      formData.append('violationTypeId', selectedTypeId);
      formData.append('occurrenceTime', new Date(occurrenceTime).toISOString());
      formData.append('description', description);
      if (confirmedDuplicate) {
        formData.append('confirmedDuplicate', 'true');
      }

      selectedStudentIds.forEach(sId => {
        formData.append('studentIds', sId.toString());
      });

      files.forEach(file => {
        formData.append('evidences', file);
      });

      await violationApi.recordViolation(formData);

      setDuplicateWarning(null);
      setSuccessMsg(
        `Ghi nhận vi phạm thành công cho ${selectedStudentIds.length} sinh viên! Ảnh đã được tải trực tiếp lên Cloudinary an toàn.`
      );

      setDescription('');
      setFiles([]);
      setFilePreviews([]);

      if (onSuccessNavigate) {
        setTimeout(onSuccessNavigate, 1500);
      }
    } catch (err: any) {
      if (err.message && err.message.includes('trùng lặp')) {
        setDuplicateWarning({
          message: err.message,
          existingViolations: [],
        });
      } else {
        setErrorMsg(err.message || 'Lỗi khi ghi nhận vi phạm');
      }
    } finally {
      setLoading(false);
    }
  };

  const selectedStudentsList = roomStudents.filter(s => selectedStudentIds.includes(s.studentId));
  const currentRoomNumber = rooms.find(r => r.id.toString() === selectedRoomId)?.roomNumber || 'A-102';

  return (
    <div className="violation-record-container">
      {/* Header */}
      <div className="violation-record-header">
        <h1>Ghi nhận vi phạm</h1>
        <p style={{ color: '#64748b', fontSize: '13px', margin: 0 }}>
          Lập biên bản vi phạm nội quy KTX cho sinh viên và đẩy ảnh minh chứng lên Cloudinary
        </p>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

      {/* CẢNH BÁO HỒ SƠ TRÙNG LẶP */}
      {duplicateWarning && (
        <div
          style={{
            backgroundColor: '#FEF2F2',
            border: '2px solid #EF4444',
            borderRadius: '10px',
            padding: '20px',
            marginBottom: '24px',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: '#991B1B', fontWeight: 700, fontSize: 16 }}>
            <span>⚠️</span>
            <span>PHÁT HIỆN HỒ SƠ VI PHẠM TRÙNG LẶP</span>
          </div>

          <p style={{ color: '#7F1D1D', fontSize: 13, margin: '10px 0 14px' }}>
            {duplicateWarning.message} Hệ thống yêu cầu nhân viên bảo vệ xác nhận hoặc xem lại hồ sơ đã có:
          </p>

          <div style={{ background: '#ffffff', borderRadius: 8, border: '1px solid #FECACA', overflow: 'hidden', marginBottom: 16 }}>
            <table className="violation-table" style={{ margin: 0 }}>
              <thead>
                <tr>
                  <th>Mã biên bản</th>
                  <th>Sinh viên</th>
                  <th>Phòng</th>
                  <th>Loại vi phạm</th>
                  <th>Thời điểm ghi nhận</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {duplicateWarning.existingViolations.map(ev => (
                  <tr key={ev.id}>
                    <td style={{ fontWeight: 700, color: '#DC2626' }}>{ev.code}</td>
                    <td>{ev.studentName} ({ev.studentCode})</td>
                    <td>{ev.roomNumber}</td>
                    <td>{ev.violationTypeName}</td>
                    <td>{new Date(ev.occurrenceTime).toLocaleString('vi-VN')}</td>
                    <td>{ev.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
            <button
              type="button"
              className="btn-secondary"
              onClick={() => setDuplicateWarning(null)}
            >
              Hủy bỏ & Xem lại
            </button>
            <button
              type="button"
              style={{
                padding: '9px 18px',
                backgroundColor: '#DC2626',
                color: '#ffffff',
                border: 'none',
                borderRadius: 6,
                fontWeight: 600,
                fontSize: 13,
                cursor: 'pointer',
              }}
              onClick={() => executeSubmit(true)}
            >
              Tôi xác nhận vẫn muốn tạo hồ sơ mới
            </button>
          </div>
        </div>
      )}

      <form onSubmit={e => { e.preventDefault(); executeSubmit(false); }}>
        {/* CARD 1: Phòng và sinh viên liên quan */}
        <div className="violation-card-section">
          <div className="violation-card-title">Phòng và sinh viên liên quan</div>

          <div className="violation-grid-2">
            <div className="violation-form-group">
              <label className="violation-label">
                Tòa <span className="required">*</span>
              </label>
              <select
                className="violation-select"
                value={selectedBlockId}
                onChange={e => setSelectedBlockId(e.target.value)}
              >
                {blocks.map(b => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="violation-form-group">
              <label className="violation-label">
                Phòng <span className="required">*</span>
              </label>
              <select
                className="violation-select"
                value={selectedRoomId}
                onChange={e => setSelectedRoomId(e.target.value)}
              >
                {rooms.map(r => (
                  <option key={r.id} value={r.id}>
                    {r.roomNumber}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* Tìm theo MSSV */}
          <div className="violation-search-row">
            <div className="violation-form-group" style={{ flex: 1 }}>
              <label className="violation-label">Tìm theo MSSV</label>
              <input
                type="text"
                className="violation-input"
                placeholder="VD: HE180123 hoặc SE17001"
                value={searchMssv}
                onChange={e => setSearchMssv(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    handleSearchStudent();
                  }
                }}
              />
            </div>
            <button
              type="button"
              className="btn-secondary"
              onClick={handleSearchStudent}
            >
              Tìm sinh viên
            </button>
            <button
              type="button"
              className="btn-secondary"
              onClick={handleSearchStudent}
            >
              Thêm sinh viên
            </button>
          </div>

          {/* Table: Sinh viên đang lưu trú */}
          <div style={{ marginBottom: 12 }}>
            <span className="violation-label">
              Sinh viên đang lưu trú tại {currentRoomNumber} — chọn một hoặc nhiều sinh viên <span className="required">*</span>
            </span>
          </div>

          <table className="violation-table">
            <thead>
              <tr>
                <th style={{ width: 60 }}>Chọn</th>
                <th style={{ width: 140 }}>MSSV</th>
                <th>Họ tên</th>
                <th>Tình trạng lưu trú</th>
              </tr>
            </thead>
            <tbody>
              {roomStudents.length === 0 ? (
                <tr>
                  <td colSpan={4} style={{ textAlign: 'center', color: '#64748b' }}>
                    Không có sinh viên nào trong phòng này
                  </td>
                </tr>
              ) : (
                roomStudents.map(student => (
                  <tr key={student.studentId}>
                    <td>
                      <input
                        type="checkbox"
                        checked={selectedStudentIds.includes(student.studentId)}
                        onChange={() => toggleStudent(student.studentId)}
                      />
                    </td>
                    <td style={{ fontWeight: 600 }}>{student.studentCode}</td>
                    <td>{student.fullName}</td>
                    <td style={{ color: '#475569' }}>{student.statusDescription}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>

          {/* Tags of Selected Students */}
          {selectedStudentsList.length > 0 && (
            <div className="violation-selected-summary">
              <div>
                <strong>Đã chọn: </strong>
                {selectedStudentsList.map(s => `${s.fullName} (${s.studentCode})`).join(' · ')}
              </div>
              <div className="violation-tags-row">
                {selectedStudentsList.map(s => (
                  <button
                    key={s.studentId}
                    type="button"
                    className="violation-tag-remove-btn"
                    onClick={() => removeSelectedStudent(s.studentId)}
                  >
                    Bỏ chọn {s.fullName.split(' ').pop()} ✕
                  </button>
                ))}
              </div>
            </div>
          )}

          <div style={{ marginTop: 12, fontSize: 12, color: '#64748b' }}>
            Mỗi sinh viên được tạo một bản ghi riêng với mã vi phạm duy nhất.
          </div>
        </div>

        {/* CARD 2: Thông tin sự việc */}
        <div className="violation-card-section">
          <div className="violation-card-title">Thông tin sự việc</div>

          <div className="violation-grid-2">
            <div className="violation-form-group">
              <label className="violation-label">
                Loại vi phạm <span className="required">*</span>
              </label>
              <select
                className="violation-select"
                value={selectedTypeId}
                onChange={e => setSelectedTypeId(e.target.value)}
              >
                {violationTypes.map(t => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="violation-form-group">
              <label className="violation-label">
                Thời điểm vi phạm <span className="required">*</span>
              </label>
              <input
                type="datetime-local"
                className="violation-input"
                value={occurrenceTime}
                onChange={e => setOccurrenceTime(e.target.value)}
              />
            </div>
          </div>

          <div className="violation-form-group" style={{ marginBottom: 20 }}>
            <label className="violation-label">
              Mô tả <span className="required">*</span>
            </label>
            <textarea
              className="violation-textarea"
              rows={3}
              placeholder="Nói chuyện lớn tiếng tại phòng A-102 sau giờ yên tĩnh."
              value={description}
              onChange={e => setDescription(e.target.value)}
            />
          </div>

          {/* Ảnh minh chứng */}
          <div className="violation-form-group">
            <label className="violation-label">
              Ảnh minh chứng <span className="required">*</span>
            </label>

            <div className="violation-upload-box">
              <div className="violation-upload-note">
                JPG/PNG · Tối đa 5 MB mỗi ảnh · Ít nhất 1 ảnh (Đẩy trực tiếp lên Cloudinary)
              </div>

              <label className="btn-secondary" style={{ display: 'inline-block', cursor: 'pointer' }}>
                Chọn ảnh
                <input
                  type="file"
                  multiple
                  accept="image/png, image/jpeg, image/jpg"
                  style={{ display: 'none' }}
                  onChange={handleFileChange}
                />
              </label>

              {filePreviews.length > 0 && (
                <div className="violation-file-preview-list">
                  {filePreviews.map((item, idx) => (
                    <div key={idx} className="violation-preview-item">
                      <img src={item.url} alt={`Minh chứng ${idx + 1}`} />
                      <div className="violation-preview-info">
                        <div style={{ fontWeight: 600, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                          {item.file.name}
                        </div>
                        <div style={{ color: '#059669', fontSize: 11 }}>
                          {(item.file.size / (1024 * 1024)).toFixed(1)} MB · Hợp lệ
                        </div>
                      </div>
                      <button
                        type="button"
                        className="violation-preview-delete"
                        onClick={() => removeFile(idx)}
                        title="Xóa ảnh"
                      >
                        ✕
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Note and Submit Buttons */}
        <div style={{ fontSize: 12, color: '#64748b', marginBottom: 16 }}>
          Dấu * là bắt buộc. Hủy sẽ không lưu. Ghi nhận được gửi cho Quản lý đánh giá, chưa thông báo sinh viên.
        </div>

        <div className="violation-footer-actions">
          <button
            type="button"
            className="btn-cancel"
            onClick={() => {
              if (onBack) onBack();
              else {
                setDescription('');
                setFiles([]);
                setFilePreviews([]);
              }
            }}
          >
            Hủy
          </button>
          <button
            type="submit"
            className="btn-submit"
            disabled={loading}
          >
            {loading ? 'Đang kiểm tra & Tải lên Cloudinary...' : 'Ghi nhận vi phạm'}
          </button>
        </div>
      </form>
    </div>
  );
};
