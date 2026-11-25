import React, { useEffect, useState } from 'react';
import {
  checkinApi,
  facilityCommonApi,
  type BlockItem,
  type RoomItem,
  type CheckinHandoverDetail,
} from '../../services/api';
import '../../styles/checkin.css';
import '../../styles/violation.css';

const CONDITION_LABELS: Record<'Good' | 'Damaged' | 'Missing', string> = {
  Good: 'Bình thường / Tốt',
  Damaged: 'Hư hỏng',
  Missing: 'Thiếu',
};

type FilterStage = 'all' | 'troubleshooting' | 'check-record' | 'completed';

export const ManagerCheckinManagement: React.FC = () => {
  const [blocks, setBlocks] = useState<BlockItem[]>([]);
  const [rooms, setRooms] = useState<RoomItem[]>([]);
  const [selectedBlockId, setSelectedBlockId] = useState<string>('');
  const [selectedRoomId, setSelectedRoomId] = useState<string>('');
  const [searchKeyword, setSearchKeyword] = useState<string>('');
  const [stageFilter, setStageFilter] = useState<FilterStage>('all');

  const [list, setList] = useState<CheckinHandoverDetail[]>([]);
  const [viewingHandover, setViewingHandover] = useState<CheckinHandoverDetail | null>(null);
  const [managerNote, setManagerNote] = useState<string>('');
  const [repairEquips, setRepairEquips] = useState<boolean>(true);

  const [loading, setLoading] = useState<boolean>(false);
  const [processingId, setProcessingId] = useState<number | null>(null);
  const [errorMsg, setErrorMsg] = useState<string>('');
  const [successMsg, setSuccessMsg] = useState<string>('');

  useEffect(() => {
    facilityCommonApi
      .getBlocks()
      .then(data => setBlocks(data || []))
      .catch(() => setBlocks([]));
  }, []);

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

  const loadData = async () => {
    setLoading(true);
    setErrorMsg('');
    try {
      const bId = selectedBlockId ? parseInt(selectedBlockId, 10) : undefined;
      const rId = selectedRoomId ? parseInt(selectedRoomId, 10) : undefined;
      const kw = searchKeyword.trim() || undefined;
      const data = await checkinApi.getHistory({ blockId: bId, roomId: rId, search: kw });
      setList(data || []);
    } catch (err: any) {
      setErrorMsg(err?.message || 'Không thể tải danh sách biên bản Check-in.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [selectedBlockId, selectedRoomId]);

  const handleTroubleshoot = async (handover: CheckinHandoverDetail) => {
    try {
      setProcessingId(handover.id);
      setErrorMsg('');
      setSuccessMsg('');
      const updated = await checkinApi.managerTroubleshoot(handover.id, {
        note: managerNote.trim() || 'Đã xử lý khắc phục/thay thế thiết bị hư hỏng, bàn giao cho Sinh viên ký xác nhận.',
        repairDamagedEquipments: repairEquips,
      });
      setList(prev => prev.map(h => (h.id === handover.id ? updated : h)));
      if (viewingHandover?.id === handover.id) {
        setViewingHandover(updated);
      }
      setManagerNote('');
      setSuccessMsg(
        `Đã hoàn tất Troubleshooting cho biên bản #BB-${String(handover.id).padStart(4, '0')} (${handover.studentName}). Biên bản đã chuyển sang chờ Sinh viên ký xác nhận (Sign to confirm)!`
      );
    } catch (err: any) {
      setErrorMsg(err?.message || 'Lỗi khi xử lý sự cố thiết bị (Troubleshooting).');
    } finally {
      setProcessingId(null);
    }
  };

  const handleCheckRecord = async (handover: CheckinHandoverDetail) => {
    try {
      setProcessingId(handover.id);
      setErrorMsg('');
      setSuccessMsg('');
      const updated = await checkinApi.managerCheckRecord(handover.id, {
        note: managerNote.trim() || 'Ban Quản lý đã kiểm tra và duyệt lưu hồ sơ biên bản Check-in.',
      });
      setList(prev => prev.map(h => (h.id === handover.id ? updated : h)));
      if (viewingHandover?.id === handover.id) {
        setViewingHandover(updated);
      }
      setManagerNote('');
      setSuccessMsg(
        `Đã hoàn tất kiểm tra hồ sơ (Check record -> End) cho biên bản #BB-${String(handover.id).padStart(4, '0')} (${handover.studentName})!`
      );
    } catch (err: any) {
      setErrorMsg(err?.message || 'Lỗi khi kiểm tra chốt hồ sơ biên bản (Check record).');
    } finally {
      setProcessingId(null);
    }
  };

  const filteredList = list.filter(h => {
    if (stageFilter === 'troubleshooting') {
      return h.workflowStage === 'PendingManagerTroubleshooting' || h.status === 'Draft';
    }
    if (stageFilter === 'check-record') {
      return h.workflowStage === 'PendingManagerCheckRecord' || (h.status === 'Completed' && !h.managerReviewedTime);
    }
    if (stageFilter === 'completed') {
      return h.workflowStage === 'Completed' || Boolean(h.managerReviewedTime);
    }
    return true;
  });

  const troubleshootingCount = list.filter(
    h => h.workflowStage === 'PendingManagerTroubleshooting' || h.status === 'Draft'
  ).length;
  const checkRecordCount = list.filter(
    h => h.workflowStage === 'PendingManagerCheckRecord' || (h.status === 'Completed' && !h.managerReviewedTime)
  ).length;

  return (
    <div className="checkin-page-wrapper">
      <div className="checkin-header no-print">
        <div>
          <h1>Quản lý & Hậu kiểm Biên bản Check-in</h1>
          <p>
            Xử lý sự cố thiết bị khi nhận phòng (Troubleshooting) và kiểm tra chốt hồ sơ biên bản bàn giao (Check record)
          </p>
        </div>
      </div>

      {errorMsg && <div className="violation-alert violation-alert-error no-print">{errorMsg}</div>}
      {successMsg && <div className="violation-alert violation-alert-success no-print">{successMsg}</div>}

      <div className="checkin-filter-card no-print">
        <div style={{ display: 'flex', gap: 10, marginBottom: 16, flexWrap: 'wrap' }}>
          <button
            type="button"
            className={stageFilter === 'all' ? 'btn-submit' : 'btn-secondary'}
            onClick={() => setStageFilter('all')}
          >
            Tất cả ({list.length})
          </button>
          <button
            type="button"
            className={stageFilter === 'troubleshooting' ? 'btn-submit' : 'btn-secondary'}
            onClick={() => setStageFilter('troubleshooting')}
          >
            🔧 Cần Troubleshooting ({troubleshootingCount})
          </button>
          <button
            type="button"
            className={stageFilter === 'check-record' ? 'btn-submit' : 'btn-secondary'}
            onClick={() => setStageFilter('check-record')}
          >
            📋 Chờ Check Record ({checkRecordCount})
          </button>
          <button
            type="button"
            className={stageFilter === 'completed' ? 'btn-submit' : 'btn-secondary'}
            onClick={() => setStageFilter('completed')}
          >
            ✓ Đã hoàn tất
          </button>
        </div>

        <div className="checkin-filter-row">
          <div>
            <label className="violation-label">Block</label>
            <select
              className="violation-select"
              value={selectedBlockId}
              onChange={e => setSelectedBlockId(e.target.value)}
            >
              <option value="">-- Tất cả các Block --</option>
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
              placeholder="VD: SE170002, Hoàng Nữ Sinh, 202..."
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

        <div className="checkin-pending-table-wrap" style={{ marginTop: 16 }}>
          <table className="checkin-pending-table">
            <thead>
              <tr>
                <th>Mã BB</th>
                <th>MSSV</th>
                <th>Họ tên sinh viên</th>
                <th>Block / Phòng / Giường</th>
                <th>Bảo vệ lập</th>
                <th>Trạng thái luồng Check-in</th>
                <th style={{ textAlign: 'right' }}>Thao tác Quản lý</th>
              </tr>
            </thead>
            <tbody>
              {filteredList.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ textAlign: 'center', color: '#64748b', padding: '18px' }}>
                    Không có biên bản bàn giao Check-in nào theo bộ lọc hiện tại.
                  </td>
                </tr>
              ) : (
                filteredList.map(h => {
                  const isTroubleshooting =
                    h.workflowStage === 'PendingManagerTroubleshooting' || h.status === 'Draft';
                  const isCheckRecord =
                    h.workflowStage === 'PendingManagerCheckRecord' ||
                    (h.status === 'Completed' && !h.managerReviewedTime);

                  return (
                    <tr
                      key={h.id}
                      onClick={() => {
                        setViewingHandover(h);
                        setManagerNote(h.managerNote || '');
                      }}
                      style={{ cursor: 'pointer' }}
                    >
                      <td style={{ fontWeight: 700 }}>#BB-{String(h.id).padStart(4, '0')}</td>
                      <td style={{ fontWeight: 600 }}>{h.studentCode}</td>
                      <td>{h.studentName}</td>
                      <td>
                        {h.blockCode} · P.{h.roomNumber} · Giường {h.bedCode}
                      </td>
                      <td>{h.guardName}</td>
                      <td>
                        {isTroubleshooting ? (
                          <span className="checkin-status-badge" style={{ backgroundColor: '#FEF3C7', color: '#B45309' }}>
                            🔧 Cần Troubleshooting (Thiết bị hỏng/thiếu)
                          </span>
                        ) : h.workflowStage === 'PendingStudentSign' ? (
                          <span className="checkin-status-badge" style={{ backgroundColor: '#DBEAFE', color: '#1D4ED8' }}>
                            ✍️ Chờ SV ký (Sign to confirm)
                          </span>
                        ) : h.workflowStage === 'PendingGuardConfirm' ? (
                          <span className="checkin-status-badge" style={{ backgroundColor: '#E0E7FF', color: '#4338CA' }}>
                            🛡️ SV đã ký - Chờ Bảo vệ Confirm
                          </span>
                        ) : isCheckRecord ? (
                          <span className="checkin-status-badge" style={{ backgroundColor: '#FCE7F3', color: '#BE185D' }}>
                            📋 Đã lưu (Bed Occupied) - Chờ QL Check Record
                          </span>
                        ) : (
                          <span className="checkin-status-badge completed">
                            ✓ Hoàn tất (Đã Check Record)
                          </span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right', display: 'flex', gap: 6, justifyContent: 'flex-end' }}>
                        {isTroubleshooting && (
                          <button
                            type="button"
                            className="btn-submit"
                            style={{ padding: '5px 12px', fontSize: 12, backgroundColor: '#D97706' }}
                            disabled={processingId === h.id}
                            onClick={e => {
                              e.stopPropagation();
                              handleTroubleshoot(h);
                            }}
                          >
                            {processingId === h.id ? 'Đang xử lý...' : '🔧 Troubleshooting'}
                          </button>
                        )}
                        {isCheckRecord && (
                          <button
                            type="button"
                            className="btn-submit"
                            style={{ padding: '5px 12px', fontSize: 12, backgroundColor: '#16A34A' }}
                            disabled={processingId === h.id}
                            onClick={e => {
                              e.stopPropagation();
                              handleCheckRecord(h);
                            }}
                          >
                            {processingId === h.id ? 'Đang duyệt...' : '✓ Check Record'}
                          </button>
                        )}
                        <button
                          type="button"
                          className="btn-secondary"
                          style={{ padding: '5px 12px', fontSize: 12 }}
                          onClick={e => {
                            e.stopPropagation();
                            setViewingHandover(h);
                            setManagerNote(h.managerNote || '');
                          }}
                        >
                          Chi tiết
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

      {viewingHandover && (
        <div className="checkin-modal-overlay" onClick={() => setViewingHandover(null)}>
          <div className="checkin-modal-content" onClick={e => e.stopPropagation()}>
            <div className="checkin-modal-header no-print">
              <div>
                <h2 className="checkin-modal-title">
                  Biên bản Check-in #BB-{String(viewingHandover.id).padStart(4, '0')}
                </h2>
                <p className="checkin-modal-subtitle">
                  Sinh viên: {viewingHandover.studentName} ({viewingHandover.studentCode}) · Phòng{' '}
                  {viewingHandover.roomNumber} - Giường {viewingHandover.bedCode}
                </p>
              </div>
              <button
                type="button"
                className="checkin-modal-close"
                onClick={() => setViewingHandover(null)}
                aria-label="Đóng"
              >
                ✕
              </button>
            </div>

            <div className="checkin-modal-body">
              <div
                style={{
                  backgroundColor: '#F8FAFC',
                  border: '1px solid #E2E8F0',
                  borderRadius: '10px',
                  padding: '14px 16px',
                  marginBottom: 16,
                }}
              >
                <div style={{ fontWeight: 700, marginBottom: 8, color: '#0F172A' }}>
                  Danh sách thiết bị ghi nhận trong biên bản:
                </div>
                <table className="fpt-equip-table">
                  <thead>
                    <tr>
                      <th style={{ width: 45 }}>TT</th>
                      <th>Thiết bị</th>
                      <th style={{ width: 140 }}>Tình trạng</th>
                      <th>Ghi chú</th>
                    </tr>
                  </thead>
                  <tbody>
                    {viewingHandover.items.map((it, idx) => (
                      <tr key={it.id}>
                        <td style={{ textAlign: 'center' }}>{idx + 1}</td>
                        <td>
                          <strong>{it.catalogName}</strong> ({it.assetCode})
                        </td>
                        <td>{CONDITION_LABELS[it.itemCondition] || it.itemCondition}</td>
                        <td>{it.note || 'Bình thường'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <div style={{ marginTop: 12 }}>
                  <label className="violation-label">Ghi chú xử lý của Ban Quản lý (Troubleshooting / Check record)</label>
                  <input
                    type="text"
                    className="violation-input"
                    value={managerNote}
                    onChange={e => setManagerNote(e.target.value)}
                    placeholder="Nhập ghi chú khắc phục thiết bị hoặc xác nhận hồ sơ..."
                  />
                </div>

                {(viewingHandover.workflowStage === 'PendingManagerTroubleshooting' ||
                  viewingHandover.status === 'Draft') && (
                  <label style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 10, fontSize: 13 }}>
                    <input
                      type="checkbox"
                      checked={repairEquips}
                      onChange={e => setRepairEquips(e.target.checked)}
                    />
                    <span>Đã sửa chữa / thay thế thiết bị hư hỏng về trạng thái Tốt (Good) trước khi chuyển cho SV ký</span>
                  </label>
                )}
              </div>
            </div>

            <div className="checkin-modal-footer no-print">
              <button type="button" className="btn-secondary" onClick={() => setViewingHandover(null)}>
                Đóng
              </button>
              {(viewingHandover.workflowStage === 'PendingManagerTroubleshooting' ||
                viewingHandover.status === 'Draft') && (
                <button
                  type="button"
                  className="btn-submit"
                  style={{ backgroundColor: '#D97706' }}
                  disabled={processingId === viewingHandover.id}
                  onClick={() => handleTroubleshoot(viewingHandover)}
                >
                  {processingId === viewingHandover.id
                    ? 'Đang xử lý...'
                    : '🔧 Hoàn tất Troubleshooting -> Chuyển SV ký xác nhận'}
                </button>
              )}
              {(viewingHandover.workflowStage === 'PendingManagerCheckRecord' ||
                (viewingHandover.status === 'Completed' && !viewingHandover.managerReviewedTime)) && (
                <button
                  type="button"
                  className="btn-submit"
                  style={{ backgroundColor: '#16A34A' }}
                  disabled={processingId === viewingHandover.id}
                  onClick={() => handleCheckRecord(viewingHandover)}
                >
                  {processingId === viewingHandover.id ? 'Đang duyệt...' : '✓ Xác nhận Check Record (Kết thúc)'}
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
