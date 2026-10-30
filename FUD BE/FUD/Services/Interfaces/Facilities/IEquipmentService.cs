using FUD.Common;
using FUD.DTOs.Facilities.Equipment;

namespace FUD.Services.Interfaces.Facilities
{
    public interface IEquipmentService
    {
        Task<PagedResult<EquipmentDto>> GetAllAsync(EquipmentQueryDto query);
        Task<EquipmentDto?> GetByIdAsync(int id);
        Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, int? userId = null);
        Task<EquipmentDto> UpdateAsync(int id, UpdateEquipmentDto dto, int? userId = null);
        Task<EquipmentDto> MoveAsync(int id, MoveEquipmentDto dto, int? userId = null);
        Task<EquipmentDto> ChangeConditionAsync(int id, ChangeConditionDto dto, int? userId = null);
        Task LiquidateAsync(int id, int? userId = null);
        Task<IEnumerable<TicketDto>> GetRepairHistoryAsync(int id);
        
        // Catalog
        Task<IEnumerable<EquipmentCatalogDto>> GetCatalogsAsync();
        Task<EquipmentCatalogDto> CreateCatalogAsync(CreateEquipmentCatalogDto dto);
        Task<bool> DeleteCatalogAsync(int id);

        // Locations
        Task<IEnumerable<LocationOptionDto>> GetLocationsAsync();
    }
}
