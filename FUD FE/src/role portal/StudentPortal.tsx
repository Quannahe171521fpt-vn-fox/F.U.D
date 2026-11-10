import React, { useState, useEffect } from 'react';
import { SharedSidebar, type SidebarSection } from './SharedSidebar';
import { StudentViolationNoticesPage } from '../pages/Violations/StudentViolationNoticesPage';
import { StudentViolationHistoryPage } from '../pages/Violations/StudentViolationHistoryPage';
import { SubmitRequestPage } from '../pages/Requests/SubmitRequestPage';
import { StudentRequestListPage } from '../pages/Requests/StudentRequestListPage';
import { AnnouncementsPage } from '../pages/Announcements/AnnouncementsPage';
import { StudentCheckinRecordsPage } from '../pages/Checkin/StudentCheckinRecordsPage';
import { StudentResidencyHistoryPage } from '../pages/Residency/StudentResidencyHistoryPage';
import { newsApi } from '../services/api';
import type { NewsItem } from '../types/management';
import { useAuth } from '../contexts/AuthContext';
import '../styles/management.css';

interface StudentPortalProps {
  onLogout?: () => void;
  onOpenAnnouncements?: () => void;
}

export const StudentPortal: React.FC<StudentPortalProps> = ({ onLogout }) => {
  const { currentUser } = useAuth();
  const [activeTab, setActiveTab] = useState<string>('profile');
  const [newsList, setNewsList] = useState<NewsItem[]>([]);

  // Tải danh sách thông báo tin tức KTX
  useEffect(() => {
    const loadNews = async () => {
      try {
        const data = await newsApi.getPublished();
        const mapped = data.map((item: any) => ({
          ...item,
          status: 'Published' as const,
        }));
        setNewsList(mapped);
      } catch (err) {
        console.error('Không thể tải tin tức:', err);
      }
    };
    loadNews();
  }, []);

  // Menu bên trái dành cho Sinh viên
  const sidebarSections: SidebarSection[] = [
    {
      label: 'HỒ SƠ CÁ NHÂN',
      items: [
        { id: 'profile', name: 'Thông tin sinh viên' },
      ],
    },
    {
      label: 'NỘI TRÚ & PHÒNG Ở',
      items: [
        { id: 'residency-history', name: 'Lịch sử lưu trú' },
        { id: 'checkin-records', name: 'Biên bản nhận phòng' },
      ],
    },
    {
      label: 'THÔNG BÁO & TIN TỨC',
      items: [
        { id: 'announcements', name: 'Bảng tin KTX' },
      ],
    },
    {
      label: 'ĐƠN TỪ & YÊU CẦU',
      items: [
        { id: 'request-list', name: 'Danh sách đơn từ' },
        { id: 'submit-request', name: 'Gửi đơn yêu cầu' },
      ],
    },
    {
      label: 'KỶ LUẬT & NỘI QUY',
      items: [
        { id: 'notices', name: 'Thông báo vi phạm' },
        { id: 'history', name: 'Lịch sử vi phạm' },
      ],
    },
  ];

  return (
    <div className="manager-shell" style={{ display: 'flex', width: '100%', minHeight: 'calc(100vh - 64px)' }}>
      {/* Sidebar */}
      <SharedSidebar
        roleBadge="STUDENT"
        roleBadgeColor={{ bg: '#C2410C', text: '#FFFFFF' }}
        title="Cổng Sinh Viên"
        subText={currentUser?.fullName || 'Sinh viên KTX'}
        sections={sidebarSections}
        activeId={activeTab}
        onSelect={(tabId) => setActiveTab(tabId)}
        onLogout={onLogout}
      />

      {/* Main Content Area */}
      <main className="manager-content" style={{ flex: 1, backgroundColor: '#F8FAFC', overflowY: 'auto' }}>
        {currentUser?.accountStatus === 'Suspended' && (
          <div
            style={{
              margin: '20px 32px 0',
              padding: '16px 20px',
              backgroundColor: '#FEF2F2',
              border: '1px solid #F87171',
              borderRadius: '10px',
              color: '#991B1B',
              display: 'flex',
              alignItems: 'center',
              gap: '14px',
            }}
          >
            <span style={{ fontSize: '24px' }}>⚠️</span>
            <div>
              <div style={{ fontWeight: 700, fontSize: '15px', marginBottom: '2px' }}>
                Tài khoản đang trong diện ĐÌNH CHỈ NỘI TRÚ
              </div>
              <div style={{ fontSize: '13px', lineHeight: 1.5 }}>
                {currentUser?.suspensionReason || 'Bạn đã bị đình chỉ nội trú do vi phạm quy chế KTX.'}{' '}
                <strong>Hệ thống đã khóa quyền Book phòng, giữ phòng và toàn bộ chức năng gửi đơn yêu cầu (Request).</strong> Chỉ khi Ban Quản lý mở lại tài khoản bạn mới có thể thực hiện thao tác.
              </div>
            </div>
          </div>
        )}

        {activeTab === 'notices' && (
          <StudentViolationNoticesPage
            onGoToAppeal={() => {
              setActiveTab('submit-request');
            }}
          />
        )}

        {activeTab === 'history' && <StudentViolationHistoryPage />}

        {activeTab === 'request-list' && (
          <StudentRequestListPage onGoToSubmit={() => setActiveTab('submit-request')} />
        )}

        {activeTab === 'submit-request' && (
          <SubmitRequestPage onSubmitted={() => setActiveTab('request-list')} />
        )}

        {activeTab === 'residency-history' && (
          <StudentResidencyHistoryPage
            onOpenCheckinRecords={() => setActiveTab('checkin-records')}
            onBackToProfile={() => setActiveTab('profile')}
          />
        )}

        {activeTab === 'checkin-records' && (
          <StudentCheckinRecordsPage onBackToProfile={() => setActiveTab('profile')} />
        )}

        {/* Xem trực tiếp Bảng tin Thông báo ngay bên trong StudentPortal */}
        {activeTab === 'announcements' && (
          <AnnouncementsPage
            newsList={newsList}
            onBackToHome={() => setActiveTab('profile')}
          />
        )}

        {activeTab === 'profile' && (
          <div style={{ padding: '24px 32px' }}>
            <div style={{ marginBottom: 20 }}>
              <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px', color: '#0F172A' }}>
                Hồ sơ sinh viên
              </h1>
              <p style={{ color: '#64748B', fontSize: '13px', margin: 0 }}>
                Thông tin sinh viên nội trú ký túc xá Đại học FPT
              </p>
            </div>

            <div
              style={{
                backgroundColor: '#FFFFFF',
                borderRadius: '12px',
                border: '1px solid #E2E8F0',
                padding: '24px',
                maxWidth: '700px',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '16px', marginBottom: 20 }}>
                <div
                  style={{
                    width: '60px',
                    height: '60px',
                    borderRadius: '50%',
                    backgroundColor: '#FFF7ED',
                    color: '#EA580C',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    boxShadow: 'inset 0 0 0 1px #FDBA74',
                  }}
                >
                  <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
                    <path d="M6 12v5c3 3 9 3 12 0v-5" />
                  </svg>
                </div>
                <div>
                  <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '0 0 4px', color: '#0F172A' }}>
                    {currentUser?.fullName || 'Sinh viên KTX'}
                  </h2>
                  <span
                    style={{
                      display: 'inline-block',
                      padding: '3px 10px',
                      borderRadius: '9999px',
                      backgroundColor: '#FFF7ED',
                      color: '#C2410C',
                      fontSize: '11px',
                      fontWeight: 700,
                    }}
                  >
                    SINH VIÊN NỘI TRÚ
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
                  lineHeight: 1.8,
                  marginBottom: 20,
                }}
              >
                <div><strong>Mã sinh viên (MSSV):</strong> {currentUser?.code || 'SE...'}</div>
                <div><strong>Email FPT:</strong> {currentUser?.email || '—'}</div>
                <div><strong>Họ và tên:</strong> {currentUser?.fullName || '—'}</div>
                <div><strong>Vai trò hệ thống:</strong> Sinh viên (Student)</div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <button
                  type="button"
                  onClick={() => setActiveTab('notices')}
                  style={{
                    padding: '12px 16px',
                    backgroundColor: '#EA580C',
                    color: '#FFFFFF',
                    border: 'none',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  Xem Thông báo vi phạm
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab('history')}
                  style={{
                    padding: '12px 16px',
                    backgroundColor: '#F1F5F9',
                    color: '#0F172A',
                    border: '1px solid #CBD5E1',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                >
                  Xem Lịch sử vi phạm
                </button>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginTop: 12 }}>
                <button
                  type="button"
                  onClick={() => setActiveTab('residency-history')}
                  style={{
                    padding: '12px 16px',
                    backgroundColor: '#FFF7ED',
                    color: '#C2410C',
                    border: '1px solid #FDBA74',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 8,
                  }}
                >
                  <span>🏠</span> Xem Lịch sử lưu trú
                </button>

                <button
                  type="button"
                  onClick={() => setActiveTab('checkin-records')}
                  style={{
                    padding: '12px 16px',
                    backgroundColor: '#FFF7ED',
                    color: '#C2410C',
                    border: '1px solid #FDBA74',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 8,
                  }}
                >
                  <span>📋</span> Xem Biên bản nhận phòng
                </button>
              </div>

              <div style={{ marginTop: 12 }}>
                <button
                  type="button"
                  onClick={() => setActiveTab('announcements')}
                  style={{
                    width: '100%',
                    padding: '10px 16px',
                    backgroundColor: '#FFFFFF',
                    color: '#475569',
                    border: '1px solid #CBD5E1',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 600,
                    cursor: 'pointer',
                  }}
                >
                  Xem bảng tin thông báo KTX
                </button>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
