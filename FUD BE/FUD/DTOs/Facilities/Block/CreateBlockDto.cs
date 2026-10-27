using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Block
{
    /// <summary>Body cho POST api/blocks — Tạo block mới</summary>
    public class CreateBlockDto
    {
        [Required(ErrorMessage = "DormId là bắt buộc")]
        public int DormId { get; set; }

        [Required(ErrorMessage = "Code là bắt buộc")]
        [MaxLength(50, ErrorMessage = "Code không vượt quá 50 ký tự")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Name là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Name không vượt quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "Số tầng phải từ 1 đến 100")]
        public int? NumberOfFloors { get; set; }

        [Required(ErrorMessage = "GenderType là bắt buộc")]
        [RegularExpression("^(Male|Female)$", ErrorMessage = "GenderType chỉ chấp nhận 'Male' hoặc 'Female'")]
        public string GenderType { get; set; } = string.Empty;
    }
}
