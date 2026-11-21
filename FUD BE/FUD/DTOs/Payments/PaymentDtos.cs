using System.ComponentModel.DataAnnotations;

namespace FUD.DTOs.Payments
{
    // ============================================================
    //  DTOs cho luồng thanh toán của Sinh viên (VietQR + giả lập thành công)
    // ============================================================

    /// <summary>Trạng thái hóa đơn (CHK_Invoice_Status).</summary>
    public static class InvoiceStatuses
    {
        public const string Unpaid = "Unpaid";
        public const string Processing = "Processing";
        public const string Paid = "Paid";
        public const string Overdue = "Overdue";
        public const string Cancelled = "Cancelled";
    }

    /// <summary>Loại hóa đơn (CHK_Invoice_Type).</summary>
    public static class InvoiceTypes
    {
        public const string RoomFee = "RoomFee";
        public const string Utility = "Utility";
        public const string BedTransfer = "BedTransfer";
        public const string Compensation = "Compensation";
    }

    /// <summary>Trạng thái giao dịch thanh toán (CHK_Payment_Status).</summary>
    public static class PaymentTransactionStatuses
    {
        public const string Processing = "Processing";
        public const string Success = "Success";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
    }

    /// <summary>Chi tiết một dòng khoản phí trong hóa đơn (Invoice_Items).</summary>
    public class InvoiceItemDto
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    /// <summary>Một hóa đơn của sinh viên dùng để hiển thị trên màn hình Choose Paid Item.</summary>
    public class StudentInvoiceDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int TermId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceType { get; set; } = string.Empty;
        public string? AdjustmentKind { get; set; }

        /// <summary>Nguồn phát sinh: Booking | Keeping | Transfer | Utility | Checkout ...</summary>
        public string? SourceType { get; set; }
        public int? SourceId { get; set; }

        public decimal Amount { get; set; }
        public DateTime IssuedTime { get; set; }
        public DateOnly DueDate { get; set; }
        public DateTime? PaidTime { get; set; }
        public string Status { get; set; } = string.Empty;

        /// <summary>true nếu hóa đơn đã quá hạn (DueDate &lt; hôm nay).</summary>
        public bool IsOverdue { get; set; }

        /// <summary>Số ngày quá hạn (0 nếu chưa quá hạn).</summary>
        public int OverdueDays { get; set; }

        /// <summary>Chi tiết khoản phí (Invoice_Items). Có thể rỗng.</summary>
        public List<InvoiceItemDto> Items { get; set; } = new();

        /// <summary>true nếu hóa đơn đang có phiên QR chưa hết hạn (Status = Processing).</summary>
        public bool HasActiveTransaction { get; set; }

        /// <summary>Id giao dịch đang xử lý (nếu có) để Frontend gọi "Tiếp tục thanh toán" / "Hủy phiên cũ".</summary>
        public int? ActiveTransactionId { get; set; }

        /// <summary>Thời điểm hết hạn của phiên QR đang chạy (nếu có).</summary>
        public DateTime? ActiveTransactionExpiryTime { get; set; }

