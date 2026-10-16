import React from 'react';
import { ShieldIcon, BuildingIcon, WifiIcon } from './Icons';

export const AboutSection: React.FC = () => {
  return (
    <section className="about-section-container" id="about-section">
      <div className="section-header">
        <span className="section-kicker">Về chúng tôi</span>
        <h2 className="section-title">Hệ Thống On Campus Dormitory</h2>
        <p className="section-description">
          Được thành lập với sứ mệnh kiến tạo môi trường sống hiện đại, văn minh và gắn kết
          cho cộng đồng sinh viên Đại học FPT trong suốt những năm tháng thanh xuân đại học.
        </p>
      </div>

      <div className="about-cards-row">
        <div className="about-card">
          <div className="about-card-icon">
            <BuildingIcon size={26} />
          </div>
          <h3 className="about-card-title">Triết lý thiết kế</h3>
          <p className="about-card-text">
            Tận dụng tối đa ánh sáng tự nhiên và gió trời với kiến trúc mở nhìn ra hồ sen sinh thái.
            Khuôn viên xanh không chỉ đem lại bầu không khí trong lành mà còn là không gian truyền cảm hứng sáng tạo.
          </p>
        </div>

        <div className="about-card">
          <div className="about-card-icon">
            <ShieldIcon size={26} />
          </div>
          <h3 className="about-card-title">Môi trường an toàn</h3>
          <p className="about-card-text">
            Đặt sự an tâm của phụ huynh và sinh viên lên hàng đầu. Đội ngũ an ninh tuần tra 24/7,
            hệ thống phòng cháy chữa cháy tự động đạt chuẩn quốc gia cùng phòng y tế túc trực chăm sóc sức khỏe.
          </p>
        </div>

        <div className="about-card">
          <div className="about-card-icon">
            <WifiIcon size={26} />
          </div>
          <h3 className="about-card-title">Chuyển đổi số toàn diện</h3>
          <p className="about-card-text">
            Mọi thủ tục từ đăng ký phòng, thanh toán lệ phí, báo hỏng thiết bị đến kiểm tra điện nước
            đều được số hóa 100% qua cổng thông tin trực tuyến, giúp sinh viên tiết kiệm thời gian tối đa.
          </p>
        </div>
      </div>
    </section>
  );
};
