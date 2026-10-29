using FUD.Common;
using FUD.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUD.Controllers.Facilities
{
    [Route("api/facilities/dorm-info")]
    [ApiController]
    public class DormInfoController : ControllerBase
    {
        private readonly FudContext _context;

        public DormInfoController(FudContext context)
        {
            _context = context;
        }

        [HttpGet("buildings")]
        public async Task<IActionResult> GetBuildings()
        {
            var blocks = await _context.Blocks
                .Include(b => b.Rooms)
                .OrderBy(b => b.Code)
                .Select(b => new
                {
                    Name = "Dom " + b.Code,
                    Gender = b.GenderType == "Male" ? "Nam" : (b.GenderType == "Female" ? "Nữ" : b.GenderType),
                    Floors = b.NumberOfFloors ?? 5,
                    Rooms = b.Rooms.Count,
                    Status = b.Status == "Active" ? "Đang hoạt động" : "Bảo trì"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(blocks));
        }

        [HttpGet("room-types")]
        public async Task<IActionResult> GetRoomTypes()
        {
            var roomTypes = await _context.RoomTypes
                .Select(rt => new
                {
                    Id = "room-" + rt.Id,
                    Name = rt.Name,
                    Capacity = rt.BedCapacity,
                    Area = rt.BedCapacity <= 4 ? "28 m²" : "34 m²",
                    PricePerSemester = rt.BedCapacity <= 4 ? "4.800.000 VNĐ / kỳ (4 tháng)" : "3.600.000 VNĐ / kỳ (4 tháng)",
                    Features = new List<string>
                    {
                        $"{rt.BedCapacity / 2} giường tầng khung thép sơn tĩnh điện",
                        $"{rt.BedCapacity} cụm bàn học và tủ đồ cá nhân",
                        "Điều hòa Inverter tiết kiệm điện",
                        "Bình nóng lạnh & phòng vệ sinh khép kín",
                        "Wifi băng thông rộng phủ sóng toàn phòng"
                    },
                    Description = rt.BedCapacity <= 4 
                        ? "Không gian yên tĩnh, tối ưu cho việc học tập chuyên sâu và sinh hoạt nhóm nhỏ thoải mái."
                        : "Lựa chọn phổ biến nhất của sinh viên, cân bằng giữa chi phí hợp lý và không gian sống tiện nghi.",
                    Popular = rt.BedCapacity <= 4
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(roomTypes));
        }

        [HttpGet("faqs")]
        public IActionResult GetFaqs()
        {
            var faqs = new List<object>
            {
                new {
                    Id = "faq-1",
                    Category = "dang-ky",
                    Question = "Thời gian mở cổng đăng ký KTX cho học kỳ mới là khi nào?",
                    Answer = "Cổng đăng ký KTX thường mở trước kỳ học mới 3 tuần (khoảng tháng 1, tháng 5 và tháng 9 hàng năm). Thông báo chính thức sẽ được gửi qua email sinh viên (@fpt.edu.vn) và đăng tải trên cổng thông tin KTX."
                },
                new {
                    Id = "faq-2",
                    Category = "dang-ky",
                    Question = "Sinh viên tân sinh viên năm nhất có được ưu tiên phòng ở KTX không?",
                    Answer = "Có. Nhà trường luôn dành tối thiểu 70% chỉ tiêu phòng KTX cho tân sinh viên khóa mới đến từ các tỉnh xa nhằm hỗ trợ các bạn nhanh chóng ổn định việc học tập và làm quen với môi trường đại học."
                },
                new {
                    Id = "faq-3",
                    Category = "dang-ky",
                    Question = "Tôi có thể đăng ký ở cùng phòng với bạn bè cùng lớp/cùng quê không?",
                    Answer = "Hoàn toàn được. Trong mẫu đăng ký trực tuyến, sinh viên chỉ cần điền MSSV của bạn cùng phòng vào mục 'Nguyện vọng ở chung'. Hệ thống sẽ tự động ghép phòng khi cả hai bạn cùng chọn cùng một loại phòng."
                },
                new {
                    Id = "faq-4",
                    Category = "chi-phi",
                    Question = "Chi phí điện nước và dịch vụ tại KTX được tính như thế nào?",
                    Answer = "Tiền phòng đã bao gồm chi phí wifi và dịch vụ vệ sinh khu vực công cộng. Tiền điện và nước sinh hoạt được cấp định mức tiêu chuẩn, phần vượt định mức sẽ tính theo đơn giá nhà nước và chia đều cho các thành viên trong phòng."
                },
                new {
                    Id = "faq-5",
                    Category = "noi-quy",
                    Question = "Giờ giới nghiêm ra vào KTX quy định như thế nào?",
                    Answer = "Cổng KTX mở cửa từ 05:30 sáng và đóng cổng vào lúc 23:00 hàng ngày. Trường hợp sinh viên có việc học tập nghiên cứu, làm thêm hoặc về quê cần báo trước với Quản lý tòa nhà qua ứng dụng hoặc viết phiếu xin phép."
                },
                new {
                    Id = "faq-6",
                    Category = "tien-ich",
                    Question = "KTX có những tiện ích và dịch vụ giải trí nào trong khuôn viên?",
                    Answer = "Khuôn viên KTX có hệ thống căng tin, siêu thị tiện lợi 24/7, tiệm giặt là sấy tự động, phòng tự học có điều hòa, sân bóng đá cỏ nhân tạo, sân bóng rổ, bàn bóng bàn và khuôn viên hồ sen dạo bộ."
                },
                new {
                    Id = "faq-7",
                    Category = "tien-ich",
                    Question = "Khi thiết bị trong phòng bị hỏng (đèn, vòi nước, điều hòa), tôi báo cho ai?",
                    Answer = "Sinh viên có thể báo trực tiếp tại phòng trực Quản lý tầng 1 của từng Dom hoặc liên hệ hotline Đội Kỹ thuật Điện & Nước 24/7 (0968 114 114). Đội ngũ kỹ thuật sẽ tiếp nhận và kiểm tra, sửa chữa trong vòng 2 - 4 giờ làm việc."
                }
            };

            return Ok(ApiResponse<object>.Ok(faqs));
        }
    }
}

