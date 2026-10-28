using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Room
{
    /// <summary>Body cho POST api/rooms — Tạo phòng mới</summary>
    public class CreateRoomDto
    {
        [Required(ErrorMessage = "BlockId là bắt buộc")]
        public int BlockId { get; set; }

        [Required(ErrorMessage = "RoomTypeId là bắt buộc")]
        public int RoomTypeId { get; set; }

        [Required(ErrorMessage = "RoomNumber là bắt buộc")]
        [MaxLength(50, ErrorMessage = "RoomNumber không vượt quá 50 ký tự")]
        public string RoomNumber { get; set; } = string.Empty;

        [Range(1, 50, ErrorMessage = "Số tầng phải từ 1 đến 50")]
        public int? FloorLevel { get; set; }
    }
}
