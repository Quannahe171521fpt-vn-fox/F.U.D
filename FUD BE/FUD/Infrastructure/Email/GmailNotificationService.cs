using System;
using System.Threading.Tasks;
using FUD.Settings.pattern;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FUD.Infrastructure.Email;

/// <summary>
/// Adapter gửi email qua Gmail SMTP (MailKit).
/// Service Layer chỉ phụ thuộc <see cref="INotificationService"/>.
/// </summary>
public class GmailNotificationService : INotificationService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<GmailNotificationService> _logger;

    public GmailNotificationService(IOptions<EmailSettings> settings, ILogger<GmailNotificationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return false;

        if (!_settings.Enabled)
        {
            _logger.LogInformation("Gửi email bị tắt theo cấu hình. Bỏ qua email tới {Email} - {Subject}", toEmail, subject);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || string.IsNullOrWhiteSpace(_settings.SenderPassword))
        {
            _logger.LogWarning("Cấu hình Email:SenderEmail hoặc SenderPassword chưa được thiết lập. Bỏ qua email tới {Email}", toEmail);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Đã gửi email '{Subject}' tới {Email}", subject, toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email '{Subject}' tới {Email} thất bại", subject, toEmail);
            return false;
        }
    }

    public Task<bool> SendAccountSuspendedAsync(string toEmail, string fullName, string reason, string? notes)
    {
        var reasonLabel = reason switch
        {
            "violation" => "Vi phạm chính sách KTX",
            "graduation" => "Đã tốt nghiệp",
            "termination" => "Kết thúc công việc / học tập",
            _ => "Lý do khác"
        };

        var body = $@"
            <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937"">
                <h2 style=""color:#b91c1c;margin-bottom:12px"">Tài khoản của bạn đã bị tạm ngưng</h2>
                <p>Xin chào <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
                <p>Tài khoản của bạn trên hệ thống Quản lý Ký túc xá đã bị <strong>tạm ngưng</strong>.</p>
                <ul>
                    <li><strong>Lý do:</strong> {System.Net.WebUtility.HtmlEncode(reasonLabel)}</li>
                    {(string.IsNullOrWhiteSpace(notes) ? string.Empty : $"<li><strong>Ghi chú:</strong> {System.Net.WebUtility.HtmlEncode(notes)}</li>")}
                </ul>
                <p>Trong thời gian bị tạm ngưng, bạn không thể đăng nhập và sử dụng các chức năng của hệ thống.</p>
                <p>Nếu bạn cần hỗ trợ, vui lòng liên hệ Ban Quản lý Ký túc xá.</p>
                <p style=""margin-top:20px;color:#6b7280;font-size:12px"">Trân trọng,<br/>Ban Quản lý Ký túc xá</p>
            </div>";

        return SendEmailAsync(toEmail, "[KTX] Tài khoản của bạn đã bị tạm ngưng", body);
    }

    public Task<bool> SendAccountRestoredAsync(string toEmail, string fullName, string? reason)
    {
        var body = $@"
            <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937"">
                <h2 style=""color:#15803d;margin-bottom:12px"">Tài khoản của bạn đã được khôi phục</h2>
                <p>Xin chào <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p>
                <p>Tài khoản của bạn trên hệ thống Quản lý Ký túc xá đã được <strong>kích hoạt lại</strong>.</p>
                {(string.IsNullOrWhiteSpace(reason) ? string.Empty : $"<p><strong>Lý do khôi phục:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>")}
                <p>Bạn có thể đăng nhập trở lại bằng tài khoản của mình.</p>
                <p style=""margin-top:20px;color:#6b7280;font-size:12px"">Trân trọng,<br/>Ban Quản lý Ký túc xá</p>
            </div>";

        return SendEmailAsync(toEmail, "[KTX] Tài khoản của bạn đã được khôi phục", body);
    }
}
