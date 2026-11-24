import { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { Footer } from './components/Footer';
import { LoginModal } from './components/LoginModal';
import { AdminPortal } from './role portal/AdminPortal';
import { GuardPortal } from './role portal/GuardPortal';
import { StudentPortal } from './role portal/StudentPortal';
import { useAuth } from './contexts/AuthContext';
import type { UserInfo } from './contexts/AuthContext';
import { clearSessionStorage } from './services/sessionManager';
// Pages split into dedicated folders
import { HomePage } from './pages/Homepage/HomePage';
import { ManagerPortal } from './role portal/ManagerPortal';
import { AnnouncementsPage } from './pages/Announcements/AnnouncementsPage';
import { NotificationsPage } from './pages/Notifications/NotificationsPage';
import { ImpersonationBanner } from './components/ImpersonationBanner';

import { newsApi, navigateToStudentPayment, type NotificationItem } from './services/api';
import type { NewsItem } from './types/management';
import {
  isManagerTab,
  resolveNotificationTarget,
  type AppView,
  type GuardTab,
  type ManagerTab,
  type StudentTab,
} from './utils/notificationRouting';

import './styles/dormitory.css';
import './styles/management.css';

function getDefaultViewForRole(role: UserInfo['primaryRole']): AppView {
  if (role === 'Admin') return 'admin';
  if (role === 'Guard') return 'guard';
  if (role === 'Manager') return 'management';
  return 'student';
}

function getDefaultPortalTabs(): { guard: GuardTab; management: ManagerTab; student: StudentTab } {
  const savedManagerTab = typeof localStorage !== 'undefined' ? localStorage.getItem('fud_managerTab') : null;
  return {
    guard: 'checkin',
    management: savedManagerTab && isManagerTab(savedManagerTab) ? savedManagerTab : 'terms',
    student: 'profile',
  };
}

export function App() {
  const {
    currentUser,
    isLoading,
    logout: authLogout,
    sessionMessage,
    clearSessionMessage,
    impersonation,
    isImpersonating,
    stopImpersonation,
  } = useAuth();

  const [isLoginOpen, setIsLoginOpen] = useState(false);

  const userRole: 'guest' | 'student' | 'manager' = !currentUser
    ? 'guest'
    : currentUser.primaryRole === 'Student'
      ? 'student'
      : 'manager';
  // Main views: home, announcements, management (Ban Quản lý), admin (Quản trị viên), guard (Đội Bảo vệ), student (Sinh viên), notifications (Tất cả thông báo)
  const [currentView, setCurrentView] = useState<AppView>('home');
  const [previousView, setPreviousView] = useState<AppView | null>(null);
  const [bellSyncKey, setBellSyncKey] = useState(0);
  const [pageSyncKey, setPageSyncKey] = useState(0);
  const [selectedRoomId, setSelectedRoomId] = useState('room-4');
  const [portalTabs, setPortalTabs] = useState(getDefaultPortalTabs);

  const resetPortalTabs = () => {
    setPortalTabs({
      guard: 'checkin',
      management: 'terms',
      student: 'profile',
    });
  };

  // Published news list for student Announcements page from Database
  const [publicNews, setPublicNews] = useState<NewsItem[]>([]);

  useEffect(() => {
    const fetchPublicNews = async () => {
      try {
        const data = await newsApi.getPublished();
        if (data && data.length > 0) {
          const mapped: NewsItem[] = data.map((n: any) => ({
            id: n.id,
            title: n.title,
            content: n.content,
            status: 'Published',
            publishedDate: n.publishedTime
              ? new Date(n.publishedTime).toLocaleDateString('vi-VN')
              : null,
            attachments: n.attachments?.map((a: any) => ({
              name: a.fileName || 'Tài liệu đính kèm',
              size: a.fileSize || '1.0 MB',
              type: a.fileType || 'pdf',
            })) || [],
          }));
          setPublicNews(mapped);
        }
      } catch (e) {
        console.warn('Could not load public news from API, using fallback data', e);
      }
    };
    fetchPublicNews();
  }, [currentView]);

  useEffect(() => {
    if (isLoading) return; // QUAN TRỌNG: Giữ nguyên view và role trong lúc AuthContext đang xác thực phiên khi vừa F5

    if (currentUser) {
      const role = currentUser.primaryRole;
      if (role === 'Admin') {
        setCurrentView(prev => (prev !== 'admin' ? 'admin' : prev));
      } else if (role === 'Guard') {
        setCurrentView(prev => (prev !== 'guard' && prev !== 'notifications' ? 'guard' : prev));
      } else if (role === 'Manager') {
        if (!isImpersonating) {
          setCurrentView(prev => (prev !== 'management' && prev !== 'notifications' ? 'management' : prev));
        }
      } else {
        setCurrentView(prev => (prev !== 'student' && prev !== 'notifications' ? 'student' : prev));
      }
    } else {
      setCurrentView(prev => (prev !== 'home' && prev !== 'announcements' ? 'home' : prev));
    }
  }, [currentUser, isLoading, isImpersonating]);

  useEffect(() => {
    const onSessionExpired = () => {
      setCurrentView('home');
      setPreviousView(null);
      resetPortalTabs();
    };
    window.addEventListener('fud:session-expired', onSessionExpired);
    return () => window.removeEventListener('fud:session-expired', onSessionExpired);
  }, []);

  const scrollToSection = (sectionId: string) => {
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  };

  const handleSelectRoom = (roomId: string) => {
    setSelectedRoomId(roomId);
    scrollToSection('registration-guide');
  };

  const handleLoginSuccess = (user: UserInfo) => {
    clearSessionMessage();
    resetPortalTabs();
    setPreviousView(null);
    // Điều hướng trực tiếp theo role ngay khi login thành công
    setCurrentView(getDefaultViewForRole(user.primaryRole));
  };

  const handleExitImpersonation = () => {
    stopImpersonation();
    setCurrentView('management');
    setPortalTabs((prev) => ({ ...prev, management: 'impersonate', student: 'profile' }));
  };

  useEffect(() => {
    if (isImpersonating) {
      setPortalTabs((prev) => ({ ...prev, student: 'profile' }));
    }
  }, [isImpersonating]);

  const handleLogout = async () => {
    if (isImpersonating) {
      handleExitImpersonation();
      return;
    }
    await authLogout();
    setCurrentView('home');
    setPreviousView(null);
    clearSessionStorage();
    resetPortalTabs();
  };

  const handleNavigateFromNotification = (item: NotificationItem) => {
    if (!currentUser) return;
    const target = resolveNotificationTarget(currentUser.primaryRole, item.referenceType);
    if (!target) return;

    setPreviousView(null);
    if (target.role === 'Guard') {
      setCurrentView('guard');
      setPortalTabs((prev) => ({ ...prev, guard: target.tabId }));
    } else if (target.role === 'Manager') {
      setCurrentView('management');
      setPortalTabs((prev) => ({ ...prev, management: target.tabId }));
    } else {
      if (target.tabId === 'payment') {
        const refInvoiceId = Number(item.referenceId || 0);
        if (refInvoiceId > 0) {
          navigateToStudentPayment({ invoiceId: refInvoiceId });
        }
      }
      setCurrentView('student');
      setPortalTabs((prev) => ({ ...prev, student: target.tabId }));
    }
  };

  const handleOpenAllNotifications = () => {
    if (!currentUser || currentUser.primaryRole === 'Admin') return;
    setPreviousView((prev) => (currentView !== 'notifications' ? currentView : prev));
    setCurrentView('notifications');
  };

  const handleBackFromNotifications = () => {
    const fallback = currentUser ? getDefaultViewForRole(currentUser.primaryRole) : 'home';
    const target =
      previousView && previousView !== 'notifications'
        ? previousView
        : fallback;
    setPreviousView(null);
    setCurrentView(target);
  };

  const isRestoringSession = isLoading;
  const effectiveView: AppView = (() => {
    if (!currentUser) {
      return currentView === 'announcements' ? 'announcements' : 'home';
    }
    const role = currentUser.primaryRole;
    if (currentView === 'announcements') {
      return 'announcements';
    }
    if (currentView === 'notifications') {
      return role !== 'Admin' ? 'notifications' : getDefaultViewForRole(role);
    }
    if (currentView === 'admin') {
      return role === 'Admin' ? 'admin' : getDefaultViewForRole(role);
    }
    if (currentView === 'guard') {
      return role === 'Guard' ? 'guard' : getDefaultViewForRole(role);
    }
    if (currentView === 'management') {
      return role === 'Manager' ? 'management' : getDefaultViewForRole(role);
    }
    if (currentView === 'student') {
      return role === 'Student' || isImpersonating ? 'student' : getDefaultViewForRole(role);
    }
    return getDefaultViewForRole(role);
  })();

  return (
    <div className="dormitory-page-wrapper">
      {/* 0. Impersonation Banner cảnh báo khi Manager đang hỗ trợ sinh viên */}
      <ImpersonationBanner />

      {/* 1. Navigation Header */}
      <Navbar
        onOpenLogin={() => setIsLoginOpen(true)}
        onNavigate={scrollToSection}
        currentRole={isImpersonating ? 'student' : userRole}
        currentView={isImpersonating ? 'student' : effectiveView}
        onChangeView={setCurrentView}
        onLogout={isImpersonating ? handleExitImpersonation : handleLogout}
        onNavigateNotification={handleNavigateFromNotification}
        onOpenAllNotifications={handleOpenAllNotifications}
        notificationSyncKey={bellSyncKey}
        onNotificationsChanged={() => setPageSyncKey((k) => k + 1)}
      />

      {sessionMessage && !currentUser && !isLoading && (
        <div
          role="alert"
          style={{
            background: '#FFF7ED',
            borderBottom: '1px solid #FDBA74',
            color: '#9A3412',
            padding: '12px 24px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            gap: '16px',
            flexWrap: 'wrap',
            fontSize: '14px',
          }}
        >
          <span>{sessionMessage}</span>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              onClick={() => setIsLoginOpen(true)}
              style={{
                background: '#EA580C',
                color: '#FFFFFF',
                border: 'none',
                borderRadius: '6px',
                padding: '6px 12px',
                cursor: 'pointer',
                fontWeight: 600,
              }}
            >
              Đăng nhập lại
            </button>
            <button
              type="button"
              onClick={clearSessionMessage}
              style={{
                background: 'transparent',
                color: '#9A3412',
                border: '1px solid #FDBA74',
                borderRadius: '6px',
                padding: '6px 12px',
                cursor: 'pointer',
              }}
            >
              Đóng
            </button>
          </div>
        </div>
      )}

      {isRestoringSession && (
        <main
          style={{
            minHeight: '60vh',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#475569',
            fontSize: '15px',
          }}
        >
          Đang kiểm tra phiên đăng nhập...
        </main>
      )}

      {/* VIEW 0: Admin Portal (Trang quản trị hệ thống dành riêng cho Admin) */}
      {!isRestoringSession && effectiveView === 'admin' && (
        <AdminPortal onLogout={handleLogout} />
      )}

      {/* VIEW 0.5: Guard Portal (Trang nghiệp vụ dành riêng cho Bảo vệ) */}
      {!isRestoringSession && effectiveView === 'guard' && (
        <GuardPortal
          onLogout={handleLogout}
          activeTab={portalTabs.guard}
          onTabChange={(tab) => setPortalTabs((prev) => ({ ...prev, guard: tab }))}
        />
      )}

      {/* VIEW 1: Dedicated Management Portal with Sidebar (Ban Quản lý) */}
      {!isRestoringSession && effectiveView === 'management' && !isImpersonating && (
        <ManagerPortal
          activeTab={portalTabs.management}
          onTabChange={(tab) => setPortalTabs((prev) => ({ ...prev, management: tab }))}
        />
      )}

      {/* VIEW 1.5: Student Portal (Cổng Sinh viên: Normal hoặc Impersonation) */}
      {!isRestoringSession &&
        (effectiveView === 'student' ||
          (isImpersonating && effectiveView !== 'notifications' && effectiveView !== 'announcements')) && (
        <StudentPortal
          onLogout={isImpersonating ? handleExitImpersonation : handleLogout}
          impersonatedUser={isImpersonating ? impersonation?.student : null}
          onOpenAnnouncements={() => setCurrentView('announcements')}
          activeTab={portalTabs.student}
          onTabChange={(tab) => setPortalTabs((prev) => ({ ...prev, student: tab }))}
        />
      )}

      {/* VIEW 1.8: All Notifications Page (Student, Manager, Guard) */}
      {!isRestoringSession &&
        effectiveView === 'notifications' &&
        currentUser &&
        currentUser.primaryRole !== 'Admin' && (
          <NotificationsPage
            userRole={currentUser.primaryRole}
            syncKey={pageSyncKey}
            onBack={handleBackFromNotifications}
            onNavigateTarget={handleNavigateFromNotification}
            onNotificationsChanged={() => setBellSyncKey((k) => k + 1)}
          />
        )}

      {/* VIEW 2: Dedicated Announcements Page */}
      {!isRestoringSession && effectiveView === 'announcements' && (
        <main>
          <AnnouncementsPage
            newsList={publicNews}
            onBackToHome={() => setCurrentView(currentUser ? getDefaultViewForRole(currentUser.primaryRole) : 'home')}
          />
        </main>
      )}

      {/* VIEW 3: Homepage Page in Homepage folder */}
      {!isRestoringSession && effectiveView === 'home' && (
        <HomePage
          selectedRoomId={selectedRoomId}
          onSelectRoom={handleSelectRoom}
          onNavigateToSection={scrollToSection}
          onOpenAnnouncements={() => setCurrentView('announcements')}
        />
      )}

      {/* Footer is only rendered on Homepage & Announcements, keeping Portals & Notifications layout clean without line cuts */}
      {!isRestoringSession && (effectiveView === 'home' || effectiveView === 'announcements') && (
        <Footer onNavigate={(sec) => {
          if (effectiveView !== 'home') {
            setCurrentView('home');
            setTimeout(() => scrollToSection(sec), 100);
          } else {
            scrollToSection(sec);
          }
        }} />
      )}

      {/* Login Modal */}
      <LoginModal
        isOpen={isLoginOpen}
        onClose={() => setIsLoginOpen(false)}
        onLoginSuccess={handleLoginSuccess}
      />
    </div>
  );
}

export default App;

