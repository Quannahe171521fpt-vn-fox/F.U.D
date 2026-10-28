using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Room
{
    /// <summary>
    /// Body cho PATCH api/rooms/{id}/status
    /// Tương ứng nút "Bảo trì cả phòng" trên UI
    /// Khi đổi sang Maintenance sẽ đổi toàn bộ Bed trong phòng sang Maintenance
    /// </summary>
    public class UpdateRoomStatusDto
    {
        [Required(ErrorMessage = "Status là bắt buộc")]
        [RegularExpression("^(Active|Maintenance)$", ErrorMessage = "Status chỉ chấp nhận 'Active' hoặc 'Maintenance'")]
        public string Status { get; set; } = string.Empty;
    }
}
