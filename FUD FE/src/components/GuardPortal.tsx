import React, { useState } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { RecordViolationPage } from '../pages/Violations/RecordViolationPage';
import { ViolationListPage } from '../pages/Violations/ViolationListPage';

interface GuardPortalProps {
  onLogout: () => void;
}

export const GuardPortal: React.FC<GuardPortalProps> = ({ onLogout: _onLogout }) => {
  const { currentUser } = useAuth();
  const [activeTab, setActiveTab] = useState<'create' | 'list' | 'overview'>('create');

  return (
    <div className="manager-dashboard-layout">
      {/* 1. Dedicated Guard Sidebar (Chuẩn theo Layout của Manager) */}
      <aside className="manager-sidebar">
        <div className="sidebar-brand-block">
          <div className="sidebar-brand-badge" style={{ backgroundColor: '#ecfdf5', color: '#047857' }}>
            BẢO VỆ
          </div>
          <h2 className="sidebar-brand-title">An ninh Ký túc xá</h2>
          <span className="sidebar-subtext">Cổng nghiệp vụ Đội Bảo vệ</span>
        </div>

        <nav className="sidebar-nav-list">
          <div className="sidebar-nav-section-label">HỒ SƠ VI PHẠM</div>

          <button
            type="button"
            className={`sidebar-nav-item ${activeTab === 'create' ? 'active' : ''}`}
            onClick={() => setActiveTab('create')}
          >
            <span className="nav-item-indicator"></span>
            <div className="nav-item-content">
              <span className="nav-item-name">Ghi nhận vi phạm</span>
              <span className="nav-item-counter">Lập biên bản mới</span>
            </div>
          </button>

          <button
            type="button"
            className={`sidebar-nav-item ${activeTab === 'list' ? 'active' : ''}`}
            onClick={() => setActiveTab('list')}
          >
            <span className="nav-item-indicator"></span>
            <div className="nav-item-content">
              <span className="nav-item-name">Danh sách hồ sơ</span>
              <span className="nav-item-counter">Xem & đối soát</span>
            </div>
          </button>

          <div className="sidebar-nav-section-label" style={{ marginTop: 24 }}>TÀI KHOẢN</div>

          <button
            type="button"
            className={`sidebar-nav-item ${activeTab === 'overview' ? 'active' : ''}`}
            onClick={() => setActiveTab('overview')}
          >
            <span className="nav-item-indicator"></span>
            <div className="nav-item-content">
              <span className="nav-item-name">Thông tin cán bộ</span>
              <span className="nav-item-counter">{currentUser?.code || 'GRD01'}</span>
            </div>
          </button>
        </nav>
      </aside>

      {/* 2. Main Content Area */}
      <main className="manager-main-content">
        {activeTab === 'create' ? (
          <RecordViolationPage onSuccessNavigate={() => setActiveTab('list')} />
        ) : activeTab === 'list' ? (
          <ViolationListPage />
        ) : (
          <div
            style={{
              maxWidth: '520px',
              margin: '60px auto',
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
              🛡️
            </div>
            <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '0 0 16px' }}>
              Cổng Nghiệp Vụ Bảo Vệ KTX
            </h2>
            <div
              style={{
                backgroundColor: '#F1F5F9',
                borderRadius: '12px',
                padding: '16px',
                textAlign: 'left',
                fontSize: '13px',
                lineHeight: 1.8,
                marginBottom: '20px',
              }}
            >
              <div><strong>Mã cán bộ:</strong> {currentUser?.code || 'GRD01'}</div>
              <div><strong>Họ tên:</strong> {currentUser?.fullName || 'Lê Văn Bảo Vệ'}</div>
              <div><strong>Email:</strong> {currentUser?.email || 'guard01@fpt.edu.vn'}</div>
              <div><strong>Vai trò:</strong> Đội Bảo vệ KTX (Guard)</div>
            </div>
            <button
              type="button"
              className="btn-submit"
              onClick={() => setActiveTab('create')}
            >
              Đi đến Ghi nhận vi phạm
            </button>
          </div>
        )}
      </main>
    </div>
  );
};
