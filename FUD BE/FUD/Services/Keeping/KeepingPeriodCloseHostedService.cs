using FUD.Models;
using FUD.Services.Keeping;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Keeping
{
    /// <summary>
    /// Hosted service chạy định kỳ <see cref="KeepingPeriodCloseJob"/>.
    /// Tự động phát hiện các kỳ Giữ phòng (Registration_Period loại Keeping) đã kết thúc
    /// và trả giường về pool booking của kỳ kế tiếp.
    /// </summary>
    public class KeepingPeriodCloseHostedService : BackgroundService
    {
        /// <summary>Chu kỳ quét: 1 lần/giờ.</summary>
        private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KeepingPeriodCloseHostedService> _logger;

        public KeepingPeriodCloseHostedService(
            IServiceScopeFactory scopeFactory,
            ILogger<KeepingPeriodCloseHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("KeepingPeriodCloseHostedService đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEndedKeepingPeriodsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "KeepingPeriodCloseHostedService gặp lỗi khi quét kỳ giữ phòng đã kết thúc.");
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

            _logger.LogInformation("KeepingPeriodCloseHostedService đã dừng.");
        }

        private async Task ProcessEndedKeepingPeriodsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FudContext>();
            var job = scope.ServiceProvider.GetRequiredService<KeepingPeriodCloseJob>();

            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

            // Các kỳ giữ phòng đã kết thúc (EndDate < hôm nay) và chưa được xử lý.
            var endedPeriods = await context.RegistrationPeriods
                .AsNoTracking()
                .Where(p => p.PeriodType == "Keeping" && p.EndDate < today)
                .Select(p => new { p.Id, p.TermId, p.EndDate })
                .ToListAsync(cancellationToken);

            foreach (var period in endedPeriods)
            {
                // Đã xử lý kỳ này rồi → bỏ qua (idempotent).
                // TargetId lưu Id của Registration_Period đã xử lý.
                var alreadyProcessed = await context.AuditLogs
                    .AsNoTracking()
                    .AnyAsync(a => a.TargetEntity == "KeepingPeriodClose"
                                && a.TargetId == period.Id.ToString(),
                                cancellationToken);

                if (alreadyProcessed)
                {
                    continue;
                }

                _logger.LogInformation(
                    "KeepingPeriodCloseHostedService: xử lý kỳ giữ phòng đã kết thúc (TermId={TermId}).",
                    period.TermId);

                await job.ExecuteAsync(period.TermId, period.Id);
            }
        }
    }
}
