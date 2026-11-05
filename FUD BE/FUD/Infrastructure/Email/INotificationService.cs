using System.Threading.Tasks;

namespace FUD.Infrastructure.Email;

/// <summary>
/// Adapter gửi thông báo cho người dùng. Service Layer chỉ phụ thuộc interface này,
/// không phụ thuộc trực tiếp vào Gmail SMTP.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Gửi email thông báo tới một người dùng.
    /// Trả về true nếu gửi thành công, false nếu thất bại (không ném exception ra ngoài).
    /// </summary>
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);

    /// <summary>Gửi email thông báo tạm ngưng tài khoản.</summary>
    Task<bool> SendAccountSuspendedAsync(string toEmail, string fullName, string reason, string? notes);

    /// <summary>Gửi email thông báo khôi phục tài khoản.</summary>
    Task<bool> SendAccountRestoredAsync(string toEmail, string fullName, string? reason);
}
