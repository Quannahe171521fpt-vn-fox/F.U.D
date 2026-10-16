import React from 'react';
import { Logo, PhoneIcon, MailIcon } from './Icons';

interface FooterProps {
  onNavigate: (sectionId: string) => void;
}

export const Footer: React.FC<FooterProps> = ({ onNavigate }) => {
  return (
    <footer className="footer-container">
      <div className="footer-inner">
        {/* Brand & Description */}
        <div className="footer-brand-col">
          <Logo />
          <p className="footer-desc">
            Cổng thông tin quản lý & dịch vụ Ký túc xá Trường Đại học FPT.
            Đem lại trải nghiệm lưu trú tiện nghi, an toàn và tràn đầy năng lượng cho sinh viên.
          </p>
          <div className="footer-contact-mini">
            <p><PhoneIcon size={14} /> Hotline: (024) 7300 1866</p>
            <p><MailIcon size={14} /> Email: ktx.fpt@fe.edu.vn</p>
          </div>
        </div>

        {/* Quick Links */}
        <div className="footer-links-col">
          <h4 className="footer-heading">Liên kết nhanh</h4>
          <ul className="footer-links-list">
            <li>
              <button type="button" onClick={() => onNavigate('dorm-info')} className="footer-link-btn">
                Thông tin KTX FPT
              </button>
            </li>
            <li>
              <button type="button" onClick={() => onNavigate('room-types')} className="footer-link-btn">
                Các loại phòng & Biểu phí
              </button>
            </li>
            <li>
              <button type="button" onClick={() => onNavigate('registration-guide')} className="footer-link-btn">
                Đăng ký sử dụng KTX
              </button>
            </li>
            <li>
              <button type="button" onClick={() => onNavigate('faq-section')} className="footer-link-btn">
                Câu hỏi thường gặp (FAQ)
              </button>
            </li>
            <li>
              <button type="button" onClick={() => onNavigate('support-section')} className="footer-link-btn">
                Báo sự cố & Phản hồi
              </button>
            </li>
          </ul>
        </div>

        {/* Campus Locations */}
        <div className="footer-links-col">
          <h4 className="footer-heading">Hệ thống cơ sở KTX</h4>
          <ul className="footer-campuses-list">
            <li><strong>Hà Nội:</strong> Khu CNC Hòa Lạc, Km 29 Đại Lộ Thăng Long, Thạch Thất</li>
            <li><strong>Đà Nẵng:</strong> Khu Đô thị Công nghệ FPT, Ngũ Hành Sơn</li>
            <li><strong>Quy Nhơn:</strong> Khu Đô thị An Phú Thịnh, P. Đống Đa</li>
            <li><strong>TP. HCM:</strong> Đường D1, Khu Công nghệ cao, TP. Thủ Đức</li>
            <li><strong>Cần Thơ:</strong> 600 Nguyễn Văn Cừ Nối Dài, An Bình, Ninh Kiều</li>
          </ul>
        </div>
      </div>

      <div className="footer-bottom-bar">
        <div className="footer-bottom-inner">
          <p>© {new Date().getFullYear()} Trường Đại học FPT - Ban Quản lý Ký túc xá (On campus Dormitory). All rights reserved.</p>
          <div className="footer-legal-links">
            <a href="#terms" onClick={(e) => { e.preventDefault(); onNavigate('faq-section'); }}>Quy chế ký túc xá</a>
            <span>•</span>
            <a href="#privacy" onClick={(e) => { e.preventDefault(); onNavigate('faq-section'); }}>Chính sách bảo mật</a>
          </div>
        </div>
      </div>
    </footer>
  );
};
