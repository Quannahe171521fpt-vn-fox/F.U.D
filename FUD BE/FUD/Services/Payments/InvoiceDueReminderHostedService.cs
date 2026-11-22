using System.Net;
using FUD.DTOs.Payments;
using FUD.Infrastructure.Email;
using FUD.Models;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Payments
{
    /// <summary>
    /// Hosted service chạy định kỳ để quét các hóa đơn chưa thanh toán (Unpaid)
    /// sắp đến hạn trong vòng 3 ngày (today &lt;= DueDate &lt;= today.AddDays(3))
    /// và tự động gửi email + thông báo In-App nhắc nhở sinh viên (idempotent qua Audit_Logs).
    /// </summary>
    public class InvoiceDueReminderHostedService : BackgroundService
    {
        private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(30);
        private const int ReminderDaysBeforeDue = 3;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InvoiceDueReminderHostedService> _logger;

        public InvoiceDueReminderHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<InvoiceDueReminderHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InvoiceDueReminderHostedService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAndSendDueRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "InvoiceDueReminderHostedService gặp lỗi khi quét hóa đơn sắp đến hạn 3 ngày.");
                }

                try
                {
                    await Task.Delay(ScanInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("InvoiceDueReminderHostedService đã dừng.");
        }

        private async Task ScanAndSendDueRemindersAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FudContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var limitDate = today.AddDays(ReminderDaysBeforeDue);

            var dueSoonInvoices = await context.Invoices
                .AsNoTracking()
                .Where(i => i.Status == InvoiceStatuses.Unpaid
                            && i.DeletedAt == null
                            && i.DueDate >= today
                            && i.DueDate <= limitDate)
                .ToListAsync(cancellationToken);

            if (dueSoonInvoices.Count == 0)
            {
                return;
            }

            var targetIds = dueSoonInvoices.Select(i => i.Id.ToString()).ToList();
            var alreadyReminded = await context.AuditLogs
                .AsNoTracking()
                .Where(a => a.TargetEntity == "InvoiceDueReminder3Days"
                            && a.TargetId != null
                            && targetIds.Contains(a.TargetId))
                .Select(a => a.TargetId!)
                .ToListAsync(cancellationToken);

            var remindedSet = alreadyReminded.ToHashSet();
            var pendingInvoices = dueSoonInvoices
                .Where(i => !remindedSet.Contains(i.Id.ToString()))
                .ToList();

            if (pendingInvoices.Count == 0)
            {
                return;
            }

            var studentIds = pendingInvoices.Select(i => i.StudentId).Distinct().ToList();
            var users = await context.Users
                .AsNoTracking()
                .Where(u => studentIds.Contains(u.Id) && u.DeletedAt == null)
                .ToDictionaryAsync(u => u.Id, cancellationToken);

            var addedAny = false;

            foreach (var invoice in pendingInvoices)
            {
                if (!users.TryGetValue(invoice.StudentId, out var user) || string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }

                var key = invoice.Id.ToString();
                var daysLeft = invoice.DueDate.DayNumber - today.DayNumber;
                var daysText = daysLeft == 0 ? "hôm nay" : $"còn {daysLeft} ngày";
                var subject = $"[KTX FUD] Nhắc hạn thanh toán hóa đơn {invoice.InvoiceNumber} ({daysText})";
                var htmlBody = $@"
                    <div style=""font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1e293b;max-width:620px;margin:0 auto;border:1px solid #fed7aa;border-radius:12px;padding:24px"">
                        <h2 style=""color:#c2410c;margin:0 0 12px"">⏰ Nhắc nhở hóa đơn sắp đến hạn thanh toán</h2>
                        <p>Xin chào <strong>{WebUtility.HtmlEncode(user.FullName)}</strong> ({WebUtility.HtmlEncode(user.Code)}),</p>
                        <p>Bạn có hóa đơn tại Ký túc xá FUD sắp đến hạn thanh toán (<strong>{daysText}</strong>):</p>
                        <div style=""background:#fff7ed;border:1px solid #fdba74;border-radius:8px;padding:14px;margin:14px 0;line-height:1.7"">
                            <div><strong>Mã hóa đơn:</strong> <span style=""color:#c2410c;font-weight:700"">{WebUtility.HtmlEncode(invoice.InvoiceNumber)}</span></div>
                            <div><strong>Loại khoản thu:</strong> {WebUtility.HtmlEncode(invoice.InvoiceType)}</div>
                            <div><strong>Số tiền cần thanh toán:</strong> <span style=""color:#b91c1c;font-weight:700;font-size:16px"">{invoice.Amount:N0} đ</span></div>
                            <div><strong>Hạn chót thanh toán:</strong> <strong>{invoice.DueDate:dd/MM/yyyy}</strong></div>
                        </div>
                        <p>Vui lòng đăng nhập Cổng Sinh viên KTX FUD mục <strong>Thanh toán hóa đơn</strong> để quét mã VietQR thanh toán đúng hạn, tránh phát sinh nợ quá hạn làm gián đoạn quyền gửi đơn từ và đăng ký phòng.</p>
                        <p style=""margin-top:20px;color:#64748b;font-size:12px"">Trân trọng,<br/><strong>Ban Quản lý Ký túc xá FUD - Đại học FPT</strong></p>
                    </div>";

                await emailSender.SendEmailAsync(user.Email, subject, htmlBody);

                context.Notifications.Add(new Notification
                {
                    UserId = invoice.StudentId,
                    Channel = "InApp",
                    Title = "Hóa đơn sắp đến hạn thanh toán",
                    Content = $"Hóa đơn {invoice.InvoiceNumber} ({invoice.Amount:N0} đồng) sẽ đến hạn vào ngày {invoice.DueDate:dd/MM/yyyy} ({daysText}). Vui lòng thanh toán đúng hạn.",
                    ReferenceType = "Invoice",
                    ReferenceId = key,
                    ReadFlag = false,
                    SentTime = now
                });

                context.AuditLogs.Add(new AuditLog
                {
                    UserId = invoice.StudentId,
                    ActionType = "REMIND_3DAYS",
                    TargetEntity = "InvoiceDueReminder3Days",
                    TargetId = key,
                    NewValue = $"Reminded {user.Email} for Invoice {invoice.InvoiceNumber} due {invoice.DueDate:yyyy-MM-dd}",
                    CreatedAt = now
                });

                addedAny = true;
            }

            if (addedAny)
            {
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
