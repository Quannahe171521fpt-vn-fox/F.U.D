namespace FUD.DTOs.Facilities.Block
{
    /// <summary>
    /// Response khi GET danh sách / chi tiết Block.
    /// Khớp với cột table UI: Dorm Name | Block Name | Floor | Gender | Status
    /// </summary>
    public class BlockDto
    {
        public int Id { get; set; }
        public int DormId { get; set; }
        public string DormName { get; set; } = string.Empty;       // cột "Dorm Name"
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;           // cột "Block Name"
        public int? NumberOfFloors { get; set; }                   // cột "Floor"
        public string GenderType { get; set; } = string.Empty;     // cột "Gender" (Male/Female)
        public string Status { get; set; } = string.Empty;         // "Active" | "Maintenance"
        public int TotalRooms { get; set; }
    }
}
