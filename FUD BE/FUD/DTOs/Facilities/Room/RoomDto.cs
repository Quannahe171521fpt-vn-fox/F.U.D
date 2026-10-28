namespace FUD.DTOs.Facilities.Room
{
    /// <summary>
    /// Response khi GET danh sách / chi tiết Room.
    /// Khớp với cột table UI: Block Name | Room Name | RoomType | Status
    /// </summary>
    public class RoomDto
    {
        public int Id { get; set; }
        public int BlockId { get; set; }
        public string BlockName { get; set; } = string.Empty;           // cột "Block Name"
        public string BlockGenderType { get; set; } = string.Empty;     // để FE filter giới tính
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;        // cột "RoomType" (vd: "Phòng 6 người")
        public int BedCapacity { get; set; }                            // số giường theo RoomType
        public string RoomNumber { get; set; } = string.Empty;          // cột "Room Name"
        public int? FloorLevel { get; set; }
        public string Status { get; set; } = string.Empty;              // "Active" | "Maintenance"
        public int TotalBeds { get; set; }                              // tổng số giường
        public int FreeBeds { get; set; }                               // số giường trống (Status = 'Free')
    }
}
