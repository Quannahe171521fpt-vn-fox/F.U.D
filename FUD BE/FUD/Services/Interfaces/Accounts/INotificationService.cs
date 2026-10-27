namespace FUD.Services.Interfaces.Accounts
{
    /// <summary>
    /// Adapter gửi thông báo cho người dùng (email qua Gmail SMTP).
    /// Tầng Service chỉ phụ thuộc interface này.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Gửi email thông báo tài khoản bị tạm ngưng.
        /// Trả về true nếu gửi thành công.
        /// </summary>
        Task<bool> SendAccountSuspendedAsync(string toEmail, string fullName, string reason, string? notes, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gửi email thông báo tài khoản được khôi phục.
        /// Trả về true nếu gửi thành công.
        /// </summary>
        Task<bool> SendAccountRestoredAsync(string toEmail, string fullName, string reason, CancellationToken cancellationToken = default);
    }
}
