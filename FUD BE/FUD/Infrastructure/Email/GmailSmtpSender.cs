using FUD.Settings.pattern;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FUD.Infrastructure.Email
{
    /// <summary>Gửi email qua Gmail SMTP (smtp.gmail.com:587 + STARTTLS).</summary>
    public class GmailSmtpSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<GmailSmtpSender> _logger;

        public GmailSmtpSender(IOptions<EmailSettings> settings, ILogger<GmailSmtpSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || string.IsNullOrWhiteSpace(_settings.SenderPassword))
            {
                _logger.LogWarning("Cấu hình Email chưa đầy đủ, bỏ qua gửi email tới {Email}", toEmail);
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
                await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, cancellationToken);
                await client.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword, cancellationToken);
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Đã gửi email '{Subject}' tới {Email}", subject, toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email '{Subject}' tới {Email} thất bại", subject, toEmail);
                return false;
            }
        }
    }
}
