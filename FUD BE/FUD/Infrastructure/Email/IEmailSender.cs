namespace FUD.Infrastructure.Email
{
    /// <summary>
    /// Adapter gửi email. Tầng Service chỉ phụ thuộc interface này,
    /// không phụ thuộc trực tiếp vào SMTP.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>Gửi email. Trả về true nếu thành công, false nếu thất bại (không throw).</summary>
        Task<bool> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
    }
}
