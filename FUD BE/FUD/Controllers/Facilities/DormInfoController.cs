using FUD.Common;
using FUD.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUD.Controllers.Facilities
{
    [Route("api/facilities/dorm-info")]
    [ApiController]
    [AllowAnonymous]
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
                    name = "Dom " + b.Code,
                    gender = b.GenderType == "Male" ? "Nam" : (b.GenderType == "Female" ? "Nữ" : b.GenderType),
                    floors = b.NumberOfFloors ?? 5,
                    rooms = b.Rooms.Count,
                    status = b.Status == "Active" ? "Đang hoạt động" : "Bảo trì"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Ok(blocks));
        }

        [HttpGet("room-types")]
        public async Task<IActionResult> GetRoomTypes()
        {
            // Lấy học kỳ mới nhất được tạo trong hệ thống có thiết lập bảng giá (Price_Items)
            var latestTermWithPrice = await _context.Terms
                .Include(t => t.PriceList)
                    .ThenInclude(pl => pl!.PriceItems)
                .Where(t => t.PriceList != null && t.PriceList.PriceItems.Any())
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();

            // Build map: RoomTypeId -> đơn giá từ Price_Items của kỳ mới nhất (min nếu có nhiều dorm)
            var priceMap = new Dictionary<int, decimal>();
            if (latestTermWithPrice?.PriceList?.PriceItems != null && latestTermWithPrice.PriceList.PriceItems.Any())
            {
                priceMap = latestTermWithPrice.PriceList.PriceItems
                    .GroupBy(pi => pi.RoomTypeId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Min(pi => pi.UnitPrice)
                    );
            }

            // Load toàn bộ loại phòng trực tiếp từ Database kèm danh sách thiết bị
            var roomTypes = await _context.RoomTypes
                .Include(rt => rt.RoomTypeEquipments)
                    .ThenInclude(rte => rte.Catalog)
                .OrderBy(rt => rt.BedCapacity)
                .ToListAsync();

            var result = roomTypes.Select(rt =>
            {
                // Giá từ Database (Price_Items của kỳ mới nhất)
                string priceText;
                if (priceMap.TryGetValue(rt.Id, out decimal dbPrice) && dbPrice > 0)
                {
                    priceText = dbPrice.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ / kỳ";
                }
                else
                {
                    priceText = "Liên hệ";
                }

                // Danh sách trang thiết bị từ RoomTypeEquipments trong Database
                var features = rt.RoomTypeEquipments
                    .Where(rte => rte.Catalog != null)
                    .Select(rte => rte.StandardQuantity > 1
                        ? $"{rte.StandardQuantity} {rte.Catalog.Name}"
                        : rte.Catalog.Name)
                    .ToList();

                if (!features.Any())
                {
                    features.Add($"{rt.BedCapacity / 2} giường tầng khung thép chịu lực");
                    features.Add($"{rt.BedCapacity} cụm bàn học và tủ đồ cá nhân");
                    features.Add("Điều hòa Inverter & bình nóng lạnh khép kín");
                    features.Add("Wifi băng thông rộng phủ sóng toàn phòng");
                }

                return new
                {
                    id = $"room-{rt.Id}",
                    name = rt.Name,
                    capacity = rt.BedCapacity,
                    area = $"{rt.BedCapacity * 6 + 4} m²",
                    pricePerSemester = priceText,
                    features = features,
                    description = $"Loại phòng {rt.Name} với sức chứa {rt.BedCapacity} người, đầy đủ tiện nghi, không gian thoáng mát và an ninh đảm bảo.",
                    popular = rt.BedCapacity == 6 || rt.BedCapacity == 4
                };
            }).ToList();

            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpGet("faqs")]
        public IActionResult GetFaqs()
        {
            var faqs = new List<object>
            {
                new {
                    id = "faq-1",
                    category = "dang-ky",
                    question = "Thời gian mở cổng đăng ký KTX cho học kỳ mới là khi nào?",
                    answer = "Cổng đăng ký KTX thường mở trước kỳ học mới 3 tuần (khoảng tháng 1, tháng 5 và tháng 9 hàng năm). Thông báo chính thức sẽ được gửi qua email sinh viên (@fpt.edu.vn) và đăng tải trên cổng thông tin KTX."
                },
                new {
                    id = "faq-2",
                    category = "dang-ky",
                    question = "Sinh viên tân sinh viên năm nhất có được ưu tiên phòng ở KTX không?",
                    answer = "Có. Nhà trường luôn dành tối thiểu 70% chỉ tiêu phòng KTX cho tân sinh viên khóa mới đến từ các tỉnh xa nhằm hỗ trợ các bạn nhanh chóng ổn định việc học tập và làm quen với môi trường đại học."
                },
                new {
                    id = "faq-3",
                    category = "dang-ky",
                    question = "Tôi có thể đăng ký ở cùng phòng với bạn bè cùng lớp/cùng quê không?",
                    answer = "Hoàn toàn được. Trong mẫu đăng ký trực tuyến, sinh viên chỉ cần điền MSSV của bạn cùng phòng vào mục 'Nguyện vọng ở chung'. Hệ thống sẽ tự động ghép phòng khi cả hai bạn cùng chọn cùng một loại phòng."
                },
                new {
                    id = "faq-4",
                    category = "chi-phi",
                    question = "Chi phí điện nước và dịch vụ tại KTX được tính như thế nào?",
                    answer = "Tiền phòng đã bao gồm chi phí wifi và dịch vụ vệ sinh khu vực công cộng. Tiền điện và nước sinh hoạt được cấp định mức tiêu chuẩn, phần vượt định mức sẽ tính theo đơn giá nhà nước và chia đều cho các thành viên trong phòng."
                },
                new {
                    id = "faq-5",
                    category = "noi-quy",
                    question = "Giờ giới nghiêm ra vào KTX quy định như thế nào?",
                    answer = "Cổng KTX mở cửa từ 05:30 sáng và đóng cổng vào lúc 23:00 hàng ngày. Trường hợp sinh viên có việc học tập nghiên cứu, làm thêm hoặc về quê cần báo trước với Quản lý qua ứng dụng hoặc viết phiếu xin phép."
                },
                new {
                    id = "faq-6",
                    category = "tien-ich",
                    question = "KTX có những tiện ích và dịch vụ giải trí nào trong khuôn viên?",
                    answer = "Khuôn viên KTX có hệ thống căng tin, siêu thị tiện lợi 24/7, tiệm giặt là sấy tự động, phòng tự học có điều hòa, sân bóng đá cỏ nhân tạo, sân bóng rổ, bàn bóng bàn và khuôn viên hồ sen dạo bộ."
                },
                new {
                    id = "faq-7",
                    category = "tien-ich",
                    question = "Khi thiết bị trong phòng bị hỏng (đèn, vòi nước, điều hòa), tôi báo cho ai?",
                    answer = "Sinh viên có thể báo trực tiếp tại phòng trực Quản lý tầng 1 của từng Dom hoặc liên hệ hotline Đội Kỹ thuật Điện & Nước 24/7 (0968 114 114). Đội ngũ kỹ thuật sẽ tiếp nhận và kiểm tra, sửa chữa trong vòng 2 - 4 giờ làm việc."
                }
            };

            return Ok(ApiResponse<object>.Ok(faqs));
        }
    }
}
