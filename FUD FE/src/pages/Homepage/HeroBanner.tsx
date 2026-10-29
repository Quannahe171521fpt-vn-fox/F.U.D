import React, { useState, useEffect } from 'react';
import { ChevronLeftIcon, ChevronRightIcon } from '../../components/Icons';

interface Slide {
  badgeTitle: string;
  subtitle: string;
  themeGradient: string;
}

const SLIDES: Slide[] = [
  {
    badgeTitle: 'KTX ĐẠI HỌC FPT',
    subtitle: 'Không gian sống xanh',
    themeGradient: 'linear-gradient(180deg, rgba(20, 48, 35, 0.65) 0%, rgba(13, 31, 45, 0.75) 100%)',
  },
  {
    badgeTitle: 'TIỆN NGHI HIỆN ĐẠI',
    subtitle: 'An toàn 24/7 & Đầy đủ tiện ích sinh hoạt',
    themeGradient: 'linear-gradient(180deg, rgba(31, 41, 55, 0.65) 0%, rgba(15, 23, 42, 0.8) 100%)',
  },
  {
    badgeTitle: 'CỘNG ĐỒNG GẮN KẾT',
    subtitle: 'Trải nghiệm thanh xuân trọn vẹn tại Hola Park',
    themeGradient: 'linear-gradient(180deg, rgba(67, 34, 18, 0.65) 0%, rgba(20, 32, 48, 0.8) 100%)',
  },
];

