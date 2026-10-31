import React from 'react';
import { HeroBanner } from './HeroBanner';
import { QuickCards } from './QuickCards';
import { DormInfoSection } from './DormInfoSection';
import { RoomTypesSection } from './RoomTypesSection';
import { RegistrationGuideAndForm } from './RegistrationGuideAndForm';
import { FaqSection } from './FaqSection';
import { SupportAndFeedbackSection } from './SupportAndFeedbackSection';
import { AboutSection } from './AboutSection';

interface HomePageProps {
  selectedRoomId: string;
  onSelectRoom?: (roomId: string) => void;
  onNavigateToSection: (sectionId: string) => void;
  onOpenAnnouncements: () => void;
}

export const HomePage: React.FC<HomePageProps> = ({
  selectedRoomId,
  onNavigateToSection,
  onOpenAnnouncements,
}) => {
  return (
    <main>
      {/* Hero Banner Carousel */}
      <HeroBanner />

      {/* Quick Action Cards */}
      <QuickCards
        onSelectAction={(sec) => {
          if (
            sec === 'support-section' ||
            sec === 'registration-guide' ||
            sec === 'faq-section' ||
            sec === 'dorm-info'
          ) {
            onNavigateToSection(sec);
          } else {
            onOpenAnnouncements();
          }
        }}
      />

      {/* Dormitory General Information & Status */}
      <DormInfoSection />

      {/* Room Types & Facilities */}
      <RoomTypesSection />

      {/* Registration Process & Online Dormitory Form */}
      <RegistrationGuideAndForm selectedRoomId={selectedRoomId} />

      {/* Frequently Asked Questions (FAQ) */}
      <FaqSection />

      {/* Maintenance / Support & Emergency Contacts */}
      <SupportAndFeedbackSection />

      {/* About On campus Dormitory */}
      <AboutSection />
    </main>
  );
};

