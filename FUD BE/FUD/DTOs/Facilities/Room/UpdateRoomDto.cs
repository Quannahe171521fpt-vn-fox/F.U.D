using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Room
{
    /// <summary>Body cho PUT api/rooms/{id} — Cập nhật thông tin phòng</summary>
    public class UpdateRoomDto
    {
        [Required(ErrorMessage = "RoomTypeId là bắt buộc")]
        public int RoomTypeId { get; set; }

        [Required(ErrorMessage = "RoomNumber là bắt buộc")]
        [MaxLength(50, ErrorMessage = "RoomNumber không vượt quá 50 ký tự")]
        public string RoomNumber { get; set; } = string.Empty;

        [Range(1, 50, ErrorMessage = "Số tầng phải từ 1 đến 50")]
        public int? FloorLevel { get; set; }
    }
}
