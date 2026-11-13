import React, { useState, useEffect } from 'react';
import {
  violationApi,
  facilityCommonApi,
  type ViolationType,
  type RoomStudent,
  type GuardBlockItem,
  type RoomItem,
} from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';
import '../../styles/violation.css';

interface RecordViolationPageProps {
  onBack?: () => void;
  onSuccessNavigate?: () => void;
}

export const RecordViolationPage: React.FC<RecordViolationPageProps> = ({ onBack, onSuccessNavigate }) => {
  const { currentUser } = useAuth();
  // Master data
  const [blocks, setBlocks] = useState<GuardBlockItem[]>([]);
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
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
  });
  const [description, setDescription] = useState<string>('');
  const [files, setFiles] = useState<File[]>([]);
  const [filePreviews, setFilePreviews] = useState<{ file: File; url: string }[]>([]);


  // Status message
  const [loading, setLoading] = useState<boolean>(false);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState<string>('');

  // 1. Initial Load: Blocks & Violation Types
  useEffect(() => {
    const initData = async () => {
      try {
        const [blocksData, typesData] = await Promise.all([
          violationApi.getGuardBlocks(),
          violationApi.getTypes()
        ]);

        const validBlocks = blocksData || [];
        const validTypes = typesData || [];

        setBlocks(validBlocks);
        setViolationTypes(validTypes);

        if (validBlocks.length > 0) {
          setSelectedBlockId(validBlocks[0].id.toString());
        }
        if (validTypes.length > 0) {
          setSelectedTypeId(validTypes[0].id.toString());
        }
      } catch (err: any) {
        console.error('Error loading initial data:', err);
        setErrorMsg('Không thể tải danh sách Block hoặc Loại vi phạm từ hệ thống.');
      }
    };

    initData();
  }, []);

  // 2. Fetch Rooms when Block changes
  useEffect(() => {
    if (!selectedBlockId) return;

    const fetchRooms = async () => {
      try {
        const roomsData = await facilityCommonApi.getRoomsByBlock(parseInt(selectedBlockId, 10));
        const validRooms = roomsData || [];
        setRooms(validRooms);
        if (validRooms.length > 0) {
          setSelectedRoomId(validRooms[0].id.toString());
        } else {
          setSelectedRoomId('');
          setRoomStudents([]);
          setSelectedStudentIds([]);
        }
      } catch (err: any) {
        console.error('Error loading rooms:', err);
        setRooms([]);
        setSelectedRoomId('');
        setRoomStudents([]);
        setSelectedStudentIds([]);
      }
    };

    fetchRooms();
  }, [selectedBlockId]);

  // 3. Fetch Students when Room changes
  useEffect(() => {
    if (!selectedRoomId) {
      setRoomStudents([]);
      setSelectedStudentIds([]);
      return;
    }

    const fetchStudents = async () => {
      try {
        const studentsData = await violationApi.getStudentsByRoom(parseInt(selectedRoomId, 10));
        // Deduplicate theo studentId (đề phòng API trả về trùng)
        const seen = new Set<number>();
        const unique = (studentsData || []).filter(s => {
          if (seen.has(s.studentId)) return false;
          seen.add(s.studentId);
          return true;
        });
        setRoomStudents(unique);
        setSelectedStudentIds([]);
      } catch (err: any) {
        console.error('Error loading room students:', err);
        setRoomStudents([]);
        setSelectedStudentIds([]);
      }
    };

    fetchStudents();
  }, [selectedRoomId]);

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

  // Submit Violation Recording (UC22: Bỏ kiểm tra trùng lặp)
  const executeSubmit = async () => {
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

    setLoading(true);

    try {
      const formData = new FormData();
      formData.append('roomId', selectedRoomId);
      formData.append('violationTypeId', selectedTypeId);
      // occurrenceTime từ input dạng 'YYYY-MM-DDTHH:mm', chuyển sang UTC ISO String
      formData.append('occurrenceTime', new Date(occurrenceTime).toISOString());
      formData.append('description', description);

      selectedStudentIds.forEach(sId => {
        formData.append('studentIds', sId.toString());
      });

      files.forEach(file => {
        formData.append('evidences', file);
      });

      await violationApi.recordViolation(formData);

      setSuccessMsg(
        `Ghi nhận vi phạm thành công cho ${selectedStudentIds.length} sinh viên! Ảnh đã được tải lên.`
      );

      setDescription('');
      setFiles([]);
      setFilePreviews([]);

      if (onSuccessNavigate) {
        setTimeout(onSuccessNavigate, 1500);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Lỗi khi ghi nhận vi phạm');
    } finally {
      setLoading(false);
    }
  };

  const selectedStudentsList = roomStudents.filter(s => selectedStudentIds.includes(s.studentId));
  const currentRoomNumber = rooms.find(r => r.id.toString() === selectedRoomId)?.roomNumber || '';

  return (
    <div className="violation-record-container">
      {/* Header */}
      <div className="violation-record-header">
        <h1>Ghi nhận vi phạm</h1>
        <p style={{ color: '#64748b', fontSize: '13px', margin: 0 }}>
          Lập biên bản vi phạm nội quy KTX cho sinh viên và đẩy ảnh minh chứng
        </p>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success">{successMsg}</div>}

      <form onSubmit={e => { e.preventDefault(); executeSubmit(); }}>
        {/* CARD 1: Phòng và sinh viên liên quan */}
        <div className="violation-card-section">
          <div className="violation-card-title">Phòng và sinh viên liên quan</div>
          <div className="violation-grid-2">
            <div className="violation-form-group">
              {(blocks.length > 0 && blocks[0].dormName) ? (
                <div style={{ marginBottom: 6, fontSize: 13, color: '#000000ff', fontWeight: 600 }}>
                  KTX: {blocks[0].dormName}
                </div>
              ) : currentUser?.dormName ? (
                <div style={{ marginBottom: 6, fontSize: 13, color: '#000000ff', fontWeight: 600 }}>
                  KTX: {currentUser.dormName}
                </div>
              ) : null}
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

          {/* Tìm theo MSSV / Tên */}
          <div className="violation-search-row">
            <div className="violation-form-group" style={{ flex: 1 }}>
              <label className="violation-label">Tìm theo MSSV hoặc họ tên</label>
              <input
                type="text"
                className="violation-input"
                placeholder="VD: SE180003 hoặc Lê Quang Huy"
                value={searchMssv}
                onChange={e => setSearchMssv(e.target.value)}
              />
            </div>
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
              ) : (() => {
                const term = searchMssv.trim().toLowerCase();
                const displayed = term
                  ? roomStudents.filter(s =>
                      s.studentCode.toLowerCase().includes(term) ||
                      s.fullName.toLowerCase().includes(term)
                    )
                  : roomStudents;
                return displayed.length === 0 ? (
                  <tr>
                    <td colSpan={4} style={{ textAlign: 'center', color: '#64748b' }}>
                      Không tìm thấy sinh viên khớp — nhấn "Tìm sinh viên" để tìm ngoài phòng
                    </td>
                  </tr>
                ) : displayed.map(student => (
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
                ));
              })()}
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
                JPG/PNG · Tối đa 5 MB mỗi ảnh · Ít nhất 1 ảnh (Đẩy trực tiếp lên)
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
            {loading ? 'Đang kiểm tra & Tải lên...' : 'Ghi nhận vi phạm'}
          </button>
        </div>
      </form>
    </div>
  );
};
