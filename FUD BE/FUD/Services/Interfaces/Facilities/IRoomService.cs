using FUD.Common;
using FUD.DTOs.Facilities.Room;

namespace FUD.Services.Interfaces.Facilities
{
    public interface IRoomService
    {
        Task<PagedResult<RoomDto>> GetAllAsync(RoomQueryDto query);
        Task<RoomDto?> GetByIdAsync(int id);
        Task<RoomDto> CreateAsync(CreateRoomDto dto);
        Task<RoomDto> UpdateAsync(int id, UpdateRoomDto dto);

        /// <summary>Đổi trạng thái Room — "Bảo trì cả phòng" trên UI (đổi luôn tất cả Beds)</summary>
        Task<RoomDto> UpdateStatusAsync(int id, UpdateRoomStatusDto dto);
    }
}
