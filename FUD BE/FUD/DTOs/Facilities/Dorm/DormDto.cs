namespace FUD.DTOs.Facilities.Dorm
{
    /// <summary>
    /// Response khi GET danh sách / chi tiết Dorm.
    /// Khớp với cột table UI: Code | Tên KTX | Campus | Địa chỉ | Số tòa | Trạng thái
    /// </summary>
    public class DormDto
    {
        public int Id { get; set; }
        public int CampusId { get; set; }
        public string CampusName { get; set; } = string.Empty;     // cột "Campus"
        public string Code { get; set; } = string.Empty;           // mã KTX
        public string Name { get; set; } = string.Empty;           // cột "Tên KTX"
        public string? Address { get; set; }                        // cột "Địa chỉ"
        public string Status { get; set; } = string.Empty;         // "Active" | "Inactive"
        public int TotalBlocks { get; set; }                       // số tòa hiện có
    }
}
