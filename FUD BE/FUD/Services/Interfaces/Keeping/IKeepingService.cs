using FUD.DTOs.Keeping;

namespace FUD.Services.Interfaces.Keeping
{
    /// <summary>
    /// Nghiệp vụ Giữ phòng (UC02).
    ///
    /// Luồng đầy đủ (giai đoạn 2 — đã có thanh toán):
    ///   Sinh viên tạo yêu cầu (Request = 'Pending' + hóa đơn RoomFee 'Unpaid')
    ///     → sinh viên thanh toán (hóa đơn → 'Paid')
    ///     → Request chuyển 'Pending Approval' (chờ Ban Quản lý)
    ///     → Ban Quản lý duyệt
    ///     → Request = 'Completed' + tạo Residency kỳ kế tiếp.
    /// </summary>
    public interface IKeepingService
    {
        /// <summary>
        /// Sinh yêu cầu giữ phòng cho học kỳ kế tiếp.
        /// Tạo Booking_Request (BookingKind = 'Keeping') + Request ('Pending') + hóa đơn phí giữ phòng ('Unpaid').
        /// Frontend redirect sinh viên sang màn hình thanh toán.
        /// </summary>
        Task<KeepingRequestResponseDto> CreateAsync(int studentId, CreateKeepingRequestDto dto);

        /// <summary>
        /// Hoàn tất giữ phòng: chuyển Request sang Completed, tạo Residency kỳ kế tiếp,
        /// ghi Request_Log và gửi thông báo. Được gọi bởi luồng duyệt của Ban Quản lý.
        /// </summary>
        Task<KeepingRequestResponseDto> CompleteKeepingAsync(int requestId);

        /// <summary>
        /// Ban Quản lý phê duyệt yêu cầu giữ phòng đã thanh toán.
        /// Yêu cầu phải ở trạng thái 'Pending Approval' (đã thanh toán, chờ duyệt).
        /// </summary>
        Task<KeepingRequestResponseDto> ApproveByManagerAsync(int requestId, int managerId);

        /// <summary>
        /// Ban Quản lý từ chối yêu cầu giữ phòng đã thanh toán.
        /// </summary>
        Task<KeepingRequestResponseDto> RejectByManagerAsync(int requestId, int managerId, string reason);

        /// <summary>
        /// Lấy danh sách yêu cầu giữ phòng đang chờ Ban Quản lý duyệt (đã thanh toán).
        /// </summary>
        Task<List<KeepingRequestManagerDto>> GetPendingForManagerAsync();

        /// <summary>Lấy chi tiết yêu cầu giữ phòng (dùng cho GET /api/keeping/requests/{id}).</summary>
        Task<KeepingRequestResponseDto?> GetRequestAsync(int requestId);

        /// <summary>Lấy StudentId chủ sở hữu yêu cầu (dùng để kiểm tra quyền xem: 403 nếu không phải chủ).</summary>
        Task<int?> GetRequestOwnerIdAsync(int requestId);
    }
}
