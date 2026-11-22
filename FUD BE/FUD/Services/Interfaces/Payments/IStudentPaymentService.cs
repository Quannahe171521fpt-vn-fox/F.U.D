using FUD.DTOs.Payments;

namespace FUD.Services.Interfaces.Payments
{
    /// <summary>
    /// Nghiệp vụ thanh toán của Sinh viên (UC-Payment).
    ///
    /// - Xem danh sách hóa đơn đang nợ (màn hình Choose Paid Item).
    /// - Tạo giao dịch VietQR cho 1 hoặc nhiều hóa đơn.
    /// - Giả lập callback thành công (kèm side-effects cập nhật Booking/Transfer/Handover).
    /// - Hủy giao dịch và mở khóa lại các hóa đơn.
    /// </summary>
    public interface IStudentPaymentService
    {
    /// <summary>
    /// Lấy danh sách hóa đơn của sinh viên đang nợ:
    /// DeletedAt = null và Status ∈ {Unpaid, Overdue, Processing}.
    /// </summary>
    Task<StudentInvoiceListResponseDto> GetPayableInvoicesAsync(int studentId);

    /// <summary>
    /// Tìm hóa đơn của sinh viên theo nguồn phát sinh (Booking/Keeping/Utility/BedTransfer/Checkout).
    /// Dùng cho Frontend khi URL mang <c>?sourceType=Booking&amp;sourceId=123</c> để tự động tick đúng hóa đơn.
    /// Trả về null nếu không tìm thấy hóa đơn phù hợp.
    /// </summary>
    Task<StudentInvoiceDto?> FindInvoiceBySourceAsync(int studentId, string sourceType, int sourceId);

        /// <summary>
        /// Tạo giao dịch QR cho danh sách hóa đơn được chọn.
        /// Khóa các hóa đơn (Status = Processing) và tạo Transaction_Invoices.
        /// </summary>
        Task<PaymentTransactionDto> CreateTransactionAsync(int studentId, CreatePaymentTransactionDto dto);

        /// <summary>
        /// Giả lập xác nhận thanh toán thành công: chuyển giao dịch sang Success,
        /// các hóa đơn sang Paid và chạy side-effects cập nhật Booking/Transfer/Handover.
        /// </summary>
        Task<PaymentSuccessDto> MockConfirmAsync(int studentId, int transactionId, MockConfirmPaymentDto? dto);

        /// <summary>
        /// Hủy giao dịch: chuyển giao dịch sang Cancelled và mở khóa các hóa đơn
        /// về lại Overdue (nếu quá hạn) hoặc Unpaid.
        /// </summary>
        Task<PaymentCancelResultDto> CancelTransactionAsync(int studentId, int transactionId, CancelPaymentDto? dto);

        /// <summary>
        /// Kiểm tra trạng thái giao dịch (dùng cho Frontend polling chờ SePay Webhook).
        /// Nếu đã Success thì trả kèm thông tin biên lai PaymentSuccessDto.
        /// </summary>
        Task<PaymentTransactionStatusDto> GetTransactionStatusAsync(int studentId, int transactionId);

        /// <summary>
        /// Xử lý Webhook tiền vào từ SePay (Test Mode &amp; Live Mode).
        /// Tự động đối soát nội dung chuyển khoản / số tiền và gạch nợ hóa đơn.
        /// </summary>
        Task<(bool Handled, string Message, PaymentSuccessDto? Receipt)> ProcessSePayWebhookAsync(
            SePayWebhookPayloadDto payload,
            string? authorizationHeader = null);

        /// <summary>
        /// Lấy lịch sử các giao dịch và hóa đơn đã thanh toán của sinh viên.
        /// </summary>
        Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(int studentId);
    }
}
