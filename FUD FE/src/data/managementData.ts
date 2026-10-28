import type { NewsItem, EquipmentRecord, EquipmentCatalogItem } from '../types/management';

export const INITIAL_NEWS: NewsItem[] = [
  {
    id: 1,
    title: 'Thông báo lịch đăng ký ký túc xá học kỳ II',
    content: 'Ban Quản lý Ký túc xá thông báo kế hoạch mở cổng đăng ký phòng ở học kỳ II dành cho toàn thể sinh viên. Sinh viên vui lòng hoàn thành đăng ký và đóng phí trước ngày quy định.',
    status: 'Published',
    publishedDate: '10/02/2025',
    attachments: [
      { name: 'Ke_hoach_dang_ky_phong_ky2.pdf', size: '1.2 MB', type: 'pdf' }
    ]
  },
  {
    id: 2,
    title: 'Kế hoạch bảo trì hệ thống điện Dom A',
    content: 'Thực hiện công tác bảo dưỡng định kỳ lưới điện và các trạm biến áp phụ trợ tại tòa nhà Dom A nhằm đảm bảo an toàn tuyệt đối.',
    status: 'Published',
    publishedDate: '11/02/2025',
    attachments: []
  },
  {
    id: 3,
    title: 'Hướng dẫn cập nhật thông tin cư trú',
    content: 'Tất cả sinh viên nội trú cần thực hiện khai báo tạm trú và cập nhật thông tin căn cước công dân gắn chip trên hệ thống phần mềm.',
    status: 'Draft',
    publishedDate: null,
    attachments: [
      { name: 'Mau_khai_bao_tam_tru.docx', size: '450 KB', type: 'doc' }
    ]
  },
  {
    id: 4,
    title: 'Chương trình vệ sinh khuôn viên cuối tuần',
    content: 'Phát động phong trào "Chung tay vì môi trường Ký túc xá Xanh - Sạch - Đẹp" với sự tham gia của tất cả các phòng.',
    status: 'Published',
    publishedDate: '15/02/2025',
    attachments: []
  },
  {
    id: 5,
    title: 'Quy định sử dụng phòng sinh hoạt chung',
    content: 'Cập nhật nội quy ra vào, thời gian mượn phòng học nhóm và các thiết bị sinh hoạt cộng đồng tại tầng 1 các tòa nhà.',
    status: 'Draft',
    publishedDate: null,
    attachments: []
  },
  {
    id: 6,
    title: 'Lịch kiểm tra an toàn phòng cháy chữa cháy tháng 3',
    content: 'Tổ chức kiểm tra định kỳ hệ thống báo cháy, bình cứu hỏa và đèn thoát hiểm tại các tầng thuộc cụm ký túc xá.',
    status: 'Published',
    publishedDate: '20/02/2025',
    attachments: []
  },
  {
    id: 7,
    title: 'Thông báo tiếp nhận sinh viên quốc tế học kỳ mới',
    content: 'Hướng dẫn đón tiếp và bố trí phòng ở đối với sinh viên trao đổi quốc tế đợt xuân hè.',
    status: 'Published',
    publishedDate: '22/02/2025',
    attachments: []
  },
  {
    id: 8,
    title: 'Kế hoạch phun khử khuẩn phòng dịch định kỳ',
    content: 'Phun khử trùng toàn bộ hành lang, khu vệ sinh chung và khuôn viên ngoại cảnh trong hai ngày cuối tuần.',
    status: 'Draft',
    publishedDate: null,
    attachments: []
  },
  {
    id: 9,
    title: 'Khảo sát chất lượng dịch vụ Internet KTX',
    content: 'Thu thập ý kiến đánh giá của sinh viên về tốc độ đường truyền và độ phủ sóng wifi trong các phòng ở.',
    status: 'Published',
    publishedDate: '25/02/2025',
    attachments: []
  },
  {
    id: 10,
    title: 'Quy trình bàn giao cơ sở vật chất cuối kỳ',
    content: 'Các bước kiểm kê trang thiết bị, chìa khóa phòng và xác nhận tình trạng trước khi sinh viên trả phòng nghỉ hè.',
    status: 'Draft',
    publishedDate: null,
    attachments: []
  },
  {
    id: 11,
    title: 'Mở rộng điểm đỗ xe đạp điện tầng hầm Dom C',
    content: 'Hoàn thành lắp đặt thêm 40 cổng sạc thông minh tự ngắt khi đầy pin tại khu vực đỗ xe Dom C.',
    status: 'Published',
    publishedDate: '28/02/2025',
    attachments: []
  },
  {
    id: 12,
    title: 'Hội thao Ký túc xá mở rộng chào hè',
    content: 'Thông tin giải bóng đá mini, bóng chuyền và cờ vua dành cho các phòng ký túc xá đăng ký tranh tài.',
    status: 'Published',
    publishedDate: '02/03/2025',
    attachments: []
  }
];

