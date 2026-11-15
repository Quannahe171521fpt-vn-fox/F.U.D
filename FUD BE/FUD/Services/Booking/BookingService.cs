using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Booking;
using FUD.Models;
using FUD.Repositories.Interfaces.Booking;
using FUD.Services.Interfaces.Booking;

namespace FUD.Services.Booking;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepo;

    public BookingService(IBookingRepository bookingRepo)
    {
        _bookingRepo = bookingRepo;
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
        var bookingRequest = new BookingRequest
        {
            StudentId = studentId,
            TermId = term.Id,
            BedId = dto.BedId,
            BookingKind = "Booking",
            KeepFromResidencyId = null,
            AgreementToRegulations = true,
            PaymentDeadline = now.AddHours(24),
            Status = "Pending",
            CancelledBySystem = false,
            CreatedTime = now
        };

        var created = await _bookingRepo.CreateBookingRequestAsync(bookingRequest);
        var fullBooking = await _bookingRepo.GetBookingRequestByIdAsync(created.Id);

        var price = await _bookingRepo.GetBedPriceAsync(dto.BedId, term.Id);
        var result = MapToBookingResponse(fullBooking!);
        result.TermPrice = price;

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
            RejectReason = b.RejectReason
        };
    }
}

