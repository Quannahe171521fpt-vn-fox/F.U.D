import React from 'react';
import { BuildingIcon, ShieldIcon, WifiIcon, BedIcon } from '../../components/Icons';

export const DormInfoSection: React.FC = () => {
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
          <div className="metric-label">Hệ thống an ninh, bảo vệ &amp; y tế thường trực</div>
        </div>

        <div className="metric-card">
          <div className="metric-icon-box">
            <WifiIcon size={24} />
          </div>
          <div className="metric-number">100%</div>
          <div className="metric-label">Phủ sóng Wifi tốc độ cao &amp; điều hòa nhiệt độ</div>
        </div>
      </div>

      {/* Features Showcase */}
      <div className="features-grid">
        <div className="feature-item">
          <div className="feature-badge">01</div>
          <h3 className="feature-title">Khuôn viên sống xanh &amp; trong lành</h3>
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
          <h3 className="feature-title">An ninh &amp; Kiểm soát thông minh</h3>
          <p className="feature-text">
            Cửa ra vào tích hợp thẻ từ sinh viên và nhận diện khuôn mặt, hệ thống camera hành lang 24/7,
            đội ngũ quản lý tòa nhà và bảo vệ luôn túc trực hỗ trợ sinh viên bất cứ lúc nào.
          </p>
        </div>

        <div className="feature-item">
          <div className="feature-badge">04</div>
          <h3 className="feature-title">Cộng đồng năng động &amp; Gắn kết</h3>
          <p className="feature-text">
            Thường xuyên diễn ra các hoạt động giao lưu văn hóa, giải đấu thể thao, câu lạc bộ sở thích
            và các sự kiện chào đón tân sinh viên, gắn kết cộng đồng cư dân ký túc xá.
          </p>
        </div>
      </div>
    </section>
  );
};