export const INITIAL_CATALOG: EquipmentCatalogItem[] = [
  { id: 1, category: 'Thiết bị điện', name: 'Quạt treo tường', inUseCount: 85 },
  { id: 2, category: 'Nội thất', name: 'Bàn học cá nhân', inUseCount: 160 },
  { id: 3, category: 'Thiết bị điện', name: 'Đèn bàn LED', inUseCount: 154 },
  { id: 4, category: 'Nội thất', name: 'Tủ quần áo 2 cánh', inUseCount: 80 },
  { id: 5, category: 'Thiết bị điện', name: 'Ổ cắm điện đa năng', inUseCount: 200 },
  { id: 6, category: 'Thiết bị làm mát', name: 'Điều hòa Inverter 12000 BTU', inUseCount: 45 },
  { id: 7, category: 'Thiết bị vệ sinh', name: 'Bình nóng lạnh 30L', inUseCount: 45 },
  { id: 8, category: 'Thiết bị PCCC', name: 'Bình chữa cháy bột ABC 4kg', inUseCount: 60 },
  { id: 9, category: 'Nội thất', name: 'Giường tầng khung thép', inUseCount: 120 },
  { id: 10, category: 'Thiết bị mạng', name: 'Router Wifi gắn trần', inUseCount: 50 },
];

export const INITIAL_EQUIPMENT: EquipmentRecord[] = [
  {
    id: 1,
    assetCode: 'TB-000120',
    name: 'Quạt treo tường',
    category: 'Thiết bị điện',
    location: 'Phòng A101',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 2,
    assetCode: 'TB-000121',
    name: 'Bàn học cá nhân',
    category: 'Nội thất',
    location: 'Giường 1/A101',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 3,
    assetCode: 'TB-000122',
    name: 'Đèn bàn LED',
    category: 'Thiết bị điện',
    location: 'Giường 1/A101',
    condition: 'Under repair',
    hasOpenTicket: true
  },
  {
    id: 4,
    assetCode: 'TB-000123',
    name: 'Tủ quần áo 2 cánh',
    category: 'Nội thất',
    location: 'Phòng A102',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 5,
    assetCode: 'TB-000124',
    name: 'Ổ cắm điện đa năng',
    category: 'Thiết bị điện',
    location: 'Phòng A201',
    condition: 'Broken',
    hasOpenTicket: true
  },
  {
    id: 6,
    assetCode: 'TB-000125',
    name: 'Điều hòa Inverter 12000 BTU',
    category: 'Thiết bị làm mát',
    location: 'Phòng B204',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 7,
    assetCode: 'TB-000126',
    name: 'Bình nóng lạnh 30L',
    category: 'Thiết bị vệ sinh',
    location: 'Phòng C305',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 8,
    assetCode: 'TB-000127',
    name: 'Quạt treo tường',
    category: 'Thiết bị điện',
    location: 'Phòng A102',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 9,
    assetCode: 'TB-000128',
    name: 'Bàn học cá nhân',
    category: 'Nội thất',
    location: 'Giường 2/A101',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 10,
    assetCode: 'TB-000129',
    name: 'Giường tầng khung thép',
    category: 'Nội thất',
    location: 'Phòng A101',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 11,
    assetCode: 'TB-000130',
    name: 'Bình chữa cháy bột ABC 4kg',
    category: 'Thiết bị PCCC',
    location: 'Hành lang A1',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 12,
    assetCode: 'TB-000131',
    name: 'Router Wifi gắn trần',
    category: 'Thiết bị mạng',
    location: 'Hành lang B2',
    condition: 'Under repair',
    hasOpenTicket: true
  },
  {
    id: 13,
    assetCode: 'TB-000132',
    name: 'Điều hòa Inverter 12000 BTU',
    category: 'Thiết bị làm mát',
    location: 'Phòng A201',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 14,
    assetCode: 'TB-000133',
    name: 'Tủ quần áo 2 cánh',
    category: 'Nội thất',
    location: 'Phòng B204',
    condition: 'Good',
    hasOpenTicket: false
  },
  {
    id: 15,
    assetCode: 'TB-000134',
    name: 'Đèn bàn LED',
    category: 'Thiết bị điện',
    location: 'Giường 2/A101',
    condition: 'Good',
    hasOpenTicket: false
  }
];

export const AVAILABLE_LOCATIONS = [
  'Phòng A101',
  'Giường 1/A101',
  'Giường 2/A101',
  'Giường 3/A101',
  'Giường 4/A101',
  'Phòng A102',
  'Giường 1/A102',
  'Giường 2/A102',
  'Phòng A201',
  'Phòng B101',
  'Phòng B204',
  'Giường 1/B204',
  'Phòng C305',
  'Phòng D102',
  'Hành lang A1',
  'Hành lang B2'
];
