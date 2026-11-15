using FUD.Common;
using FUD.DTOs.Facilities.Bed;

namespace FUD.Services.Interfaces.Facilities
{
    public interface IBedService
    {
        Task<PagedResult<BedDto>> GetAllAsync(BedQueryDto query);
        Task<BedDto?>             GetByIdAsync(int id);
        Task<BedDto>              CreateAsync(CreateBedDto dto);
        Task<BedDto>              UpdateAsync(int id, UpdateBedDto dto);

        /// <summary>
        /// Chỉ cho phép đổi sang Free / Maintenance.
        /// Ném InvalidOperationException nếu bed đang Occupied/Holding/Pending Transfer.
        /// </summary>
        Task<BedDto>              UpdateStatusAsync(int id, UpdateBedStatusDto dto);
    }
}

