import { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { Footer } from './components/Footer';
import { LoginModal } from './components/LoginModal';
import { AdminPortal } from './components/AdminPortal';
import { GuardPortal } from './components/GuardPortal';
import { useAuth } from './contexts/AuthContext';
// Pages split into dedicated folders
import { HomePage } from './pages/Homepage/HomePage';
import { ManagerPortal } from './pages/FacilityManagement/ManagerPortal';
import { AnnouncementsPage } from './pages/Announcements/AnnouncementsPage';

import { newsApi } from './services/api';
import type { NewsItem } from './types/management';

import './styles/dormitory.css';
import './styles/management.css';

export function App() {
  const { currentUser, logout: authLogout } = useAuth();

  const [isLoginOpen, setIsLoginOpen] = useState(false);

  const [userRole, setUserRole] = useState<'guest' | 'student' | 'manager'>(() => {
    return (localStorage.getItem('fud_userRole') as 'guest' | 'student' | 'manager') || 'guest';
  });
  // Main views: home, announcements, management (Ban Quản lý), admin (Quản trị viên), guard (Đội Bảo vệ)
  const [currentView, setCurrentView] = useState<'home' | 'announcements' | 'management' | 'admin' | 'guard'>(() => {
    const saved = localStorage.getItem('fud_currentView') as 'home' | 'announcements' | 'management' | 'admin' | 'guard';
    return saved || 'home';
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
    if (currentUser) {
      const role = currentUser.primaryRole;
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
        if (currentView === 'management' || currentView === 'admin' || currentView === 'guard') {
          setCurrentView('home');
        }
      }
    } else {
      setUserRole('guest');
      if (currentView === 'management' || currentView === 'admin' || currentView === 'guard') {
        setCurrentView('home');
      }
    }
  }, [currentUser]);

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

  const handleLoginSuccess = () => {
    // Điều hướng được tự động xử lý chuẩn xác theo currentUser.primaryRole trong useEffect
  };

  const handleLogout = async () => {
    await authLogout();
    setUserRole('guest');
    setCurrentView('home');
    localStorage.removeItem('fud_userRole');
    localStorage.removeItem('fud_currentView');
  };

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

      {/* VIEW 0: Admin Portal (Trang quản trị hệ thống dành riêng cho Admin) */}
      {currentView === 'admin' && (
        <AdminPortal onLogout={handleLogout} />
      )}

      {/* VIEW 0.5: Guard Portal (Trang nghiệp vụ dành riêng cho Bảo vệ) */}
      {currentView === 'guard' && (
        <GuardPortal onLogout={handleLogout} />
      )}

      {/* VIEW 1: Dedicated Management Portal with Sidebar (Ban Quản lý) */}
      {currentView === 'management' && (
        <ManagerPortal />
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
      {currentView !== 'management' && currentView !== 'admin' && currentView !== 'guard' && (
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