export const HeroBanner: React.FC = () => {
  const [currentSlide, setCurrentSlide] = useState(0);

  const prevSlide = () => {
    setCurrentSlide((prev) => (prev === 0 ? SLIDES.length - 1 : prev - 1));
  };

  const nextSlide = () => {
    setCurrentSlide((prev) => (prev === SLIDES.length - 1 ? 0 : prev + 1));
  };

  // Optional subtle auto-play with pause on hover
  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentSlide((prev) => (prev === SLIDES.length - 1 ? 0 : prev + 1));
    }, 6000);
    return () => clearInterval(timer);
  }, []);

  const slide = SLIDES[currentSlide];

  return (
    <section className="hero-banner-section" id="hero">
      {/* Background container with layered campus & lake scenic styling without requiring image files */}
      <div className="hero-scenery-backdrop" style={{ background: slide.themeGradient }}>
        {/* Artistic SVG stylized lake reflection and lush campus skyline representation */}
        <div className="hero-campus-art" aria-hidden="true">
          <svg className="hero-svg-landscape" viewBox="0 0 1440 450" preserveAspectRatio="none" fill="none" xmlns="http://www.w3.org/2000/svg">
            <defs>
              <linearGradient id="skyGrad" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stopColor="#87CEEB" stopOpacity="0.45" />
                <stop offset="50%" stopColor="#B0E0E6" stopOpacity="0.35" />
                <stop offset="100%" stopColor="#4A7B59" stopOpacity="0.2" />
              </linearGradient>
              <linearGradient id="lakeGrad" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stopColor="#1E3A5F" stopOpacity="0.8" />
                <stop offset="50%" stopColor="#132B45" stopOpacity="0.9" />
                <stop offset="100%" stopColor="#0B1C2E" stopOpacity="0.95" />
              </linearGradient>
              <linearGradient id="dormGrad" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stopColor="#D96528" stopOpacity="0.75" />
                <stop offset="100%" stopColor="#8C3A0F" stopOpacity="0.85" />
              </linearGradient>
            </defs>

            {/* Distant Trees & Dorm Buildings skyline */}
            <path d="M0 220 L70 215 L140 225 L210 218 L280 222 L360 214 L420 220 L510 212 L600 225 L720 210 L840 222 L940 215 L1060 220 L1180 212 L1320 225 L1440 218 L1440 450 L0 450 Z" fill="#1C3D2B" opacity="0.4" />

            {/* Midground Dormitory Building Blocks (Dom A/B/C/D) */}
            <rect x="180" y="80" width="160" height="150" rx="3" fill="url(#dormGrad)" opacity="0.65" />
            <rect x="200" y="95" width="20" height="25" fill="#FFEAA7" opacity="0.3" />
            <rect x="235" y="95" width="20" height="25" fill="#FFEAA7" opacity="0.3" />
            <rect x="270" y="95" width="20" height="25" fill="#FFEAA7" opacity="0.3" />
            <rect x="200" y="135" width="20" height="25" fill="#FFEAA7" opacity="0.3" />
            <rect x="235" y="135" width="20" height="25" fill="#FFEAA7" opacity="0.3" />
            <rect x="270" y="135" width="20" height="25" fill="#FFEAA7" opacity="0.3" />

            {/* Dom Orange Central Building */}
            <rect x="440" y="60" width="200" height="170" rx="4" fill="#D96528" opacity="0.7" />
            <rect x="760" y="70" width="180" height="160" rx="4" fill="#BD5711" opacity="0.6" />

            {/* Pine & Lotus lake park trees */}
            <path d="M40 230 C70 140 100 130 130 230 Z" fill="#2E5A36" opacity="0.85" />
            <path d="M110 230 C135 150 160 145 185 230 Z" fill="#3B7043" opacity="0.85" />
            <path d="M370 230 C395 130 425 125 450 230 Z" fill="#244D2B" opacity="0.9" />
            <path d="M660 230 C690 120 730 115 760 230 Z" fill="#2E5A36" opacity="0.85" />
            <path d="M960 230 C990 135 1025 130 1055 230 Z" fill="#35653E" opacity="0.85" />
            <path d="M1250 230 C1280 145 1315 140 1345 230 Z" fill="#244D2B" opacity="0.9" />

            {/* Lake Surface & Water Lilies reflection */}
            <rect x="0" y="230" width="1440" height="220" fill="url(#lakeGrad)" />
            {/* Water ripples and reflections */}
            <ellipse cx="260" cy="280" rx="90" ry="10" fill="#D96528" opacity="0.2" />
            <ellipse cx="540" cy="275" rx="110" ry="12" fill="#D96528" opacity="0.25" />
            <ellipse cx="850" cy="285" rx="100" ry="11" fill="#D96528" opacity="0.2" />
            <ellipse cx="380" cy="350" rx="180" ry="16" fill="#4A7B59" opacity="0.25" />
            <ellipse cx="1020" cy="360" rx="200" ry="18" fill="#4A7B59" opacity="0.2" />

            {/* Lotus leaf pads */}
            <ellipse cx="120" cy="390" rx="35" ry="12" fill="#2E5A36" opacity="0.75" />
            <ellipse cx="310" cy="410" rx="42" ry="14" fill="#3B7043" opacity="0.8" />
            <ellipse cx="730" cy="400" rx="48" ry="15" fill="#2E5A36" opacity="0.7" />
            <ellipse cx="1190" cy="405" rx="45" ry="14" fill="#35653E" opacity="0.75" />
          </svg>
        </div>

        {/* Ambient Overlay for crystal clear readability */}
        <div className="hero-dark-overlay" />

        {/* Previous Button `<` */}
        <button
          type="button"
          onClick={prevSlide}
          className="carousel-btn carousel-btn-prev"
          aria-label="Previous slide"
        >
          <ChevronLeftIcon size={26} />
        </button>

        {/* Hero Center Content matching the screenshot exactly */}
        <div className="hero-center-box">
          <div className="hero-badge-container">
            <h1 className="hero-badge-title">{slide.badgeTitle}</h1>
          </div>
          <p className="hero-subtitle">{slide.subtitle}</p>
        </div>

        {/* Next Button `>` */}
        <button
          type="button"
          onClick={nextSlide}
          className="carousel-btn carousel-btn-next"
          aria-label="Next slide"
        >
          <ChevronRightIcon size={26} />
        </button>

        {/* Carousel indicators */}
        <div className="carousel-indicators">
          {SLIDES.map((_, idx) => (
            <button
              key={idx}
              type="button"
              className={`carousel-dot ${idx === currentSlide ? 'active' : ''}`}
              onClick={() => setCurrentSlide(idx)}
              aria-label={`Go to slide ${idx + 1}`}
            />
          ))}
        </div>
      </div>
    </section>
  );
};
