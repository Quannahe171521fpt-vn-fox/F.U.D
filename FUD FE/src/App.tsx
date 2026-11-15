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

import { newsApi } from './services/api';
import type { NewsItem } from './types/management';

import './styles/dormitory.css';
import './styles/management.css';

export function App() {
  const { currentUser, isLoading, logout: authLogout, sessionMessage, clearSessionMessage } = useAuth();

  const [isLoginOpen, setIsLoginOpen] = useState(false);

  const [userRole, setUserRole] = useState<'guest' | 'student' | 'manager'>(() => {
    const hasUser = !!localStorage.getItem('fud_currentUser');
    if (!hasUser) return 'guest';
    return (localStorage.getItem('fud_userRole') as 'guest' | 'student' | 'manager') || 'guest';
  });
  // Main views: home, announcements, management (Ban Quản lý), admin (Quản trị viên), guard (Đội Bảo vệ), student (Sinh viên)
  const [currentView, setCurrentView] = useState<'home' | 'announcements' | 'management' | 'admin' | 'guard' | 'student'>(() => {
    const rawUser = localStorage.getItem('fud_currentUser');
    if (!rawUser) {
      const saved = localStorage.getItem('fud_currentView');
      return saved === 'announcements' ? 'announcements' : 'home';
    }
    try {
      const user = JSON.parse(rawUser);
      const role = user.primaryRole;
      if (role === 'Admin') return 'admin';
      if (role === 'Guard') return 'guard';
      if (role === 'Manager') return 'management';
      return 'student';
    } catch {
      return 'home';
    }
  });
  const [selectedRoomId, setSelectedRoomId] = useState('room-4');

  // Sync role and view to localStorage to prevent reset on reload/refresh
  useEffect(() => {
    localStorage.setItem('fud_userRole', userRole);
  }, [userRole]);

  useEffect(() => {
    localStorage.setItem('fud_currentView', currentView);
  }, [currentView]);

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
        setUserRole('manager');
        setCurrentView(prev => (prev !== 'admin' ? 'admin' : prev));
      } else if (role === 'Guard') {
        setUserRole('manager');
        setCurrentView(prev => (prev !== 'guard' ? 'guard' : prev));
      } else if (role === 'Manager') {
        setUserRole('manager');
        setCurrentView(prev => (prev !== 'management' ? 'management' : prev));
      } else {
        setUserRole('student');
        setCurrentView(prev => (prev !== 'student' ? 'student' : prev));
      }
    } else {
      setUserRole('guest');
      setCurrentView(prev => (prev !== 'home' && prev !== 'announcements' ? 'home' : prev));
    }
  }, [currentUser, isLoading]);

  useEffect(() => {
    const onSessionExpired = () => {
      setUserRole('guest');
      setCurrentView('home');
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
    // Điều hướng trực tiếp theo role ngay khi login thành công
    const role = user.primaryRole;
    if (role === 'Admin') {
      setUserRole('manager');
      setCurrentView('admin');
    } else if (role === 'Guard') {
      setUserRole('manager');
      setCurrentView('guard');
    } else if (role === 'Manager') {
      setUserRole('manager');
      setCurrentView('management');
    } else {
      setUserRole('student');
      setCurrentView('student');
    }
  };

  const handleLogout = async () => {
    await authLogout();
    setUserRole('guest');
    setCurrentView('home');
    clearSessionStorage();
  };

  const isRestoringSession = isLoading && Boolean(currentUser);

  return (
    <div className="dormitory-page-wrapper">
      {/* 1. Navigation Header */}
      <Navbar
        onOpenLogin={() => setIsLoginOpen(true)}
        onNavigate={scrollToSection}
        currentRole={userRole}
        currentView={currentView}
        onChangeView={setCurrentView}
        onLogout={handleLogout}
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
      {!isRestoringSession && currentView === 'admin' && (
        <AdminPortal onLogout={handleLogout} />
      )}

      {/* VIEW 0.5: Guard Portal (Trang nghiệp vụ dành riêng cho Bảo vệ) */}
      {!isRestoringSession && currentView === 'guard' && (
        <GuardPortal onLogout={handleLogout} />
      )}

      {/* VIEW 1: Dedicated Management Portal with Sidebar (Ban Quản lý) */}
      {!isRestoringSession && currentView === 'management' && (
        <ManagerPortal />
      )}

      {/* VIEW 1.5: Student Portal (Cổng Sinh viên) */}
      {!isRestoringSession && currentView === 'student' && (
        <StudentPortal
          onLogout={handleLogout}
          onOpenAnnouncements={() => setCurrentView('announcements')}
        />
      )}

      {/* VIEW 2: Dedicated Announcements Page */}
      {currentView === 'announcements' && (
        <main>
          <AnnouncementsPage
            newsList={publicNews}
            onBackToHome={() => setCurrentView('home')}
          />
        </main>
      )}

      {/* VIEW 3: Homepage Page in Homepage folder */}
      {currentView === 'home' && (
        <HomePage
          selectedRoomId={selectedRoomId}
          onSelectRoom={handleSelectRoom}
          onNavigateToSection={scrollToSection}
          onOpenAnnouncements={() => setCurrentView('announcements')}
        />
      )}

      {/* Footer is only rendered on Homepage & Announcements, keeping Portals layout clean without line cuts */}
      {currentView !== 'management' && currentView !== 'admin' && currentView !== 'guard' && currentView !== 'student' && (
        <Footer onNavigate={(sec) => {
          if (currentView !== 'home') {
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

