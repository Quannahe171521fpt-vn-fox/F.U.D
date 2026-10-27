using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Block
{
    /// <summary>Body cho PUT api/blocks/{id} — Cập nhật thông tin block</summary>
    public class UpdateBlockDto
    {
        [Required(ErrorMessage = "Name là bắt buộc")]
        [MaxLength(100, ErrorMessage = "Name không vượt quá 100 ký tự")]
        public string Name { get; set; } = string.Empty;

        [Range(1, 100, ErrorMessage = "Số tầng phải từ 1 đến 100")]
        public int? NumberOfFloors { get; set; }
    }
}
