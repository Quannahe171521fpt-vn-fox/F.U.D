using System.Collections.Concurrent;
using FUD.Common;
using FUD.DTOs.Booking;
using FUD.DTOs.Keeping;
using FUD.DTOs.Payments;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Repositories.Interfaces.Keeping;
using FUD.Services.Interfaces.Keeping;
using FUD.Services.Interfaces.Payments;
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
        private readonly IRoomFeeInvoiceService _roomFeeInvoiceService;
        private readonly INotificationService _notificationService;
        private readonly FudContext _context;
        private readonly ILogger<KeepingService> _logger;

        public KeepingService(
            IKeepingRepository keepingRepository,
            IKeepingEligibilityService eligibilityService,
            IRoomFeeInvoiceService roomFeeInvoiceService,
            INotificationService notificationService,
            FudContext context,
            ILogger<KeepingService> logger)
        {
            _keepingRepository = keepingRepository;
            _eligibilityService = eligibilityService;
            _roomFeeInvoiceService = roomFeeInvoiceService;
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

            // 4. Tạo Booking_Request (BookingKind = 'Keeping') ở trạng thái 'Pending Payment'
            //    + hóa đơn phí giữ phòng (Unpaid).
    //
    //    Luồng đầy đủ:
    //    Sinh viên tạo yêu cầu  ->  thanh toán phí giữ phòng  ->  Booking_Request → 'Pending'
    //    ->  Ban Quản lý duyệt  ->  'Approved' + tạo Residency kỳ kế tiếp.
    //
    //    Lưu ý: theo thiết kế DB, lượt giữ phòng nằm ở Booking_Requests (BookingKind = 'Keeping'),
    //    không dùng bảng Requests (CHK_Request_Type không có giá trị 'Keeping').
            var termName = eligibility.NextTerm.Name;

            var currentResidency = await _keepingRepository.GetActiveResidencyAsync(studentId);
            if (currentResidency == null)
            {
                throw new KeepingNotEligibleException(
                    KeepingReasonCodes.NotResident,
                    "Bạn không phải là cư dân hiện tại nên không thể giữ phòng cho học kỳ kế tiếp.");
            }

            var now = DateTime.UtcNow.AddHours(7);
            var paymentDeadline = now.AddHours(24);

            // Booking_Request: nguồn phát sinh bắt buộc của hóa đơn RoomFee (CHK_Invoice_SourceMatchesType).
            var bookingRequest = new BookingRequest
            {
                StudentId = studentId,
                TermId = eligibility.NextTerm.Id,
                BedId = currentResidency.BedId,
                BookingKind = BookingKindConstants.Keeping,
                KeepFromResidencyId = currentResidency.Id,
                AgreementToRegulations = true,
                PaymentDeadline = paymentDeadline,
                // Chờ sinh viên thanh toán phí giữ phòng.
                Status = BookingStatusConstants.PendingPayment,
                CancelledBySystem = false,
                CreatedTime = now
            };
            _context.BookingRequests.Add(bookingRequest);

            // Lưu trước để có Id cho Invoice.BookingRequestId.
            await _context.SaveChangesAsync();

            // Phí giữ phòng lấy từ bảng giá Published của kỳ kế tiếp.
            var fee = eligibility.Fee?.Amount ?? 0m;
            if (fee <= 0)
            {
                throw new KeepingConflictException(
                    "Chưa có bảng giá hợp lệ cho học kỳ kế tiếp nên chưa thể tạo hóa đơn phí giữ phòng. Vui lòng liên hệ Ban Quản lý.");
            }

            var bedCode = currentResidency.Bed?.BedCode ?? string.Empty;
            var roomNumber = currentResidency.Bed?.Room?.RoomNumber ?? string.Empty;

            var invoice = await _roomFeeInvoiceService.CreateRoomFeeInvoiceAsync(
                bookingRequest,
                fee,
                $"Phí giữ chỗ {bedCode} - P.{roomNumber} ({termName})",
                DateOnly.FromDateTime(paymentDeadline),
                now);

            await _roomFeeInvoiceService.SaveChangesAsync();

            // Audit log (studentId, bookingId, kết quả).
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = studentId,
                ActionType = "CREATE",
                TargetEntity = "KeepingRequest",
                TargetId = bookingRequest.Id.ToString(),
                OldValue = null,
                NewValue = $"Pending Payment - Bed {bedCode} - Term {termName}",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Keeping đã tạo yêu cầu chờ thanh toán: student {StudentId}, booking {BookingId}, invoice {InvoiceId}, term {TermId}",
                studentId, bookingRequest.Id, invoice.Id, eligibility.NextTerm.Id);

            var saved = await _keepingRepository.GetKeepingBookingAsync(bookingRequest.Id);
            var result = await MapToResponseAsync(saved!, studentId);
            result.InvoiceId = invoice.Id;
            result.RequiresPayment = true;

            await SendKeepingPaymentRequiredNotificationAsync(bookingRequest, bedCode, roomNumber, fee, termName, paymentDeadline);

            return result;
        }

        /// <summary>
        /// Thông báo cho sinh viên: đã tạo yêu cầu giữ phòng, cần thanh toán phí giữ phòng.
        /// </summary>
        private async Task SendKeepingPaymentRequiredNotificationAsync(
            BookingRequest bookingRequest,
            string bedCode,
            string roomNumber,
            decimal fee,
            string termName,
            DateTime paymentDeadline)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = bookingRequest.StudentId,
                    Channel = "InApp",
                    Title = "Yêu cầu giữ phòng đã được tạo - cần thanh toán",
                    Content = $"Yêu cầu giữ giường {bedCode} - P.{roomNumber} cho học kỳ {termName} đã được tạo. "
                              + $"Vui lòng thanh toán {fee:N0} đồng trước {paymentDeadline:dd/MM/yyyy HH:mm} "
                              + "để yêu cầu được chuyển sang Ban Quản lý xét duyệt.",
                    ReferenceType = "Keeping",
                    ReferenceId = bookingRequest.Id.ToString(),
                    ReadFlag = false,
                    SentTime = DateTime.UtcNow.AddHours(7)
                };
                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Keeping: gửi in-app notification thất bại cho student {StudentId}", bookingRequest.StudentId);
            }
        }

        public async Task<KeepingRequestResponseDto> CompleteKeepingAsync(int requestId)
        {
            var request = await _keepingRepository.GetKeepingBookingAsync(requestId);

            if (request == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy yêu cầu giữ phòng với mã {requestId}");
            }

            // Đã hoàn tất rồi → trả về kết quả cũ (idempotent).
            // Chỉ coi là đã hoàn tất khi đã có Residency kỳ kế tiếp đi kèm,
            // nếu không (trường hợp dữ liệu bị nửa vời) thì tiếp tục xử lý để tự sửa.
            if (request.Status == BookingStatusConstants.Approved)
            {
                var nextResidency = await _keepingRepository.GetResidencyByStudentAndTermAsync(request.StudentId, request.TermId);
                var hasNextResidency = nextResidency != null && nextResidency.Status == KeepingConstants.ResidencyUpcoming;

                if (hasNextResidency)
                {
                    return await MapToResponseAsync(request, request.StudentId);
                }
            }

            return await CompleteKeepingInternalAsync(request, request.Term?.Name);
        }

        // ─────────────────────────────────────────────
        // MANAGER APPROVAL
        // ─────────────────────────────────────────────

        /// <summary>
        /// Ban Quản lý phê duyệt yêu cầu giữ phòng đã thanh toán.
        /// Yêu cầu phải ở trạng thái 'Pending' (đã thanh toán, chờ QL duyệt).
        /// Khi duyệt: Booking_Request → 'Approved' + tạo Residency kỳ kế tiếp.
        /// </summary>
        public async Task<KeepingRequestResponseDto> ApproveByManagerAsync(int bookingRequestId, int managerId)
        {
            var booking = await _keepingRepository.GetKeepingBookingAsync(bookingRequestId);

            if (booking == null)
            {
                throw new KeyNotFoundException($"KEEP_404: Không tìm thấy yêu cầu giữ phòng với mã {bookingRequestId}.");
            }

            if (booking.Status == BookingStatusConstants.Approved)
            {
                // Idempotent: đã duyệt rồi.
                return await MapToResponseAsync(booking, booking.StudentId);
            }

            if (booking.Status != BookingStatusConstants.Pending)
            {
                throw new InvalidOperationException(
                    $"KEEP_409: Yêu cầu đang ở trạng thái '{booking.Status}' nên không thể phê duyệt. "
                    + "Chỉ duyệt được yêu cầu đã thanh toán và đang chờ.");
            }

            // Đảm bảo bản ghi Manager tồn tại (FK ProcessedById).
            if (!await _context.Managers.AnyAsync(m => m.UserId == managerId))
            {
                _context.Managers.Add(new Manager { UserId = managerId });
                await _context.SaveChangesAsync();
            }

            booking.Status = BookingStatusConstants.Approved;
            booking.ProcessedById = managerId;
            booking.ProcessedTime = DateTime.UtcNow.AddHours(7);

            await _context.SaveChangesAsync();

            // Hoàn tất giữ phòng: tạo Residency kỳ kế tiếp (dùng lại logic đã có).
            var result = await CompleteKeepingInternalAsync(booking, booking.Term?.Name);

            await SendKeepingApprovedNotificationAsync(booking);

            return result;
        }

        /// <summary>
        /// Ban Quản lý từ chối yêu cầu giữ phòng.
        /// </summary>
        public async Task<KeepingRequestResponseDto> RejectByManagerAsync(int bookingRequestId, int managerId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("KEEP_400: Vui lòng nhập lý do từ chối yêu cầu giữ phòng.");
            }

            var booking = await _keepingRepository.GetKeepingBookingAsync(bookingRequestId);

            if (booking == null)
            {
                throw new KeyNotFoundException($"KEEP_404: Không tìm thấy yêu cầu giữ phòng với mã {bookingRequestId}.");
            }

            if (booking.Status != BookingStatusConstants.Pending)
            {
                throw new InvalidOperationException(
                    $"KEEP_409: Yêu cầu đang ở trạng thái '{booking.Status}' nên không thể từ chối.");
            }

            if (!await _context.Managers.AnyAsync(m => m.UserId == managerId))
            {
                _context.Managers.Add(new Manager { UserId = managerId });
                await _context.SaveChangesAsync();
            }

            booking.Status = BookingStatusConstants.Rejected;
            booking.RejectReason = reason;
            booking.ProcessedById = managerId;
            booking.ProcessedTime = DateTime.UtcNow.AddHours(7);

            await _context.SaveChangesAsync();

            await SendKeepingRejectedNotificationAsync(booking, reason);

            return await MapToResponseAsync(booking, booking.StudentId);
        }

        /// <summary>
        /// Danh sách yêu cầu giữ phòng đang chờ Ban Quản lý duyệt (đã thanh toán).
        /// </summary>
        public async Task<List<KeepingRequestManagerDto>> GetPendingForManagerAsync()
        {
            var bookings = await _keepingRepository.GetKeepingBookingsPendingApprovalAsync();

            var result = new List<KeepingRequestManagerDto>();

            foreach (var booking in bookings)
            {
                var invoice = booking.Invoices?
                    .Where(i => i.DeletedAt == null)
                    .OrderByDescending(i => i.Id)
                    .FirstOrDefault();

                var fee = await _keepingRepository.GetKeepingBedPriceAsync(booking.BedId, booking.TermId);

                result.Add(new KeepingRequestManagerDto
                {
                    RequestId = booking.Id,
                    BookingRequestId = booking.Id,
                    InvoiceId = invoice?.Id,
                    InvoiceNumber = invoice?.InvoiceNumber,
                    InvoiceStatus = invoice?.Status,
                    IsPaid = invoice?.Status == InvoiceStatuses.Paid,
                    StudentId = booking.StudentId,
                    StudentCode = booking.Student?.User?.Code ?? string.Empty,
                    StudentName = booking.Student?.User?.FullName ?? string.Empty,
                    StudentGender = booking.Student?.User?.Gender ?? string.Empty,
                    Status = booking.Status,
                    BedId = booking.BedId,
                    BedCode = booking.Bed?.BedCode ?? string.Empty,
                    DormName = booking.Bed?.Room?.Block?.Dorm?.Name ?? string.Empty,
                    BlockName = booking.Bed?.Room?.Block?.Name ?? string.Empty,
                    RoomNumber = booking.Bed?.Room?.RoomNumber ?? string.Empty,
                    TermId = booking.TermId,
                    TermName = booking.Term?.Name ?? string.Empty,
                    FeeAmount = fee ?? 0m,
                    CreatedTime = booking.CreatedTime,
                    ProcessedTime = booking.ProcessedTime,
                    ProcessedByName = booking.ProcessedBy?.User?.FullName,
                    RejectReason = booking.RejectReason
                });
            }

            return result;
        }

        /// <summary>
        /// Thông báo cho sinh viên khi yêu cầu giữ phòng được Ban Quản lý phê duyệt.
        /// </summary>
        private async Task SendKeepingApprovedNotificationAsync(BookingRequest booking)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = booking.StudentId,
                    Channel = "InApp",
                    Title = "Yêu cầu giữ phòng đã được phê duyệt",
                    Content = $"Yêu cầu giữ giường {booking.Bed?.BedCode} - P.{booking.Bed?.Room?.RoomNumber} "
                              + $"({booking.Term?.Name}) đã được Ban Quản lý phê duyệt. Bạn đã giữ phòng thành công cho học kỳ kế tiếp.",
                    ReferenceType = "Keeping",
                    ReferenceId = booking.Id.ToString(),
                    ReadFlag = false,
                    SentTime = DateTime.UtcNow.AddHours(7)
                };
                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Keeping: gửi in-app notification phê duyệt thất bại cho student {StudentId}", booking.StudentId);
            }
        }

        /// <summary>
        /// Thông báo cho sinh viên khi yêu cầu giữ phòng bị từ chối.
        /// </summary>
        private async Task SendKeepingRejectedNotificationAsync(BookingRequest booking, string reason)
        {
            try
            {
                var notification = new Notification
                {
                    UserId = booking.StudentId,
                    Channel = "InApp",
                    Title = "Yêu cầu giữ phòng bị từ chối",
                    Content = $"Yêu cầu giữ phòng #{booking.Id} đã bị Ban Quản lý từ chối. Lý do: {reason}",
                    ReferenceType = "Keeping",
                    ReferenceId = booking.Id.ToString(),
                    ReadFlag = false,
                    SentTime = DateTime.UtcNow.AddHours(7)
                };
                await _context.Notifications.AddAsync(notification);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Keeping: gửi in-app notification từ chối thất bại cho student {StudentId}", booking.StudentId);
            }
        }

        /// <summary>
        /// Thực thi phần "hoàn tất giữ phòng" trong 1 transaction:
        /// tạo Residency kỳ kế tiếp + AuditLog.
        /// Tách riêng để bước Ban Quản lý duyệt có thể gọi lại mà không viết lại logic.
        /// </summary>
        private async Task<KeepingRequestResponseDto> CompleteKeepingInternalAsync(BookingRequest request, string? termName)
        {
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

                var nextResidency = new Residency
                {
                    StudentId = request.StudentId,
                    BedId = currentResidency.BedId,
                    TermId = request.TermId,
                    SourceBookingId = request.Id,
                    RegistrationType = KeepingConstants.RegistrationType,
                    Status = KeepingConstants.ResidencyUpcoming
                };
                _context.Residencies.Add(nextResidency);

                // Audit log (studentId, bookingId, kết quả).
                var auditLog = new AuditLog
                {
                    UserId = request.StudentId,
                    ActionType = "UPDATE",
                    TargetEntity = "KeepingRequest",
                    TargetId = request.Id.ToString(),
                    OldValue = "Pending",
                    NewValue = $"Approved - Bed {currentResidency.Bed?.BedCode} - Term {termName}",
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

            var saved = await _keepingRepository.GetKeepingBookingAsync(request.Id);
            return await MapToResponseAsync(saved!, request.StudentId);
        }

        public async Task<KeepingRequestResponseDto?> GetRequestAsync(int requestId)
        {
            var request = await _keepingRepository.GetKeepingBookingAsync(requestId);
            if (request == null)
            {
                return null;
            }

            return await MapToResponseAsync(request, request.StudentId);
        }

        public async Task<int?> GetRequestOwnerIdAsync(int requestId)
        {
            var request = await _context.BookingRequests
                .AsNoTracking()
                .Where(b => b.Id == requestId && b.BookingKind == KeepingConstants.RequestType)
                .Select(b => (int?)b.StudentId)
                .FirstOrDefaultAsync();

            return request;
        }

        // ─────────────────────────────────────────────
        // NOTIFICATION
        // ─────────────────────────────────────────────

        private async Task SendKeepingNotificationsAsync(BookingRequest request, int bedId, string? termName)
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
                    ReferenceType = "Keeping",
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
                    <p>Phí giữ phòng đã được thanh toán. Vui lòng kiểm tra hồ sơ đăng ký của bạn.</p>
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

        private async Task<KeepingRequestResponseDto> MapToResponseAsync(BookingRequest request, int studentId)
        {
            var term = request.Term;

            // Giường được giữ: ưu tiên giường đã đăng ký (Residency kỳ kế tiếp),
            // nếu chưa có thì lấy từ Booking_Request (đang chờ thanh toán / chờ duyệt).
            var residency = await _keepingRepository.GetResidencyByStudentAndTermAsync(studentId, request.TermId);
            var bed = residency?.Bed ?? request.Bed;
            var room = bed?.Room;
            var block = room?.Block;
            var dorm = block?.Dorm;

            var feeAmount = bed != null
                ? await _keepingRepository.GetKeepingBedPriceAsync(bed.Id, request.TermId)
                : null;

            var invoice = request.Invoices?
                .Where(i => i.DeletedAt == null)
                .OrderByDescending(i => i.Id)
                .FirstOrDefault();

            return new KeepingRequestResponseDto
            {
                RequestId = request.Id,
                Status = request.Status,
                RequestType = KeepingConstants.RequestType,
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
                CreatedAt = request.CreatedTime,
                InvoiceId = invoice?.Id,
                RequiresPayment = request.Status == BookingStatusConstants.PendingPayment,
                AwaitingManagerApproval = request.Status == BookingStatusConstants.Pending
            };
        }
    }
}
