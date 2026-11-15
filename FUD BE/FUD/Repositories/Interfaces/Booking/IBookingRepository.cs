using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Booking;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Booking;

public interface IBookingRepository
{
    Task<(RegistrationPeriod? Period, Term? Term)> GetOpenBookingPeriodAsync(DateOnly today);
    Task<User?> GetUserByIdAsync(int userId);
    Task<Residency?> GetStudentResidencyForTermAsync(int studentId, int termId);
    Task<BookingRequest?> GetStudentBookingForTermAsync(int studentId, int termId);
    Task<List<BookingAvailableDormDto>> GetAvailableDormsAsync(int termId, string gender);
    Task<List<BookingAvailableBlockDto>> GetAvailableBlocksAsync(int dormId, string gender, int termId);
    Task<List<BookingAvailableRoomDto>> GetAvailableRoomsAsync(int blockId, int termId);
    Task<List<BookingAvailableBedDto>> GetAvailableBedsAsync(int roomId, int termId);
    Task<decimal?> GetBedPriceAsync(int bedId, int termId);
    Task<Bed?> GetBedByIdAsync(int bedId);
    Task<bool> IsBedAvailableForTermAsync(int bedId, int termId, int? excludeBookingId = null);
    Task<BookingRequest> CreateBookingRequestAsync(BookingRequest request);
    Task<BookingRequest?> GetBookingRequestByIdAsync(int id);
    Task<List<BookingRequest>> GetAllForManagerAsync(string? status);
    Task ApproveBookingAsync(int bookingId, int managerId);
    Task RejectBookingAsync(int bookingId, int managerId, string reason);
}

