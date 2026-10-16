import { useState } from 'react';
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
import './styles/dormitory.css';

export function App() {
  const [isLoginOpen, setIsLoginOpen] = useState(false);
  const [selectedRoomId, setSelectedRoomId] = useState('room-4');

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

  return (
    <div className="dormitory-page-wrapper">
      {/* 1. Header / Navigation */}
      <Navbar
        onOpenLogin={() => setIsLoginOpen(true)}
        onNavigate={scrollToSection}
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

export default App;
