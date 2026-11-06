import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { SharedSidebar, type SidebarSection } from './SharedSidebar';

interface AdminPortalProps {
  onLogout: () => void;
}

export const AdminPortal: React.FC<AdminPortalProps> = ({ onLogout }) => {
  const { currentUser } = useAuth();
  const [activeTab, setActiveTab] = useState<'overview' | 'accounts' | 'roles' | 'system'>('overview');

  const adminSections: SidebarSection[] = [
    {
      label: 'QUẢN TRỊ HỆ THỐNG',
      items: [
        { id: 'overview', name: 'Tổng quan hệ thống', counter: 'Dashboard' },
        { id: 'accounts', name: 'Quản lý Tài khoản', counter: 'Users' },
        { id: 'roles', name: 'Phân quyền & Vai trò', counter: 'Roles' },
        { id: 'system', name: 'Nhật ký & Cấu hình', counter: 'Audit Logs' },
      ],
    },
  ];

  return (
    <div className="manager-dashboard-layout">
      {/* Sidebar chung - Phân chia theo Role Admin */}
      <SharedSidebar
        roleBadge="QUẢN TRỊ VIÊN"
        roleBadgeColor={{ bg: '#EFF6FF', text: '#1D4ED8' }}
        title="Quản Trị Hệ Thống"
        subText={currentUser?.fullName ? `${currentUser.fullName} (${currentUser.code})` : 'Cổng Admin'}
        sections={adminSections}
        activeId={activeTab}
        onSelect={(id) => setActiveTab(id as any)}
        onLogout={onLogout}
      />

      {/* Vùng nội dung chính */}
      <main className="manager-main-content">
        <div style={{ padding: '24px 32px' }}>
          <div style={{ marginBottom: '24px' }}>
            <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
              {activeTab === 'overview' && 'Tổng quan hệ thống KTX FUD'}
              {activeTab === 'accounts' && 'Quản lý danh sách tài khoản'}
              {activeTab === 'roles' && 'Phân quyền & Gán vai trò người dùng'}
              {activeTab === 'system' && 'Nhật ký kiểm toán & Cấu hình hệ thống'}
            </h1>
            <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
              Dành riêng cho Quản trị viên (Admin) quản lý và vận hành toàn bộ nền tảng
            </p>
          </div>

          {/* Account Profile Card */}
          <div
            style={{
              backgroundColor: '#FFFFFF',
              borderRadius: '16px',
              border: '1px solid #E2E8F0',
              padding: '24px',
              maxWidth: '680px',
              boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.05)',
              marginBottom: '20px',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: '20px' }}>
              <div
                style={{
                  width: '56px',
                  height: '56px',
                  borderRadius: '50%',
                  backgroundColor: '#EFF6FF',
                  color: '#2563EB',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontWeight: 700,
                  fontSize: '20px',
                }}
              >
                ADM
              </div>
              <div>
                <h2 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 4px', color: '#0F172A' }}>
                  {currentUser?.fullName || 'Quản trị viên'}
                </h2>
                <span
                  style={{
                    display: 'inline-block',
                    padding: '3px 10px',
                    borderRadius: '9999px',
                    backgroundColor: '#EFF6FF',
                    color: '#1D4ED8',
                    fontSize: '11px',
                    fontWeight: 700,
                  }}
                >
                  ADMINISTRATOR
                </span>
              </div>
            </div>

            <div
              style={{
                display: 'grid',
                gridTemplateColumns: '1fr 1fr',
                gap: '12px',
                backgroundColor: '#F8FAFC',
                padding: '16px',
                borderRadius: '10px',
                fontSize: '13px',
              }}
            >
              <div><strong>Mã định danh:</strong> {currentUser?.code || 'ADMIN01'}</div>
              <div><strong>Email công vụ:</strong> {currentUser?.email || 'admin@fpt.edu.vn'}</div>
              <div><strong>Trạng thái tài khoản:</strong> <span style={{ color: '#16A34A', fontWeight: 600 }}>Hoạt động (Active)</span></div>
              <div><strong>Quyền hạn:</strong> Toàn quyền hệ thống (Full Admin)</div>
            </div>
          </div>

          <div
            style={{
              backgroundColor: '#FEF3C7',
              border: '1px solid #FCD34D',
              borderRadius: '12px',
              padding: '16px 20px',
              maxWidth: '680px',
              fontSize: '13px',
              color: '#92400E',
              lineHeight: 1.6,
            }}
          >
            <strong>Lưu ý:</strong> Phân hệ quản lý tài khoản và phân quyền người dùng đang được kích hoạt kết nối CSDL và các bảng kiểm toán hệ thống.
          </div>
        </div>
      </main>
    </div>
  );
};
