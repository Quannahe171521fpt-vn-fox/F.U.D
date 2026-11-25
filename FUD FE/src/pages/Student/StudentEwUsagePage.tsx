import React, { useState, useEffect } from 'react';
import { ewApi, navigateToStudentPayment, type StudentTermOptionDto, type StudentEwReportDto, type StudentEwMonthlyRowDto } from '../../services/api';
import '../../styles/management.css';

export const StudentEwUsagePage: React.FC = () => {
  const [terms, setTerms] = useState<StudentTermOptionDto[]>([]);
  const [selectedTermId, setSelectedTermId] = useState<number | null>(null);
  const [report, setReport] = useState<StudentEwReportDto | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // 1. Tải danh sách học kỳ của sinh viên
  useEffect(() => {
    const fetchTerms = async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await ewApi.getMyTerms();
        setTerms(data);
        if (data.length > 0) {
          // Ưu tiên chọn học kỳ hiện tại, nếu không có lấy kỳ mới nhất đầu danh sách
          const current = data.find((t) => t.isCurrent) || data[0];
          setSelectedTermId(current.termId);
        } else {
          setLoading(false);
        }
      } catch (err: any) {
        console.error('Lỗi khi tải danh sách học kỳ:', err);
        setError(err?.message || 'Không thể tải danh sách học kỳ lưu trú');
        setLoading(false);
      }
    };
    fetchTerms();
  }, []);

  // 2. Tải báo cáo điện nước khi chọn kỳ
  useEffect(() => {
    if (!selectedTermId) return;

    const fetchReport = async () => {
      try {
        setLoading(true);
        setError(null);
        const data = await ewApi.getMyUsage(selectedTermId);
        setReport(data);
      } catch (err: any) {
        console.error('Lỗi khi tải dữ liệu điện nước:', err);
        setError(err?.message || 'Không thể tải dữ liệu điện nước của học kỳ');
      } finally {
        setLoading(false);
      }
    };

    fetchReport();
  }, [selectedTermId]);

  // Gom nhóm các dòng theo Tháng - Năm để gộp ô (rowSpan)
  const groupedByMonth = React.useMemo(() => {
    if (!report || !report.rows || report.rows.length === 0) return [];

    const map = new Map<string, StudentEwMonthlyRowDto[]>();
    for (const row of report.rows) {
      const key = row.monthYear;
      if (!map.has(key)) {
        map.set(key, []);
      }
      map.get(key)!.push(row);
    }
    return Array.from(map.entries()).map(([monthYear, rows]) => ({
      monthYear,
      rows,
    }));
  }, [report]);

  return (
    <div style={{ padding: '28px 32px', maxWidth: '1200px', margin: '0 auto' }}>
      {/* ── Tiêu đề trang ─────────────────────────────────── */}
      <div style={{ marginBottom: '24px' }}>
        <h1 style={{ fontSize: '26px', fontWeight: 800, color: '#0F172A', margin: 0, display: 'flex', alignItems: 'center', gap: '10px' }}>
          <span></span> Theo Dõi Điện – Nước & Định Mức
        </h1>
        <p style={{ margin: '6px 0 0', color: '#64748B', fontSize: '14px' }}>
          Tra cứu chỉ số tiêu thụ, đối chiếu định mức cá nhân và phí phụ trội được chia đều cho các thành viên đã nhận phòng.
        </p>
      </div>

      {/* ── Thanh lọc học kỳ & Thông tin chung ───────────────── */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '16px',
          backgroundColor: '#FFFFFF',
          padding: '16px 20px',
          borderRadius: '12px',
          border: '1px solid #E2E8F0',
          boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
          marginBottom: '24px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <label htmlFor="term-select" style={{ fontWeight: 600, color: '#334155', fontSize: '14px' }}>
            Học kỳ xem chỉ số:
          </label>
          <select
            id="term-select"
            value={selectedTermId || ''}
            onChange={(e) => setSelectedTermId(Number(e.target.value))}
            style={{
              padding: '8px 14px',
              borderRadius: '8px',
              border: '1px solid #CBD5E1',
              backgroundColor: '#FFFFFF',
              color: '#0F172A',
              fontWeight: 600,
              fontSize: '14px',
              cursor: 'pointer',
              outline: 'none',
              minWidth: '220px',
            }}
          >
            {terms.map((t) => (
              <option key={t.termId} value={t.termId}>
                {t.termName} ({t.academicYear}) {t.isCurrent ? '— Đang diễn ra' : ''}
              </option>
            ))}
          </select>
        </div>

        <button
          onClick={() => {
            if (selectedTermId) {
              setLoading(true);
              ewApi.getMyUsage(selectedTermId).then(setReport).finally(() => setLoading(false));
            }
          }}
          disabled={loading}
          style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '6px',
            padding: '8px 16px',
            borderRadius: '8px',
            border: '1px solid #CBD5E1',
            backgroundColor: '#F8FAFC',
            color: '#475569',
            fontWeight: 600,
            fontSize: '13px',
            cursor: loading ? 'not-allowed' : 'pointer',
          }}
        >
          Làm mới
        </button>
      </div>

      {/* ── Loading & Error states ──────────────────────────── */}
      {loading && (
        <div style={{ textAlign: 'center', padding: '60px 0', color: '#64748B' }}>
          <div style={{ fontSize: '32px', marginBottom: '12px', animation: 'spin 1s linear infinite' }}>⏳</div>
          <div style={{ fontSize: '15px', fontWeight: 600 }}>Đang tải dữ liệu điện nước của bạn...</div>
        </div>
      )}

      {!loading && error && (
        <div
          style={{
            padding: '16px 20px',
            backgroundColor: '#FEF2F2',
            border: '1px solid #FCA5A5',
            borderRadius: '10px',
            color: '#B91C1C',
            marginBottom: '24px',
            display: 'flex',
            alignItems: 'center',
            gap: '12px',
          }}
        >
          <span style={{ fontSize: '20px' }}>⚠️</span>
          <div style={{ fontSize: '14px' }}>{error}</div>
        </div>
      )}

      {!loading && !error && !report && (
        <div
          style={{
            backgroundColor: '#FFFFFF',
            borderRadius: '12px',
            padding: '48px 24px',
            textAlign: 'center',
            border: '1px solid #E2E8F0',
            color: '#64748B',
          }}
        >
          <span style={{ fontSize: '48px', display: 'block', marginBottom: '16px' }}>📋</span>
          <h3 style={{ fontSize: '18px', fontWeight: 700, color: '#1E293B', marginBottom: '8px' }}>
            Chưa có dữ liệu điện nước trong kỳ này
          </h3>
          <p style={{ fontSize: '14px', maxWidth: '520px', margin: '0 auto', lineHeight: 1.6 }}>
            Bạn chưa thực hiện Check-in nhận phòng hoặc Ban quản lý chưa cập nhật biên bản chỉ số công tơ cho phòng của bạn trong học kỳ này.
          </p>
        </div>
      )}

      {/* ── Báo cáo chi tiết ────────────────────────────────── */}
      {!loading && !error && report && (
        <>
          {/* Thông tin phòng & Thành viên cùng phòng */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
              gap: '16px',
              marginBottom: '24px',
            }}
          >
            <div
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: '12px',
                padding: '18px 20px',
                border: '1px solid #E2E8F0',
                boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
              }}
            >
              <div style={{ fontSize: '12px', fontWeight: 700, textTransform: 'uppercase', color: '#64748B', marginBottom: '6px' }}>
                Phòng / Cụm phòng
              </div>
              <div style={{ fontSize: '20px', fontWeight: 800, color: '#0F172A', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span>{report.blockCode || report.roomNumber}</span>
                <span style={{ fontSize: '14px', fontWeight: 500, color: '#64748B' }}>({report.blockName})</span>
              </div>
              <div style={{ fontSize: '13px', color: '#64748B', marginTop: '4px' }}>
                Học kỳ: <strong>{report.termName}</strong> ({report.academicYear})
              </div>
            </div>

            <div
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: '12px',
                padding: '18px 20px',
                border: '1px solid #E2E8F0',
                boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
              }}
            >
              <div style={{ fontSize: '12px', fontWeight: 700, textTransform: 'uppercase', color: '#64748B', marginBottom: '6px' }}>
                Thành viên chia sẻ (Đã Check-in)
              </div>
              <div style={{ fontSize: '20px', fontWeight: 800, color: '#0F172A', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span>{report.numberOfCheckedInResidents} bạn</span>
              </div>
              <div style={{ fontSize: '12px', color: '#64748B', marginTop: '4px', lineHeight: 1.4 }}>
                {report.roommates && report.roommates.length > 0 ? (
                  <span>Danh sách: {report.roommates.join(', ')}</span>
                ) : (
                  <span>Định mức được chia đều cho số sinh viên đã check-in thực tế.</span>
                )}
              </div>
            </div>

            <div
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: '12px',
                padding: '18px 20px',
                border: '1px solid #E2E8F0',
                boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
              }}
            >
              <div style={{ fontSize: '12px', fontWeight: 700, textTransform: 'uppercase', color: '#64748B', marginBottom: '6px' }}>
                Định mức miễn phí cá nhân
              </div>
              <div style={{ fontSize: '14px', fontWeight: 700, color: '#0F172A', marginTop: '4px', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <div> Điện: <span style={{ color: '#D97706' }}>50 kWh / tháng</span> (200 kWh / kỳ)</div>
                <div> Nước: <span style={{ color: '#0284C7' }}>3 m³ / tháng</span> (12 m³ / kỳ)</div>
              </div>
            </div>
          </div>

          {/* Bảng dữ liệu chi tiết theo mockup người dùng */}
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              border: '1px solid #E2E8F0',
              boxShadow: '0 2px 8px rgba(0,0,0,0.04)',
              overflow: 'hidden',
              marginBottom: '28px',
            }}
          >
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'center', fontSize: '14px' }}>
                <thead>
                  <tr style={{ backgroundColor: '#F8FAFC', borderBottom: '1px solid #E2E8F0', color: '#1E293B', fontWeight: 700 }}>
                    <th style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', minWidth: '120px' }}>Phòng</th>
                    <th style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', minWidth: '130px' }}>Tháng - Năm</th>
                    <th style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', minWidth: '130px' }}>Ngày ghi</th>
                    <th style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', minWidth: '110px' }}>Loại</th>
                    <th style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', minWidth: '150px' }}>Tiêu thụ của bạn</th>
                    <th style={{ padding: '14px 16px', minWidth: '150px' }}>Vượt định mức</th>
                  </tr>
                </thead>
                <tbody>
                  {groupedByMonth.length === 0 ? (
                    <tr>
                      <td colSpan={6} style={{ padding: '36px', color: '#94A3B8' }}>
                        Chưa có chỉ số nào được ghi nhận cho phòng trong học kỳ này.
                      </td>
                    </tr>
                  ) : (
                    groupedByMonth.map((group) => {
                      const rowCount = group.rows.length;
                      return group.rows.map((row, index) => {
                        const isFirstInMonth = index === 0;
                        const isOver = row.yourOverUsage > 0;
                        const isElec = row.rawType === 'Electricity';

                        return (
                          <tr
                            key={`${row.monthYear}-${row.rawType}-${index}`}
                            style={{
                              borderBottom: index === rowCount - 1 ? '1px solid #E2E8F0' : '1px solid #F8FAFC',
                              backgroundColor: '#FFFFFF',
                            }}
                          >
                            {/* Gộp ô Phòng theo tháng */}
                            {isFirstInMonth && (
                              <td
                                rowSpan={rowCount}
                                style={{
                                  padding: '14px 16px',
                                  borderRight: '1px solid #F1F5F9',
                                  fontWeight: 700,
                                  color: '#334155',
                                  verticalAlign: 'middle',
                                }}
                              >
                                {row.roomNumber || row.block}
                              </td>
                            )}

                            {/* Gộp ô Tháng - Năm theo tháng */}
                            {isFirstInMonth && (
                              <td
                                rowSpan={rowCount}
                                style={{
                                  padding: '14px 16px',
                                  borderRight: '1px solid #F1F5F9',
                                  fontWeight: 600,
                                  color: '#334155',
                                  verticalAlign: 'middle',
                                }}
                              >
                                {row.monthYear}
                              </td>
                            )}

                            {/* Ngày ghi */}
                            <td style={{ padding: '12px 16px', borderRight: '1px solid #F1F5F9', color: '#475569' }}>
                              {row.recordDate
                                ? new Date(row.recordDate).toLocaleDateString('vi-VN', {
                                    day: '2-digit',
                                    month: '2-digit',
                                    year: 'numeric',
                                  })
                                : '—'}
                            </td>

                            {/* Loại (Điện / Nước) */}
                            <td style={{ padding: '12px 16px', borderRight: '1px solid #F1F5F9' }}>
                              <span
                                style={{
                                  display: 'inline-flex',
                                  alignItems: 'center',
                                  gap: '4px',
                                  fontWeight: 600,
                                  color: isElec ? '#D97706' : '#0284C7',
                                }}
                              >
                                {isElec ? ' Điện' : ' Nước'}
                              </span>
                            </td>

                            {/* Tiêu thụ của bạn */}
                            <td style={{ padding: '12px 16px', borderRight: '1px solid #F1F5F9', fontWeight: 700, color: '#0F172A' }}>
                              {row.yourUsage.toFixed(2)} {isElec ? 'kWh' : 'm³'}
                            </td>

                            {/* Vượt định mức */}
                            <td
                              style={{
                                padding: '12px 16px',
                                fontWeight: 700,
                                color: isOver ? '#DC2626' : '#475569',
                              }}
                            >
                              {isOver ? `+${row.yourOverUsage.toFixed(2)}` : row.yourOverUsage.toFixed(2)} {isElec ? 'kWh' : 'm³'}
                            </td>
                          </tr>
                        );
                      });
                    })
                  )}

                  {/* ── 2 Hàng tổng kết Điện & Nước theo đúng mockup ── */}
                  {report.rows && report.rows.length > 0 && (
                    <>
                      {/* Tổng điện */}
                      <tr style={{ backgroundColor: '#F8FAFC', borderTop: '2px solid #E2E8F0', fontWeight: 800 }}>
                        <td
                          colSpan={4}
                          style={{
                            padding: '14px 20px',
                            textAlign: 'right',
                            color: '#1E293B',
                            fontSize: '14px',
                            borderRight: '1px solid #F1F5F9',
                          }}
                        >
                          Tổng điện (Định mức cá nhân: {report.electricQuota} kWh/tháng)
                        </td>
                        <td style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', color: '#0F172A', fontSize: '15px' }}>
                          {report.totalElectricUsage.toFixed(2)} kWh
                        </td>
                        <td
                          style={{
                            padding: '14px 16px',
                            fontSize: '15px',
                            color: report.totalElectricOverUsage > 0 ? '#DC2626' : '#16A34A',
                          }}
                        >
                          {report.totalElectricOverUsage > 0
                            ? `+${report.totalElectricOverUsage.toFixed(2)}`
                            : report.totalElectricOverUsage.toFixed(2)}{' '}
                          kWh
                        </td>
                      </tr>

                      {/* Tổng nước */}
                      <tr style={{ backgroundColor: '#F8FAFC', borderTop: '1px solid #E2E8F0', fontWeight: 800 }}>
                        <td
                          colSpan={4}
                          style={{
                            padding: '14px 20px',
                            textAlign: 'right',
                            color: '#1E293B',
                            fontSize: '14px',
                            borderRight: '1px solid #F1F5F9',
                          }}
                        >
                          Tổng nước (Định mức cá nhân: {report.waterQuota} m³/tháng)
                        </td>
                        <td style={{ padding: '14px 16px', borderRight: '1px solid #F1F5F9', color: '#0F172A', fontSize: '15px' }}>
                          {report.totalWaterUsage.toFixed(2)} m³
                        </td>
                        <td
                          style={{
                            padding: '14px 16px',
                            fontSize: '15px',
                            color: report.totalWaterOverUsage > 0 ? '#DC2626' : '#16A34A',
                          }}
                        >
                          {report.totalWaterOverUsage > 0
                            ? `+${report.totalWaterOverUsage.toFixed(2)}`
                            : report.totalWaterOverUsage.toFixed(2)}{' '}
                          m³
                        </td>
                      </tr>
                    </>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* ── Khối kết quả số tiền phụ trội vượt định mức ─────── */}
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '12px',
              padding: '24px',
              border: '1px solid #E2E8F0',
              boxShadow: '0 2px 6px rgba(0,0,0,0.03)',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12, marginBottom: '16px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: 700, color: '#1E293B', margin: 0 }}>
                Tổng kết phí phụ trội vượt định mức
              </h3>
              {(report.electricOverUsagePayment > 0 || report.waterOverUsagePayment > 0) && (
                <button
                  type="button"
                  onClick={() => navigateToStudentPayment({ tabFilter: 'Utility' })}
                  style={{
                    padding: '8px 16px',
                    backgroundColor: '#EA580C',
                    color: '#FFFFFF',
                    border: 'none',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 700,
                    cursor: 'pointer',
                  }}
                >
                  Thanh toán ngay hóa đơn điện nước
                </button>
              )}
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginBottom: '16px' }}>
              {/* Tiền điện phụ trội */}
              <div style={{ display: 'flex', alignItems: 'baseline', flexWrap: 'wrap', gap: '8px', fontSize: '16px' }}>
                <span style={{ fontWeight: 600, color: '#334155' }}>
                  Tiền phụ trội vượt định mức (Điện):
                </span>
                <span
                  style={{
                    fontWeight: 800,
                    fontSize: '18px',
                    color: report.electricOverUsagePayment > 0 ? '#DC2626' : '#16A34A',
                  }}
                >
                  {report.electricOverUsagePayment.toLocaleString('vi-VN')} VNĐ
                </span>
              </div>

              {/* Tiền nước phụ trội */}
              <div style={{ display: 'flex', alignItems: 'baseline', flexWrap: 'wrap', gap: '8px', fontSize: '16px' }}>
                <span style={{ fontWeight: 600, color: '#334155' }}>
                  Tiền phụ trội vượt định mức (Nước):
                </span>
                <span
                  style={{
                    fontWeight: 800,
                    fontSize: '18px',
                    color: report.waterOverUsagePayment > 0 ? '#DC2626' : '#16A34A',
                  }}
                >
                  {report.waterOverUsagePayment.toLocaleString('vi-VN')} VNĐ
                </span>
              </div>

              {/* Tổng cộng Điện + Nước lũy kế kỳ */}
              {(report.electricOverUsagePayment > 0 || report.waterOverUsagePayment > 0) && (
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'baseline',
                    flexWrap: 'wrap',
                    gap: '8px',
                    fontSize: '16px',
                    paddingTop: '10px',
                    borderTop: '1px dashed #E2E8F0',
                  }}
                >
                  <span style={{ fontWeight: 700, color: '#0F172A' }}>
                    Tổng cộng phí phụ trội lũy kế trong kỳ:
                  </span>
                  <span style={{ fontWeight: 800, fontSize: '18px', color: '#DC2626' }}>
                    {(report.electricOverUsagePayment + report.waterOverUsagePayment).toLocaleString('vi-VN')} VNĐ
                  </span>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
};

