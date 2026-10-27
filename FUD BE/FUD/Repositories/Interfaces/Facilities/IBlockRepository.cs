using FUD.DTOs.Facilities.Block;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Facilities
{
    public interface IBlockRepository
    {
        Task<(IEnumerable<Block> Items, int TotalCount)> GetAllAsync(BlockQueryDto query);

        Task<Block?> GetByIdAsync(int id);

        Task<Block?> GetByIdWithRoomsAsync(int id);

        Task<bool> IsCodeUniqueAsync(int dormId, string code, int? excludeId = null);

        Task<bool> HasActiveResidenciesAsync(int blockId);

        Task<bool> HasRoomsAsync(int blockId);

        Task<Block> CreateAsync(Block block);
        Task<Block> UpdateAsync(Block block);
    }
}
