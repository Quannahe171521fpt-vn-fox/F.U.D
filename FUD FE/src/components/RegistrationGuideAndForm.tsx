import React from 'react';
import { CheckIcon, ClockIcon, BuildingIcon, PhoneIcon } from './Icons';

interface RegistrationProps {
  selectedRoomId?: string;
}

export const RegistrationGuideAndForm: React.FC<RegistrationProps> = () => {
  return (
    <section className="registration-section-container" id="registration-guide">
      <div className="section-header">
        <span className="section-kicker">Hướng dẫn & Thủ tục</span>
        <h2 className="section-title">Quy trình đăng ký sử dụng KTX</h2>
        <p className="section-description">
          Thực hiện đăng ký chỗ ở ký túc xá theo quy trình 4 bước tiêu chuẩn của Trường Đại học FPT.
          Hệ thống sẽ tổng hợp và thông báo kết quả qua email sinh viên.
        </p>
      </div>

      {/* 4 Step Visual Workflow */}
      <div className="workflow-steps">
        <div className="step-card">
          <div className="step-number">Bước 1</div>
          <h4 className="step-heading">Khảo sát & Chọn loại phòng</h4>
          <p className="step-desc">Tìm hiểu biểu phí, tiện ích của các loại phòng 4 hoặc 6 giường để chọn phòng phù hợp.</p>
        </div>
        <div className="step-card">
          <div className="step-number">Bước 2</div>
          <h4 className="step-heading">Theo dõi thông báo mở cổng</h4>
          <p className="step-desc">Theo dõi thông báo mở cổng đăng ký KTX trên cổng thông tin đào tạo và email sinh viên @fpt.edu.vn.</p>
        </div>
        <div className="step-card">
          <div className="step-number">Bước 3</div>
          <h4 className="step-heading">Xét duyệt & Nộp phí</h4>
          <p className="step-desc">Nhận thông báo xếp phòng từ Ban Quản lý và hoàn tất nộp phí lưu trú qua cổng thanh toán.</p>
        </div>
        <div className="step-card">
          <div className="step-number">Bước 4</div>
          <h4 className="step-heading">Nhận phòng & Bàn giao</h4>
          <p className="step-desc">Đến văn phòng Dom làm thủ tục check-in, nhận chìa khóa/thẻ từ và ký biên bản cơ sở vật chất.</p>
        </div>
      </div>

      {/* Registration Information & Notes Card */}
      <div className="registration-notice-card">
        <div className="notice-card-header">
          <h3 className="notice-card-title">Thông tin & Lưu ý khi đăng ký lưu trú</h3>
          <p className="notice-card-subtitle">Áp dụng cho toàn bộ sinh viên đang theo học tại Trường Đại học FPT</p>
        </div>
        <div className="notice-grid">
          <div className="notice-item">
            <div className="notice-icon-badge">
              <ClockIcon size={20} />
            </div>
            <div>
              <h4 className="notice-heading">Thời gian mở cổng đăng ký</h4>
              <p className="notice-desc">
                Cổng đăng ký KTX mở định kỳ trước mỗi học kỳ mới 3 tuần (tháng 1, tháng 5 và tháng 9 hàng năm). Thông báo chính thức sẽ gửi qua email sinh viên.
              </p>
            </div>
          </div>
          <div className="notice-item">
            <div className="notice-icon-badge">
              <BuildingIcon size={20} />
            </div>
            <div>
              <h4 className="notice-heading">Đối tượng ưu tiên</h4>
              <p className="notice-desc">
                Nhà trường ưu tiên chỉ tiêu cho tân sinh viên khóa mới, sinh viên quốc tế / trao đổi, sinh viên diện chính sách và các bạn đến từ tỉnh xa.
              </p>
            </div>
          </div>
          <div className="notice-item">
            <div className="notice-icon-badge">
              <CheckIcon size={20} color="#D96528" />
            </div>
            <div>
              <h4 className="notice-heading">Nguyện vọng ở cùng bạn bè</h4>
              <p className="notice-desc">
                Sinh viên có thể đăng ký ở cùng phòng với bạn bè bằng cách ghi chú MSSV của bạn cùng phòng khi cả hai cùng đăng ký chung một loại phòng.
              </p>
            </div>
          </div>
          <div className="notice-item">
            <div className="notice-icon-badge">
              <PhoneIcon size={20} />
            </div>
            <div>
              <h4 className="notice-heading">Hỗ trợ & Giải đáp thắc mắc</h4>
              <p className="notice-desc">
                Mọi thắc mắc về thủ tục đăng ký xin liên hệ trực tiếp Văn phòng Quản lý KTX tại Tòa Dom A hoặc hotline: <strong>(024) 7300 1866</strong>.
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
};
