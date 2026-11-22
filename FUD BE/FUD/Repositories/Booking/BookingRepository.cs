using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Booking;
using FUD.Models;
using FUD.Repositories.Interfaces.Booking;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Booking;

public class BookingRepository : IBookingRepository
{
    private readonly FudContext _context;

    public BookingRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<(RegistrationPeriod? Period, Term? Term)> GetOpenBookingPeriodAsync(DateOnly today)
    {
        var period = await _context.RegistrationPeriods
            .Include(p => p.Term)
            .AsNoTracking()
            .Where(p => p.PeriodType == "Booking" && p.ActiveFlag && p.StartDate <= today && today <= p.EndDate)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync();

        return (period, period?.Term);
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<Residency?> GetStudentResidencyForTermAsync(int studentId, int termId)
    {
        return await _context.Residencies
            .AsNoTracking()
            .Include(r => r.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(blk => blk.Dorm)
            .Include(r => r.Term)
            .FirstOrDefaultAsync(r => r.StudentId == studentId && r.TermId == termId && (r.Status == "Active" || r.Status == "Reserved"));
    }

    public async Task<BookingRequest?> GetStudentBookingForTermAsync(int studentId, int termId)
    {
        return await _context.BookingRequests
            .AsNoTracking()
            .Include(b => b.Bed).ThenInclude(b => b.Room).ThenInclude(rm => rm.Block).ThenInclude(blk => blk.Dorm)
            .Include(b => b.Term)
            .Where(b => b.StudentId == studentId && b.TermId == termId && b.Status != "Cancelled" && b.Status != "Rejected")
            .OrderByDescending(b => b.CreatedTime)
            .FirstOrDefaultAsync();
    }

    public async Task<Bed?> GetBedByIdAsync(int bedId)
    {
        return await _context.Beds
            .Include(b => b.Room).ThenInclude(r => r.Block).ThenInclude(blk => blk.Dorm)
            .FirstOrDefaultAsync(b => b.Id == bedId);
    }

    public async Task<bool> IsBedAvailableForTermAsync(int bedId, int termId, int? excludeBookingId = null)
    {
        var bed = await _context.Beds.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bedId);
        if (bed == null || bed.Status == "Maintenance") return false;

        var hasResidency = await _context.Residencies.AsNoTracking()
            .AnyAsync(r => r.BedId == bedId && r.TermId == termId && (r.Status == "Active" || r.Status == "Reserved"));
        if (hasResidency) return false;

        var hasBooking = await _context.BookingRequests.AsNoTracking()
            .AnyAsync(b => b.BedId == bedId && b.TermId == termId 
                        && (excludeBookingId == null || b.Id != excludeBookingId) 
                        // 'Pending Payment' cũng chiếm chỗ: SV đã tạo đơn và đang chờ thanh toán.
                        && (b.Status == "Pending Payment" || b.Status == "Pending" || b.Status == "Approved"));
        if (hasBooking) return false;

        return true;
    }

    public async Task<decimal?> GetBedPriceAsync(int bedId, int termId)
    {
        var bed = await _context.Beds
            .AsNoTracking()
            .Include(b => b.Room).ThenInclude(r => r.Block)
            .FirstOrDefaultAsync(b => b.Id == bedId);

        if (bed == null) return null;

        var dormId = bed.Room.Block.DormId;
        var roomTypeId = bed.Room.RoomTypeId;

        return await _context.PriceItems
            .AsNoTracking()
            .Where(pi => pi.PriceList.TermId == termId && pi.PriceList.Status == "Published" && pi.DormId == dormId && pi.RoomTypeId == roomTypeId)
            .Select(pi => (decimal?)pi.UnitPrice)
            .FirstOrDefaultAsync();
    }

    public async Task<List<BookingAvailableDormDto>> GetAvailableDormsAsync(int termId, string gender)
    {
        var unavailableBedIds = await GetUnavailableBedIdsAsync(termId);

        var dorms = await _context.Dorms
            .AsNoTracking()
            .Where(d => d.Status == "Active")
            .Select(d => new BookingAvailableDormDto
            {
                DormId = d.Id,
                DormName = d.Name,
                DormCode = d.Code,
                TotalAvailableBeds = d.Blocks
                    .Where(b => b.Status == "Active" && b.GenderType == gender)
                    .SelectMany(b => b.Rooms)
                    .Where(r => r.Status == "Active")
                    .SelectMany(r => r.Beds)
                    .Count(b => b.Status != "Maintenance" && !unavailableBedIds.Contains(b.Id))
            })
            .Where(d => d.TotalAvailableBeds > 0)
            .ToListAsync();

        return dorms;
    }

    public async Task<List<BookingAvailableBlockDto>> GetAvailableBlocksAsync(int dormId, string gender, int termId)
    {
        var unavailableBedIds = await GetUnavailableBedIdsAsync(termId);

        var blocks = await _context.Blocks
            .AsNoTracking()
            .Where(b => b.DormId == dormId && b.Status == "Active" && b.GenderType == gender)
            .Select(b => new BookingAvailableBlockDto
            {
                BlockId = b.Id,
                BlockName = b.Name,
                BlockCode = b.Code,
                GenderType = b.GenderType,
                DormId = b.DormId,
                TotalAvailableBeds = b.Rooms
                    .Where(r => r.Status == "Active")
                    .SelectMany(r => r.Beds)
                    .Count(bed => bed.Status != "Maintenance" && !unavailableBedIds.Contains(bed.Id))
            })
            .Where(b => b.TotalAvailableBeds > 0)
            .ToListAsync();

        return blocks;
    }

    public async Task<List<BookingAvailableRoomDto>> GetAvailableRoomsAsync(int blockId, int termId)
    {
        var unavailableBedIds = await GetUnavailableBedIdsAsync(termId);

        var rooms = await _context.Rooms
            .AsNoTracking()
            .Include(r => r.RoomType)
            .Where(r => r.BlockId == blockId && r.Status == "Active")
            .Select(r => new BookingAvailableRoomDto
            {
                RoomId = r.Id,
                RoomNumber = r.RoomNumber,
                FloorLevel = r.FloorLevel,
                RoomTypeId = r.RoomTypeId,
                RoomTypeName = r.RoomType != null ? r.RoomType.Name : string.Empty,
                BedCapacity = r.RoomType != null ? r.RoomType.BedCapacity : 0,
                BlockId = r.BlockId,
                FreeBeds = r.Beds.Count(bed => bed.Status != "Maintenance" && !unavailableBedIds.Contains(bed.Id))
            })
            .Where(r => r.FreeBeds > 0)
            .ToListAsync();

        return rooms;
    }

    public async Task<List<BookingAvailableBedDto>> GetAvailableBedsAsync(int roomId, int termId)
    {
        var unavailableBedIds = await GetUnavailableBedIdsAsync(termId);

        var room = await _context.Rooms
            .AsNoTracking()
            .Include(r => r.Block)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null) return new List<BookingAvailableBedDto>();

        var price = await _context.PriceItems
            .AsNoTracking()
            .Where(pi => pi.PriceList.TermId == termId && pi.PriceList.Status == "Published" && pi.DormId == room.Block.DormId && pi.RoomTypeId == room.RoomTypeId)
            .Select(pi => (decimal?)pi.UnitPrice)
            .FirstOrDefaultAsync();

        var beds = await _context.Beds
            .AsNoTracking()
            .Where(b => b.RoomId == roomId && b.Status != "Maintenance" && !unavailableBedIds.Contains(b.Id))
            .Select(b => new BookingAvailableBedDto
            {
                BedId = b.Id,
                BedCode = b.BedCode,
                PositionInRoom = b.PositionInRoom,
                RoomId = b.RoomId,
                RoomNumber = room.RoomNumber,
                TermPrice = price
            })
            .ToListAsync();

        return beds;
    }

