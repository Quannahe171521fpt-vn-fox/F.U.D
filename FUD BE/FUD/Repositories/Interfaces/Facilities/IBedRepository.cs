using FUD.DTOs.Facilities.Bed;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Facilities
{
    public interface IBedRepository
    {
        Task<(IEnumerable<Bed> Items, int TotalCount)> GetAllAsync(BedQueryDto query);
        Task<Bed?> GetByIdAsync(int id);
        Task<bool> RoomExistsAsync(int roomId);
        Task<bool> IsBedCodeUniqueAsync(int roomId, string bedCode, int? excludeId = null);

        /// <summary>Trả true nếu bed đang Occupied/Holding/Pending Transfer</summary>
        Task<bool> HasActiveResidencyAsync(int bedId);

        Task<Bed> CreateAsync(Bed bed);
        Task<Bed> UpdateAsync(Bed bed);
    }
}

