namespace FUD.DTOs.Booking
{
    /// <summary>
    /// Hằng số trạng thái của Booking_Request (CHK_Booking_Status).
    /// </summary>
    public static class BookingStatusConstants
    {
        /// <summary>Vừa tạo, chờ Ban Quản lý kiểm tra điều kiện/giường trống.</summary>
        public const string Pending = "Pending";

        /// <summary>Đã được duyệt về điều kiện, đang chờ sinh viên thanh toán phí phòng.</summary>
        public const string PendingPayment = "Pending Payment";

        /// <summary>Đã thanh toán và đã được Ban Quản lý phê duyệt.</summary>
        public const string Approved = "Approved";

        /// <summary>Đã hoàn tất toàn bộ quy trình (đã check-in).</summary>
        public const string Completed = "Completed";

        /// <summary>Bị từ chối.</summary>
        public const string Rejected = "Rejected";

        /// <summary>Bị hủy.</summary>
        public const string Cancelled = "Cancelled";

        /// <summary>Hết hạn.</summary>
        public const string Expired = "Expired";
    }

    /// <summary>
    /// Hằng số nghiệp vụ của module Đặt phòng.
    /// </summary>
    public static class BookingKindConstants
    {
        /// <summary>Đặt phòng mới.</summary>
        public const string Booking = "Booking";

        /// <summary>Giữ phòng hiện tại cho kỳ kế tiếp.</summary>
        public const string Keeping = "Keeping";
    }
}