    public async Task<BookingRequest> CreateBookingRequestAsync(BookingRequest request)
    {
        // Ensure student record exists in Students table
        var studentExists = await _context.Students.AnyAsync(s => s.UserId == request.StudentId);
        if (!studentExists)
        {
            _context.Students.Add(new Student
            {
                UserId = request.StudentId,
                Major = "Chưa cập nhật",
                Cohort = "K17"
            });
            await _context.SaveChangesAsync();
        }

        _context.BookingRequests.Add(request);
        await _context.SaveChangesAsync();
        return request;
    }

    public async Task<BookingRequest?> GetBookingRequestByIdAsync(int id)
    {
        return await _context.BookingRequests
            .Include(b => b.Bed).ThenInclude(b => b.Room).ThenInclude(r => r.Block).ThenInclude(blk => blk.Dorm)
            .Include(b => b.Term)
            .Include(b => b.Student).ThenInclude(s => s.User)
            .Include(b => b.ProcessedBy).ThenInclude(p => p.User)
            .Include(b => b.Invoices)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<BookingRequest>> GetAllForManagerAsync(string? status)
    {
        var query = _context.BookingRequests
            .AsNoTracking()
            .Include(b => b.Bed).ThenInclude(b => b.Room).ThenInclude(r => r.Block).ThenInclude(blk => blk.Dorm)
            .Include(b => b.Term)
            .Include(b => b.Student).ThenInclude(s => s.User)
            .Include(b => b.ProcessedBy).ThenInclude(p => p.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(b => b.Status == status);
        }

        return await query.OrderByDescending(b => b.CreatedTime).ToListAsync();
    }

    public async Task ApproveBookingAsync(int bookingId, int managerId)
    {
        var booking = await _context.BookingRequests
            .Include(b => b.Bed)
            .Include(b => b.Term)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            throw new KeyNotFoundException("Không tìm thấy đơn đặt phòng.");

        if (booking.Status != "Pending")
            throw new InvalidOperationException($"Đơn đặt phòng đang ở trạng thái '{booking.Status}', không thể phê duyệt.");

        // Check if bed is still free (exclude this booking itself from collision check)
        var isFree = await IsBedAvailableForTermAsync(booking.BedId, booking.TermId, bookingId);
        if (!isFree)
            throw new InvalidOperationException("Giường này hiện không còn trống cho học kỳ này.");

        // Ensure manager record exists in Managers table to avoid FK error
        var managerExists = await _context.Managers.AnyAsync(m => m.UserId == managerId);
        if (!managerExists)
        {
            _context.Managers.Add(new Manager { UserId = managerId });
            await _context.SaveChangesAsync();
        }

        booking.Status = "Approved";
        booking.ProcessedById = managerId;
        booking.ProcessedTime = DateTime.UtcNow.AddHours(7);

        // Update Bed status to Holding
        booking.Bed.Status = "Holding";

        // Create Residency
        var residency = new Residency
        {
            StudentId = booking.StudentId,
            BedId = booking.BedId,
            TermId = booking.TermId,
            SourceBookingId = booking.Id,
            RegistrationType = "Booking",
            Status = "Reserved",
            CheckInDate = null,
            PlannedCheckOutDate = booking.Term.EndDate
        };
        _context.Residencies.Add(residency);

        await _context.SaveChangesAsync();
    }

    public async Task RejectBookingAsync(int bookingId, int managerId, string reason)
    {
        var booking = await _context.BookingRequests.FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            throw new KeyNotFoundException("Không tìm thấy đơn đặt phòng.");

        if (booking.Status != "Pending")
            throw new InvalidOperationException($"Đơn đặt phòng đang ở trạng thái '{booking.Status}', không thể từ chối.");

        // Ensure manager record exists in Managers table
        var managerExists = await _context.Managers.AnyAsync(m => m.UserId == managerId);
        if (!managerExists)
        {
            _context.Managers.Add(new Manager { UserId = managerId });
            await _context.SaveChangesAsync();
        }

        booking.Status = "Rejected";
        booking.RejectReason = reason;
        booking.ProcessedById = managerId;
        booking.ProcessedTime = DateTime.UtcNow.AddHours(7);

        await _context.SaveChangesAsync();
    }

    private async Task<List<int>> GetUnavailableBedIdsAsync(int termId)
    {
        var residencyBeds = await _context.Residencies
            .AsNoTracking()
            .Where(r => r.TermId == termId && (r.Status == "Active" || r.Status == "Reserved"))
            .Select(r => r.BedId)
            .ToListAsync();

        var bookingBeds = await _context.BookingRequests
            .AsNoTracking()
            .Where(b => b.TermId == termId && (b.Status == "Pending" || b.Status == "Approved"))
            .Select(b => b.BedId)
            .ToListAsync();

        return residencyBeds.Concat(bookingBeds).Distinct().ToList();
    }
}

