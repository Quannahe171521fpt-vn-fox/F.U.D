import { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { Footer } from './components/Footer';
import { LoginModal } from './components/LoginModal';

// Pages split into dedicated folders
import { HomePage } from './pages/Homepage/HomePage';
import { ManagerPortal } from './pages/FacilityManagement/ManagerPortal';
import { AnnouncementsPage } from './pages/Announcements/AnnouncementsPage';

import { newsApi } from './services/api';
import type { NewsItem } from './types/management';

import './styles/dormitory.css';
import './styles/management.css';

export function App() {
  const [isLoginOpen, setIsLoginOpen] = useState(false);
  const [userRole, setUserRole] = useState<'guest' | 'student' | 'staff'>(() => {
    return (localStorage.getItem('fud_userRole') as 'guest' | 'student' | 'staff') || 'guest';
  });
  const [currentView, setCurrentView] = useState<'home' | 'announcements' | 'management'>(() => {
    const saved = localStorage.getItem('fud_currentView') as 'home' | 'announcements' | 'management';
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

  const handleLoginSuccess = (role: 'student' | 'staff') => {
    setUserRole(role);
    if (role === 'staff') {
      setCurrentView('management');
    }
  };

  const handleLogout = () => {
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

      {/* VIEW 1: Dedicated Management Portal in Facility Management folder (UC28 & UC29) */}
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

      {/* Footer */}
      {currentView !== 'management' && (
        <Footer
          onNavigate={(sec) => {
            if (currentView !== 'home') {
              setCurrentView('home');
              setTimeout(() => scrollToSection(sec), 100);
            } else {
              scrollToSection(sec);
            }
          }}
        />
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

