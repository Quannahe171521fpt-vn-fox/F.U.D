using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Bed
{
    /// <summary>Body cho PUT api/beds/{id} — Cập nhật BedCode và PositionInRoom</summary>
    public class UpdateBedDto
    {
        [Required(ErrorMessage = "BedCode là bắt buộc")]
        [MaxLength(50, ErrorMessage = "BedCode không vượt quá 50 ký tự")]
        public string BedCode { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "PositionInRoom không vượt quá 50 ký tự")]
        public string? PositionInRoom { get; set; }
    }
}

