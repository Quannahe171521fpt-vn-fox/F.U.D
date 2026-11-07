using FUD.DTOs.Keeping;
using FUD.Models;
using FUD.Repositories.Interfaces.Keeping;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Keeping
{
    /// <summary>
    /// Job đóng kỳ Giữ phòng (UC02).
    ///
    /// Chạy sau khi kỳ Giữ phòng kết thúc: với mỗi cư dân Active của kỳ hiện tại
    /// KHÔNG có yêu cầu giữ phòng Completed → trả giường về pool booking của kỳ kế tiếp.
    /// Giường đã được giữ (có Keeping Completed) thì KHÔNG đụng tới.
    ///
    /// Idempotent: chạy lại nhiều lần không thay đổi kết quả.
    /// </summary>
    public class KeepingPeriodCloseJob
    {
        private const int MaxRetries = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly IKeepingRepository _keepingRepository;
        private readonly FudContext _context;
        private readonly ILogger<KeepingPeriodCloseJob> _logger;

        public KeepingPeriodCloseJob(
            IKeepingRepository keepingRepository,
            FudContext context,
            ILogger<KeepingPeriodCloseJob> logger)
        {
            _keepingRepository = keepingRepository;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Xử lý đóng kỳ giữ phòng cho một học kỳ kế tiếp cụ thể.
        /// </summary>
        /// <param name="nextTermId">Id học kỳ kế tiếp (kỳ giữ phòng vừa đóng).</param>
        /// <param name="periodId">
        /// Id của Registration_Period đã đóng (dùng để đánh dấu đã xử lý, giúp job idempotent
        /// khi Hosted Service quét lại). Bỏ trống nếu gọi thủ công.
        /// </param>
        public async Task<KeepingPeriodCloseResult> ExecuteAsync(int nextTermId, int? periodId = null)
        {
            Exception? lastError = null;

            for (var attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    return await RunOnceAsync(nextTermId, periodId);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogError(ex,
                        "KeepingPeriodCloseJob thất bại (lần {Attempt}/{MaxRetries}) cho term {TermId}",
                        attempt, MaxRetries, nextTermId);

                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelay);
                    }
                }
            }

            throw new InvalidOperationException(
                $"KeepingPeriodCloseJob thất bại sau {MaxRetries} lần thử cho term {nextTermId}.", lastError);
        }

        private async Task<KeepingPeriodCloseResult> RunOnceAsync(int nextTermId, int? periodId)
        {
            // Học kỳ hiện tại: kỳ đang chứa các cư dân Active.
            var currentTerm = await _context.Terms
                .AsNoTracking()
                .Where(t => t.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
                         && t.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)))
                .OrderByDescending(t => t.StartDate)
                .FirstOrDefaultAsync();

            if (currentTerm == null)
            {
                _logger.LogWarning("KeepingPeriodCloseJob: không tìm thấy học kỳ hiện tại, bỏ qua.");
                return new KeepingPeriodCloseResult(0, 0, 0);
            }

            // Danh sách sinh viên đã giữ phòng cho kỳ kế tiếp.
            var keptStudentIds = await _keepingRepository.GetStudentsWithCompletedKeepingAsync(nextTermId);

            // Cư dân Active của kỳ hiện tại.
            var activeResidencies = await _keepingRepository.GetActiveResidenciesForTermAsync(currentTerm.Id);

            var releasedCount = 0;
            var keptCount = 0;

            foreach (var residency in activeResidencies)
            {
                if (keptStudentIds.Contains(residency.StudentId))
                {
                    // Đã giữ phòng → KHÔNG đụng vào giường.
                    keptCount++;
                    continue;
                }

                // Chưa giữ phòng → trả giường về pool booking của kỳ kế tiếp.
                var bed = await _context.Beds
                    .FirstOrDefaultAsync(b => b.Id == residency.BedId);

                if (bed == null)
                {
                    continue;
                }

                // Idempotent: chỉ chuyển khi giường đang ở trạng thái chiếm chỗ.
                if (bed.Status == "Free")
                {
                    continue;
                }

                bed.Status = "Free";
                releasedCount++;

                // Ghi log thay đổi để truy vết (TargetEntity khác marker của kỳ đã xử lý).
                var auditLog = new Models.AuditLog
                {
                    UserId = residency.StudentId,
                    ActionType = "RELEASE_BED",
                    TargetEntity = "KeepingPeriodCloseBed",
                    TargetId = residency.Id.ToString(),
                    OldValue = $"Occupied (Residency Active - Term {currentTerm.Id})",
                    NewValue = $"Free (không giữ phòng cho term {nextTermId})",
                    CreatedAt = DateTime.UtcNow.AddHours(7)
                };
                await _context.AuditLogs.AddAsync(auditLog);
            }

            // Marker đánh dấu kỳ giữ phòng này đã được xử lý → Hosted Service không chạy lại.
            if (periodId.HasValue)
            {
                await _context.AuditLogs.AddAsync(new Models.AuditLog
                {
                    UserId = null,
                    ActionType = "CLOSE_KEEPING_PERIOD",
                    TargetEntity = "KeepingPeriodClose",
                    TargetId = periodId.Value.ToString(),
                    OldValue = null,
                    NewValue = $"Term {nextTermId}: {releasedCount} giường được trả về pool, {keptCount} giường được giữ.",
                    CreatedAt = DateTime.UtcNow.AddHours(7)
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "KeepingPeriodCloseJob hoàn tất cho term {NextTermId}: {Released} giường được trả về pool, {Kept} giường được giữ.",
                nextTermId, releasedCount, keptCount);

            return new KeepingPeriodCloseResult(activeResidencies.Count, releasedCount, keptCount);
        }
    }

    /// <summary>Kết quả chạy job đóng kỳ giữ phòng.</summary>
    public class KeepingPeriodCloseResult
    {
        public KeepingPeriodCloseResult(int totalResidents, int releasedBeds, int keptBeds)
        {
            TotalResidents = totalResidents;
            ReleasedBeds = releasedBeds;
            KeptBeds = keptBeds;
        }

        /// <summary>Tổng số cư dân Active của kỳ hiện tại được xét.</summary>
        public int TotalResidents { get; }

        /// <summary>Số giường đã trả về pool booking (không giữ phòng).</summary>
        public int ReleasedBeds { get; }

        /// <summary>Số giường được giữ (không đụng tới).</summary>
        public int KeptBeds { get; }
    }
}
