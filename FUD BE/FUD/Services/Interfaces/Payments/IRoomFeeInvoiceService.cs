using FUD.Models;

namespace FUD.Services.Interfaces.Payments
{
    /// <summary>
    /// Tạo hóa đơn phí phòng (RoomFee) cho các luồng Booking / Keeping.
    ///
    /// Hóa đơn được tạo ở trạng thái 'Unpaid' kèm Invoice_Items mô tả khoản phí,
    /// sau đó sinh viên thanh toán qua màn hình Choose Paid Item.
    /// Khi hóa đơn được thanh toán, side-effects ở
    /// <see cref="IStudentPaymentService.MockConfirmAsync"/> sẽ cập nhật
    /// Booking_Request / Request sang trạng thái chờ Ban Quản lý duyệt.
    /// </summary>
    public interface IRoomFeeInvoiceService
    {
        /// <summary>
        /// Tạo hóa đơn phí phòng gắn với một Booking_Request.
        /// Hóa đơn + Invoice_Items được Add vào ChangeTracker (chưa SaveChanges).
        /// </summary>
        Task<Invoice> CreateRoomFeeInvoiceAsync(
            BookingRequest bookingRequest,
            decimal amount,
            string description,
            DateOnly dueDate,
            DateTime issuedTime);

        /// <summary>
        /// Sinh mã hóa đơn duy nhất theo quy ước:
        /// INV-ROOM-{PREFIX}-{seq:000} (ví dụ: INV-ROOM-B26-001, INV-ROOM-K27-002).
        /// </summary>
        Task<string> GenerateInvoiceNumberAsync(string prefix);

        /// <summary>
        /// Lưu thay đổi. Tách riêng để chủ gọi kiểm soát thứ tự ghi:
        /// Invoice_Items phải được thêm khi hóa đơn còn 'Unpaid' vì trigger
        /// trg_InvoiceItems_LockedAfterUnpaid khóa chi tiết khi hóa đơn rời 'Unpaid'.
        /// </summary>
        Task SaveChangesAsync();
    }
}
