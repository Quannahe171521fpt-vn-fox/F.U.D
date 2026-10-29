import { useState, useEffect } from 'react';
import { Navbar } from './components/Navbar';
import { HeroBanner } from './components/HeroBanner';
import { QuickCards } from './components/QuickCards';
import { DormInfoSection } from './components/DormInfoSection';
import { RoomTypesSection } from './components/RoomTypesSection';
import { RegistrationGuideAndForm } from './components/RegistrationGuideAndForm';
import { FaqSection } from './components/FaqSection';
import { SupportAndFeedbackSection } from './components/SupportAndFeedbackSection';
import { AboutSection } from './components/AboutSection';
import { Footer } from './components/Footer';
import { LoginModal } from './components/LoginModal';
import { AnnouncementsPage } from './components/AnnouncementsPage';
import { ManagerPortal } from './components/ManagerPortal';
import { AdminPortal } from './components/AdminPortal';
import { GuardPortal } from './components/GuardPortal';
import { useAuth } from './contexts/AuthContext';

import { INITIAL_NEWS, INITIAL_EQUIPMENT, INITIAL_CATALOG } from './data/managementData';
import type { NewsItem, EquipmentRecord, EquipmentCatalogItem } from './types/management';

import './styles/dormitory.css';
import './styles/management.css';

export function App() {
  const { currentUser, logout: authLogout } = useAuth();

  const [isLoginOpen, setIsLoginOpen] = useState(false);
  const [userRole, setUserRole] = useState<'guest' | 'student' | 'manager'>('guest');
  // Main views: home, announcements, management (Ban Quản lý), admin (Quản trị viên), guard (Đội Bảo vệ)
  const [currentView, setCurrentView] = useState<'home' | 'announcements' | 'management' | 'admin' | 'guard'>('home');
  const [selectedRoomId, setSelectedRoomId] = useState('room-4');

  // State for News Management
  const [newsList, setNewsList] = useState<NewsItem[]>(INITIAL_NEWS);

  // State for Equipment Management
  const [equipmentList, setEquipmentList] = useState<EquipmentRecord[]>(INITIAL_EQUIPMENT);
  const [catalogList, setCatalogList] = useState<EquipmentCatalogItem[]>(INITIAL_CATALOG);

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
  };

  // News Handlers
  const handleSaveNews = (newsItem: NewsItem) => {
    setNewsList((prev) => {
      const idx = prev.findIndex((n) => n.id === newsItem.id);
      if (idx >= 0) {
        const copy = [...prev];
        copy[idx] = newsItem;
        return copy;
      }
      return [newsItem, ...prev];
    });
  };

  const handleDeleteNews = (id: number) => {
    setNewsList((prev) => prev.filter((n) => n.id !== id));
  };

  const handleUnpublishNews = (id: number) => {
    setNewsList((prev) =>
      prev.map((n) =>
        n.id === id ? { ...n, status: 'Draft', publishedDate: null } : n
      )
    );
  };

  // Equipment Handlers
  const handleSaveEquipment = (record: EquipmentRecord) => {
    setEquipmentList((prev) => {
      const idx = prev.findIndex((e) => e.id === record.id);
      if (idx >= 0) {
        const copy = [...prev];
        copy[idx] = record;
        return copy;
      }
      return [record, ...prev];
    });
  };

  const handleDeleteEquipment = (id: number) => {
    setEquipmentList((prev) => prev.filter((e) => e.id !== id));
  };

  const handleSaveCatalogItem = (item: EquipmentCatalogItem) => {
    setCatalogList((prev) => [item, ...prev]);
  };

  const handleDeleteCatalogItem = (id: number) => {
    setCatalogList((prev) => prev.filter((c) => c.id !== id));
  };

  return (
    <div className="dormitory-page-wrapper">
      {/* 1. Header / Navigation (Clean, concise, no clutter) */}
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
        <ManagerPortal
          newsList={newsList}
          equipmentList={equipmentList}
          catalogList={catalogList}
          onSaveNews={handleSaveNews}
          onDeleteNews={handleDeleteNews}
          onUnpublishNews={handleUnpublishNews}
          onSaveEquipment={handleSaveEquipment}
          onDeleteEquipment={handleDeleteEquipment}
          onSaveCatalogItem={handleSaveCatalogItem}
          onDeleteCatalogItem={handleDeleteCatalogItem}
        />
      )}

      {/* VIEW 2: Dedicated Announcements Page (UI thông báo để riêng) */}
      {currentView === 'announcements' && (
        <main>
          <AnnouncementsPage
            newsList={newsList}
            onBackToHome={() => setCurrentView('home')}
          />
        </main>
      )}

      {/* VIEW 3: Homepage (General Information & Registration) */}
      {currentView === 'home' && (
        <main>
          {/* Hero Banner Carousel */}
          <HeroBanner />

          {/* Quick Action Cards: clicking news button directs to dedicated Announcements page */}
          <QuickCards
            onSelectAction={(sec) => {
              if (sec === 'support-section' || sec === 'registration-guide' || sec === 'faq-section' || sec === 'dorm-info') {
                scrollToSection(sec);
              } else {
                setCurrentView('announcements');
              }
            }}
          />

          {/* Dormitory General Information & Status */}
          <DormInfoSection />

          {/* Room Types & Facilities */}
          <RoomTypesSection onSelectRoom={handleSelectRoom} />

          {/* Registration Process & Online Dormitory Form */}
          <RegistrationGuideAndForm selectedRoomId={selectedRoomId} />

          {/* Frequently Asked Questions (FAQ) */}
          <FaqSection />

          {/* Maintenance / Support & Emergency Contacts */}
          <SupportAndFeedbackSection />

          {/* About On campus Dormitory */}
          <AboutSection />
        </main>
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

      {/* Interactive Login Modal */}
      <LoginModal
        isOpen={isLoginOpen}
        onClose={() => setIsLoginOpen(false)}
        onLoginSuccess={handleLoginSuccess}
      />
    </div>
  );
}

export default App;
