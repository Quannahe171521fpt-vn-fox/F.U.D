import type { RoomType, FaqItem } from '../types/dormitory';

export const ROOM_TYPES: RoomType[] = [
  {
    id: 'room-4',
    name: 'Phòng Tiêu chuẩn 4 người',
    capacity: 4,
    area: '28 m²',
    pricePerSemester: '4.800.000 VNĐ / kỳ (4 tháng)',
    features: [
      '2 giường tầng cao cấp có rèm che riêng tư',
      '4 bàn học cá nhân liền kề giá sách & đèn học',
      '4 tủ quần áo có khóa an toàn',
      'Điều hòa Inverter 2 chiều tiết kiệm điện',
      'Bình nóng lạnh & phòng vệ sinh khép kín',
      'Wifi tốc độ cao phủ sóng toàn phòng',
      'Ban công phơi đồ thoáng mát, đón gió tự nhiên'
    ],
    description: 'Không gian yên tĩnh, tối ưu cho việc học tập chuyên sâu và sinh hoạt nhóm nhỏ thoải mái.',
    popular: true,
  },
  {
    id: 'room-6',
    name: 'Phòng Phổ thông 6 người',
    capacity: 6,
    area: '34 m²',
    pricePerSemester: '3.600.000 VNĐ / kỳ (4 tháng)',
    features: [
      '3 giường tầng khung thép sơn tĩnh điện chịu lực',
      '6 cụm bàn học và tủ đồ cá nhân',
      'Điều hòa 2 chiều và quạt trần đối lưu',
      'Phòng tắm và nhà vệ sinh riêng biệt',
      'Wifi băng thông rộng riêng cho phòng',
      'Hệ thống báo cháy và chữa cháy tự động Sprinkler',
      'Khu vực để giày dép và phơi quần áo riêng'
    ],
    description: 'Lựa chọn phổ biến nhất của sinh viên, cân bằng giữa chi phí hợp lý và không gian sống tiện nghi.',
  },
];

export const DORM_BUILDINGS = [
  { name: 'Dom A', gender: 'Nam', floors: 5, rooms: 120, status: 'Đang hoạt động' },
  { name: 'Dom B', gender: 'Nữ', floors: 5, rooms: 120, status: 'Đang hoạt động' },
  { name: 'Dom C', gender: 'Nam', floors: 5, rooms: 110, status: 'Đang hoạt động' },
  { name: 'Dom D', gender: 'Nữ', floors: 5, rooms: 110, status: 'Đang hoạt động' },
  { name: 'Dom E', gender: 'Nam', floors: 6, rooms: 140, status: 'Đang hoạt động' },
  { name: 'Dom G', gender: 'Nữ / Quốc tế', floors: 6, rooms: 150, status: 'Đang hoạt động' },
];

export const FAQS: FaqItem[] = [
  {
    id: 'faq-1',
    category: 'dang-ky',
    question: 'Thời gian mở cổng đăng ký KTX cho học kỳ mới là khi nào?',
    answer: 'Cổng đăng ký KTX thường mở trước kỳ học mới 3 tuần (khoảng tháng 1, tháng 5 và tháng 9 hàng năm). Thông báo chính thức sẽ được gửi qua email sinh viên (@fpt.edu.vn) và đăng tải trên cổng thông tin KTX.',
  },
  {
    id: 'faq-2',
    category: 'dang-ky',
    question: 'Sinh viên tân sinh viên năm nhất có được ưu tiên phòng ở KTX không?',
    answer: 'Có. Nhà trường luôn dành tối thiểu 70% chỉ tiêu phòng KTX cho tân sinh viên khóa mới đến từ các tỉnh xa nhằm hỗ trợ các bạn nhanh chóng ổn định việc học tập và làm quen với môi trường đại học.',
  },
  {
    id: 'faq-3',
    category: 'dang-ky',
    question: 'Tôi có thể đăng ký ở cùng phòng với bạn bè cùng lớp/cùng quê không?',
    answer: 'Hoàn toàn được. Trong mẫu đăng ký trực tuyến, sinh viên chỉ cần điền MSSV của bạn cùng phòng vào mục "Nguyện vọng ở chung". Hệ thống sẽ tự động ghép phòng khi cả hai bạn cùng chọn cùng một loại phòng.',
  },
  {
    id: 'faq-4',
    category: 'chi-phi',
    question: 'Chi phí điện nước và dịch vụ tại KTX được tính như thế nào?',
    answer: 'Tiền phòng đã bao gồm chi phí wifi và dịch vụ vệ sinh khu vực công cộng. Tiền điện và nước sinh hoạt được cấp định mức tiêu chuẩn, phần vượt định mức sẽ tính theo đơn giá nhà nước và chia đều cho các thành viên trong phòng.',
  },
  {
    id: 'faq-5',
    category: 'noi-quy',
    question: 'Giờ giới nghiêm ra vào KTX quy định như thế nào?',
    answer: 'Cổng KTX mở cửa từ 05:30 sáng và đóng cổng vào lúc 23:00 hàng ngày. Trường hợp sinh viên có việc học tập nghiên cứu, làm thêm hoặc về quê cần báo trước với Quản lý tòa nhà qua ứng dụng hoặc viết phiếu xin phép.',
  },
  {
    id: 'faq-6',
    category: 'tien-ich',
    question: 'KTX có những tiện ích và dịch vụ giải trí nào trong khuôn viên?',
    answer: 'Khuôn viên KTX có hệ thống căng tin, siêu thị tiện lợi 24/7, tiệm giặt là sấy tự động, phòng tự học có điều hòa, sân bóng đá cỏ nhân tạo, sân bóng rổ, bàn bóng bàn và khuôn viên hồ sen dạo bộ.',
  },
  {
    id: 'faq-7',
    category: 'tien-ich',
    question: 'Khi thiết bị trong phòng bị hỏng (đèn, vòi nước, điều hòa), tôi báo cho ai?',
    answer: 'Sinh viên có thể báo trực tiếp tại phòng trực Quản lý tầng 1 của từng Dom hoặc liên hệ hotline Đội Kỹ thuật Điện & Nước 24/7 (0968 114 114). Đội ngũ kỹ thuật sẽ tiếp nhận và kiểm tra, sửa chữa trong vòng 2 - 4 giờ làm việc.',
  },
];
