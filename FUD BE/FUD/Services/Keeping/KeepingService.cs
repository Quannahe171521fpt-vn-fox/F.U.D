using System.Collections.Concurrent;
using FUD.Common;
using FUD.DTOs.Keeping;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Repositories.Interfaces.Keeping;
using FUD.Services.Interfaces.Keeping;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Keeping
{
    /// <summary>
    /// Nghiệp vụ Giữ phòng (UC02) — giai đoạn 1, chưa có thanh toán.
    ///
    /// Giai đoạn 1: <see cref="CreateAsync"/> gọi thẳng <see cref="CompleteKeepingAsync"/>.
    /// Giai đoạn 2 (thêm thanh toán): <see cref="CreateAsync"/> chỉ tạo Request ở trạng thái Pending
    /// và tạo Invoice; <see cref="CompleteKeepingAsync"/> được gọi từ callback thanh toán.
    /// Vì vậy không có logic nào của giai đoạn 1 cần viết lại.
    /// </summary>
    public class KeepingService : IKeepingService
    {
        /// <summary>
        /// Khóa theo studentId để serialize các request giữ phòng cùng một sinh viên.
        /// Lớp bảo vệ đầu tiên chống race; lớp bảo vệ cuối là unique index ở DB
        /// (UX_Request_Keeping_Student_Term / UX_Residency_Student_Term / UX_Residency_Bed_Term).
        /// Với ~200 user mở kỳ đồng thời, số lock đồng thời rất nhỏ và thời gian chờ ngắn.
        /// </summary>
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> StudentLocks = new();

        private readonly IKeepingRepository _keepingRepository;
        private readonly IKeepingEligibilityService _eligibilityService;
        private readonly INotificationService _notificationService;
        private readonly FudContext _context;
        private readonly ILogger<KeepingService> _logger;

        public KeepingService(
            IKeepingRepository keepingRepository,
            IKeepingEligibilityService eligibilityService,
            INotificationService notificationService,
            FudContext context,
            ILogger<KeepingService> logger)
        {
            _keepingRepository = keepingRepository;
            _eligibilityService = eligibilityService;
            _notificationService = notificationService;
            _context = context;
            _logger = logger;
        }

        public async Task<KeepingRequestResponseDto> CreateAsync(int studentId, CreateKeepingRequestDto dto)
        {
            // Serialize request giữ phòng của cùng một sinh viên (chống race).
            var studentLock = StudentLocks.GetOrAdd(studentId, _ => new SemaphoreSlim(1, 1));
            await studentLock.WaitAsync();

            try
            {
                return await CreateInternalAsync(studentId, dto);
            }
            finally
            {
                studentLock.Release();
            }
        }

        private async Task<KeepingRequestResponseDto> CreateInternalAsync(int studentId, CreateKeepingRequestDto dto)
        {
            // 1. Bắt buộc đồng ý nội quy.
            if (!dto.AgreedToRegulations)
            {
                throw new ArgumentException("KEEP_400: Bạn phải đồng ý với nội quy KTX để giữ phòng.");
            }

            // 2. Kiểm tra lại toàn bộ điều kiện (GB-04 "on every request" — dữ liệu có thể đổi sau khi lưu).
            var eligibility = await _eligibilityService.CheckEligibilityAsync(studentId);
            if (!eligibility.Eligible)
            {
                // Idempotent: nếu lý do là "đã giữ phòng" → trả lại yêu cầu cũ thay vì báo lỗi.
                if (eligibility.ReasonCode == KeepingReasonCodes.AlreadyKept
                    && eligibility.NextTerm != null)
                {
                    var existingRequest = await _keepingRepository.GetCompletedKeepingRequestAsync(
                        studentId, eligibility.NextTerm.Id);

                    if (existingRequest != null)
                    {
                        _logger.LogInformation(
                            "Keeping idempotent: student {StudentId} đã có request {RequestId}",
                            studentId, existingRequest.Id);
                        return await MapToResponseAsync(existingRequest, studentId);
                    }
                }

                _logger.LogInformation(
                    "Keeping bị từ chối cho student {StudentId}: {ReasonCode}", studentId, eligibility.ReasonCode);
                throw new KeepingNotEligibleException(eligibility.ReasonCode!, eligibility.Message!);
            }

            // 3. Idempotent: nếu đã có yêu cầu Keeping Completed cho kỳ kế tiếp → trả lại yêu cầu cũ.
            var existing = await _keepingRepository.GetCompletedKeepingRequestAsync(
                studentId, eligibility.NextTerm!.Id);
            if (existing != null)
            {
                _logger.LogInformation(
                    "Keeping idempotent: student {StudentId} đã có request {RequestId}",
                    studentId, existing.Id);
                return await MapToResponseAsync(existing, studentId);
            }

            // 4. Tạo Request rồi hoàn tất giữ phòng.
            // Giai đoạn 1: tạo thẳng Completed, gọi thẳng CompleteKeepingAsync.
            // Giai đoạn 2: tạo Pending + Invoice, rồi CompleteKeepingAsync được gọi từ callback thanh toán.
            // Lấy tên học kỳ để ghi vào Request_Log / AuditLog / email.
            // Không gán vào request.Term để tránh xung đột tracking với Term đã load sẵn.
            var termName = eligibility.NextTerm.Name;

            var request = new Request
            {
                StudentId = studentId,
                TermId = eligibility.NextTerm.Id,
                RequestType = KeepingConstants.RequestType,
                Status = KeepingConstants.StatusCompleted,
                Reason = "Giữ phòng hiện tại cho học kỳ kế tiếp",
                CreatedTime = DateTime.UtcNow.AddHours(7),
                ProcessedTime = DateTime.UtcNow.AddHours(7)
            };
            _context.Requests.Add(request);

            // 5. Hoàn tất giữ phòng: tạo Residency + Request_Log + thông báo.
            // Toàn bộ (kể cả Request) nằm trong cùng 1 transaction ở CompleteKeepingAsync
            // → nếu có lỗi (gender mismatch, bed đã có người, ...) thì không tạo bản ghi nào.
            var result = await CompleteKeepingInternalAsync(request, termName);

            _logger.LogInformation(
                "Keeping thành công: student {StudentId}, request {RequestId}, term {TermId}, bed {BedId}",
                studentId, result.RequestId, eligibility.NextTerm.Id,
                result.Bed?.BedId ?? 0);

            return result;
        }

        public async Task<KeepingRequestResponseDto> CompleteKeepingAsync(int requestId)
        {
            var request = await _context.Requests
                .Include(r => r.Term)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.RequestType == KeepingConstants.RequestType);

            if (request == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy yêu cầu giữ phòng với mã {requestId}");
            }

            // Đã hoàn tất rồi → trả về kết quả cũ (idempotent).
            // Chỉ coi là đã hoàn tất khi đã có Residency kỳ kế tiếp đi kèm,
            // nếu không (trường hợp dữ liệu bị nửa vời) thì tiếp tục xử lý để tự sửa.
            if (request.Status == KeepingConstants.StatusCompleted)
            {
                var existing = await _keepingRepository.GetKeepingRequestByIdAsync(requestId);
                var nextResidency = await _keepingRepository.GetResidencyByStudentAndTermAsync(request.StudentId, request.TermId);
                var hasNextResidency = nextResidency != null && nextResidency.Status == KeepingConstants.ResidencyUpcoming;

                if (existing != null && hasNextResidency)
                {
                    return await MapToResponseAsync(existing, request.StudentId);
                }
            }

            return await CompleteKeepingInternalAsync(request, request.Term?.Name);
        }

        /// <summary>
        /// Thực thi phần "hoàn tất giữ phòng" trong 1 transaction:
        /// tạo Residency kỳ kế tiếp + Request_Log + AuditLog.
        /// Tách riêng để giai đoạn 2 (thanh toán) có thể gọi lại mà không viết lại logic.
        /// </summary>
        private async Task<KeepingRequestResponseDto> CompleteKeepingInternalAsync(Request request, string? termName)
        {
            // Giai đoạn 1: transaction bao trọn việc tạo Request + Residency + Request_Log.
            // Giai đoạn 2: sẽ tách phần tạo Invoice ra khỏi transaction này.
            var keptBedId = 0;
            await using var transaction = await _keepingRepository.BeginTransactionAsync();
            try
            {
                // Lấy lại cư trú hiện tại (bed cần giữ).
                var currentResidency = await _keepingRepository.GetActiveResidencyAsync(request.StudentId);
                if (currentResidency == null)
                {
                    throw new KeepingNotEligibleException(
                        KeepingReasonCodes.NotResident,
                        "Bạn không phải là cư dân hiện tại nên không thể giữ phòng cho học kỳ kế tiếp.");
                }

                // GB-01: giường phải cùng giới tính với Block (Dorm → Block → Room → Bed).
                // Giường hiện tại vốn đã khớp nên đây là assert phòng ngừa dữ liệu sai.
                var studentGender = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == request.StudentId)
                    .Select(u => u.Gender)
                    .FirstOrDefaultAsync();

                var blockGender = currentResidency.Bed?.Room?.Block?.GenderType;

                if (!string.IsNullOrWhiteSpace(studentGender)
                    && !string.IsNullOrWhiteSpace(blockGender)
                    && !string.Equals(studentGender, blockGender, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(
                        "Keeping vi phạm GB-01: student {StudentId} (giới tính {Gender}) giữ giường {BedCode} thuộc Block {BlockGender}.",
                        request.StudentId, studentGender, currentResidency.Bed?.BedCode, blockGender);

                    throw new KeepingConflictException(
                        "Giường hiện tại không khớp giới tính của bạn. Vui lòng liên hệ Ban Quản lý KTX.");
                }

                // GB-03: một sinh viên chỉ có 1 cư trú cho kỳ kế tiếp; một giường chỉ có 1 sinh viên.
                if (await _keepingRepository.IsBedOccupiedForTermAsync(currentResidency.BedId, request.TermId))
                {
                    throw new KeepingConflictException(
                        "Giường này đã có sinh viên khác đăng ký cho học kỳ kế tiếp. Vui lòng liên hệ quản lý KTX.");
                }

                // Chống race (GB-03): kiểm tra lại lần nữa ngay trước khi ghi.
                // Ở production, unique index UX_Request_Keeping_Student_Term là lớp bảo vệ cuối.
                if (await _keepingRepository.HasCompletedKeepingRequestAsync(request.StudentId, request.TermId))
                {
                    throw new KeepingConflictException(
                        "Bạn đã giữ phòng cho học kỳ kế tiếp. Vui lòng tải lại trang để kiểm tra.");
                }

                if (await _keepingRepository.HasResidencyForTermAsync(request.StudentId, request.TermId))
                {
                    throw new KeepingConflictException(
                        "Bạn đã có cư trú cho học kỳ kế tiếp. Vui lòng kiểm tra lại thông tin.");
                }

                // Tạo Residency kỳ kế tiếp (Upcoming — không tính là Active nên không vi phạm
                // rule "một Residency Active/student").
                keptBedId = currentResidency.BedId;

                // Đảm bảo Request có trong Change Tracker (chưa lưu) → lưu trước để có Id
                // cho Residency.SourceRequestId và Request_Log.RequestId.
                // Việc này đặt SAU tất cả các bước validate để khi validate fail
                // thì không tạo bản ghi Request nào (transaction rollback).
                if (request.Id == 0)
                {
                    _context.Requests.Add(request);
                    await _context.SaveChangesAsync();
                }

                var nextResidency = new Residency
                {
                    StudentId = request.StudentId,
                    BedId = currentResidency.BedId,
                    TermId = request.TermId,
                    RegistrationType = KeepingConstants.RegistrationType,
                    Status = KeepingConstants.ResidencyUpcoming
                };
                _context.Residencies.Add(nextResidency);

                // Ghi Request_Log (mọi thay đổi trạng thái đều được ghi log).
                var requestLog = new RequestLog
                {
                    RequestId = request.Id,
                    FromStatus = null,
                    ToStatus = KeepingConstants.StatusCompleted,
                    Note = $"Giữ phòng thành công cho học kỳ {termName}. Giường: {currentResidency.Bed?.BedCode}.",
                    ChangeTime = DateTime.UtcNow.AddHours(7)
                };
                _context.RequestLogs.Add(requestLog);

                // Audit log (studentId, requestId, kết quả).
                var auditLog = new AuditLog
                {
                    UserId = request.StudentId,
                    ActionType = "CREATE",
                    TargetEntity = "KeepingRequest",
                    TargetId = request.Id.ToString(),
                    OldValue = null,
                    NewValue = $"Completed - Bed {currentResidency.Bed?.BedCode} - Term {termName}",
                    CreatedAt = DateTime.UtcNow.AddHours(7)
                };
                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            // Thông báo: gửi sau commit để lỗi email không làm rollback nghiệp vụ đã hoàn tất.
            await SendKeepingNotificationsAsync(request, keptBedId, termName);

            var saved = await _keepingRepository.GetKeepingRequestByIdAsync(request.Id);
            return await MapToResponseAsync(saved!, request.StudentId);
        }

        public async Task<KeepingRequestResponseDto?> GetRequestAsync(int requestId)
        {
            var request = await _keepingRepository.GetKeepingRequestByIdAsync(requestId);
            if (request == null)
            {
                return null;
            }

            return await MapToResponseAsync(request, request.StudentId);
        }

        public async Task<int?> GetRequestOwnerIdAsync(int requestId)
        {
            var request = await _context.Requests
                .AsNoTracking()
                .Where(r => r.Id == requestId && r.RequestType == KeepingConstants.RequestType)
                .Select(r => (int?)r.StudentId)
                .FirstOrDefaultAsync();

            return request;
        }

        // ─────────────────────────────────────────────
        // NOTIFICATION
        // ─────────────────────────────────────────────

        private async Task SendKeepingNotificationsAsync(Request request, int bedId, string? termName)
        {
            var student = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.StudentId);

            if (student == null)
            {
                _logger.LogWarning("Keeping: không tìm thấy sinh viên {StudentId} để gửi thông báo", request.StudentId);
                return;
            }

            termName ??= request.Term?.Name;
            var bed = await _context.Beds
                .Include(b => b.Room)
                    .ThenInclude(r => r.Block)
                        .ThenInclude(bl => bl.Dorm)
                .FirstOrDefaultAsync(b => b.Id == bedId);

            var bedCode = bed?.BedCode ?? string.Empty;
            var location = bed == null
                ? string.Empty
                : $"{bed.Room?.Block?.Dorm?.Name} - {bed.Room?.Block?.Name} - Phòng {bed.Room?.RoomNumber} - Giường {bedCode}";

            var title = $"Giữ phòng thành công - {termName}";
            var content = $"Bạn đã giữ thành công giường {bedCode} cho học kỳ {termName}. "
                        + $"Vị trí: {location}.";

            // 1. In-app notification (mục tiêu: trong 5 giây).
            try
            {
                var notification = new Notification
                {
                    UserId = request.StudentId,
                    Channel = "InApp",
                    Title = title,
                    Content = content,
                    ReferenceType = "Request",
                    ReferenceId = request.Id.ToString(),
                    ReadFlag = false,
                    SentTime = DateTime.UtcNow.AddHours(7)
                };
                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Keeping: gửi in-app notification thất bại cho student {StudentId}", request.StudentId);
            }

            // 2. Email qua hàm gửi chung của hệ thống.
            try
            {
                var htmlBody = $@"
                    <p>Chào {System.Net.WebUtility.HtmlEncode(student.FullName)},</p>
                    <p>Bạn đã <strong>giữ phòng thành công</strong> cho học kỳ <strong>{System.Net.WebUtility.HtmlEncode(termName)}</strong>.</p>
                    <ul>
                        <li>Giường: <strong>{System.Net.WebUtility.HtmlEncode(bedCode)}</strong></li>
                        <li>Vị trí: {System.Net.WebUtility.HtmlEncode(location)}</li>
                        <li>Mã yêu cầu: <strong>#{request.Id}</strong></li>
                    </ul>
                    <p>Phí giữ phòng sẽ được gửi qua hóa đơn khi học kỳ bắt đầu. Vui lòng kiểm tra hồ sơ đăng ký của bạn.</p>
                    <p>Trân trọng,<br/>Ban Quản lý KTX FPT</p>";

                await _notificationService.SendEmailAsync(student.Email, title, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Keeping: gửi email thất bại cho student {StudentId}", request.StudentId);
            }
        }

        // ─────────────────────────────────────────────
        // MAPPING
        // ─────────────────────────────────────────────

        private async Task<KeepingRequestResponseDto> MapToResponseAsync(Request request, int studentId)
        {
            var term = request.Term;
            var residency = await _keepingRepository.GetResidencyByStudentAndTermAsync(studentId, request.TermId);

            var bed = residency?.Bed;
            var room = bed?.Room;
            var block = room?.Block;
            var dorm = block?.Dorm;

            var feeAmount = bed != null
                ? await _keepingRepository.GetKeepingBedPriceAsync(bed.Id, request.TermId)
                : null;

            return new KeepingRequestResponseDto
            {
                RequestId = request.Id,
                Status = request.Status,
                RequestType = request.RequestType,
                NextTerm = term == null
                    ? null
                    : new NextTermDto
                    {
                        Id = term.Id,
                        Name = term.Name,
                        AcademicYear = term.AcademicYear,
                        StartDate = term.StartDate,
                        EndDate = term.EndDate
                    },
                Bed = bed == null
                    ? null
                    : new KeepingBedDto
                    {
                        BedId = bed.Id,
                        BedCode = bed.BedCode,
                        DormName = dorm?.Name ?? string.Empty,
                        BlockName = block?.Name ?? string.Empty,
                        RoomNumber = room?.RoomNumber ?? string.Empty
                    },
                FeeAmount = feeAmount,
                Currency = "VND",
                CreatedAt = request.CreatedTime
            };
        }
    }
}
