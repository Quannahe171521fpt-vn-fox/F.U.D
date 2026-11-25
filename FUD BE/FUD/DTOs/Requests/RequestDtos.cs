using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace FUD.DTOs.Requests
{
    public class CreateRequestDto
    {
        public string RequestType { get; set; } = null!; // BedTransfer, Checkout, Complaint, Others
        public int? TermId { get; set; }
        public string? Reason { get; set; }

        // Dành cho chuyển giường (BedTransfer)
        public int? ToBedId { get; set; }
        public DateOnly? TransferDate { get; set; }

        // Dành cho trả phòng (Checkout)
        public DateOnly? ExpectedCheckOutDate { get; set; }
        public bool IsExceptionalCase { get; set; }
        public string? ExceptionNote { get; set; }

        // Dành cho khiếu nại Complaint
        public string? TargetType { get; set; } // Violation, Handover, Invoice
        public int? TargetViolationId { get; set; }
        public string? Content { get; set; }
        public List<IFormFile>? Evidences { get; set; }
    }

    public class ProcessRequestDto
    {
        public string Decision { get; set; } = null!; // Approved, Rejected, Completed
        public string? Note { get; set; }
        public string? AdjustedLevel { get; set; } // Mức phạt điều chỉnh nếu duyệt khiếu nại vi phạm (Nhắc nhở, Khiển trách, Cảnh cáo...)
        public int? AssignedGuardId { get; set; } // Bảo vệ được phân công kiểm tra phòng khi duyệt chuyển giường / trả phòng (Bước 1)
        public DateOnly? TransferDate { get; set; } // Ngày chuyển dự kiến xác nhận khi duyệt
        public DateOnly? ExpectedCheckOutDate { get; set; } // Ngày trả phòng dự kiến xác nhận khi duyệt
    }

    public class RequestItemDto
    {
        public int Id { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string RoomInfo { get; set; } = string.Empty;
        public string RequestType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? RejectReason { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime? ProcessedTime { get; set; }
        public string? ProcessedByName { get; set; }

        // Thông tin Complaint nếu có
        public ComplaintDetailDto? Complaint { get; set; }

        // Thông tin BedTransfer nếu có
        public BedTransferDetailDto? BedTransfer { get; set; }

        // Thông tin Checkout nếu có
        public CheckoutDetailDto? Checkout { get; set; }

        // Trạng thái tài khoản của sinh viên
        public string? StudentAccountStatus { get; set; }
        public string? SuspensionReason { get; set; }
    }

    public class BedTransferDetailDto
    {
        public int FromResidencyId { get; set; }
        public int FromBedId { get; set; }
        public int FromRoomId { get; set; }
        public int FromDormId { get; set; }
        public string FromBedCode { get; set; } = string.Empty;
        public string FromRoomNumber { get; set; } = string.Empty;
        public string FromBlockCode { get; set; } = string.Empty;
        public string FromBlockName { get; set; } = string.Empty;
        public string FromDormName { get; set; } = string.Empty;
        public int ToBedId { get; set; }
        public string ToBedCode { get; set; } = string.Empty;
        public string ToRoomNumber { get; set; } = string.Empty;
        public string ToBlockCode { get; set; } = string.Empty;
        public string ToBlockName { get; set; } = string.Empty;
        public string ToDormName { get; set; } = string.Empty;
        public string ToRoomTypeName { get; set; } = string.Empty;
        public string TransferReason { get; set; } = string.Empty;
        public DateOnly? TransferDate { get; set; }
        public decimal? OldBedDailyRate { get; set; }
        public decimal? NewBedDailyRate { get; set; }
        public int? DaysStayed { get; set; }
        public int? DaysRemaining { get; set; }
        public decimal? OldBedAmount { get; set; }
        public decimal? NewBedAmount { get; set; }
        public decimal? AmountAlreadyPaid { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? PriceDifference { get; set; }
        public int? AssignedGuardId { get; set; }
        public string? AssignedGuardName { get; set; }
        public string? AssignedGuardCode { get; set; }
        public int? ReviewedById { get; set; }
        public DateTime? ReviewedTime { get; set; }
        public int? NewResidencyId { get; set; }
        public BedTransferHandoverInfoDto? Handover { get; set; }
        public List<BedTransferMeterReadingDto> MeterReadings { get; set; } = new();
        public decimal? UtilityShareAmount { get; set; }
        public decimal? EquipmentCompensationAmount { get; set; }
        public decimal? TotalOutstandingAmount { get; set; }
        public decimal? RefundDueAmount { get; set; }
        public List<BedTransferInvoiceSummaryDto> Invoices { get; set; } = new();
    }

    public class BedTransferHandoverInfoDto
    {
        public int Id { get; set; }
        public string HandoverCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string InspectorName { get; set; } = string.Empty;
        public string? InspectionNote { get; set; }
        public decimal TotalCompensation { get; set; }
        public bool StudentAcceptedFlag { get; set; }
        public DateTime? StudentAcceptedTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public List<BedTransferHandoverItemDto> Items { get; set; } = new();
    }

    public class BedTransferHandoverItemDto
    {
        public int Id { get; set; }
        public int EquipmentId { get; set; }
        public string EquipmentCode { get; set; } = string.Empty;
        public string EquipmentName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ItemCondition { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal CompensationAmount { get; set; }
    }

    public class BedTransferMeterReadingDto
    {
        public int Id { get; set; }
        public string MeterType { get; set; } = string.Empty; // Electricity, Water
        public decimal PreviousValue { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal Consumption { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalCost { get; set; }
        public decimal StudentShareCost { get; set; }
        public int ResidentCount { get; set; }
        public DateTime RecordedTime { get; set; }
        public string? Note { get; set; }
        public string? EvidenceImageUrl { get; set; }
    }

    public class BedTransferInvoiceSummaryDto
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateOnly IssueDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public string? Description { get; set; }
    }

    public class GuardLookupDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string GuardCode { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public int AssignedDormId { get; set; }
        public string AssignedDormName { get; set; } = string.Empty;
    }

    public class TransferInspectionTaskDto
    {
        public int RequestId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string? StudentPhone { get; set; }
        public int FromResidencyId { get; set; }
        public string FromDormName { get; set; } = string.Empty;
        public string FromBlockName { get; set; } = string.Empty;
        public int FromRoomId { get; set; }
        public string FromRoomNumber { get; set; } = string.Empty;
        public int FromBedId { get; set; }
        public string FromBedCode { get; set; } = string.Empty;
        public string ToDormName { get; set; } = string.Empty;
        public string ToBlockName { get; set; } = string.Empty;
        public string ToRoomNumber { get; set; } = string.Empty;
        public string ToBedCode { get; set; } = string.Empty;
        public DateOnly? TransferDate { get; set; }
        public string TransferReason { get; set; } = string.Empty;
        public int? AssignedGuardId { get; set; }
        public string? AssignedGuardName { get; set; }
        public decimal PreviousElectricityReading { get; set; }
        public decimal PreviousWaterReading { get; set; }
        public decimal ElectricityUnitPrice { get; set; }
        public decimal WaterUnitPrice { get; set; }
        public int ActiveResidentCount { get; set; }
        public bool HandoverCompleted { get; set; }
        public BedTransferHandoverInfoDto? Handover { get; set; }
        public List<BedTransferMeterReadingDto> MeterReadings { get; set; } = new();
    }

    public class SubmitTransferInspectionDto
    {
        public decimal CurrentElectricityReading { get; set; }
        public decimal CurrentWaterReading { get; set; }
        public string? ElectricityEvidenceImageUrl { get; set; }
        public string? WaterEvidenceImageUrl { get; set; }
        public string? Note { get; set; }
        public List<TransferInspectionItemInputDto> Items { get; set; } = new();
    }

    public class TransferInspectionItemInputDto
    {
        public int EquipmentId { get; set; }
        public string ItemCondition { get; set; } = "Good"; // Good, Damaged, Missing
        public string? Description { get; set; }
        public decimal CompensationAmount { get; set; }
    }

    public class BedTransferContextDto
    {
        public bool Eligible { get; set; }
        public string? IneligibleReason { get; set; }
        public CurrentResidencyTransferDto? CurrentResidency { get; set; }
        public List<AvailableTransferBedDto> AvailableBeds { get; set; } = new();
    }

    public class CurrentResidencyTransferDto
    {
        public int ResidencyId { get; set; }
        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public DateOnly TermStartDate { get; set; }
        public DateOnly TermEndDate { get; set; }
        public string DormName { get; set; } = string.Empty;
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string RoomTypeName { get; set; } = string.Empty;
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string? PositionInRoom { get; set; }
        public DateOnly? CheckInDate { get; set; }
        public decimal? CurrentTermPrice { get; set; }
    }

    public class AvailableTransferBedDto
    {
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string? PositionInRoom { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int? FloorLevel { get; set; }
        public int RoomTypeId { get; set; }
        public string RoomTypeName { get; set; } = string.Empty;
        public int BedCapacity { get; set; }
        public int BlockId { get; set; }
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int DormId { get; set; }
        public string DormCode { get; set; } = string.Empty;
        public string DormName { get; set; } = string.Empty;
        public decimal? TermPrice { get; set; }
    }

    public class ComplaintDetailDto
    {
        public string TargetType { get; set; } = string.Empty;
        public int? ViolationId { get; set; }
        public string? ViolationCode { get; set; }
        public string? ViolationTypeName { get; set; }
        public string? InitialLevel { get; set; }
        public string? FinalLevel { get; set; }
        public string? ViolationStatus { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ResolutionNote { get; set; }
        public List<string> Evidences { get; set; } = new List<string>();
    }

    public class CheckoutContextDto
    {
        public bool Eligible { get; set; }
        public string? IneligibleReason { get; set; }
        public bool HasOverdueInvoices { get; set; }
        public CurrentResidencyTransferDto? CurrentResidency { get; set; }
    }

    public class CheckoutDetailDto
    {
        public int ResidencyId { get; set; }
        public int BedId { get; set; }
        public int RoomId { get; set; }
        public int DormId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public string BlockCode { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string DormName { get; set; } = string.Empty;
        public DateOnly ExpectedCheckOutDate { get; set; }
        public DateOnly? ActualCheckOutDate { get; set; }
        public bool IsExceptionalCase { get; set; }
        public string? ExceptionNote { get; set; }
        public int? AssignedGuardId { get; set; }
        public string? AssignedGuardName { get; set; }
        public string? AssignedGuardCode { get; set; }
        public int? ReviewedById { get; set; }
        public DateTime? ReviewedTime { get; set; }
        public BedTransferHandoverInfoDto? Handover { get; set; }
        public List<BedTransferMeterReadingDto> MeterReadings { get; set; } = new();
        public decimal? UtilityShareAmount { get; set; }
        public decimal? EquipmentCompensationAmount { get; set; }
        public decimal? TotalOutstandingAmount { get; set; }
        public List<BedTransferInvoiceSummaryDto> Invoices { get; set; } = new();
    }

    public class CheckoutInspectionTaskDto
    {
        public int RequestId { get; set; }
        public string RequestCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string? StudentPhone { get; set; }
        public int ResidencyId { get; set; }
        public string DormName { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public DateOnly ExpectedCheckOutDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public int? AssignedGuardId { get; set; }
        public string? AssignedGuardName { get; set; }
        public decimal PreviousElectricityReading { get; set; }
        public decimal PreviousWaterReading { get; set; }
        public decimal ElectricityUnitPrice { get; set; }
        public decimal WaterUnitPrice { get; set; }
        public int ActiveResidentCount { get; set; }
        public bool HandoverCompleted { get; set; }
        public BedTransferHandoverInfoDto? Handover { get; set; }
        public List<BedTransferMeterReadingDto> MeterReadings { get; set; } = new();
    }

    public class SubmitCheckoutInspectionDto
    {
        public bool BelongingsCleared { get; set; }
        public decimal CurrentElectricityReading { get; set; }
        public decimal CurrentWaterReading { get; set; }
        public string? ElectricityEvidenceImageUrl { get; set; }
        public string? WaterEvidenceImageUrl { get; set; }
        public string? Note { get; set; }
        public List<TransferInspectionItemInputDto> Items { get; set; } = new();
    }
}

