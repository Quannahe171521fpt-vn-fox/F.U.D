using System;
using System.Collections.Generic;

namespace FUD.DTOs.Booking;

public class BookingEligibilityResponseDto
{
    public bool Eligible { get; set; }
    public string? IneligibleReason { get; set; }
    public bool IsBookingPeriodOpen { get; set; }
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public int? NextTermId { get; set; }
    public string? NextTermName { get; set; }
    public string? StudentGender { get; set; }
    public BookingRequestResponseDto? CurrentBooking { get; set; }
}

public class BookingAvailableDormDto
{
    public int DormId { get; set; }
    public string DormName { get; set; } = string.Empty;
    public string DormCode { get; set; } = string.Empty;
    public int TotalAvailableBeds { get; set; }
}

public class BookingAvailableBlockDto
{
    public int BlockId { get; set; }
    public string BlockName { get; set; } = string.Empty;
    public string BlockCode { get; set; } = string.Empty;
    public string GenderType { get; set; } = string.Empty;
    public int DormId { get; set; }
    public int TotalAvailableBeds { get; set; }
}

public class BookingAvailableRoomDto
{
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public int? FloorLevel { get; set; }
    public int RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public int BedCapacity { get; set; }
    public int FreeBeds { get; set; }
    public int BlockId { get; set; }
}

public class BookingAvailableBedDto
{
    public int BedId { get; set; }
    public string BedCode { get; set; } = string.Empty;
    public string? PositionInRoom { get; set; }
    public int RoomId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public decimal? TermPrice { get; set; }
}

public class CreateBookingRequestDto
{
    public int BedId { get; set; }
    public bool AgreementToRegulations { get; set; }
}

public class BookingRequestResponseDto
{
    public int RequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int BedId { get; set; }
    public string BedCode { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string BlockName { get; set; } = string.Empty;
    public string DormName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public decimal? TermPrice { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime PaymentDeadline { get; set; }
    public bool AgreementToRegulations { get; set; }
    public string? RejectReason { get; set; }
}

public class BookingRequestManagerDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentGender { get; set; } = string.Empty;
    public int BedId { get; set; }
    public string BedCode { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string BlockName { get; set; } = string.Empty;
    public string DormName { get; set; } = string.Empty;
    public int TermId { get; set; }
    public string TermName { get; set; } = string.Empty;
    public decimal? TermPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public DateTime? ProcessedTime { get; set; }
    public string? ProcessedByName { get; set; }
    public string? RejectReason { get; set; }
}

public class ProcessBookingDto
{
    public string Decision { get; set; } = string.Empty; // "Approved" | "Rejected"
    public string? RejectReason { get; set; }
}

