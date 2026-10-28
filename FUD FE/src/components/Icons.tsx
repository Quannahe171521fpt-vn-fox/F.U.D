import React from 'react';

export const Logo: React.FC<{ className?: string }> = ({ className = 'h-10' }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }} className={className}>
    {/* Biểu tượng Ếch Vàng KTX FUD với đôi mắt to tròn, rõ nét */}
    <svg width="44" height="44" viewBox="0 0 100 100" fill="none" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id="frogGoldGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" stopColor="#F59E0B" />
          <stop offset="100%" stopColor="#D97706" />
        </linearGradient>
        <linearGradient id="frogBellyGrad" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="#FFFBEB" />
          <stop offset="100%" stopColor="#FDE68A" />
        </linearGradient>
      </defs>

      {/* Thân & đầu chính hình ếch vàng */}
      <ellipse cx="50" cy="62" rx="36" ry="28" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="2" />

      {/* Bụng ếch vàng kem sáng */}
      <ellipse cx="50" cy="68" rx="22" ry="17" fill="url(#frogBellyGrad)" />

      {/* Chân sau trái & phải */}
      <ellipse cx="18" cy="74" rx="11" ry="8" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="1.5" />
      <ellipse cx="82" cy="74" rx="11" ry="8" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="1.5" />

      {/* Hai ụ mắt lồi đặc trưng của ếch */}
      <circle cx="31" cy="36" r="17" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="2.5" />
      <circle cx="69" cy="36" r="17" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="2.5" />

      {/* Lòng trắng mắt to tròn, nổi bật */}
      <circle cx="31" cy="36" r="12" fill="#FFFFFF" stroke="#334155" strokeWidth="1.5" />
      <circle cx="69" cy="36" r="12" fill="#FFFFFF" stroke="#334155" strokeWidth="1.5" />

      {/* Con ngươi đen to rõ */}
      <circle cx="33" cy="36" r="6.5" fill="#0F172A" />
      <circle cx="71" cy="36" r="6.5" fill="#0F172A" />

      {/* Ánh sáng trắng long lanh trong mắt */}
      <circle cx="31" cy="33.5" r="2.8" fill="#FFFFFF" />
      <circle cx="69" cy="33.5" r="2.8" fill="#FFFFFF" />
      <circle cx="35" cy="38" r="1.2" fill="#FFFFFF" />
      <circle cx="73" cy="38" r="1.2" fill="#FFFFFF" />

      {/* Mũ cử nhân trên đầu ếch */}
      <polygon points="50,11 68,17 50,23 32,17" fill="#1E293B" />
      <polygon points="42,20 58,20 56,24 44,24" fill="#0F172A" />
      <line x1="64" y1="18" x2="68" y2="28" stroke="#DC2626" strokeWidth="2" strokeLinecap="round" />
      <circle cx="68" cy="29" r="2" fill="#DC2626" />

      {/* Hai má hồng cam nhẹ */}
      <ellipse cx="24" cy="60" rx="5" ry="3" fill="#FB7185" opacity="0.7" />
      <ellipse cx="76" cy="60" rx="5" ry="3" fill="#FB7185" opacity="0.7" />

      {/* Nụ cười vui tươi */}
      <path d="M 37 57 Q 50 67 63 57" stroke="#92400E" strokeWidth="2.8" strokeLinecap="round" fill="none" />

      {/* Hai bàn chân trước */}
      <circle cx="41" cy="88" r="5" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="1.5" />
      <circle cx="59" cy="88" r="5" fill="url(#frogGoldGrad)" stroke="#B45309" strokeWidth="1.5" />
    </svg>
    <div style={{ display: 'flex', flexDirection: 'column', lineHeight: 1.15 }}>
      <span style={{ fontSize: '20px', fontWeight: 800, color: '#D97706', letterSpacing: '0.8px', fontFamily: 'system-ui, sans-serif' }}>
        FUD
      </span>
      <span style={{ fontSize: '12px', fontWeight: 600, color: '#475569', fontFamily: 'system-ui, sans-serif' }}>
        Ký túc xá FPT
      </span>
    </div>
  </div>
);

export const ChevronLeftIcon: React.FC<{ size?: number }> = ({ size = 24 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="15 18 9 12 15 6"></polyline>
  </svg>
);

export const ChevronRightIcon: React.FC<{ size?: number }> = ({ size = 24 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="9 18 15 12 9 6"></polyline>
  </svg>
);

export const ArrowRightIcon: React.FC<{ size?: number }> = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="5" y1="12" x2="19" y2="12"></line>
    <polyline points="12 5 19 12 12 19"></polyline>
  </svg>
);

export const CheckIcon: React.FC<{ size?: number; color?: string }> = ({ size = 18, color = '#E26726' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="20 6 9 17 4 12"></polyline>
  </svg>
);

export const BedIcon: React.FC<{ size?: number }> = ({ size = 20 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M2 4v16" />
    <path d="M2 8h18a2 2 0 0 1 2 2v10" />
    <path d="M2 17h20" />
    <path d="M6 8v9" />
  </svg>
);

export const BuildingIcon: React.FC<{ size?: number }> = ({ size = 20 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="4" y="2" width="16" height="20" rx="2" ry="2" />
    <line x1="9" y1="22" x2="9" y2="22.01" />
    <line x1="15" y1="22" x2="15" y2="22.01" />
    <line x1="8" y1="6" x2="8" y2="6.01" />
    <line x1="12" y1="6" x2="12" y2="6.01" />
    <line x1="16" y1="6" x2="16" y2="6.01" />
    <line x1="8" y1="10" x2="8" y2="10.01" />
    <line x1="12" y1="10" x2="12" y2="10.01" />
    <line x1="16" y1="10" x2="16" y2="10.01" />
    <line x1="8" y1="14" x2="8" y2="14.01" />
    <line x1="12" y1="14" x2="12" y2="14.01" />
    <line x1="16" y1="14" x2="16" y2="14.01" />
  </svg>
);

export const ShieldIcon: React.FC<{ size?: number }> = ({ size = 20 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
  </svg>
);

export const WifiIcon: React.FC<{ size?: number }> = ({ size = 20 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M5 12.55a11 11 0 0 1 14.08 0" />
    <path d="M1.42 9a16 16 0 0 1 21.16 0" />
    <path d="M8.53 16.11a6 6 0 0 1 6.95 0" />
    <line x1="12" y1="20" x2="12.01" y2="20" />
  </svg>
);

export const PhoneIcon: React.FC<{ size?: number }> = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z" />
  </svg>
);

export const MailIcon: React.FC<{ size?: number }> = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
    <polyline points="22,6 12,13 2,6" />
  </svg>
);

export const ClockIcon: React.FC<{ size?: number }> = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </svg>
);

export const CloseIcon: React.FC<{ size?: number }> = ({ size = 20 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="18" y1="6" x2="6" y2="18" />
    <line x1="6" y1="6" x2="18" y2="18" />
  </svg>
);

export const WrenchIcon: React.FC<{ size?: number }> = ({ size = 18 }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" />
  </svg>
);

export const UserIcon: React.FC<{ size?: number; color?: string }> = ({ size = 18, color = 'currentColor' }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
    <circle cx="12" cy="7" r="4" />
  </svg>
);
