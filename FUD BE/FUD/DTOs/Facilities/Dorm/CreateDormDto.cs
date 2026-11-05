using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Dorm
{
    /// <summary>
    /// Payload để tạo mới Dorm.
    /// </summary>
    public class CreateDormDto
    {
        [Required(ErrorMessage = "CampusId là bắt buộc")]
        public int CampusId { get; set; }

        [Required(ErrorMessage = "Code là bắt buộc")]
        [MaxLength(50, ErrorMessage = "Code không được vượt quá 50 ký tự")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên KTX là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tên KTX không được vượt quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự")]
        public string? Address { get; set; }
    }
}
