import { useState, useEffect } from 'react';
import { AuthProvider, useAuth } from './context/AuthContext';
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
import { StudentDashboard } from './components/dashboards/StudentDashboard';
import { ManagerDashboard } from './components/dashboards/ManagerDashboard';
import { GuardDashboard } from './components/dashboards/GuardDashboard';
import { AdminDashboard } from './components/dashboards/AdminDashboard';
import './styles/dormitory.css';

function MainAppContent() {
  const { user, isAuthenticated } = useAuth();
  const [isLoginOpen, setIsLoginOpen] = useState(false);
  const [selectedRoomId, setSelectedRoomId] = useState('room-4');
  const [currentView, setCurrentView] = useState<'home' | 'dashboard'>('home');

  // Khi người dùng đăng nhập thành công, tự động chuyển vào dashboard tương ứng
  useEffect(() => {
    if (isAuthenticated) {
      setCurrentView('dashboard');
    } else {
      setCurrentView('home');
    }
  }, [isAuthenticated]);

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

  // Render Dashboard theo từng Role
  if (currentView === 'dashboard' && isAuthenticated && user) {
    if (user.roles.includes('Admin')) {
      return <AdminDashboard onBackToHome={() => setCurrentView('home')} />;
    }
    if (user.roles.includes('Manager')) {
      return <ManagerDashboard onBackToHome={() => setCurrentView('home')} />;
    }
    if (user.roles.includes('Guard')) {
      return <GuardDashboard onBackToHome={() => setCurrentView('home')} />;
    }
    // Mặc định hoặc là Student
    return <StudentDashboard onBackToHome={() => setCurrentView('home')} />;
  }

  // Render Trang chủ KTX
  return (
    <div className="dormitory-page-wrapper">
      {/* 1. Header / Navigation */}
      <Navbar
        onOpenLogin={() => setIsLoginOpen(true)}
        onNavigate={scrollToSection}
        onGoToDashboard={isAuthenticated ? () => setCurrentView('dashboard') : undefined}
      />

      {/* 2. Hero Banner Carousel with center badge matching sample */}
      <HeroBanner />

      {/* 3. Three Orange Quick Action Cards */}
      <QuickCards onSelectAction={scrollToSection} />

      {/* 4. Dormitory General Information & Status */}
      <DormInfoSection />

      {/* 5. Room Types & Facilities */}
      <RoomTypesSection onSelectRoom={handleSelectRoom} />

      {/* 6. Registration Process & Online Dormitory Form */}
      <RegistrationGuideAndForm selectedRoomId={selectedRoomId} />

      {/* 7. Frequently Asked Questions (FAQ) */}
      <FaqSection />

      {/* 8. Maintenance / Support & Emergency Contacts */}
      <SupportAndFeedbackSection />

      {/* 9. About On campus Dormitory */}
      <AboutSection />

      {/* 10. Footer */}
      <Footer onNavigate={scrollToSection} />

      {/* 11. Interactive Login Modal for Students & Staff */}
      <LoginModal
        isOpen={isLoginOpen}
        onClose={() => setIsLoginOpen(false)}
      />
    </div>
  );
}

export function App() {
  return (
    <AuthProvider>
      <MainAppContent />
    </AuthProvider>
  );
}

export default App;
