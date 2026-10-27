using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Room
{
    /// <summary>
    /// Query parameters cho GET danh sách Room.
    /// Khớp với form search UI: BlockName (text), RoomNumber (text), Loại phòng (dropdown), Giới tính (dropdown)
    /// </summary>
    public class RoomQueryDto
    {
        public string? BlockName { get; set; }      // ô tìm "Block Name" (text search)
        public string? RoomNumber { get; set; }     // ô tìm "Room Name" (text search)
        public int? RoomTypeId { get; set; }        // dropdown "Loại phòng"
        public string? GenderType { get; set; }     // dropdown "Giới tính" (lọc qua Block.GenderType)
        public string? Status { get; set; }         // "Active" | "Maintenance"

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}
