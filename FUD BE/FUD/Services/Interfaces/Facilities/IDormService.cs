using FUD.Common;
using FUD.DTOs.Facilities.Dorm;

namespace FUD.Services.Interfaces.Facilities
{
    public interface IDormService
    {
        Task<PagedResult<DormDto>> GetAllAsync(DormQueryDto query);
        Task<DormDto?> GetByIdAsync(int id);
        Task<DormDto> CreateAsync(CreateDormDto dto);
        Task<DormDto> UpdateAsync(int id, UpdateDormDto dto);
        Task<DormDto> UpdateStatusAsync(int id, UpdateDormStatusDto dto);
        Task SoftDeleteAsync(int id);
    }
}
