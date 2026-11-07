using FUD.DTOs.Keeping;

namespace FUD.Services.Interfaces.Keeping
{
    /// <summary>
    /// Kiểm tra điều kiện sinh viên có đủ điều kiện giữ phòng cho kỳ kế tiếp hay không.
    /// Tách riêng khỏi <see cref="IKeepingService"/> để giai đoạn sau thêm thanh toán
    /// không phải viết lại logic kiểm tra.
    /// </summary>
    public interface IKeepingEligibilityService
    {
        /// <summary>
        /// Kiểm tra điều kiện giữ phòng. Luôn trả về kết quả kèm lý do (nếu không đủ điều kiện)
        /// để Frontend hiển thị đúng thông báo.
        /// </summary>
        Task<KeepingEligibilityResponseDto> CheckEligibilityAsync(int studentId);
    }
}
