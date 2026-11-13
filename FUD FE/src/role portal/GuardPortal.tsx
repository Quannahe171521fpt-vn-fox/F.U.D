import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { RecordViolationPage } from '../pages/Violations/RecordViolationPage';
import { ViolationListPage } from '../pages/Violations/ViolationListPage';
import { GuardCheckinPage } from '../pages/Checkin/GuardCheckinPage';
import { SharedSidebar, type SidebarSection } from './SharedSidebar';

interface GuardPortalProps {
  onLogout: () => void;
}

export const GuardPortal: React.FC<GuardPortalProps> = ({ onLogout }) => {
  const { currentUser } = useAuth();
  const [activeTab, setActiveTab] = useState<'checkin' | 'checkin-list' | 'create' | 'list' | 'overview'>('checkin');

  const guardSections: SidebarSection[] = [
    {
      label: 'BÀN GIAO PHÒNG (CHECK-IN)',
      items: [
        { id: 'checkin', name: 'Check-in nhận phòng', counter: 'Lập biên bản bàn giao' },
        { id: 'checkin-list', name: 'Biên bản đã Check-in', counter: 'Tra cứu & In biên bản' },
      ],
    },
    {
      label: 'HỒ SƠ VI PHẠM',
      items: [
        { id: 'create', name: 'Ghi nhận vi phạm', counter: 'Lập biên bản mới' },
        { id: 'list', name: 'Danh sách hồ sơ', counter: 'Xem & đối soát' },
      ],
    },
    {
      label: 'TÀI KHOẢN',
      items: [
        { id: 'overview', name: 'Thông tin cán bộ', counter: currentUser?.code || 'GRD01' },
      ],
    },
  ];

  return (
    <div className="manager-dashboard-layout">
      {/* Sidebar chung - Phân chia theo Role Guard */}
      <SharedSidebar
        roleBadge="BẢO VỆ"
        roleBadgeColor={{ bg: '#ECFDF5', text: '#047857' }}
        title="An ninh Ký túc xá"
        subText={currentUser?.fullName 
          ? `${currentUser.fullName} (${currentUser.code})${currentUser.dormName ? ` · KTX: ${currentUser.dormName}` : ''}`
          : 'Cổng Đội Bảo vệ'}
        sections={guardSections}
        activeId={activeTab}
        onSelect={(id) => setActiveTab(id as any)}
        onLogout={onLogout}
      />

      {/* Vùng nội dung chính */}
      <main className="manager-main-content">
        {activeTab === 'checkin' ? (
          <GuardCheckinPage mode="create" onNavigateHistory={() => setActiveTab('checkin-list')} />
        ) : activeTab === 'checkin-list' ? (
          <GuardCheckinPage mode="history" />
        ) : activeTab === 'create' ? (
          <RecordViolationPage onSuccessNavigate={() => setActiveTab('list')} />
        ) : activeTab === 'list' ? (
          <ViolationListPage />
        ) : (
          <div style={{ padding: '24px 32px' }}>
            <div
              style={{
                maxWidth: '560px',
                margin: '40px auto',
                backgroundColor: '#FFFFFF',
                borderRadius: '16px',
                border: '1px solid #E2E8F0',
                boxShadow: '0 10px 25px -5px rgba(15, 23, 42, 0.08)',
                padding: '36px 32px',
                textAlign: 'center',
              }}
            >
              <div
                style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: '50%',
                  backgroundColor: '#ECFDF5',
                  color: '#059669',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  margin: '0 auto 20px',
                }}
              >
                <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                </svg>
              </div>

              <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '0 0 8px', color: '#0F172A' }}>
                {currentUser?.fullName || 'Cán bộ bảo vệ'}
              </h2>
              <p style={{ color: '#64748B', fontSize: '13px', margin: '0 0 20px' }}>
                Đội an ninh trật tự & quản lý kỷ luật Ký túc xá FUD
              </p>

              <div
                style={{
                  backgroundColor: '#F8FAFC',
                  borderRadius: '12px',
                  padding: '16px 20px',
                  textAlign: 'left',
                  fontSize: '13px',
                  lineHeight: 1.8,
                  color: '#334155',
                }}
              >
                <div><strong>Mã cán bộ:</strong> {currentUser?.code || 'GRD01'}</div>
                <div><strong>Email công vụ:</strong> {currentUser?.email || 'guard@fpt.edu.vn'}</div>
                {currentUser?.dormName && (
                  <div><strong>KTX phụ trách:</strong> {currentUser.dormName}</div>
                )}
                <div><strong>Ca trực:</strong> Toàn thời gian ký túc xá</div>
                <div><strong>Nhiệm vụ chính:</strong> Bàn giao nhận phòng (Check-in), tuần tra & lập biên bản vi phạm</div>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
