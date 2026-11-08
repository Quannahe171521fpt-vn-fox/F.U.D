using System;
using System.Collections.Generic;

namespace FUD.DTOs.Checkin
{
    public class PendingCheckinStudentDto
    {
        public int? BookingRequestId { get; set; }
        public int? ResidencyId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Cohort { get; set; }
        public string? Major { get; set; }
        public int BlockId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
    }

    public class RoomEquipmentCheckinDto
    {
        public int EquipmentId { get; set; }
        public int CatalogId { get; set; }
        public string CatalogName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string AssetCode { get; set; } = string.Empty;
        public int? BedId { get; set; }
        public string? BedCode { get; set; }
        public string CurrentCondition { get; set; } = "Good";
        public string? Note { get; set; }
    }

    public class CheckinItemInputDto
    {
        public int EquipmentId { get; set; }
        public string ItemCondition { get; set; } = "Good"; // Good | Damaged | Missing
        public string? Note { get; set; }
    }

    public class CreateCheckinHandoverDto
    {
        public int? BookingRequestId { get; set; }
        public int? ResidencyId { get; set; }
        public int StudentId { get; set; }
        public int RoomId { get; set; }
        public int BedId { get; set; }
        public string? StudentTitle { get; set; }
        public string? OtherItems { get; set; }
        public string? ElectricityL { get; set; }
        public string? ElectricityR { get; set; }
        public string? WaterL { get; set; }
        public string? WaterR { get; set; }
        public List<CheckinItemInputDto> Items { get; set; } = new();
    }

    public class CheckinHandoverItemResponseDto
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public string CatalogName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string AssetCode { get; set; } = string.Empty;
        public string? BedCode { get; set; }
        public int Quantity { get; set; } = 1;
        public string ItemCondition { get; set; } = "Good";
        public string? Note { get; set; }
    }

    public class CheckinHandoverResponseDto
    {
        public int Id { get; set; }
        public int ResidencyId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentTitle { get; set; } = "Sinh viên";
        public int GuardId { get; set; }
        public string GuardCode { get; set; } = string.Empty;
        public string GuardName { get; set; } = string.Empty;
        public int BlockId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string TermName { get; set; } = string.Empty;
        public string Status { get; set; } = "Completed";
        public bool HasDamageOrMissingFlag { get; set; }
        public string? OtherItems { get; set; }
        public string? ElectricityL { get; set; }
        public string? ElectricityR { get; set; }
        public string? WaterL { get; set; }
        public string? WaterR { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public List<CheckinHandoverItemResponseDto> Items { get; set; } = new();
    }
}
