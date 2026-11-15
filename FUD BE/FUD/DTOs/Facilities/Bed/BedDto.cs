namespace FUD.DTOs.Facilities.Bed
{
    /// <summary>
    /// Response khi GET danh sách / chi tiết Bed.
    /// </summary>
    public class BedDto
    {
        public int     Id             { get; set; }
        public int     RoomId         { get; set; }
        public string  RoomNumber     { get; set; } = string.Empty;  // "101L"
        public string  BlockName      { get; set; } = string.Empty;  // "Block 101"
        public string  BedCode        { get; set; } = string.Empty;  // "101L-B1"
        public string? PositionInRoom { get; set; }                  // "Top","Bottom",null
        public string  Status         { get; set; } = string.Empty;  // "Free"|"Holding"|"Pending Transfer"|"Occupied"|"Maintenance"
    }
}

