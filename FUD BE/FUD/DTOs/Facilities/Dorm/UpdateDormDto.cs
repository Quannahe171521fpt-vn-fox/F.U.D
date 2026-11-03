using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Dorm
{
    /// <summary>
    /// Payload để cập nhật thông tin Dorm (Name, Address).
    /// Code không thay đổi được; CampusId không thay đổi được.
    /// </summary>
    public class UpdateDormDto
    {
        [Required(ErrorMessage = "Tên KTX là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Tên KTX không được vượt quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự")]
        public string? Address { get; set; }
    }
}
