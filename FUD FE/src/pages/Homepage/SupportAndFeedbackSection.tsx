import React from 'react';
import { PhoneIcon, MailIcon, ClockIcon, BuildingIcon, WrenchIcon, ShieldIcon } from '../../components/Icons';

export const SupportAndFeedbackSection: React.FC = () => {
  return (
    <section className="support-section-container" id="support-section">
      <div className="section-header">
        <span className="section-kicker">Hỗ trợ & Tiếp nhận sự cố</span>
        <h2 className="section-title">Ban Quản Lý & Kênh Tiếp Nhận Hỗ Trợ</h2>
        <p className="section-description">
          Đội ngũ Ban Quản lý KTX và Tổ kỹ thuật luôn sẵn sàng hỗ trợ, xử lý kịp thời các vấn đề phát sinh
          để đảm bảo điều kiện sinh hoạt tốt nhất cho sinh viên.
        </p>
      </div>

      <div className="support-layout-grid">
        {/* Left Column: Contact info & Office details */}
        <div className="contact-card primary-contact">
          <h3 className="contact-card-title">Văn phòng Ban Quản lý KTX</h3>
          <ul className="contact-list">
            <li>
              <span className="contact-icon-wrap"><BuildingIcon size={18} /></span>
              <span><strong>Địa chỉ:</strong> Tầng 1, Tòa Dom A, KTX ĐH FPT (Khu CNC Hòa Lạc, Hà Nội)</span>
            </li>
            <li>
              <span className="contact-icon-wrap"><ClockIcon size={18} /></span>
              <span><strong>Giờ làm việc:</strong> Thứ Hai – Thứ Bảy: 08:00 – 17:30</span>
            </li>
            <li>
              <span className="contact-icon-wrap"><PhoneIcon size={18} /></span>
              <span><strong>Hotline tiếp nhận:</strong> (024) 7300 1866 / 0988 123 456</span>
            </li>
            <li>
              <span className="contact-icon-wrap"><MailIcon size={18} /></span>
              <span><strong>Email hỗ trợ:</strong> ktx.fpt@fe.edu.vn</span>
            </li>
          </ul>

          <div className="office-notice-box">
            <div className="notice-icon-mini">
              <WrenchIcon size={18} />
            </div>
            <div className="notice-mini-text">
              <strong>Hướng dẫn báo sự cố thiết bị:</strong>
              <p>
                Khi phát hiện sự cố cơ sở vật chất trong phòng (điều hòa, bóng đèn, điện nước, khóa cửa...),
                sinh viên vui lòng báo trực tiếp tại Bàn trực Quản lý tầng 1 của từng Dom hoặc liên hệ hotline Đội Kỹ thuật.
                Tổ kỹ thuật sẽ tiếp nhận và xử lý trong vòng 2 - 4 giờ làm việc.
              </p>
            </div>
          </div>
        </div>

        {/* Right Column: 24/7 Emergency Hotlines */}
        <div className="emergency-hotlines-box">
          <h3 className="contact-card-title">Đường dây nóng hỗ trợ 24/7</h3>
          <div className="hotline-badges-grid">
            <div className="hotline-badge">
              <span className="hotline-service">Phòng Y tế KTX</span>
              <span className="hotline-num">0968 115 115</span>
            </div>
            <div className="hotline-badge">
              <span className="hotline-service">Đội An ninh / Bảo vệ</span>
              <span className="hotline-num">0968 113 113</span>
            </div>
            <div className="hotline-badge">
              <span className="hotline-service">Kỹ thuật Điện & Nước</span>
              <span className="hotline-num">0968 114 114</span>
            </div>
            <div className="hotline-badge">
              <span className="hotline-service">Hỗ trợ mạng Wifi KTX</span>
              <span className="hotline-num">0968 119 119</span>
            </div>
          </div>

          <div className="emergency-notice-banner">
            <div className="notice-icon-mini security-icon">
              <ShieldIcon size={18} />
            </div>
            <div className="notice-mini-text">
              <strong>Trực ứng cứu khẩn cấp 24/7:</strong>
              <p>
                Đường dây nóng hoạt động liên tục 24/7 kể cả ban đêm và các ngày nghỉ lễ để tiếp nhận, ứng phó kịp thời
                các tình huống khẩn cấp về an ninh, y tế cũng như sự cố cấp bách trong ký túc xá.
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
};
