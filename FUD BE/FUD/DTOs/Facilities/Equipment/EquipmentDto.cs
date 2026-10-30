namespace FUD.DTOs.Facilities.Equipment
{
    public class EquipmentDto
    {
        public int Id { get; set; }
        public int CatalogId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string AssetCode { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string? RoomNumber { get; set; }
        public int? BedId { get; set; }
        public string? BedCode { get; set; }
        public string Location { get; set; } = string.Empty; // formatted as "Phòng A101" or "Giường 1/A101"
        public string Condition { get; set; } = "Good"; // 'Good', 'Under repair', 'Broken', 'Liquidated'
        public DateOnly? PurchaseDate { get; set; }
        public string? Note { get; set; }
        public bool HasOpenTicket { get; set; }
    }

    public class EquipmentQueryDto
    {
        public string? Search { get; set; }
        public string? Condition { get; set; }
        public int? RoomId { get; set; }
        public int? CatalogId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class CreateEquipmentDto
    {
        public string AssetCode { get; set; } = string.Empty;
        public int CatalogId { get; set; }
        public int RoomId { get; set; }
        public int? BedId { get; set; }
        public string Condition { get; set; } = "Good";
        public DateOnly? PurchaseDate { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateEquipmentDto
    {
        public int CatalogId { get; set; }
        public int RoomId { get; set; }
        public int? BedId { get; set; }
        public string Condition { get; set; } = "Good";
        public DateOnly? PurchaseDate { get; set; }
        public string? Note { get; set; }
    }

    public class MoveEquipmentDto
    {
        public int RoomId { get; set; }
        public int? BedId { get; set; }
    }

    public class ChangeConditionDto
    {
        public string Condition { get; set; } = string.Empty;
    }

    public class EquipmentCatalogDto
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool ActiveFlag { get; set; }
        public int InUseCount { get; set; }
    }

    public class CreateEquipmentCatalogDto
    {
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class TicketDto
    {
        public int Id { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? HandlingResult { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? DoneTime { get; set; }
    }

    public class LocationOptionDto
    {
        public int RoomId { get; set; }
        public int? BedId { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
