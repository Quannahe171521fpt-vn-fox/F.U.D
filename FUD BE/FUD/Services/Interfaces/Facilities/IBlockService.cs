using FUD.Common;
using FUD.DTOs.Facilities.Block;
using FUD.DTOs.Facilities.Room;

namespace FUD.Services.Interfaces.Facilities
{
    public interface IBlockService
    {
        Task<PagedResult<BlockDto>> GetAllAsync(BlockQueryDto query);
        Task<BlockDto?> GetByIdAsync(int id);
        Task<IEnumerable<RoomDto>> GetRoomsByBlockIdAsync(int blockId);
        Task<BlockDto> CreateAsync(CreateBlockDto dto);
        Task<BlockDto> UpdateAsync(int id, UpdateBlockDto dto);

        /// <summary>Đổi trạng thái Block — "Bảo trì cả block" trên UI</summary>
        Task<BlockDto> UpdateStatusAsync(int id, UpdateBlockStatusDto dto);

        /// <summary>Đổi giới tính Block — "Đổi giới tính" trên UI (chỉ khi không có SV đang ở)</summary>
        Task<BlockDto> UpdateGenderAsync(int id, UpdateBlockGenderDto dto);
    }
}
