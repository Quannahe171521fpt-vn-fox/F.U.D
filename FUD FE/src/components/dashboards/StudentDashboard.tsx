import React from 'react';
import { useAuth } from '../../context/AuthContext';
import { Logo, UserIcon, BedIcon, ShieldIcon } from '../Icons';

interface StudentDashboardProps {
  onBackToHome: () => void;
}

export const StudentDashboard: React.FC<StudentDashboardProps> = ({ onBackToHome }) => {
  const { user, logout } = useAuth();

  const isMale = user?.gender === 'Male';

  return (
    <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc', display: 'flex', flexDirection: 'column' }}>
      {/* Top Navigation */}
      <header style={{ backgroundColor: '#fff', borderBottom: '1px solid #e2e8f0', padding: '12px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <Logo />
          <span style={{ fontSize: '1rem', fontWeight: 700, color: '#ea580c', borderLeft: '2px solid #e2e8f0', paddingLeft: '16px' }}>
            Cổng Sinh Viên Nội Trú (Student Portal)
          </span>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            type="button"
            onClick={onBackToHome}
            style={{ padding: '8px 14px', borderRadius: '8px', border: '1px solid #cbd5e1', backgroundColor: '#fff', fontSize: '0.875rem', fontWeight: 600, cursor: 'pointer' }}
          >
            ← Trang chủ KTX
          </button>
          <button
            type="button"
            onClick={logout}
            style={{ padding: '8px 16px', borderRadius: '8px', border: 'none', backgroundColor: '#dc2626', color: '#fff', fontSize: '0.875rem', fontWeight: 600, cursor: 'pointer' }}
          >
            Đăng xuất
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main style={{ maxWidth: '1200px', width: '100%', margin: '32px auto', padding: '0 20px', flex: 1 }}>
        {/* Welcome Card */}
        <div style={{ backgroundColor: '#fff', borderRadius: '12px', padding: '24px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)', marginBottom: '24px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
            <div>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
                <span style={{ backgroundColor: '#ea580c', color: '#fff', padding: '3px 10px', borderRadius: '20px', fontSize: '0.75rem', fontWeight: 700 }}>
                  SINH VIÊN NỘI TRÚ
                </span>
                <span style={{ backgroundColor: isMale ? '#dbeafe' : '#fce7f3', color: isMale ? '#1d4ed8' : '#be185d', padding: '3px 10px', borderRadius: '20px', fontSize: '0.75rem', fontWeight: 700 }}>
                  Giới tính: {isMale ? 'Nam' : 'Nữ'}
                </span>
              </div>
              <h1 style={{ fontSize: '1.5rem', fontWeight: 800, color: '#1e293b', margin: '4px 0' }}>
                Xin chào, {user?.fullName} ({user?.code})
              </h1>
              <p style={{ color: '#64748b', fontSize: '0.9rem', margin: 0 }}>
                Email: {user?.email} • Khóa: {user?.studentInfo?.cohort || 'K18'} • Chuyên ngành: {user?.studentInfo?.major || 'SE'}
              </p>
            </div>
            <div style={{ backgroundColor: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '8px', padding: '12px 16px', maxWidth: '380px' }}>
              <div style={{ fontWeight: 700, color: '#15803d', fontSize: '0.9rem', marginBottom: '4px' }}>
                ✦ Tự động Lọc theo Giới tính [GD]
              </div>
              <p style={{ color: '#166534', fontSize: '0.825rem', margin: 0 }}>
                Hệ thống nhận diện bạn là sinh viên <strong>{isMale ? 'Nam' : 'Nữ'}</strong>. Mọi thao tác tìm phòng, đặt chỗ và chuyển giường sẽ chỉ áp dụng cho các tòa nhà dành cho sinh viên {isMale ? 'Nam (Dom A, C, E)' : 'Nữ (Dom B, D, G)'}.
              </p>
            </div>
          </div>
        </div>

        {/* Feature Cards Grid (Placeholders) */}
        <h2 style={{ fontSize: '1.15rem', fontWeight: 700, color: '#334155', marginBottom: '16px' }}>
          Các chức năng sinh viên (Trang đang phát triển)
        </h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '20px' }}>
          {/* Card 1 */}
          <div style={{ backgroundColor: '#fff', borderRadius: '10px', padding: '20px', border: '1px solid #e2e8f0', boxShadow: '0 1px 2px rgba(0,0,0,0.04)' }}>
            <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: '#fff7ed', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '12px', color: '#ea580c' }}>
              <BedIcon size={22} />
            </div>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 700, color: '#1e293b', margin: '0 0 8px 0' }}>Đăng Ký Đặt Phòng (Booking)</h3>
            <p style={{ fontSize: '0.85rem', color: '#64748b', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Xem danh sách phòng trống thuộc các tòa {isMale ? 'Nam' : 'Nữ'} và đăng ký lưu trú cho học kỳ mới.
            </p>
            <button type="button" style={{ width: '100%', padding: '8px 0', borderRadius: '6px', border: '1px solid #ea580c', color: '#ea580c', backgroundColor: 'transparent', fontWeight: 600, fontSize: '0.85rem', cursor: 'pointer' }}>
              Vào chức năng Booking →
            </button>
          </div>

          {/* Card 2 */}
          <div style={{ backgroundColor: '#fff', borderRadius: '10px', padding: '20px', border: '1px solid #e2e8f0', boxShadow: '0 1px 2px rgba(0,0,0,0.04)' }}>
            <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: '#eff6ff', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '12px', color: '#2563eb' }}>
              <UserIcon size={22} />
            </div>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 700, color: '#1e293b', margin: '0 0 8px 0' }}>Thông Tin Cư Trú & Phòng</h3>
            <p style={{ fontSize: '0.85rem', color: '#64748b', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Xem thông tin giường hiện tại: {user?.studentInfo?.currentBedNumber ? `${user.studentInfo.currentBlockName} - Phòng ${user.studentInfo.currentRoomCode}` : 'Chưa xếp phòng'}.
            </p>
            <button type="button" style={{ width: '100%', padding: '8px 0', borderRadius: '6px', border: '1px solid #cbd5e1', color: '#475569', backgroundColor: 'transparent', fontWeight: 600, fontSize: '0.85rem', cursor: 'pointer' }}>
              Xem chi tiết cư trú →
            </button>
          </div>

          {/* Card 3 */}
          <div style={{ backgroundColor: '#fff', borderRadius: '10px', padding: '20px', border: '1px solid #e2e8f0', boxShadow: '0 1px 2px rgba(0,0,0,0.04)' }}>
            <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: '#f0fdf4', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '12px', color: '#16a34a' }}>
              <ShieldIcon size={22} />
            </div>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 700, color: '#1e293b', margin: '0 0 8px 0' }}>Hóa Đơn & Điện Nước</h3>
            <p style={{ fontSize: '0.85rem', color: '#64748b', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Tra cứu công nợ tiền phòng, tiền điện nước hàng tháng và thanh toán qua mã VietQR.
            </p>
            <button type="button" style={{ width: '100%', padding: '8px 0', borderRadius: '6px', border: '1px solid #cbd5e1', color: '#475569', backgroundColor: 'transparent', fontWeight: 600, fontSize: '0.85rem', cursor: 'pointer' }}>
              Tra cứu hóa đơn →
            </button>
          </div>
        </div>
      </main>
    </div>
  );
};
