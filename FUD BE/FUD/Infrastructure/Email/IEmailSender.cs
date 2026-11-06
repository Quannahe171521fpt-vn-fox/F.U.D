using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

            var senderEmail = _config["Email:Sender"] ?? _config["Email__Sender"];
            var senderPassword = _config["Email:Password"] ?? _config["Email__Password"];

            // Nếu cấu hình mặc định chưa đổi mật khẩu thật, ghi log và giả lập gửi thành công
            if (string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword) ||
                senderPassword.Contains("your-gmail", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[EMAIL MOCK] Gửi tới: {Email} | Chủ đề: {Subject}\nNội dung: {Content}",
                    toEmail, subject, htmlMessage);
                return;
            }

            try
            {
                using var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(senderEmail, "Ký túc xá FUD - FPT University"),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true
                };
                mail.To.Add(toEmail);

                await client.SendMailAsync(mail);
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
