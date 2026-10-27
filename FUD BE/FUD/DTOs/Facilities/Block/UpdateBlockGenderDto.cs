using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Block
{
    /// <summary>
    /// Body cho PATCH api/blocks/{id}/gender
    /// Tương ứng nút "Đổi giới tính" trên UI
    /// Chỉ được đổi khi block không còn Residency Active (không có SV đang ở)
    /// </summary>
    public class UpdateBlockGenderDto
    {
        [Required(ErrorMessage = "GenderType là bắt buộc")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "GenderType chỉ chấp nhận 'Male' hoặc 'Female'")]
        public string GenderType { get; set; } = string.Empty;
    }
}
