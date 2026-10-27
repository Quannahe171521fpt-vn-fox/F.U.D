using FUD.DTOs.Facilities.Room;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Facilities
{
    public interface IRoomRepository
    {
        Task<(IEnumerable<Room> Items, int TotalCount)> GetAllAsync(RoomQueryDto query);

        Task<Room?> GetByIdAsync(int id);

        Task<Room?> GetByIdWithBedsAsync(int id);

        Task<bool> IsRoomNumberUniqueAsync(int blockId, string roomNumber, int? excludeId = null);

        Task<bool> HasActiveBedsAsync(int roomId);

        Task<IEnumerable<Bed>> GetBedsByRoomIdAsync(int roomId);

        Task<Room> CreateAsync(Room room);
        Task<Room> UpdateAsync(Room room);
    }
}
