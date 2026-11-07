using FUD.DTOs.Keeping;

namespace FUD.Services.Interfaces.Keeping
{
    /// <summary>
    /// Nghiệp vụ Giữ phòng (UC02).
    ///
    /// Giai đoạn 1 (hiện tại): <see cref="CreateAsync"/> gọi thẳng <see cref="CompleteKeepingAsync"/>.
    /// Giai đoạn 2 (thêm thanh toán): <see cref="CreateAsync"/> chỉ tạo Request ở trạng thái Pending
    /// + tạo Invoice; <see cref="CompleteKeepingAsync"/> được gọi từ callback thanh toán.
    /// </summary>
    public interface IKeepingService
    {
        /// <summary>
        /// Sinh yêu cầu giữ phòng cho học kỳ kế tiếp.
        /// Giai đoạn 1: tạo Request + Residency + Request_Log + thông báo, tất cả trong 1 transaction.
        /// </summary>
        Task<KeepingRequestResponseDto> CreateAsync(int studentId, CreateKeepingRequestDto dto);

        /// <summary>
        /// Hoàn tất giữ phòng: chuyển Request sang Completed, tạo Residency kỳ kế tiếp,
        /// ghi Request_Log và gửi thông báo. Giai đoạn 2 sẽ gọi hàm này từ callback thanh toán.
        /// </summary>
        Task<KeepingRequestResponseDto> CompleteKeepingAsync(int requestId);

        /// <summary>Lấy chi tiết yêu cầu giữ phòng (dùng cho GET /api/keeping/requests/{id}).</summary>
        Task<KeepingRequestResponseDto?> GetRequestAsync(int requestId);

        /// <summary>Lấy StudentId chủ sở hữu yêu cầu (dùng để kiểm tra quyền xem: 403 nếu không phải chủ).</summary>
        Task<int?> GetRequestOwnerIdAsync(int requestId);
    }
}
