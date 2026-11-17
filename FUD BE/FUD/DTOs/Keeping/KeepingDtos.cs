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

    /// <summary>
    /// Body khi Ban Quản lý xử lý yêu cầu giữ phòng (duyệt / từ chối).
    /// </summary>
    public class ProcessKeepingRequestDto
    {
        /// <summary>Lý do từ chối (bắt buộc khi Decision = 'Rejected').</summary>
        public string? RejectReason { get; set; }
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

        /// <summary>
        /// Id hóa đơn phí giữ phòng đã tạo (nếu có). Frontend dùng để redirect
        /// sang màn hình thanh toán: /student/payment?invoiceId={InvoiceId}
        /// </summary>
        public int? InvoiceId { get; set; }

        /// <summary>
        /// true nếu đang chờ sinh viên thanh toán phí giữ phòng.
        /// Frontend dùng để hiển thị nút "Thanh toán ngay".
        /// </summary>
        public bool RequiresPayment { get; set; }

        /// <summary>
        /// true nếu đã thanh toán xong và đang chờ Ban Quản lý duyệt.
        /// </summary>
        public bool AwaitingManagerApproval { get; set; }
    }

    /// <summary>
    /// Yêu cầu giữ phòng hiển thị cho Ban Quản lý (màn hình duyệt).
    /// </summary>
    public class KeepingRequestManagerDto
    {
        public int RequestId { get; set; }

        /// <summary>Id của Booking_Request sinh ra hóa đơn phí giữ phòng (nếu có).</summary>
        public int? BookingRequestId { get; set; }

        /// <summary>Id hóa đơn phí giữ phòng (nếu có).</summary>
        public int? InvoiceId { get; set; }

        /// <summary>Mã hóa đơn (để đối soát khi đã thanh toán).</summary>
        public string? InvoiceNumber { get; set; }

        public int StudentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string StudentGender { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        /// <summary>Trạng thái hóa đơn phí giữ phòng (Unpaid / Processing / Paid).</summary>
        public string? InvoiceStatus { get; set; }

        /// <summary>true nếu sinh viên đã thanh toán xong phí giữ phòng.</summary>
        public bool IsPaid { get; set; }

        public int BedId { get; set; }
        public string BedCode { get; set; } = string.Empty;
        public string DormName { get; set; } = string.Empty;
        public string BlockName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;

        public int TermId { get; set; }
        public string TermName { get; set; } = string.Empty;

        /// <summary>Phí giữ phòng (đơn vị VND).</summary>
        public decimal FeeAmount { get; set; }

        public string Currency { get; set; } = "VND";

        public DateTime CreatedTime { get; set; }

        /// <summary>Thời điểm Ban Quản lý xử lý (nếu đã xử lý).</summary>
        public DateTime? ProcessedTime { get; set; }

        /// <summary>Tên người xử lý (nếu có).</summary>
        public string? ProcessedByName { get; set; }

        /// <summary>Lý do từ chối (nếu bị từ chối).</summary>
        public string? RejectReason { get; set; }
    }

    /// <summary>Hằng số nghiệp vụ của module Giữ phòng (UC02).</summary>
    public static class KeepingConstants
    {
        /// <summary>
        /// Loại yêu cầu giữ phòng.
        /// Lưu ý: theo thiết kế DB, lượt giữ phòng được lưu ở bảng Booking_Requests
        /// với BookingKind = 'Keeping' (CHK_Booking_Kind cho phép 'Booking' | 'Keeping'),
        /// chứ không dùng bảng Requests (CHK_Request_Type không có giá trị 'Keeping').
        /// </summary>
        public const string RequestType = "Keeping";

        /// <summary>Loại đăng ký cư trú cho giường được giữ (CHK_Residency_Type cho phép 'Keeping').</summary>
        public const string RegistrationType = "Keeping";

        /// <summary>
        /// Trạng thái yêu cầu giữ phòng khi hoàn tất.
        /// Trạng thái này thuộc về Booking_Request.Status (CHK_Booking_Status).
        /// </summary>
        public const string StatusCompleted = "Completed";

        /// <summary>
        /// Trạng thái yêu cầu giữ phòng khi đã thanh toán xong, đang chờ Ban Quản lý duyệt.
        /// Thực chất trạng thái này được biểu diễn bằng Booking_Request.Status = 'Pending'
        /// (đã thanh toán nhưng chưa được QL duyệt).
        /// </summary>
        public const string StatusPendingApproval = "Pending";

        /// <summary>Trạng thái yêu cầu giữ phòng khi đang chờ sinh viên thanh toán phí giữ phòng.</summary>
        public const string StatusPending = "Pending Payment";

        /// <summary>Trạng thái yêu cầu giữ phòng khi bị hủy.</summary>
        public const string StatusCancelled = "Cancelled";

        /// <summary>
        /// Trạng thái cư trú kỳ kế tiếp (đã giữ phòng, chờ check-in).
        /// Lưu ý: theo DB, CHK_Residency_Status chỉ cho phép
        /// ('Reserved','Active','Ended','Cancelled'). Cư trú kỳ kế tiếp dùng 'Reserved'.
        /// </summary>
        public const string ResidencyUpcoming = "Reserved";

        /// <summary>Loại đăng ký cư trú cho Booking (đối chiếu với Residency.RegistrationType).</summary>
        public const string RegistrationTypeBooking = "Booking";
    }
}
