using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Booking;
using FUD.Models;
using FUD.Repositories.Interfaces.Booking;
using FUD.Services.Interfaces;
using FUD.Services.Interfaces.Booking;
using FUD.Services.Interfaces.Payments;

namespace FUD.Services.Booking;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IRoomFeeInvoiceService _roomFeeInvoiceService;
    private readonly IInAppNotificationService? _inAppNotificationService;

    public BookingService(
        IBookingRepository bookingRepo,
        IRoomFeeInvoiceService roomFeeInvoiceService,
        IInAppNotificationService? inAppNotificationService = null)
    {
        _bookingRepo = bookingRepo;
        _roomFeeInvoiceService = roomFeeInvoiceService;
        _inAppNotificationService = inAppNotificationService;
    }

    public async Task<BookingEligibilityResponseDto> CheckEligibilityAsync(int studentId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        var user = await _bookingRepo.GetUserByIdAsync(studentId);
        if (user == null)
        {
            return new BookingEligibilityResponseDto
            {
                Eligible = false,
                IneligibleReason = "Không tìm thấy thông tin tài khoản sinh viên.",
                IsBookingPeriodOpen = false
            };
        }

        if (user.AccountStatus == "Suspended")
        {
            return new BookingEligibilityResponseDto
            {
                Eligible = false,
                IneligibleReason = $"Tài khoản đang bị đình chỉ: {user.SuspensionReason ?? "Vi phạm nội quy"}",
                IsBookingPeriodOpen = false,
                StudentGender = user.Gender
            };
        }

        var (period, term) = await _bookingRepo.GetOpenBookingPeriodAsync(today);
        if (period == null || term == null)
        {
            return new BookingEligibilityResponseDto
            {
                Eligible = false,
                IneligibleReason = "Hiện không trong thời gian mở cổng đặt phòng KTX.",
                IsBookingPeriodOpen = false,
                StudentGender = user.Gender
            };
        }

        // Check if student already has a confirmed residency in this term
        var existingResidency = await _bookingRepo.GetStudentResidencyForTermAsync(studentId, term.Id);
        if (existingResidency != null)
        {
            return new BookingEligibilityResponseDto
            {
                Eligible = false,
                IneligibleReason = $"Bạn đã có phòng ở được xác nhận trong học kỳ {term.Name} ({existingResidency.Bed.BedCode} - Phòng {existingResidency.Bed.Room.RoomNumber}).",
                IsBookingPeriodOpen = true,
                PeriodStart = period.StartDate,
                PeriodEnd = period.EndDate,
                NextTermId = term.Id,
                NextTermName = term.Name,
                StudentGender = user.Gender
            };
        }

        // Check if student already has a pending or approved booking
        var existingBooking = await _bookingRepo.GetStudentBookingForTermAsync(studentId, term.Id);
        if (existingBooking != null)
        {
            var bookingDto = MapToBookingResponse(existingBooking);
            return new BookingEligibilityResponseDto
            {
                Eligible = false,
                IneligibleReason = existingBooking.Status == "Pending"
                    ? "Bạn đang có đơn đặt phòng đang chờ Ban Quản lý phê duyệt."
                    : "Đơn đặt phòng của bạn đã được phê duyệt.",
                IsBookingPeriodOpen = true,
                PeriodStart = period.StartDate,
                PeriodEnd = period.EndDate,
                NextTermId = term.Id,
                NextTermName = term.Name,
                StudentGender = user.Gender,
                CurrentBooking = bookingDto
            };
        }

        return new BookingEligibilityResponseDto
        {
            Eligible = true,
            IsBookingPeriodOpen = true,
            PeriodStart = period.StartDate,
            PeriodEnd = period.EndDate,
            NextTermId = term.Id,
            NextTermName = term.Name,
            StudentGender = user.Gender
        };
    }

    public async Task<List<BookingAvailableDormDto>> GetAvailableDormsAsync(int studentId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var (period, term) = await _bookingRepo.GetOpenBookingPeriodAsync(today);
        if (term == null) return new List<BookingAvailableDormDto>();

        var user = await _bookingRepo.GetUserByIdAsync(studentId);
        if (user == null) return new List<BookingAvailableDormDto>();

        return await _bookingRepo.GetAvailableDormsAsync(term.Id, user.Gender);
    }

    public async Task<List<BookingAvailableBlockDto>> GetAvailableBlocksAsync(int studentId, int dormId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var (period, term) = await _bookingRepo.GetOpenBookingPeriodAsync(today);
        if (term == null) return new List<BookingAvailableBlockDto>();

        var user = await _bookingRepo.GetUserByIdAsync(studentId);
        if (user == null) return new List<BookingAvailableBlockDto>();

        return await _bookingRepo.GetAvailableBlocksAsync(dormId, user.Gender, term.Id);
    }

    public async Task<List<BookingAvailableRoomDto>> GetAvailableRoomsAsync(int blockId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var (period, term) = await _bookingRepo.GetOpenBookingPeriodAsync(today);
        if (term == null) return new List<BookingAvailableRoomDto>();

        return await _bookingRepo.GetAvailableRoomsAsync(blockId, term.Id);
    }

    public async Task<List<BookingAvailableBedDto>> GetAvailableBedsAsync(int roomId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var (period, term) = await _bookingRepo.GetOpenBookingPeriodAsync(today);
        if (term == null) return new List<BookingAvailableBedDto>();

        return await _bookingRepo.GetAvailableBedsAsync(roomId, term.Id);
    }

    public async Task<BookingRequestResponseDto> CreateBookingAsync(int studentId, CreateBookingRequestDto dto)
    {
        if (!dto.AgreementToRegulations)
        {
            throw new ArgumentException("Bạn phải đọc và đồng ý với nội quy KTX để tiến hành đặt phòng.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var (period, term) = await _bookingRepo.GetOpenBookingPeriodAsync(today);
        if (period == null || term == null)
        {
            throw new InvalidOperationException("Hiện không trong thời gian mở cổng đặt phòng KTX.");
        }

        var user = await _bookingRepo.GetUserByIdAsync(studentId);
        if (user == null || user.AccountStatus == "Suspended")
        {
            throw new InvalidOperationException("Tài khoản của bạn không có quyền đặt phòng.");
        }

        // Check if student already has a residency in this term
        var existingResidency = await _bookingRepo.GetStudentResidencyForTermAsync(studentId, term.Id);
        if (existingResidency != null)
        {
            throw new InvalidOperationException("Bạn đã có phòng ở trong học kỳ này.");
        }

        // Check if student already has a pending or approved booking
        var existingBooking = await _bookingRepo.GetStudentBookingForTermAsync(studentId, term.Id);
        if (existingBooking != null)
        {
            throw new InvalidOperationException("Bạn đã có đơn đặt phòng cho học kỳ này.");
        }

        // Check Bed validity and gender match
        var bed = await _bookingRepo.GetBedByIdAsync(dto.BedId);
        if (bed == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông tin giường đã chọn.");
        }

        if (bed.Room.Block.GenderType != user.Gender)
        {
            throw new InvalidOperationException($"Phòng thuộc khu vực dành cho {bed.Room.Block.GenderType}, không phù hợp với giới tính tài khoản của bạn ({user.Gender}).");
        }

        var isAvailable = await _bookingRepo.IsBedAvailableForTermAsync(dto.BedId, term.Id);
        if (!isAvailable)
        {
            throw new InvalidOperationException("Giường bạn chọn vừa có người đặt hoặc không khả dụng. Vui lòng chọn giường khác.");
        }

        var now = DateTime.UtcNow.AddHours(7);
        var paymentDeadline = now.AddHours(24);

        // Đơn đặt phòng được tạo ở trạng thái 'Pending Payment':
        // sinh viên phải thanh toán phí phòng trước, sau đó đơn mới chuyển
        // sang 'Pending' để Ban Quản lý xét duyệt.
        var bookingRequest = new BookingRequest
        {
            StudentId = studentId,
            TermId = term.Id,
            BedId = dto.BedId,
            BookingKind = BookingKindConstants.Booking,
            KeepFromResidencyId = null,
            AgreementToRegulations = true,
            PaymentDeadline = paymentDeadline,
            Status = BookingStatusConstants.PendingPayment,
            CancelledBySystem = false,
            CreatedTime = now
        };

        var created = await _bookingRepo.CreateBookingRequestAsync(bookingRequest);

        // Tạo hóa đơn phí phòng (Unpaid) để sinh viên thanh toán.
        var price = await _bookingRepo.GetBedPriceAsync(dto.BedId, term.Id);
        if (price == null || price <= 0)
        {
            throw new InvalidOperationException(
                "Chưa có bảng giá hợp lệ cho học kỳ này nên chưa thể tạo hóa đơn phí phòng. Vui lòng liên hệ Ban Quản lý.");
        }

        var fullBooking = await _bookingRepo.GetBookingRequestByIdAsync(created.Id);

        var invoice = await _roomFeeInvoiceService.CreateRoomFeeInvoiceAsync(
            fullBooking!,
            price.Value,
            $"Phí phòng {fullBooking?.Bed?.BedCode} - P.{fullBooking?.Bed?.Room?.RoomNumber} ({fullBooking?.Term?.Name ?? term.Name})",
            DateOnly.FromDateTime(paymentDeadline),
            now);

        await _roomFeeInvoiceService.SaveChangesAsync();

        var result = MapToBookingResponse(fullBooking!);
        result.TermPrice = price;
        result.InvoiceId = invoice.Id;
        result.RequiresPayment = true;

        // Thông báo cho sinh viên: đã tạo hóa đơn, cần thanh toán trước khi gửi QL duyệt.
        if (_inAppNotificationService != null)
        {
            await _inAppNotificationService.NotifyUserAsync(
                studentId,
                "Đơn đặt phòng đã được tạo - cần thanh toán phí phòng",
                $"Đơn đặt giường {fullBooking?.Bed?.BedCode} - P.{fullBooking?.Bed?.Room?.RoomNumber} ({fullBooking?.Term?.Name}) đã được tạo. " +
                $"Vui lòng thanh toán {price.Value:N0} đồng trước {paymentDeadline:dd/MM/yyyy HH:mm} để đơn được gửi Ban Quản lý xét duyệt.",
                "Booking",
                created.Id.ToString());
        }

        return result;
    }

    public async Task<List<BookingRequestManagerDto>> GetAllForManagerAsync(string? status)
    {
        var list = await _bookingRepo.GetAllForManagerAsync(status);
        var result = new List<BookingRequestManagerDto>();

        foreach (var b in list)
        {
            var price = await _bookingRepo.GetBedPriceAsync(b.BedId, b.TermId);
            result.Add(new BookingRequestManagerDto
            {
                Id = b.Id,
                StudentId = b.StudentId,
                StudentCode = b.Student?.User?.Code ?? string.Empty,
                StudentName = b.Student?.User?.FullName ?? string.Empty,
                StudentGender = b.Student?.User?.Gender ?? string.Empty,
                BedId = b.BedId,
                BedCode = b.Bed?.BedCode ?? string.Empty,
                RoomNumber = b.Bed?.Room?.RoomNumber ?? string.Empty,
                BlockName = b.Bed?.Room?.Block?.Name ?? string.Empty,
                DormName = b.Bed?.Room?.Block?.Dorm?.Name ?? string.Empty,
                TermId = b.TermId,
                TermName = b.Term?.Name ?? string.Empty,
                TermPrice = price,
                Status = b.Status,
                CreatedTime = b.CreatedTime,
                ProcessedTime = b.ProcessedTime,
                ProcessedByName = b.ProcessedBy?.User?.FullName,
                RejectReason = b.RejectReason
            });
        }

        return result;
    }

    public async Task<BookingRequestManagerDto> ApproveAsync(int bookingId, int managerId)
    {
        await _bookingRepo.ApproveBookingAsync(bookingId, managerId);
        var updated = await _bookingRepo.GetBookingRequestByIdAsync(bookingId);
        var price = await _bookingRepo.GetBedPriceAsync(updated!.BedId, updated.TermId);

        if (_inAppNotificationService != null)
        {
            await _inAppNotificationService.NotifyUserAsync(
                updated.StudentId,
                "Đơn đặt phòng đã được phê duyệt",
                $"Đơn đặt giường {updated.Bed?.BedCode} - P.{updated.Bed?.Room?.RoomNumber} ({updated.Term?.Name}) đã được Ban Quản lý phê duyệt. Vui lòng chuẩn bị nhận phòng (Check-in).",
                "Booking",
                bookingId.ToString());

            if (updated.Bed?.Room?.Block?.DormId is int dormId && dormId > 0)
            {
                await _inAppNotificationService.NotifyDormGuardsAsync(
                    dormId,
                    "Sinh viên mới chờ nhận phòng (Check-in)",
                    $"Sinh viên {updated.Student?.User?.FullName} ({updated.Student?.User?.Code}) đã được duyệt vào P.{updated.Bed?.Room?.RoomNumber} - Giường {updated.Bed?.BedCode}.",
                    "Checkin",
                    bookingId.ToString());
            }
        }

        return new BookingRequestManagerDto
        {
            Id = updated.Id,
            StudentId = updated.StudentId,
            StudentCode = updated.Student?.User?.Code ?? string.Empty,
            StudentName = updated.Student?.User?.FullName ?? string.Empty,
            StudentGender = updated.Student?.User?.Gender ?? string.Empty,
            BedId = updated.BedId,
            BedCode = updated.Bed?.BedCode ?? string.Empty,
            RoomNumber = updated.Bed?.Room?.RoomNumber ?? string.Empty,
            BlockName = updated.Bed?.Room?.Block?.Name ?? string.Empty,
            DormName = updated.Bed?.Room?.Block?.Dorm?.Name ?? string.Empty,
            TermId = updated.TermId,
            TermName = updated.Term?.Name ?? string.Empty,
            TermPrice = price,
            Status = updated.Status,
            CreatedTime = updated.CreatedTime,
            ProcessedTime = updated.ProcessedTime,
            ProcessedByName = updated.ProcessedBy?.User?.FullName,
            RejectReason = updated.RejectReason
        };
    }

    public async Task<BookingRequestManagerDto> RejectAsync(int bookingId, int managerId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Vui lòng nhập lý do từ chối đơn đặt phòng.");
        }

        await _bookingRepo.RejectBookingAsync(bookingId, managerId, reason);
        var updated = await _bookingRepo.GetBookingRequestByIdAsync(bookingId);
        var price = await _bookingRepo.GetBedPriceAsync(updated!.BedId, updated.TermId);

        if (_inAppNotificationService != null)
        {
            await _inAppNotificationService.NotifyUserAsync(
                updated.StudentId,
                "Đơn đặt phòng bị từ chối",
                $"Đơn đặt giường {updated.Bed?.BedCode} ({updated.Term?.Name}) đã bị từ chối. Lý do: {reason}",
                "Booking",
                bookingId.ToString());
        }

        return new BookingRequestManagerDto
        {
            Id = updated.Id,
            StudentId = updated.StudentId,
            StudentCode = updated.Student?.User?.Code ?? string.Empty,
            StudentName = updated.Student?.User?.FullName ?? string.Empty,
            StudentGender = updated.Student?.User?.Gender ?? string.Empty,
            BedId = updated.BedId,
            BedCode = updated.Bed?.BedCode ?? string.Empty,
            RoomNumber = updated.Bed?.Room?.RoomNumber ?? string.Empty,
            BlockName = updated.Bed?.Room?.Block?.Name ?? string.Empty,
            DormName = updated.Bed?.Room?.Block?.Dorm?.Name ?? string.Empty,
            TermId = updated.TermId,
            TermName = updated.Term?.Name ?? string.Empty,
            TermPrice = price,
            Status = updated.Status,
            CreatedTime = updated.CreatedTime,
            ProcessedTime = updated.ProcessedTime,
            ProcessedByName = updated.ProcessedBy?.User?.FullName,
            RejectReason = updated.RejectReason
        };
    }

    private static BookingRequestResponseDto MapToBookingResponse(BookingRequest b)
    {
        var invoice = b.Invoices?
            .Where(i => i.DeletedAt == null)
            .OrderByDescending(i => i.Id)
            .FirstOrDefault();

        return new BookingRequestResponseDto
        {
            RequestId = b.Id,
            Status = b.Status,
            BedId = b.BedId,
            BedCode = b.Bed?.BedCode ?? string.Empty,
            RoomNumber = b.Bed?.Room?.RoomNumber ?? string.Empty,
            BlockName = b.Bed?.Room?.Block?.Name ?? string.Empty,
            DormName = b.Bed?.Room?.Block?.Dorm?.Name ?? string.Empty,
            TermId = b.TermId,
            TermName = b.Term?.Name ?? string.Empty,
            CreatedTime = b.CreatedTime,
            PaymentDeadline = b.PaymentDeadline,
            AgreementToRegulations = b.AgreementToRegulations,
            RejectReason = b.RejectReason,
            InvoiceId = invoice?.Id,
            RequiresPayment = b.Status == BookingStatusConstants.PendingPayment
        };
    }
}

