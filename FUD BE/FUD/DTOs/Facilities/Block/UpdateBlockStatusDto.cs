using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Block
{
    /// <summary>
    /// Body cho PATCH api/blocks/{id}/status
    /// Tương ứng nút "Bảo trì cả block" trên UI
    /// </summary>
    public class UpdateBlockStatusDto
    {
        [Required(ErrorMessage = "Status là bắt buộc")]
        [RegularExpression("^(Active|Maintenance)$", ErrorMessage = "Status chỉ chấp nhận 'Active' hoặc 'Maintenance'")]
        public string Status { get; set; } = string.Empty;
    }
}
