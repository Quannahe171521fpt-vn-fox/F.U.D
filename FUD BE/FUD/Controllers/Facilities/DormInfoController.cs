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
            // Lấy học kỳ hiện tại (term đang diễn ra hoặc gần nhất)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var currentTerm = await _context.Terms
                .Include(t => t.PriceList)
                    .ThenInclude(pl => pl!.PriceItems)
                .OrderByDescending(t => t.StartDate)
                .FirstOrDefaultAsync(t => t.StartDate <= today);

            // Build map: RoomTypeId -> giá nhỏ nhất trong các Dom
            var priceMap = new Dictionary<int, decimal>();
            if (currentTerm?.PriceList != null)
            {
                priceMap = currentTerm.PriceList.PriceItems
                    .GroupBy(pi => pi.RoomTypeId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Min(pi => pi.UnitPrice)
                    );
            }

            // Load room types kèm theo danh sách thiết bị tiêu chuẩn từ DB
            var roomTypes = await _context.RoomTypes
                .Include(rt => rt.RoomTypeEquipments)
                    .ThenInclude(rte => rte.Catalog)
                .OrderBy(rt => rt.BedCapacity)
                .ToListAsync();

            // Nhóm theo loại: phòng ≤4 giường và >4 giường
            var room4List = roomTypes.Where(rt => rt.BedCapacity <= 4).ToList();
            var room6List = roomTypes.Where(rt => rt.BedCapacity > 4).ToList();

            var result = new List<object>();

            // Helper: build features list từ DB (RoomTypeEquipments) hoặc fallback
            List<string> BuildFeatures(List<RoomType> group)
            {
                // Gom features từ tất cả RoomType trong nhóm (unique theo catalog name)
                var features = group
                    .SelectMany(rt => rt.RoomTypeEquipments)
                    .Where(rte => rte.Catalog != null)
                    .GroupBy(rte => rte.CatalogId)
                    .Select(g =>
                    {
                        var rte = g.First();
                        return rte.StandardQuantity > 1
                            ? $"{rte.StandardQuantity} {rte.Catalog.Name}"
                            : rte.Catalog.Name;
                    })
                    .ToList();

                return features;
            }

            // Helper: tính giá min của nhóm
            decimal GetGroupPrice(List<RoomType> group, decimal fallback)
            {
                var prices = group
                    .Where(rt => priceMap.ContainsKey(rt.Id))
                    .Select(rt => priceMap[rt.Id])
                    .ToList();
                return prices.Any() ? prices.Min() : fallback;
            }

            // --- Phòng 4 giường ---
            if (room4List.Any())
            {
                var rt = room4List.First();
                var price4 = GetGroupPrice(room4List, 4_800_000m);
                var priceText4 = price4.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ / kỳ";
                var features4 = BuildFeatures(room4List);
                if (!features4.Any())
                {
                    features4 = new List<string>
                    {
                        "2 giường tầng khung thấp sơn tĩnh điện",
                        "4 cụm bàn học và tủ đồ cá nhân",
                        "Điều hòa Inverter tiết kiệm điện",
                        "Bình nóng lạnh & phòng vệ sinh khép kín",
                        "Wifi băng thông rộng phủ sóng toàn phòng"
                    };
                }

                result.Add(new
                {
                    id = "room-4bed",
                    name = rt.Name,
                    capacity = rt.BedCapacity,
                    area = "28 m²",
                    pricePerSemester = priceText4,
                    features = features4,
                    description = "Không gian yên tĩnh, tối ưu cho việc học tập chuyên sâu và sinh hoạt nhóm nhỏ thoải mái.",
                    popular = false
                });
            }
            else
            {
                // Fallback nếu DB chưa có dữ liệu
                result.Add(new
                {
                    id = "room-4bed",
                    name = "Phòng 4 Giường",
                    capacity = 4,
                    area = "28 m²",
                    pricePerSemester = "4.800.000 VNĐ / kỳ",
                    features = new List<string>
                    {
                        "2 giường tầng khung thấp sơn tĩnh điện",
                        "4 cụm bàn học và tủ đồ cá nhân",
                        "Điều hòa Inverter tiết kiệm điện",
                        "Bình nóng lạnh & phòng vệ sinh khép kín",
                        "Wifi băng thông rộng phủ sóng toàn phòng"
                    },
                    description = "Không gian yên tĩnh, tối ưu cho việc học tập chuyên sâu và sinh hoạt nhóm nhỏ thoải mái.",
                    popular = false
                });
            }

            // --- Phòng 6 giường ---
            if (room6List.Any())
            {
                var rt = room6List.First();
                var price6 = GetGroupPrice(room6List, 3_600_000m);
                var priceText6 = price6.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ / kỳ";
                var features6 = BuildFeatures(room6List);
                if (!features6.Any())
                {
                    features6 = new List<string>
                    {
                        "3 giường tầng khung thấp sơn tĩnh điện",
                        "6 cụm bàn học và tủ đồ cá nhân",
                        "Điều hòa Inverter tiết kiệm điện",
                        "Bình nóng lạnh & phòng vệ sinh khép kín",
                        "Wifi băng thông rộng phủ sóng toàn phòng"
                    };
                }

                result.Add(new
                {
                    id = "room-6bed",
                    name = rt.Name,
                    capacity = rt.BedCapacity,
                    area = "34 m²",
                    pricePerSemester = priceText6,
                    features = features6,
                    description = "Lựa chọn phổ biến nhất của sinh viên, cân bằng giữa chi phí hợp lý và không gian sống tiện nghi.",
                    popular = true
                });
            }
            else
            {
                result.Add(new
                {
                    id = "room-6bed",
                    name = "Phòng 6 Giường",
                    capacity = 6,
                    area = "34 m²",
                    pricePerSemester = "3.600.000 VNĐ / kỳ",
                    features = new List<string>
                    {
                        "3 giường tầng khung thấp sơn tĩnh điện",
                        "6 cụm bàn học và tủ đồ cá nhân",
                        "Điều hòa Inverter tiết kiệm điện",
                        "Bình nóng lạnh & phòng vệ sinh khép kín",
                        "Wifi băng thông rộng phủ sóng toàn phòng"
                    },
                    description = "Lựa chọn phổ biến nhất của sinh viên, cân bằng giữa chi phí hợp lý và không gian sống tiện nghi.",
                    popular = true
                });
            }

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
                    answer = "Cổng KTX mở cửa từ 05:30 sáng và đóng cổng vào lúc 23:00 hàng ngày. Trường hợp sinh viên có việc học tập nghiên cứu, làm thêm hoặc về quê cần báo trước với Quản lý tòa nhà qua ứng dụng hoặc viết phiếu xin phép."
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