        /// <summary>true nếu phiên QR hiện tại còn hiệu lực (chưa quá ExpiryTime).</summary>
        public bool ActiveTransactionNotExpired { get; set; }
    }

    /// <summary>Danh sách hóa đơn + tổng nợ của sinh viên.</summary>
    public class StudentInvoiceListResponseDto
    {
        public List<StudentInvoiceDto> Items { get; set; } = new();

        /// <summary>Tổng số tiền đang nợ (chỉ tính Unpaid + Overdue).</summary>
        public decimal TotalOutstanding { get; set; }

        /// <summary>Số hóa đơn quá hạn (để Frontend hiện banner cảnh báo).</summary>
        public int OverdueCount { get; set; }

        /// <summary>Thời điểm server xử lý yêu cầu (frontend dùng để đồng bộ countdown).</summary>
        public DateTime ServerTime { get; set; } = DateTime.Now;
    }

    /// <summary>Body cho endpoint tạo giao dịch QR.</summary>
    public class CreatePaymentTransactionDto
    {
        [Required(ErrorMessage = "Danh sách hóa đơn không được để trống.")]
        [MinLength(1, ErrorMessage = "Phải chọn ít nhất một hóa đơn để thanh toán.")]
        public List<int> InvoiceIds { get; set; } = new();
    }

    /// <summary>Kết quả tạo giao dịch QR (màn hình quét mã).</summary>
    public class PaymentTransactionDto
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public string Method { get; set; } = string.Empty;
        public string GatewayReference { get; set; } = string.Empty;
        public string? QrLink { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ReceiptNumber { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime ExpiryTime { get; set; }
        public DateTime? PaidTime { get; set; }
        public DateTime? CallbackTime { get; set; }

        /// <summary>Các hóa đơn được gộp vào giao dịch này.</summary>
        public List<TransactionInvoiceDto> Invoices { get; set; } = new();

        /// <summary>Số giây còn lại của phiên QR (tính ở server).</summary>
        public int RemainingSeconds { get; set; }

        /// <summary>Link ảnh QR chuẩn SePay (https://qr.sepay.vn/img?...).</summary>
        public string? SePayQrLink { get; set; }

        /// <summary>Mã ngân hàng nhận thanh toán SePay (BIDV).</summary>
        public string SePayBankCode { get; set; } = "BIDV";

        /// <summary>Số tài khoản VA nhận thanh toán SePay (96247FUD26 - BIDV 4271030639).</summary>
        public string SePayAccountNumber { get; set; } = "96247FUD26";

        /// <summary>Tên chủ tài khoản hiển thị trên SePay.</summary>
        public string SePayAccountName { get; set; } = "LE ANH QUANG (TK: 4271030639)";
    }

    /// <summary>Payload Webhook chuẩn từ SePay gửi về khi có giao dịch tiền vào.</summary>
    public class SePayWebhookPayloadDto
    {
        public long Id { get; set; }
        public string? Gateway { get; set; }
        public string? TransactionDate { get; set; }
        public string? AccountNumber { get; set; }
        public string? SubAccount { get; set; }
        public string? Code { get; set; }
        public string? Content { get; set; }
        public string? TransferType { get; set; }
        public decimal TransferAmount { get; set; }
        public decimal Accumulated { get; set; }
        public string? ReferenceCode { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>Trạng thái giao dịch dùng cho Frontend polling chờ Webhook SePay.</summary>
    public class PaymentTransactionStatusDto
    {
        public int TransactionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RemainingSeconds { get; set; }
        public PaymentSuccessDto? Receipt { get; set; }
    }

    /// <summary>Một bản ghi lịch sử giao dịch / hóa đơn đã thanh toán của sinh viên.</summary>
    public class PaymentHistoryItemDto
    {
        public int TransactionId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string GatewayReference { get; set; } = string.Empty;
        public string Method { get; set; } = "VietQR";
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Success";
        public DateTime PaidTime { get; set; }
        public List<TransactionInvoiceDto> Invoices { get; set; } = new();
    }

    /// <summary>Một hóa đơn được gộp vào giao dịch thanh toán.</summary>
    public class TransactionInvoiceDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string InvoiceType { get; set; } = string.Empty;
        public decimal AllocatedAmount { get; set; }
        public DateOnly DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<InvoiceItemDto> Items { get; set; } = new();
    }

    /// <summary>Body cho endpoint giả lập xác nhận thanh toán (mock callback).</summary>
    public class MockConfirmPaymentDto
    {
        /// <summary>Mã tham chiếu gateway (tuỳ chọn – dùng để đối soát trước khi xác nhận).</summary>
        public string? GatewayReference { get; set; }
    }

    /// <summary>Body cho endpoint hủy giao dịch.</summary>
    public class CancelPaymentDto
    {
        /// <summary>Lý do hủy (tuỳ chọn).</summary>
        public string? Reason { get; set; }
    }

    /// <summary>Kết quả thanh toán thành công (màn hình biên lai).</summary>
    public class PaymentSuccessDto
    {
        public int TransactionId { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
        public string GatewayReference { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public DateTime PaidTime { get; set; }
        public DateTime CallbackTime { get; set; }

        /// <summary>Danh sách hóa đơn đã thanh toán.</summary>
        public List<TransactionInvoiceDto> Invoices { get; set; } = new();

        /// <summary>Các thông báo side-effect đã ghi (booking/transfer/handover) để hiển thị cho FE.</summary>
        public List<string> SideEffectNotes { get; set; } = new();
    }

    /// <summary>Kết quả hủy giao dịch: các hóa đơn đã được mở khóa về trạng thái trước.</summary>
    public class PaymentCancelResultDto
    {
        public int TransactionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CancelledTime { get; set; }

        /// <summary>Danh sách hóa đơn đã được mở khóa kèm trạng thái mới.</summary>
        public List<UnlockedInvoiceDto> UnlockedInvoices { get; set; } = new();
    }

    /// <summary>Hóa đơn được mở khóa sau khi hủy giao dịch.</summary>
    public class UnlockedInvoiceDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PreviousStatus { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
    }
}
