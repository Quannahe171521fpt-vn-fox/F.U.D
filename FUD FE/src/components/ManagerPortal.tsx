import React, { useState } from 'react';
import { EquipmentManagement } from './EquipmentManagement';
import { NewsManagement } from './NewsManagement';
import type { NewsItem, EquipmentRecord, EquipmentCatalogItem } from '../types/management';

interface ManagerPortalProps {
  newsList: NewsItem[];
  equipmentList: EquipmentRecord[];
  catalogList: EquipmentCatalogItem[];
  onSaveNews: (news: NewsItem) => void;
  onDeleteNews: (id: number) => void;
  onUnpublishNews: (id: number) => void;
  onSaveEquipment: (record: EquipmentRecord) => void;
  onDeleteEquipment: (id: number) => void;
  onSaveCatalogItem: (item: EquipmentCatalogItem) => void;
  onDeleteCatalogItem: (id: number) => void;
}

export const ManagerPortal: React.FC<ManagerPortalProps> = ({
  newsList,
  equipmentList,
  catalogList,
  onSaveNews,
  onDeleteNews,
  onUnpublishNews,
  onSaveEquipment,
  onDeleteEquipment,
  onSaveCatalogItem,
  onDeleteCatalogItem,
}) => {
  // Sidebar navigation tabs
  const [activeTab, setActiveTab] = useState<'equipment' | 'news'>('equipment');

  // Counts
  const brokenCount = equipmentList.filter((e) => e.condition === 'Broken' || e.condition === 'Under repair').length;
  const draftNewsCount = newsList.filter((n) => n.status === 'Draft').length;

  return (
    <div className="manager-dashboard-layout">
      {/* 1. Dedicated Admin Sidebar (Without logout or student info links) */}
      <aside className="manager-sidebar">
        <div className="sidebar-brand-block">
          <div className="sidebar-brand-badge">Ban Quản lý</div>
          <h2 className="sidebar-brand-title">Hệ thống KTX FUD</h2>
          <span className="sidebar-subtext">Cổng nghiệp vụ cán bộ</span>
        </div>

        <nav className="sidebar-nav-list">
          <div className="sidebar-nav-section-label">QUẢN TRỊ NGHIỆP VỤ</div>

          <button
            type="button"
            className={`sidebar-nav-item ${activeTab === 'equipment' ? 'active' : ''}`}
            onClick={() => setActiveTab('equipment')}
          >
            <span className="nav-item-indicator"></span>
            <div className="nav-item-content">
              <span className="nav-item-name">Quản lý Thiết bị</span>
              <span className="nav-item-counter">{equipmentList.length} tài sản</span>
            </div>
            {brokenCount > 0 && (
              <span className="sidebar-alert-pill" title={`${brokenCount} thiết bị cần sửa chữa hoặc hỏng`}>
                {brokenCount} lỗi
              </span>
            )}
          </button>

          <button
            type="button"
            className={`sidebar-nav-item ${activeTab === 'news' ? 'active' : ''}`}
            onClick={() => setActiveTab('news')}
          >
            <span className="nav-item-indicator"></span>
            <div className="nav-item-content">
              <span className="nav-item-name">Quản lý Tin tức</span>
              <span className="nav-item-counter">{newsList.length} bản tin</span>
            </div>
            {draftNewsCount > 0 && (
              <span className="sidebar-draft-pill" title={`${draftNewsCount} bản tin nháp`}>
                {draftNewsCount} nháp
              </span>
            )}
          </button>
        </nav>
      </aside>

      {/* 2. Main Content Area */}
      <main className="manager-main-content">
        {activeTab === 'equipment' ? (
          <EquipmentManagement
            equipmentList={equipmentList}
            catalogList={catalogList}
            onSaveEquipment={onSaveEquipment}
            onDeleteEquipment={onDeleteEquipment}
            onSaveCatalogItem={onSaveCatalogItem}
            onDeleteCatalogItem={onDeleteCatalogItem}
          />
        ) : (
          <NewsManagement
            newsList={newsList}
            onSaveNews={onSaveNews}
            onDeleteNews={onDeleteNews}
            onUnpublishNews={onUnpublishNews}
          />
        )}
      </main>
    </div>
  );
};
