using FUD.DTOs.Facilities.Equipment;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Facilities
{
    public interface IEquipmentRepository
    {
        Task<(IEnumerable<Equipment> Items, int TotalCount)> GetAllAsync(EquipmentQueryDto query);
        Task<Equipment?> GetByIdAsync(int id);
        Task<Equipment?> GetByAssetCodeAsync(string assetCode);
        Task<Equipment> AddAsync(Equipment equipment);
        Task UpdateAsync(Equipment equipment);
        
        // Catalog operations
        Task<IEnumerable<EquipmentCatalog>> GetCatalogsAsync();
        Task<EquipmentCatalog?> GetCatalogByIdAsync(int id);
        Task<EquipmentCatalog> AddCatalogAsync(EquipmentCatalog catalog);
        Task<bool> DeleteCatalogAsync(int id);

        // Location options
        Task<IEnumerable<LocationOptionDto>> GetLocationsAsync();
    }
}
