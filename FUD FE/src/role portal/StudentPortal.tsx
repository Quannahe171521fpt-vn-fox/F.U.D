import React, { useState, useEffect } from 'react';
import { SharedSidebar, type SidebarSection } from './SharedSidebar';
import { StudentViolationNoticesPage } from '../pages/Violations/StudentViolationNoticesPage';
import { StudentViolationHistoryPage } from '../pages/Violations/StudentViolationHistoryPage';
import { SubmitRequestPage } from '../pages/Requests/SubmitRequestPage';
import { StudentRequestListPage } from '../pages/Requests/StudentRequestListPage';
import { AnnouncementsPage } from '../pages/Announcements/AnnouncementsPage';
import { StudentCheckinRecordsPage } from '../pages/Checkin/StudentCheckinRecordsPage';
import { StudentResidencyHistoryPage } from '../pages/Residency/StudentResidencyHistoryPage';
import { StudentKeepingPage } from '../pages/Keeping/StudentKeepingPage';
import { newsApi, violationApi, requestApi, bookingApi } from '../services/api';
import { BookingPage } from '../pages/Booking/BookingPage';
import { StudentEwUsagePage } from '../pages/Student/StudentEwUsagePage';
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
  const [hasNewViolationNotice, setHasNewViolationNotice] = useState<boolean>(false);
  const [hasNewAnnouncements, setHasNewAnnouncements] = useState<boolean>(false);
  const [hasNewRequestUpdate, setHasNewRequestUpdate] = useState<boolean>(false);
  const [isBookingOpen, setIsBookingOpen] = useState<boolean>(false);

  // Kiểm tra thời gian mở cổng đặt phòng (Booking)
  useEffect(() => {
    const checkBooking = async () => {
      try {
        const elig = await bookingApi.getEligibility();
        setIsBookingOpen(elig.isBookingPeriodOpen);
      } catch {
        setIsBookingOpen(false);
      }
    };
    checkBooking();
    const interval = setInterval(checkBooking, 15000);
    return () => clearInterval(interval);
  }, [currentUser?.id]);

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

        // Kiểm tra xem có tin tức mới (trong 3 ngày gần nhất hoặc chưa xem)
        const lastSeenNewsTime = localStorage.getItem(`lastSeenNews_${currentUser?.id || 'guest'}`);
        if (data.length > 0) {
          const latestNewsDate = new Date(data[0].createdTime || data[0].publishDate || 0).getTime();
          if (!lastSeenNewsTime || latestNewsDate > Number(lastSeenNewsTime)) {
            setHasNewAnnouncements(true);
          }
        }
      } catch (err) {
        console.error('Không thể tải tin tức:', err);
      }
    };
    loadNews();
  }, [currentUser?.id]);

  // Kiểm tra thông báo vi phạm mới và cập nhật đơn từ
  useEffect(() => {
    const checkBadges = async () => {
      try {
        const [notices, requests] = await Promise.all([
          violationApi.getStudentNotices().catch(() => []),
          requestApi.getMyRequests().catch(() => []),
        ]);

        // Thông báo vi phạm: Có biên bản vi phạm mới (Recorded) và chưa đọc
        const lastSeenNoticeTime = localStorage.getItem(`lastSeenNotice_${currentUser?.id || 'guest'}`);
        const unhandledNotices = notices.filter((n: any) => {
          if (n.status !== 'Recorded') return false;
          const time = new Date(n.occurrenceTime || 0).getTime();
          return !lastSeenNoticeTime || time > Number(lastSeenNoticeTime);
        });
        setHasNewViolationNotice(unhandledNotices.length > 0);

        // Đơn từ: Có đơn được duyệt / từ chối gần đây hoặc đang có phản hồi
        const lastSeenReqTime = localStorage.getItem(`lastSeenReq_${currentUser?.id || 'guest'}`);
        const recentlyProcessed = requests.find((r: any) => {
          if (r.status === 'Approved' || r.status === 'Rejected') {
            const time = new Date(r.processedTime || r.createdTime).getTime();
            return !lastSeenReqTime || time > Number(lastSeenReqTime);
          }
          return false;
        });
        setHasNewRequestUpdate(!!recentlyProcessed);
      } catch (err) {
        console.error('Lỗi khi kiểm tra thông báo sidebar:', err);
      }
    };

    checkBadges();
    const interval = setInterval(checkBadges, 10000);
    return () => clearInterval(interval);
  }, [currentUser?.id]);

  const handleTabSelect = (tabId: string) => {
    setActiveTab(tabId);
    if (tabId === 'announcements') {
      setHasNewAnnouncements(false);
      localStorage.setItem(`lastSeenNews_${currentUser?.id || 'guest'}`, Date.now().toString());
    } else if (tabId === 'request-list') {
      setHasNewRequestUpdate(false);
      localStorage.setItem(`lastSeenReq_${currentUser?.id || 'guest'}`, Date.now().toString());
    } else if (tabId === 'notices') {
      setHasNewViolationNotice(false);
      localStorage.setItem(`lastSeenNotice_${currentUser?.id || 'guest'}`, Date.now().toString());
    }
  };

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
        { id: 'keeping', name: 'Giữ phòng kỳ tiếp' },
        { id: 'booking', name: 'Đặt phòng KTX', showBadgeDot: isBookingOpen },
        { id: 'residency-history', name: 'Lịch sử lưu trú' },
        { id: 'checkin-records', name: 'Biên bản nhận phòng' },
        { id: 'student-ew', name: 'Điện nước & Định mức' },
      ],
    },
    {
      label: 'THÔNG BÁO & TIN TỨC',
      items: [
        { id: 'announcements', name: 'Bảng tin KTX', showBadgeDot: hasNewAnnouncements },
      ],
    },
    {
      label: 'ĐƠN TỪ & YÊU CẦU',
      items: [
        { id: 'request-list', name: 'Danh sách đơn từ', showBadgeDot: hasNewRequestUpdate },
        { id: 'submit-request', name: 'Gửi đơn yêu cầu' },
      ],
    },
    {
      label: 'KỶ LUẬT & NỘI QUY',
      items: [
        { id: 'notices', name: 'Thông báo vi phạm', showBadgeDot: hasNewViolationNotice },
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
        onSelect={handleTabSelect}
        onLogout={onLogout}
      />

      {/* Main Content Area */}
      <main className="manager-content" style={{ flex: 1, backgroundColor: '#FFF1F2', overflowY: 'auto' }}>
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
            <span style={{ fontSize: '24px' }}></span>
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

        {activeTab === 'booking' && (
          <BookingPage onGoToRequests={() => setActiveTab('request-list')} />
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

        {activeTab === 'keeping' && <StudentKeepingPage />}

        {activeTab === 'residency-history' && (
          <StudentResidencyHistoryPage
            onOpenCheckinRecords={() => setActiveTab('checkin-records')}
            onBackToProfile={() => setActiveTab('profile')}
          />
        )}

        {activeTab === 'checkin-records' && (
          <StudentCheckinRecordsPage onBackToProfile={() => setActiveTab('profile')} />
        )}

        {activeTab === 'student-ew' && <StudentEwUsagePage />}

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

              {isBookingOpen && (
                <div
                  style={{
                    marginBottom: 20,
                    padding: '16px 20px',
                    backgroundColor: '#FFF7ED',
                    border: '1.5px solid #F97316',
                    borderRadius: '10px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 700, fontSize: '15px', color: '#9A3412', display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span>🛏️</span> Cổng Đặt phòng KTX đang mở!
                    </div>
                    <div style={{ fontSize: '13px', color: '#C2410C', marginTop: 2 }}>
                      Đang trong thời gian đăng ký chỗ ở Ký túc xá cho học kỳ mới.
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setActiveTab('booking')}
                    style={{
                      padding: '10px 20px',
                      backgroundColor: '#EA580C',
                      color: '#FFFFFF',
                      border: 'none',
                      borderRadius: '8px',
                      fontSize: '14px',
                      fontWeight: 700,
                      cursor: 'pointer',
                      boxShadow: '0 2px 8px rgba(234, 88, 12, 0.35)',
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                    }}
                  >
                    <span>🛏️</span> Booking now
                  </button>
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <button
                  type="button"
                  onClick={() => handleTabSelect('notices')}
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
                  onClick={() => setActiveTab('booking')}
                  style={{
                    padding: '12px 16px',
                    backgroundColor: isBookingOpen ? '#EA580C' : '#FFF7ED',
                    color: isBookingOpen ? '#FFFFFF' : '#C2410C',
                    border: isBookingOpen ? 'none' : '1px solid #FDBA74',
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
                  <span>🛏️</span> {isBookingOpen ? 'Booking now (Đang mở)' : 'Đặt phòng KTX'}
                </button>

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
              </div>

              <div style={{ marginTop: 12 }}>
                <button
                  type="button"
                  onClick={() => setActiveTab('checkin-records')}
                  style={{
                    width: '100%',
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
                  onClick={() => setActiveTab('student-ew')}
                  style={{
                    width: '100%',
                    padding: '12px 16px',
                    backgroundColor: '#EFF6FF',
                    color: '#1D4ED8',
                    border: '1px solid #BFDBFE',
                    borderRadius: '8px',
                    fontSize: '13px',
                    fontWeight: 700,
                    cursor: 'pointer',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    gap: 8,
                  }}
                >
                  <span>⚡💧</span> Xem Điện nước & Phí phụ trội
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

