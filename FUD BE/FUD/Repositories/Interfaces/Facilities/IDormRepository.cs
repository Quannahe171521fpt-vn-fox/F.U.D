using FUD.DTOs.Facilities.Dorm;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Facilities
{
    public interface IDormRepository
    {
        Task<(IEnumerable<Dorm> Items, int TotalCount)> GetAllAsync(DormQueryDto query);
        Task<Dorm?> GetByIdAsync(int id);
        Task<bool> IsCodeUniqueAsync(int campusId, string code, int? excludeId = null);
        Task<bool> HasActiveBlocksAsync(int dormId);
        Task<bool> CampusExistsAsync(int campusId);
        Task<Dorm> CreateAsync(Dorm dorm);
        Task<Dorm> UpdateAsync(Dorm dorm);
        Task<bool> SoftDeleteAsync(int dormId);
    }
}
