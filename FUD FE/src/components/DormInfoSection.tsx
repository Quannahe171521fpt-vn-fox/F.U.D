import React, { useState } from 'react';
import { BuildingIcon, ShieldIcon, WifiIcon, BedIcon } from './Icons';
import { DORM_BUILDINGS } from '../data/dormData';
import { useAuth } from '../context/AuthContext';

export const DormInfoSection: React.FC = () => {
  const { user, isAuthenticated } = useAuth();
  const [showAllBuildings, setShowAllBuildings] = useState(false);

  const isStudent = isAuthenticated && user && user.roles.includes('Student');
  const studentGender = user?.gender; // 'Male' | 'Female'

  // Lọc theo giới tính sinh viên
  const filteredBuildings = DORM_BUILDINGS.filter((dom) => {
    if (!isStudent || showAllBuildings) return true;
    if (studentGender === 'Male') {
      return dom.gender.includes('Nam');
    }
    if (studentGender === 'Female') {
      return dom.gender.includes('Nữ');
    }
    return true;
  });

  return (
    <section className="info-section-container" id="dorm-info">
      {/* Section Header */}
      <div className="section-header">
        <span className="section-kicker">Tổng quan ký túc xá</span>
        <h2 className="section-title">Ngôi nhà thứ hai của sinh viên FPT</h2>
        <p className="section-description">
          Ký túc xá Đại học FPT được thiết kế hiện đại, quy hoạch đồng bộ trong khuôn viên xanh
          với hồ sen sinh thái, đem đến môi trường học tập, sinh hoạt tiện nghi và an toàn bậc nhất.
        </p>
      </div>

      {/* Highlights Metric Grid */}
      <div className="metrics-grid">
        <div className="metric-card">
          <div className="metric-icon-box">
            <BuildingIcon size={24} />
          </div>
          <div className="metric-number">10+</div>
          <div className="metric-label">Tòa nhà KTX hiện đại (Dom A - Dom K)</div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-box">
            <BedIcon size={24} />
          </div>
          <div className="metric-number">12.000+</div>
          <div className="metric-label">Chỗ ở tiện nghi cho sinh viên</div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-box">
            <ShieldIcon size={24} />
          </div>
          <div className="metric-number">24/7</div>
          <div className="metric-label">Hệ thống an ninh, bảo vệ & y tế thường trực</div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-box">
            <WifiIcon size={24} />
          </div>
          <div className="metric-number">100%</div>
          <div className="metric-label">Phủ sóng Wifi tốc độ cao & điều hòa nhiệt độ</div>
        </div>
      </div>

      {/* Features Showcase */}
      <div className="features-grid">
        <div className="feature-item">
          <div className="feature-badge">01</div>
          <h3 className="feature-title">Khuôn viên sống xanh & trong lành</h3>
          <p className="feature-text">
            Bao quanh bởi hồ sen sinh thái và các rặng thông xanh mát, tạo bầu không khí thoáng đãng,
            giúp sinh viên giải tỏa căng thẳng sau những giờ học tập và làm đồ án căng thẳng.
          </p>
        </div>

        <div className="feature-item">
          <div className="feature-badge">02</div>
          <h3 className="feature-title">Hệ thống tiện ích nội khu all-in-one</h3>
          <p className="feature-text">
            Tích hợp đầy đủ căn tin ẩm thực đa dạng, siêu thị tiện lợi mở cửa 24/7, phòng giặt sấy tự động,
            sân bóng đá cỏ nhân tạo, sân bóng rổ và các khu tự học mở.
          </p>
        </div>

        <div className="feature-item">
          <div className="feature-badge">03</div>
          <h3 className="feature-title">An ninh & Kiểm soát thông minh</h3>
          <p className="feature-text">
            Cửa ra vào tích hợp thẻ từ sinh viên và nhận diện khuôn mặt, hệ thống camera hành lang 24/7,
            đội ngũ quản lý tòa nhà và bảo vệ luôn túc trực hỗ trợ sinh viên bất cứ lúc nào.
          </p>
        </div>

        <div className="feature-item">
          <div className="feature-badge">04</div>
          <h3 className="feature-title">Cộng đồng năng động & Gắn kết</h3>
          <p className="feature-text">
            Thường xuyên diễn ra các hoạt động giao lưu văn hóa, giải đấu thể thao, câu lạc bộ sở thích
            và các sự kiện chào đón tân sinh viên, gắn kết cộng đồng cư dân ký túc xá.
          </p>
        </div>
      </div>

      {/* Dormitory Buildings Status Table / Form */}
      <div className="buildings-box">
        <div className="buildings-header">
          <div>
            <h3 className="buildings-title">Danh sách các Tòa nhà Ký túc xá (Dom)</h3>
            {isStudent && (
              <div style={{ marginTop: '4px', fontSize: '0.85rem', color: '#16a34a' }}>
                ✓ Đang tự động lọc theo giới tính sinh viên: <strong>{studentGender === 'Male' ? 'Nam' : 'Nữ'}</strong> (Chỉ hiển thị các tòa phù hợp)
              </div>
            )}
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            {isStudent && (
              <button
                type="button"
                onClick={() => setShowAllBuildings(!showAllBuildings)}
                style={{
                  padding: '4px 10px',
                  borderRadius: '6px',
                  border: '1px solid #cbd5e1',
                  backgroundColor: showAllBuildings ? '#e2e8f0' : '#fff',
                  fontSize: '0.8rem',
                  cursor: 'pointer',
                }}
              >
                {showAllBuildings ? 'Khóa lại theo giới tính' : 'Xem tất cả tòa'}
              </button>
            )}
            <span className="buildings-status-tag">Học kỳ Fall 2026</span>
          </div>
        </div>

        <div className="table-responsive">
          <table className="dorm-table">
            <thead>
              <tr>
                <th>Tòa nhà</th>
                <th>Đối tượng lưu trú</th>
                <th>Quy mô tầng</th>
                <th>Số lượng phòng</th>
                <th>Tình trạng phòng</th>
              </tr>
            </thead>
            <tbody>
              {filteredBuildings.map((dom) => {
                const isGenderMatch = !isStudent || (studentGender === 'Male' ? dom.gender.includes('Nam') : dom.gender.includes('Nữ'));
                return (
                  <tr key={dom.name} style={{ opacity: isStudent && !isGenderMatch ? 0.45 : 1 }}>
                    <td>
                      <strong>{dom.name}</strong>
                      {isStudent && isGenderMatch && (
                        <span style={{ marginLeft: '6px', fontSize: '0.75rem', color: '#15803d', fontWeight: 600 }}>[Khớp với bạn]</span>
                      )}
                    </td>
                    <td>
                      <span
                        style={{
                          display: 'inline-block',
                          padding: '2px 8px',
                          borderRadius: '12px',
                          fontSize: '0.78rem',
                          fontWeight: 600,
                          backgroundColor: dom.gender.includes('Nam') ? '#eff6ff' : '#fdf2f8',
                          color: dom.gender.includes('Nam') ? '#1d4ed8' : '#be185d',
                        }}
                      >
                        {dom.gender}
                      </span>
                    </td>
                    <td>{dom.floors} tầng</td>
                    <td>{dom.rooms} phòng</td>
                    <td>
                      <span className="status-pill available">Còn phòng nhận đăng ký</span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  );
};
