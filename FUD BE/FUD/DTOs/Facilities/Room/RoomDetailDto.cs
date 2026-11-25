using System;
using System.Collections.Generic;

namespace FUD.DTOs.Facilities.Room
{
    public class RoomDetailDto
    {
        public int Id { get; set; }
        public int DormId { get; set; }
        public string DormName { get; set; } = string.Empty;
        public int BlockId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string BlockGenderType { get; set; } = string.Empty;
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;
        public int BedCapacity { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int? FloorLevel { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalBeds { get; set; }
        public int FreeBeds { get; set; }
        public int OccupiedBeds { get; set; }
        public int CheckedInCount { get; set; }
        public List<RoomCheckedInStudentDto> CheckedInStudents { get; set; } = new();
        public List<RoomBedOccupancyDto> Beds { get; set; } = new();
    }

    public class RoomCheckedInStudentDto
    {
        public int ResidencyId { get; set; }
        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Major { get; set; }
        public string? Cohort { get; set; }
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string? PositionInRoom { get; set; }
        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;
        public string? RegistrationType { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public DateOnly? PlannedCheckOutDate { get; set; }
        public int? HandoverId { get; set; }
        public DateTime? HandoverCompletedTime { get; set; }
    }

    public class RoomBedOccupancyDto
    {
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string? PositionInRoom { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CheckedInStudentCode { get; set; }
        public string? CheckedInStudentName { get; set; }
    }

    public class GuardAssignedBlockDto
    {
        public int Id { get; set; }
        public int DormId { get; set; }
        public string DormName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GenderType { get; set; } = string.Empty;
        public int? NumberOfFloors { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<GuardRoomLookupItemDto> Rooms { get; set; } = new();
    }

    public class GuardRoomLookupItemDto
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int? FloorLevel { get; set; }
    }
}
