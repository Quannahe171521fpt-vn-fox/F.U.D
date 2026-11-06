using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace FUD.DTOs.Requests
{
    public class CreateRequestDto
    {
        public string RequestType { get; set; } = null!; // Booking, BedTransfer, Checkout, Complaint, Others
        public int? TermId { get; set; }
        public string? Reason { get; set; }

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

        // Trạng thái tài khoản của sinh viên
        public string? StudentAccountStatus { get; set; }
        public string? SuspensionReason { get; set; }
    }

    public class ComplaintDetailDto
    {
        public string TargetType { get; set; } = string.Empty;
        public int? ViolationId { get; set; }
        public string? ViolationCode { get; set; }
        public string? ViolationTypeName { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ResolutionNote { get; set; }
        public List<string> Evidences { get; set; } = new List<string>();
    }
}

