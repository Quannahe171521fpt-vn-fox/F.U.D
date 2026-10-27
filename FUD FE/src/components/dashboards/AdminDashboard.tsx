import React from 'react';
import { useAuth } from '../../context/AuthContext';
import { Logo, UserIcon, ShieldIcon } from '../Icons';

interface AdminDashboardProps {
  onBackToHome: () => void;
}

export const AdminDashboard: React.FC<AdminDashboardProps> = ({ onBackToHome }) => {
  const { user, logout } = useAuth();

  return (
    <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc', display: 'flex', flexDirection: 'column' }}>
      {/* Top Navigation */}
      <header style={{ backgroundColor: '#fff', borderBottom: '1px solid #e2e8f0', padding: '12px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
          <Logo />
          <span style={{ fontSize: '1rem', fontWeight: 700, color: '#7c3aed', borderLeft: '2px solid #e2e8f0', paddingLeft: '16px' }}>
            Hệ Thống Quản Trị Hệ Thống (System Admin Portal)
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
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '8px' }}>
            <span style={{ backgroundColor: '#7c3aed', color: '#fff', padding: '3px 10px', borderRadius: '20px', fontSize: '0.75rem', fontWeight: 700 }}>
              QUẢN TRỊ VIÊN CAO CẤP (ADMIN)
            </span>
          </div>
          <h1 style={{ fontSize: '1.5rem', fontWeight: 800, color: '#1e293b', margin: '4px 0' }}>
            Quản trị viên: {user?.fullName} ({user?.code})
          </h1>
          <p style={{ color: '#64748b', fontSize: '0.9rem', margin: 0 }}>
            Email: {user?.email} • Toàn quyền cấu hình hệ thống FUD
          </p>
        </div>

        {/* Feature Cards Grid (Placeholders) */}
        <h2 style={{ fontSize: '1.15rem', fontWeight: 700, color: '#334155', marginBottom: '16px' }}>
          Quản trị hệ thống & Phân quyền (Trang đang phát triển)
        </h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: '20px' }}>
          {/* Card 1 */}
          <div style={{ backgroundColor: '#fff', borderRadius: '10px', padding: '20px', border: '1px solid #e2e8f0' }}>
            <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: '#f5f3ff', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '12px', color: '#7c3aed' }}>
              <UserIcon size={22} />
            </div>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 700, color: '#1e293b', margin: '0 0 8px 0' }}>Quản Lý Tài Khoản & Phân Quyền</h3>
            <p style={{ fontSize: '0.85rem', color: '#64748b', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Quản lý danh sách Users, kích hoạt / đình chỉ tài khoản và gán vai trò Role (Admin, Manager, Guard, Student).
            </p>
            <button type="button" style={{ width: '100%', padding: '8px 0', borderRadius: '6px', border: '1px solid #7c3aed', color: '#7c3aed', backgroundColor: 'transparent', fontWeight: 600, fontSize: '0.85rem', cursor: 'pointer' }}>
              Vào quản trị người dùng →
            </button>
          </div>

          {/* Card 2 */}
          <div style={{ backgroundColor: '#fff', borderRadius: '10px', padding: '20px', border: '1px solid #e2e8f0' }}>
            <div style={{ width: '40px', height: '40px', borderRadius: '8px', backgroundColor: '#f1f5f9', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '12px', color: '#475569' }}>
              <ShieldIcon size={22} />
            </div>
            <h3 style={{ fontSize: '1.05rem', fontWeight: 700, color: '#1e293b', margin: '0 0 8px 0' }}>Nhật Ký Kiểm Toán (Audit Logs)</h3>
            <p style={{ fontSize: '0.85rem', color: '#64748b', margin: '0 0 16px 0', lineHeight: 1.5 }}>
              Tra cứu lịch sử đăng nhập, hành động nhạy cảm của người dùng và cơ chế đóng giả (Impersonation).
            </p>
            <button type="button" style={{ width: '100%', padding: '8px 0', borderRadius: '6px', border: '1px solid #cbd5e1', color: '#475569', backgroundColor: 'transparent', fontWeight: 600, fontSize: '0.85rem', cursor: 'pointer' }}>
              Xem Audit Logs →
            </button>
          </div>
        </div>
      </main>
    </div>
  );
};
