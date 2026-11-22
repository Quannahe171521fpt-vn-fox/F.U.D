using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace FUD.Infrastructure.Email
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }

    public class GmailSmtpSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<GmailSmtpSender> _logger;

        public GmailSmtpSender(IConfiguration config, ILogger<GmailSmtpSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(toEmail)) return;

            var enabled = !string.Equals(_config["Email:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
            if (!enabled)
            {
                _logger.LogWarning("[EMAIL SKIPPED] Gửi email đang tắt theo cấu hình. Bỏ qua email tới {Email}", toEmail);
                return;
            }

            var senderEmail = _config["Email:SenderEmail"]
                              ?? _config["Email:Sender"]
                              ?? string.Empty;
            var senderPassword = _config["Email:SenderPassword"]
                                 ?? _config["Email:Password"]
                                 ?? string.Empty;
            var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
            var smtpPort = int.TryParse(_config["Email:SmtpPort"], out var p) && p > 0 ? p : 587;
            var senderName = _config["Email:SenderName"] ?? "Ký túc xá FUD - FPT University";

            if (string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword) ||
                senderEmail.Contains("youremail@", StringComparison.OrdinalIgnoreCase) ||
                senderPassword.Contains("your-gmail", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[EMAIL SKIPPED] Cấu hình Email:SenderEmail hoặc Email:SenderPassword chưa hợp lệ. Bỏ qua email tới {Email}", toEmail);
                return;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = htmlMessage }.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(senderEmail, senderPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[EMAIL SENT] Đã gửi thư thành công tới {Email}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMAIL ERROR] Lỗi khi gửi email qua Gmail SMTP tới {Email}", toEmail);
                // Không ném lỗi ra ngoài làm nghẽn luồng xử lý chính của người dùng
            }
        }
    }
}
