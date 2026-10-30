import React, { useState, useEffect, useCallback } from 'react';
import { EquipmentManagement } from './EquipmentManagement';
import { NewsManagement } from './NewsManagement';
import type { NewsItem, EquipmentRecord, EquipmentCatalogItem } from '../../types/management';
import { equipmentApi, newsApi } from '../../services/api';
import { useAuth } from '../../contexts/AuthContext';


export const ManagerPortal: React.FC = () => {
  const { currentUser } = useAuth();

  const roleBadge = currentUser?.primaryRole === 'Admin'
    ? 'Quản trị viên (Admin)'
    : currentUser?.primaryRole === 'Guard'
      ? 'Đội Bảo vệ (Guard)'
      : 'Ban Quản lý (Manager)';

  const subText = currentUser?.fullName
    ? `${currentUser.fullName} (${currentUser.code})`
    : 'Cổng nghiệp vụ cán bộ (Database)';

  // Sidebar navigation tabs
  const [activeTab, setActiveTab] = useState<'equipment' | 'news'>(() => {
    return (localStorage.getItem('fud_managerTab') as 'equipment' | 'news') || 'equipment';
  });

  useEffect(() => {
    localStorage.setItem('fud_managerTab', activeTab);
  }, [activeTab]);

  // Backend state directly loaded from Database
  const [equipmentList, setEquipmentList] = useState<EquipmentRecord[]>([]);
  const [catalogList, setCatalogList] = useState<EquipmentCatalogItem[]>([]);
  const [locationList, setLocationList] = useState<string[]>([]);
  const [newsList, setNewsList] = useState<NewsItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Fetch data from database via backend
  const loadData = useCallback(async () => {
    setIsLoading(true);
    try {
      // 1. Load equipments from DB
      try {
        const eqData = await equipmentApi.getAll({ pageSize: 100 });
        if (eqData && eqData.items) {
          const mappedEquip: EquipmentRecord[] = eqData.items.map((e: any) => ({
            id: e.id,
            assetCode: e.assetCode,
            name: e.name,
            category: e.category,
            location: e.location || `Phòng ${e.roomId}`,
            condition: e.condition as 'Good' | 'Under repair' | 'Broken',
            hasOpenTicket: e.hasOpenTicket,
          }));
          setEquipmentList(mappedEquip);
        }
      } catch (err) {
        console.error('Lỗi khi tải danh sách thiết bị từ database:', err);
      }

      // 2. Load catalogs from DB
      try {
        const cats = await equipmentApi.getCatalogs();
        if (cats) {
          setCatalogList(
            cats.map((c: any) => ({
              id: c.id,
              category: c.category,
              name: c.name,
              inUseCount: c.inUseCount || 0,
            }))
          );
        }
      } catch (err) {
        console.error('Lỗi khi tải danh mục thiết bị từ database:', err);
      }

      // 3. Load locations (Rooms/Beds) from DB
      try {
        const locs = await equipmentApi.getLocations();
        if (locs && locs.length > 0) {
          setLocationList(locs.map((l: any) => l.label));
        }
      } catch (err) {
        console.error('Lỗi khi tải danh sách vị trí từ database:', err);
      }

      // 4. Load news from DB
      try {
        const nData = await newsApi.getAll({ pageSize: 50 });
        if (nData && nData.items) {
          const mappedNews: NewsItem[] = nData.items.map((n: any) => ({
            id: n.id,
            title: n.title,
            content: n.content,
            status: n.status === 'Published' ? 'Published' : 'Draft',
            publishedDate: n.publishedTime
              ? new Date(n.publishedTime).toLocaleDateString('vi-VN')
              : null,
            createdDate: n.createdAt
              ? new Date(n.createdAt).toLocaleDateString('vi-VN')
              : (n.publishedTime ? new Date(n.publishedTime).toLocaleDateString('vi-VN') : null),
            hiddenDate: n.hiddenAt
              ? new Date(n.hiddenAt).toLocaleDateString('vi-VN')
              : null,
            attachments: n.attachments?.map((a: any) => ({
              name: a.fileName || 'Tài liệu đính kèm',
              size: a.fileSize || '1.0 MB',
              type: a.fileType || 'pdf',
            })) || [],
          }));
          setNewsList(mappedNews);
        }
      } catch (err) {
        console.error('Lỗi khi tải danh sách tin tức từ database:', err);
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Handlers for News - Direct Database interaction
  const handleSaveNews = async (newsItem: NewsItem) => {
    try {
      if (newsItem.id && newsItem.id < 1000000000) {
        await newsApi.update(newsItem.id, {
          title: newsItem.title,
          content: newsItem.content,
          action: newsItem.status,
        });
      } else {
        await newsApi.create({
          title: newsItem.title,
          content: newsItem.content,
          action: newsItem.status,
        });
      }
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi lưu tin tức vào cơ sở dữ liệu');
      console.error(err);
    }
  };

  const handlePublishNews = async (id: number) => {
    try {
      await newsApi.publish(id);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi xuất bản tin tức');
      console.error(err);
    }
  };

  const handleUnpublishNews = async (id: number) => {
    try {
      await newsApi.unpublish(id);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi gỡ xuất bản');
      console.error(err);
    }
  };

  // Handlers for Equipment - Direct Database interaction
  const handleSaveEquipment = async (record: EquipmentRecord) => {
    try {
      const matchRoom = record.location.match(/(\d+)/);
      const roomId = matchRoom ? parseInt(matchRoom[1], 10) : 1;
      const catalog = catalogList.find((c) => c.name === record.name);
      const catalogId = catalog ? catalog.id : (catalogList[0]?.id || 1);

      if (record.id && record.id < 1000000000) {
        await equipmentApi.update(record.id, {
          catalogId,
          roomId,
          condition: record.condition,
        });
      } else {
        await equipmentApi.create({
          assetCode: record.assetCode,
          catalogId,
          roomId,
          condition: record.condition,
        });
      }
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi lưu thiết bị vào cơ sở dữ liệu');
      console.error(err);
    }
  };

  const handleDeleteEquipment = async (id: number) => {
    try {
      await equipmentApi.liquidate(id);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi thanh lý thiết bị');
      console.error(err);
    }
  };

  const handleSaveCatalogItem = async (item: EquipmentCatalogItem) => {
    try {
      await equipmentApi.createCatalog({
        category: item.category,
        name: item.name,
      });
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi thêm danh mục vào cơ sở dữ liệu');
      console.error(err);
    }
  };

  const handleDeleteCatalogItem = async (id: number) => {
    try {
      await equipmentApi.deleteCatalog(id);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Lỗi khi xóa danh mục khỏi cơ sở dữ liệu');
      console.error(err);
    }
  };

  // Counts
  const brokenCount = equipmentList.filter(
    (e) => e.condition === 'Broken' || e.condition === 'Under repair'
  ).length;
  const draftNewsCount = newsList.filter((n) => n.status === 'Draft').length;

  return (
    <div className="manager-dashboard-layout">
      {/* 1. Dedicated Admin Sidebar */}
      <aside className="manager-sidebar">
        <div className="sidebar-brand-block">
          <div className="sidebar-brand-badge">{roleBadge}</div>
          <h2 className="sidebar-brand-title">Hệ thống KTX FUD</h2>
          <span className="sidebar-subtext">{subText}</span>
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
              <span
                className="sidebar-alert-pill"
                title={`${brokenCount} thiết bị cần sửa chữa hoặc hỏng`}
              >
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
              <span
                className="sidebar-draft-pill"
                title={`${draftNewsCount} bản tin nháp`}
              >
                {draftNewsCount} nháp
              </span>
            )}
          </button>
        </nav>
      </aside>

      {/* 2. Main Content Area */}
      <main className="manager-main-content">
        {isLoading ? (
          <div style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>
            <p>Đang kết nối và nạp dữ liệu từ Cơ sở dữ liệu...</p>
          </div>
        ) : activeTab === 'equipment' ? (
          <EquipmentManagement
            equipmentList={equipmentList}
            catalogList={catalogList}
            locationList={locationList}
            onSaveEquipment={handleSaveEquipment}
            onDeleteEquipment={handleDeleteEquipment}
            onSaveCatalogItem={handleSaveCatalogItem}
            onDeleteCatalogItem={handleDeleteCatalogItem}
          />
        ) : (
          <NewsManagement
            newsList={newsList}
            onSaveNews={handleSaveNews}
            onPublishNews={handlePublishNews}
            onUnpublishNews={handleUnpublishNews}
          />
        )}
      </main>
    </div>
  );
};
