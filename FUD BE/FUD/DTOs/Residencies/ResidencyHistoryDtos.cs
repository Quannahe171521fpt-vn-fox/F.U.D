using System;
using System.Collections.Generic;

namespace FUD.DTOs.Residencies
{
    public class ResidencyHistoryQueryDto
    {
        public string? Status { get; set; }
        public string? Term { get; set; }
        public string? RegistrationType { get; set; }
        public string? Search { get; set; }
    }

    public class ResidencyHistoryItemDto
    {
        public int Id { get; set; }
        public string ResidencyCode { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? RegistrationType { get; set; }

        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public DateOnly TermStartDate { get; set; }
        public DateOnly TermEndDate { get; set; }

        public string CampusName { get; set; } = string.Empty;
        public string DormCode { get; set; } = string.Empty;
        public string DormName { get; set; } = string.Empty;
        public int BlockId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int? FloorLevel { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;
        public int BedCapacity { get; set; }
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string? PositionInRoom { get; set; }

        public DateOnly? CheckInDate { get; set; }
        public DateOnly? PlannedCheckOutDate { get; set; }
        public DateOnly? ActualCheckOutDate { get; set; }

        public int? SourceRequestId { get; set; }
        public bool HasCheckinHandover { get; set; }
        public int? CheckinHandoverId { get; set; }
        public int ViolationsCount { get; set; }
    }

    public class ResidencyTransferRecordDto
    {
        public int RequestId { get; set; }
        public string Direction { get; set; } = string.Empty; // "Outgoing" | "Incoming"
        public string Status { get; set; } = string.Empty;
        public string FromRoomNumber { get; set; } = string.Empty;
        public string FromBlockCode { get; set; } = string.Empty;
        public string FromBedCode { get; set; } = string.Empty;
        public string ToRoomNumber { get; set; } = string.Empty;
        public string ToBlockCode { get; set; } = string.Empty;
        public string ToBedCode { get; set; } = string.Empty;
        public string TransferReason { get; set; } = string.Empty;
        public DateOnly? TransferDate { get; set; }
        public decimal? PriceDifference { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class ResidencyCheckoutRecordDto
    {
        public int RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateOnly ExpectedCheckOutDate { get; set; }
        public string? Reason { get; set; }
        public bool IsExceptionalCase { get; set; }
        public string? ExceptionNote { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ReviewedTime { get; set; }
        public int? CheckoutHandoverId { get; set; }
        public string? CheckoutHandoverStatus { get; set; }
        public decimal? TotalCompensation { get; set; }
    }

    public class ResidencyInvoiceSummaryDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime IssuedTime { get; set; }
        public DateOnly DueDate { get; set; }
        public DateTime? PaidTime { get; set; }
    }

    public class ResidencyHistoryDetailDto : ResidencyHistoryItemDto
    {
        public string? SourceRequestStatus { get; set; }
        public DateTime? SourceRequestCreatedTime { get; set; }

        public string? CheckinHandoverStatus { get; set; }
        public DateTime? CheckinCompletedTime { get; set; }
        public string? CheckinGuardName { get; set; }
        public bool HasDamageOrMissingAtCheckin { get; set; }

        public List<ResidencyTransferRecordDto> Transfers { get; set; } = new();
        public List<ResidencyCheckoutRecordDto> Checkouts { get; set; } = new();
        public List<ResidencyInvoiceSummaryDto> Invoices { get; set; } = new();
    }
}
