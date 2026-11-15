using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Bed
{
    /// <summary>Body cho POST api/beds — Tạo giường mới (Status luôn là Free)</summary>
    public class CreateBedDto
    {
        [Required(ErrorMessage = "RoomId là bắt buộc")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "BedCode là bắt buộc")]
        [MaxLength(50, ErrorMessage = "BedCode không vượt quá 50 ký tự")]
        public string BedCode { get; set; } = string.Empty;

        [MaxLength(50, ErrorMessage = "PositionInRoom không vượt quá 50 ký tự")]
        public string? PositionInRoom { get; set; }
    }
}

