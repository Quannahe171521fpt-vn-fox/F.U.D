import React, { useState, useEffect } from 'react';
import { CheckIcon } from '../../components/Icons';
import type { RoomType } from '../../types/dormitory';
import { dormInfoApi } from '../../services/api';

export const RoomTypesSection: React.FC = () => {
  const [roomTypes, setRoomTypes] = useState<RoomType[]>([]);

  useEffect(() => {
    dormInfoApi.getRoomTypes()
      .then((data) => {
        if (data && data.length > 0) {
          setRoomTypes(data);
        }
      })
      .catch((err) => console.error('Error fetching room types:', err));
  }, []);

  return (
    <section className="room-types-section-container" id="room-types">
      <div className="section-header">
        <span className="section-kicker">Cơ cấu phòng &amp; Biểu phí</span>
        <h2 className="section-title">Các loại phòng ở tại KTX FPT</h2>
        <p className="section-description">
          Đa dạng sự lựa chọn tùy theo nhu cầu học tập, sinh hoạt và ngân sách của sinh viên.
          Tất cả các phòng đều được trang bị đầy đủ nội thất và tiện nghi cần thiết.
        </p>
      </div>

      <div className="rooms-grid">
        {roomTypes.map((room: RoomType) => (
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
              <div className="amenities-heading">Trang thiết bị &amp; Tiện ích tiêu chuẩn:</div>
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
          </div>
        ))}
      </div>
    </section>
  );
};
