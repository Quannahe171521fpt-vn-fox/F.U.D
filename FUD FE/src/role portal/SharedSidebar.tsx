import React, { useState } from 'react';

export interface SidebarItem {
  id: string;
  name: string;
  counter?: string;
  pill?: {
    count: number | string;
    label: string;
    color?: string;
  };
}

export interface SidebarSection {
  label: string;
  items: SidebarItem[];
}

interface SharedSidebarProps {
  roleBadge: string;
  roleBadgeColor?: { bg: string; text: string };
  title: string;
  subText: string;
  sections: SidebarSection[];
  activeId: string;
  onSelect: (id: string) => void;
  onLogout?: () => void;
}

export const SharedSidebar: React.FC<SharedSidebarProps> = ({
  roleBadge,
  roleBadgeColor,
  title,
  subText,
  sections,
  activeId,
  onSelect,
}) => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <aside className="manager-sidebar">
      {/* Brand Header */}
      <div className="sidebar-brand-block">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div>
            <div
              className="sidebar-brand-badge"
              style={
                roleBadgeColor
                  ? { backgroundColor: roleBadgeColor.bg, color: roleBadgeColor.text }
                  : undefined
              }
            >
              {roleBadge}
            </div>
            <h2 className="sidebar-brand-title">{title}</h2>
            <span className="sidebar-subtext">{subText}</span>
          </div>

          {/* Nút 3 gạch trên màn hình điện thoại / mobile */}
          <button
            type="button"
            className="sidebar-mobile-toggle"
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            aria-label="Toggle navigation menu"
          >
            <span className={`bar ${mobileMenuOpen ? 'open' : ''}`}></span>
            <span className={`bar ${mobileMenuOpen ? 'open' : ''}`}></span>
            <span className={`bar ${mobileMenuOpen ? 'open' : ''}`}></span>
          </button>
        </div>
      </div>

      {/* Nav List */}
      <nav className={`sidebar-nav-list ${mobileMenuOpen ? 'mobile-visible' : ''}`}>
        {sections.map((sec, secIdx) => (
          <div key={secIdx} className="sidebar-section-group">
            {sec.label && <div className="sidebar-nav-section-label">{sec.label}</div>}
            <div className="sidebar-section-items">
              {sec.items.map((item) => {
                const isActive = activeId === item.id;
                return (
                  <button
                    key={item.id}
                    type="button"
                    className={`sidebar-nav-item ${isActive ? 'active' : ''}`}
                    onClick={() => {
                      onSelect(item.id);
                      setMobileMenuOpen(false);
                    }}
                  >
                    <span className="nav-item-indicator"></span>
                    <div className="nav-item-content">
                      <span className="nav-item-name">{item.name}</span>
                      {item.counter && (
                        <span className="nav-item-counter">{item.counter}</span>
                      )}
                    </div>
                    {item.pill && (
                      <span
                        className="sidebar-alert-pill"
                        style={item.pill.color ? { backgroundColor: item.pill.color } : undefined}
                        title={item.pill.label}
                      >
                        {item.pill.count} {item.pill.label}
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </nav>
    </aside>
  );
};
