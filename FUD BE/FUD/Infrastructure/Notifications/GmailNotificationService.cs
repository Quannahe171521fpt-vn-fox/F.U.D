using System.Net;
using System.Text;
using FUD.Infrastructure.Email;
using FUD.Services.Interfaces.Accounts;

namespace FUD.Infrastructure.Notifications
{
    /// <summary>
    /// Gửi thông báo email tới người dùng, dựa trên <see cref="IEmailSender"/>.
    /// Không throw khi gửi thất bại — caller sẽ ghi NotificationStatus = Failed.
    /// </summary>
    public class GmailNotificationService : INotificationService
    {
        private readonly IEmailSender _emailSender;

        public GmailNotificationService(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public Task<bool> SendAccountSuspendedAsync(string toEmail, string fullName, string reason, string? notes, CancellationToken cancellationToken = default)
        {
            var reasonText = DescribeReason(reason);
            var body = new StringBuilder();
            body.Append($"<p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>");
            body.Append("<p>Tài khoản của bạn đã bị <strong>tạm ngưng (suspended)</strong> trên hệ thống Quản lý Ký túc xá.</p>");
            body.Append($"<p><strong>Lý do:</strong> {WebUtility.HtmlEncode(reasonText)}</p>");
            if (!string.IsNullOrWhiteSpace(notes))
                body.Append($"<p><strong>Ghi chú:</strong> {WebUtility.HtmlEncode(notes)}</p>");
            body.Append("<p>Trong thời gian tạm ngưng, bạn không thể đăng nhập hoặc truy cập các chức năng cần xác thực.</p>");
            body.Append("<p>Vui lòng liên hệ quản trị viên nếu cần hỗ trợ.</p>");
            body.Append("<p>Trân trọng,<br/>Ban quản trị Ký túc xá</p>");

            return _emailSender.SendAsync(toEmail, "Tài khoản của bạn đã bị tạm ngưng", body.ToString(), cancellationToken);
        }

        public Task<bool> SendAccountRestoredAsync(string toEmail, string fullName, string reason, CancellationToken cancellationToken = default)
        {
            var body = new StringBuilder();
            body.Append($"<p>Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>");
            body.Append("<p>Tài khoản của bạn đã được <strong>khôi phục (restore)</strong> và hoạt động trở lại trên hệ thống Quản lý Ký túc xá.</p>");
            body.Append($"<p><strong>Lý do:</strong> {WebUtility.HtmlEncode(reason)}</p>");
            body.Append("<p>Quyền truy cập của bạn được áp dụng theo chính sách hiện tại. Vui lòng đăng nhập lại nếu phiên đăng nhập cũ đã hết hạn.</p>");
            body.Append("<p>Trân trọng,<br/>Ban quản trị Ký túc xá</p>");

            return _emailSender.SendAsync(toEmail, "Tài khoản của bạn đã được khôi phục", body.ToString(), cancellationToken);
        }

        private static string DescribeReason(string reason) => reason switch
        {
            "Violation" => "Vi phạm chính sách ký túc xá",
            "Graduation" => "Đã tốt nghiệp",
            "Termination" => "Đã nghỉ việc / kết thúc hợp tác",
            _ => string.IsNullOrWhiteSpace(reason) ? "Không rõ" : reason
        };
    }
}
