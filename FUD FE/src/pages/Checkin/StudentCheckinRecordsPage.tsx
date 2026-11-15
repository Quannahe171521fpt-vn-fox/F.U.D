import React, { useState, useEffect } from 'react';
import { checkinApi, type CheckinHandoverDetail } from '../../services/api';
import '../../styles/checkin.css';

const CONDITION_LABELS: Record<string, string> = {
  Good: 'Bình thường (Tốt)',
  Damaged: 'Bị hỏng hóc',
  Missing: 'Thiếu / Mất',
};

interface StudentCheckinRecordsPageProps {
  onBackToProfile?: () => void;
}

export const StudentCheckinRecordsPage: React.FC<StudentCheckinRecordsPageProps> = ({ onBackToProfile }) => {
  const [handovers, setHandovers] = useState<CheckinHandoverDetail[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [viewingHandover, setViewingHandover] = useState<CheckinHandoverDetail | null>(null);

  const loadMyCheckins = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await checkinApi.getMyCheckins();
      setHandovers(data);
      // Nếu chỉ có 1 biên bản duy nhất, có thể giữ danh sách hoặc mở trực tiếp
    } catch (err: any) {
      console.error('Lỗi tải biên bản check-in:', err);
      setError(err?.message || 'Không thể tải biên bản nhận phòng.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadMyCheckins();
  }, []);

  const handlePrint = (handover: CheckinHandoverDetail) => {
    setViewingHandover(handover);
    setTimeout(() => {
      window.print();
    }, 150);
  };

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
                  Chưa có thiết bị nào được ghi nhận cho phòng {handover.roomNumber}
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
              <div style={{ fontWeight: 400, fontSize: 12, fontStyle: 'italic' }}>(Ký và ghi rõ họ tên)</div>
              <div className="fpt-sig-name">{handover.studentName}</div>
            </div>
            <div>
              <div>Người bàn giao</div>
              <div style={{ fontWeight: 400, fontSize: 12, fontStyle: 'italic' }}>(Ký và ghi rõ họ tên)</div>
              <div className="fpt-sig-name">{handover.guardName}</div>
            </div>
          </div>
        </div>
      </div>
    );
  };

  return (
    <div style={{ padding: '24px 32px', maxWidth: '1000px', margin: '0 auto' }}>
      {/* Header */}
      <div className="no-print" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
          <div>
            <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
              Biên bản Check-in nhận phòng
            </h1>
            <p style={{ color: '#64748B', fontSize: '14px', margin: 0 }}>
              Xem lại danh sách tài sản phòng, hiện trạng bàn giao và chỉ số điện/nước đã được cán bộ bảo vệ xác nhận khi bạn nhận phòng.
            </p>
          </div>

          {onBackToProfile && (
            <button
              type="button"
              onClick={onBackToProfile}
              style={{
                padding: '8px 16px',
                borderRadius: '8px',
                border: '1px solid #CBD5E1',
                backgroundColor: '#FFFFFF',
                color: '#334155',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              ← Về hồ sơ sinh viên
            </button>
          )}
        </div>
      </div>

      {/* Loading state */}
      {loading && (
        <div
          className="no-print"
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '48px 24px',
            textAlign: 'center',
            color: '#64748B',
          }}
        >
          <div style={{ fontSize: '28px', marginBottom: 12 }}>⏳</div>
          <p style={{ margin: 0, fontSize: '15px', fontWeight: 500 }}>Đang tải danh sách biên bản Check-in của bạn...</p>
        </div>
      )}

      {/* Error state */}
      {!loading && error && (
        <div
          className="no-print"
          style={{
            backgroundColor: '#FEF2F2',
            border: '1px solid #FCA5A5',
            borderRadius: '12px',
            padding: '20px 24px',
            color: '#991B1B',
            marginBottom: 20,
          }}
        >
          <div style={{ fontWeight: 600, marginBottom: 6 }}>Không thể tải biên bản</div>
          <div style={{ fontSize: '13px', marginBottom: 12 }}>{error}</div>
          <button
            type="button"
            onClick={loadMyCheckins}
            style={{
              padding: '6px 14px',
              backgroundColor: '#DC2626',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: '6px',
              fontSize: '12px',
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Thử lại
          </button>
        </div>
      )}

      {/* Empty state: Bảo vệ chưa lập biên bản */}
      {!loading && !error && handovers.length === 0 && (
        <div
          className="no-print"
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            border: '1px solid #E2E8F0',
            padding: '48px 24px',
            textAlign: 'center',
          }}
        >
          <div
            style={{
              width: '64px',
              height: '64px',
              borderRadius: '50%',
              backgroundColor: '#F1F5F9',
              color: '#94A3B8',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 16px',
              fontSize: '32px',
            }}
          >
            📋
          </div>
          <h3 style={{ fontSize: '18px', fontWeight: 700, color: '#0F172A', margin: '0 0 8px' }}>
            Chưa có biên bản Check-in nào
          </h3>
          <p style={{ color: '#64748B', fontSize: '14px', maxWidth: '520px', margin: '0 auto 20px', lineHeight: 1.6 }}>
            Sau khi cán bộ bảo vệ hoàn tất thủ tục bàn giao tài sản phòng ở tại KTX và lập biên bản Check-in,
            toàn bộ nội dung biên bản và chỉ số điện nước sẽ được hiển thị tại đây để bạn đối soát và lưu trữ.
          </p>
          <button
            type="button"
            onClick={loadMyCheckins}
            style={{
              padding: '8px 18px',
              backgroundColor: '#EA580C',
              color: '#FFFFFF',
              border: 'none',
              borderRadius: '8px',
              fontSize: '13px',
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Làm mới dữ liệu
          </button>
        </div>
      )}

      {/* Single Handover Detail Modal/Paper View */}
      {viewingHandover && (
        <div>
          <div
            className="no-print"
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              marginBottom: 16,
              backgroundColor: '#FFFFFF',
              padding: '12px 18px',
              borderRadius: '10px',
              border: '1px solid #E2E8F0',
            }}
          >
            <button
              type="button"
              onClick={() => setViewingHandover(null)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '6px 14px',
                borderRadius: '6px',
                border: '1px solid #CBD5E1',
                backgroundColor: '#F8FAFC',
                color: '#334155',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              ← Trở về danh sách biên bản
            </button>

            <button
              type="button"
              onClick={() => window.print()}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                padding: '8px 18px',
                borderRadius: '6px',
                border: 'none',
                backgroundColor: '#0284C7',
                color: '#FFFFFF',
                fontSize: '13px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              🖨️ In / Tải PDF
            </button>
          </div>

          <div
            className="no-print"
            style={{
              marginBottom: 16,
              padding: '12px 16px',
              backgroundColor: '#F0FDF4',
              border: '1px solid #86EFAC',
              borderRadius: '8px',
              color: '#166534',
              fontSize: '13.5px',
              display: 'flex',
              alignItems: 'center',
              gap: 10,
            }}
          >
            <span>ℹ️</span>
            <div>
              Biên bản này là căn cứ bàn giao tài sản phòng ở đã được hoàn tất bởi bảo vệ <strong>{viewingHandover.guardName}</strong>.
              Bạn có thể in ra hoặc lưu lại để đối chiếu khi hoàn tất thủ tục trả phòng (Check-out) sau này.
            </div>
          </div>

          {renderPrintablePaper(viewingHandover)}
        </div>
      )}

      {/* List of checkin handovers (when not viewing a specific paper) */}
      {!loading && !error && handovers.length > 0 && !viewingHandover && (
        <div className="no-print" style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {handovers.map((h) => {
            const dt = new Date(h.createdTime);
            const dateStr = dt.toLocaleDateString('vi-VN', {
              hour: '2-digit',
              minute: '2-digit',
              day: '2-digit',
              month: '2-digit',
              year: 'numeric',
            });

            return (
              <div
                key={h.id}
                style={{
                  backgroundColor: '#FFFFFF',
                  borderRadius: '12px',
                  border: '1px solid #E2E8F0',
                  padding: '20px 24px',
                  boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
                  transition: 'all 0.2s ease',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 14, flexWrap: 'wrap', gap: 8 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                    <span
                      style={{
                        padding: '4px 10px',
                        borderRadius: '9999px',
                        backgroundColor: '#DCFCE7',
                        color: '#15803D',
                        fontSize: '12px',
                        fontWeight: 700,
                      }}
                    >
                      ✓ ĐÃ HOÀN TẤT CHECK-IN
                    </span>
                    <span style={{ fontSize: '13px', color: '#64748B', fontWeight: 500 }}>
                      Mã biên bản: <strong>#{h.id}</strong>
                    </span>
                  </div>

                  <span style={{ fontSize: '12.5px', color: '#64748B' }}>
                    Thời gian lập: {dateStr}
                  </span>
                </div>

                <div
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                    gap: 16,
                    backgroundColor: '#F8FAFC',
                    padding: '16px',
                    borderRadius: '8px',
                    marginBottom: 16,
                    fontSize: '13.5px',
                  }}
                >
                  <div>
                    <div style={{ color: '#64748B', fontSize: '12px', marginBottom: 2 }}>Phòng & Giường</div>
                    <div style={{ fontWeight: 700, color: '#0F172A' }}>
                      Phòng {h.roomNumber} - Giường {h.bedCode}
                    </div>
                    <div style={{ fontSize: '12px', color: '#475569' }}>Block {h.blockCode || h.blockName}</div>
                  </div>

                  <div>
                    <div style={{ color: '#64748B', fontSize: '12px', marginBottom: 2 }}>Kỳ học</div>
                    <div style={{ fontWeight: 600, color: '#0F172A' }}>{h.termName || 'Chưa ghi nhận'}</div>
                  </div>

                  <div>
                    <div style={{ color: '#64748B', fontSize: '12px', marginBottom: 2 }}>Cán bộ bảo vệ bàn giao</div>
                    <div style={{ fontWeight: 600, color: '#0F172A' }}>{h.guardName}</div>
                    <div style={{ fontSize: '12px', color: '#475569' }}>Mã CB: {h.guardCode}</div>
                  </div>

                  <div>
                    <div style={{ color: '#64748B', fontSize: '12px', marginBottom: 2 }}>Chỉ số điện ban đầu</div>
                    <div style={{ fontWeight: 600, color: '#0F172A' }}>
                      L: {h.electricityL || '0'} | R: {h.electricityR || '0'}
                    </div>
                  </div>

                  <div>
                    <div style={{ color: '#64748B', fontSize: '12px', marginBottom: 2 }}>Chỉ số nước ban đầu</div>
                    <div style={{ fontWeight: 600, color: '#0F172A' }}>
                      L: {h.waterL || '0'} | R: {h.waterR || '0'}
                    </div>
                  </div>

                  <div>
                    <div style={{ color: '#64748B', fontSize: '12px', marginBottom: 2 }}>Hiện trạng tài sản</div>
                    {h.hasDamageOrMissingFlag ? (
                      <span style={{ color: '#B45309', fontWeight: 600 }}>⚠️ Có ghi nhận hỏng/thiếu</span>
                    ) : (
                      <span style={{ color: '#15803D', fontWeight: 600 }}>✓ Tất cả hoạt động tốt</span>
                    )}
                  </div>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 10 }}>
                  <button
                    type="button"
                    onClick={() => handlePrint(h)}
                    style={{
                      padding: '8px 16px',
                      backgroundColor: '#FFFFFF',
                      color: '#0284C7',
                      border: '1px solid #BAE6FD',
                      borderRadius: '8px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    🖨️ In biên bản
                  </button>

                  <button
                    type="button"
                    onClick={() => setViewingHandover(h)}
                    style={{
                      padding: '8px 18px',
                      backgroundColor: '#EA580C',
                      color: '#FFFFFF',
                      border: 'none',
                      borderRadius: '8px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                    }}
                  >
                    Xem chi tiết biên bản →
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
