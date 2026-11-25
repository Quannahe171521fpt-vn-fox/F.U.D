using FUD.Common;
using FUD.DTOs.Facilities.Room;

namespace FUD.Services.Interfaces.Facilities
{
    public interface IRoomService
    {
        Task<PagedResult<RoomDto>> GetAllAsync(RoomQueryDto query);
        Task<PagedResult<RoomDto>> GetAllForGuardAsync(RoomQueryDto query, int guardUserId);
        Task<RoomDto?> GetByIdAsync(int id);
        Task<RoomDto?> GetByIdForGuardAsync(int id, int guardUserId);
        Task<RoomDetailDto?> GetDetailByIdAsync(int id, int? guardUserId = null);
        Task<IEnumerable<GuardAssignedBlockDto>> GetAssignedBlocksLookupAsync(int? guardUserId = null);
        Task<RoomDto> CreateAsync(CreateRoomDto dto);
        Task<RoomDto> UpdateAsync(int id, UpdateRoomDto dto);

        /// <summary>Đổi trạng thái Room — "Bảo trì cả phòng" trên UI (đổi luôn tất cả Beds)</summary>
        Task<RoomDto> UpdateStatusAsync(int id, UpdateRoomStatusDto dto);

        Task<IEnumerable<RoomTypeLookupDto>> GetRoomTypesAsync();
    }
}
