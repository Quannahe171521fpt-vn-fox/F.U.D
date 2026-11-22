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

        /// <summary>
        /// Kiểm tra trùng tên phòng trong cùng tầng của cùng block.
        /// Ví dụ: Block 301 tầng 3 không được có 2 phòng "301L".
        /// </summary>
        Task<bool> IsRoomNumberUniqueOnFloorAsync(int blockId, int floorLevel, string roomNumber, int? excludeId = null);

        Task<bool> HasActiveBedsAsync(int roomId);

        Task<IEnumerable<Bed>> GetBedsByRoomIdAsync(int roomId);

        Task<IEnumerable<RoomType>> GetRoomTypesAsync();

        Task<int?> GetBlockNumberOfFloorsAsync(int blockId);

        Task<Room> CreateAsync(Room room);
        Task<Room> UpdateAsync(Room room);
    }
}
