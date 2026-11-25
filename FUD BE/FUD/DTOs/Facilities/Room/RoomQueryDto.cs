using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Facilities.Room
{
    /// <summary>
    /// Query parameters cho GET danh sách Room.
    /// Khớp với form search UI: BlockName (text), RoomNumber (text), Loại phòng (dropdown), Giới tính (dropdown)
    /// </summary>
    public class RoomQueryDto
    {
        public int? DormId { get; set; }            // lọc theo KTX (ép tự động cho Guard theo phân công)
        public int? BlockId { get; set; }           // dropdown lọc theo Block
        public string? BlockName { get; set; }      // ô tìm "Block Name" (text search)
        public string? RoomNumber { get; set; }     // ô tìm kiếm theo tên phòng (substring search)
        public string? ExactRoomNumber { get; set; }// dropdown lọc chính xác theo tên phòng
        public int? FloorLevel { get; set; }        // dropdown lọc theo tầng
        public int? RoomTypeId { get; set; }        // dropdown "Loại phòng"
        public string? GenderType { get; set; }     // dropdown "Giới tính" (lọc qua Block.GenderType)
        public string? Status { get; set; }         // "Active" | "Maintenance"

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 1000)]
        public int PageSize { get; set; } = 10;
    }
}
