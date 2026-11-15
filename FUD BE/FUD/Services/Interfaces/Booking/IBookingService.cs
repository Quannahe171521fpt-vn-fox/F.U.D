using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Booking;

namespace FUD.Services.Interfaces.Booking;

public interface IBookingService
{
    Task<BookingEligibilityResponseDto> CheckEligibilityAsync(int studentId);
    Task<List<BookingAvailableDormDto>> GetAvailableDormsAsync(int studentId);
    Task<List<BookingAvailableBlockDto>> GetAvailableBlocksAsync(int studentId, int dormId);
    Task<List<BookingAvailableRoomDto>> GetAvailableRoomsAsync(int blockId);
    Task<List<BookingAvailableBedDto>> GetAvailableBedsAsync(int roomId);
    Task<BookingRequestResponseDto> CreateBookingAsync(int studentId, CreateBookingRequestDto dto);
    Task<List<BookingRequestManagerDto>> GetAllForManagerAsync(string? status);
    Task<BookingRequestManagerDto> ApproveAsync(int bookingId, int managerId);
    Task<BookingRequestManagerDto> RejectAsync(int bookingId, int managerId, string reason);
}

