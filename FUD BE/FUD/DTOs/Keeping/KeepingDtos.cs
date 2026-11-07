using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Keeping
{
    /// <summary>Mã lý do không đủ điều kiện giữ phòng. Frontend map sang thông báo tiếng Việt.</summary>
    public static class KeepingReasonCodes
    {
        /// <summary>Sinh viên không có cư trú (Residency Active) ở kỳ hiện tại.</summary>
        public const string NotResident = "NOT_RESIDENT";

        /// <summary>Không có kỳ Giữ phòng (Keeping) đang mở cho học kỳ kế tiếp.</summary>
        public const string PeriodNotOpen = "PERIOD_NOT_OPEN";

        /// <summary>Học kỳ kế tiếp chưa có bảng giá (Price_List) ở trạng thái Published.</summary>
        public const string PriceNotPublished = "PRICE_NOT_PUBLISHED";

        /// <summary>Sinh viên còn tồn tại hóa đơn chưa thanh toán (Unpaid).</summary>
        public const string HasUnpaidInvoice = "HAS_UNPAID_INVOICE";

        /// <summary>Sinh viên còn tồn tại hóa đơn quá hạn (Overdue).</summary>
        public const string HasOverdueInvoice = "HAS_OVERDUE_INVOICE";

        /// <summary>Sinh viên đã giữ phòng cho học kỳ kế tiếp.</summary>
        public const string AlreadyKept = "ALREADY_KEPT";
    }

    /// <summary>Thông tin chỗ ở hiện tại của sinh viên.</summary>
    public class CurrentStayDto
    {
        public int BedId { get; set; }
        public string DormName { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public string BedCode { get; set; } = string.Empty;
        public int CurrentTermId { get; set; }
        public string CurrentTerm { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>Thông tin học kỳ kế tiếp cần giữ phòng.</summary>
    public class NextTermDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }

    /// <summary>Phí giữ phòng (chỉ để hiển thị, không hard-code vào Request).</summary>
    public class KeepingFeeDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public int Months { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>Khoảng thời gian mở kỳ giữ phòng.</summary>
    public class KeepingPeriodDto
    {
        public DateOnly Start { get; set; }
        public DateOnly End { get; set; }
    }

    /// <summary>Kết quả kiểm tra điều kiện giữ phòng.</summary>
    public class KeepingEligibilityResponseDto
    {
        public bool Eligible { get; set; }

        /// <summary>Mã lý do khi không đủ điều kiện. Xem <see cref="KeepingReasonCodes"/>.</summary>
        public string? ReasonCode { get; set; }

        /// <summary>Thông báo tiếng Việt kèm theo mã lý do.</summary>
        public string? Message { get; set; }

        public CurrentStayDto? CurrentStay { get; set; }

        public NextTermDto? NextTerm { get; set; }

        public KeepingFeeDto? Fee { get; set; }

        public KeepingPeriodDto? Period { get; set; }
    }

    /// <summary>Body khi sinh yêu cầu giữ phòng.</summary>
    public class CreateKeepingRequestDto
    {
        /// <summary>Sinh viên phải đồng ý với nội quy. Bắt buộc là true.</summary>
        [Required]
        public bool AgreedToRegulations { get; set; }
    }

    /// <summary>Thông tin giường được giữ.</summary>
    public class KeepingBedDto
    {
        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string DormName { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
    }

    /// <summary>Thông tin giường trong response giữ phòng.</summary>
    public class KeepingRequestResponseDto
    {
        public int RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RequestType { get; set; } = KeepingConstants.RequestType;
        public NextTermDto? NextTerm { get; set; }
        public KeepingBedDto? Bed { get; set; }
        public decimal? FeeAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Hằng số nghiệp vụ của module Giữ phòng (UC02).</summary>
    public static class KeepingConstants
    {
        /// <summary>Loại yêu cầu. Dùng string trực tiếp như các loại Request khác trong hệ thống.</summary>
        public const string RequestType = "Keeping";

        /// <summary>Loại đăng ký cư trú cho giường được giữ.</summary>
        public const string RegistrationType = "Keeping";

        /// <summary>Trạng thái yêu cầu giữ phòng khi hoàn tất.</summary>
        public const string StatusCompleted = "Completed";

        /// <summary>Trạng thái yêu cầu giữ phòng khi đang chờ (giai đoạn sau khi thêm thanh toán).</summary>
        public const string StatusPending = "Pending";

        /// <summary>Trạng thái yêu cầu giữ phòng khi bị hủy.</summary>
        public const string StatusCancelled = "Cancelled";

        /// <summary>Trạng thái cư trú kỳ kế tiếp (đã giữ phòng, chưa check-in).</summary>
        public const string ResidencyUpcoming = "Upcoming";
    }
}
