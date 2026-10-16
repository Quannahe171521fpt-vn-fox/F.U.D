import React from 'react';
import { ROOM_TYPES } from '../data/dormData';
import { CheckIcon } from './Icons';
import type { RoomType } from '../types/dormitory';

interface RoomTypesSectionProps {
  onSelectRoom: (roomId: string) => void;
}

export const RoomTypesSection: React.FC<RoomTypesSectionProps> = ({ onSelectRoom }) => {
  return (
    <section className="room-types-section-container" id="room-types">
      <div className="section-header">
        <span className="section-kicker">Cơ cấu phòng & Biểu phí</span>
        <h2 className="section-title">Các loại phòng ở tại KTX FPT</h2>
        <p className="section-description">
          Đa dạng sự lựa chọn tùy theo nhu cầu học tập, sinh hoạt và ngân sách của sinh viên.
          Tất cả các phòng đều được trang bị đầy đủ nội thất và tiện nghi cần thiết.
        </p>
      </div>

      <div className="rooms-grid">
        {ROOM_TYPES.map((room: RoomType) => (
          <div key={room.id} className={`room-card ${room.popular ? 'highlighted' : ''}`}>
            {room.popular && <div className="popular-badge">Được lựa chọn nhiều nhất</div>}
            
            <div className="room-card-header">
              <h3 className="room-card-title">{room.name}</h3>
              <p className="room-card-desc">{room.description}</p>
            </div>

            <div className="room-card-price-box">
              <span className="room-price">{room.pricePerSemester}</span>
              <span className="room-meta">Sức chứa: {room.capacity} sinh viên • Diện tích: {room.area}</span>
            </div>

            <div className="room-amenities-list">
              <div className="amenities-heading">Trang thiết bị & Tiện ích tiêu chuẩn:</div>
              <ul>
                {room.features.map((feature, idx) => (
                  <li key={idx}>
                    <span className="check-icon-wrapper">
                      <CheckIcon size={16} />
                    </span>
                    <span>{feature}</span>
                  </li>
                ))}
              </ul>
            </div>

            <button
              type="button"
              className={`select-room-btn ${room.popular ? 'primary' : 'secondary'}`}
              onClick={() => onSelectRoom(room.id)}
            >
              Chọn đăng ký loại phòng này
            </button>
          </div>
        ))}
      </div>

      {/* Electricity & Water Policy Form Box */}
      <div className="utility-policy-box">
        <h3 className="policy-title">Chính sách định mức điện nước & Phí dịch vụ đi kèm</h3>
        <div className="policy-grid">
          <div className="policy-item">
            <strong>Định mức điện thoại thoại & quạt/đèn:</strong>
            <p>Hỗ trợ định mức cơ bản 15 kWh/sinh viên/tháng. Sử dụng điều hòa vượt định mức tính theo công tơ điện tử riêng từng phòng.</p>
          </div>
          <div className="policy-item">
            <strong>Định mức nước sinh hoạt:</strong>
            <p>Cung cấp định mức nước sinh hoạt 4 m³/sinh viên/tháng, nước nóng sinh hoạt năng lượng mặt trời phục vụ liên tục.</p>
          </div>
          <div className="policy-item">
            <strong>Wifi & Internet băng thông rộng:</strong>
            <p>Miễn phí 100% trong suốt kỳ học với đường truyền cáp quang riêng cho từng khu ký túc xá.</p>
          </div>
          <div className="policy-item">
            <strong>Vệ sinh & An ninh công cộng:</strong>
            <p>Khu vực hành lang, thang bộ và khuôn viên được nhân viên tạp vụ dọn dẹp hàng ngày.</p>
          </div>
        </div>
      </div>
    </section>
  );
};
