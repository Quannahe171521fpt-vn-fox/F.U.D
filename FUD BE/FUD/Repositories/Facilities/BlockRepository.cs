using FUD.DTOs.Facilities.Block;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Facilities
{
    public class BlockRepository : IBlockRepository
    {
        private readonly FudContext _context;

        public BlockRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<Block> Items, int TotalCount)> GetAllAsync(BlockQueryDto query)
        {
            var q = _context.Blocks
                .Include(b => b.Dorm)
                .Include(b => b.Rooms)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.DormName))
                q = q.Where(b => b.Dorm.Name.Contains(query.DormName));

            if (!string.IsNullOrWhiteSpace(query.BlockName))
                q = q.Where(b => b.Name.Contains(query.BlockName) || b.Code.Contains(query.BlockName));

            if (!string.IsNullOrWhiteSpace(query.GenderType))
                q = q.Where(b => b.GenderType == query.GenderType);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(b => b.Status == query.Status);

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderBy(b => b.Dorm.Name)
                .ThenBy(b => b.Code)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Block?> GetByIdAsync(int id)
        {
            return await _context.Blocks
                .Include(b => b.Dorm)
                .Include(b => b.Rooms)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<Block?> GetByIdWithRoomsAsync(int id)
        {
            return await _context.Blocks
                .Include(b => b.Dorm)
                .Include(b => b.Rooms)
                    .ThenInclude(r => r.RoomType)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(int dormId, string code, int? excludeId = null)
        {
            return !await _context.Blocks
                .AnyAsync(b => b.DormId == dormId
                            && b.Code == code
                            && (excludeId == null || b.Id != excludeId));
        }

        public async Task<bool> HasActiveResidenciesAsync(int blockId)
        {
            return await _context.Residencies
                .AnyAsync(r => r.Bed.Room.BlockId == blockId
                            && (r.Status == "Active" || r.Status == "Reserved"));
        }

        public async Task<bool> HasRoomsAsync(int blockId)
        {
            return await _context.Rooms.AnyAsync(r => r.BlockId == blockId);
        }

        public async Task<Block> CreateAsync(Block block)
        {
            _context.Blocks.Add(block);
            await _context.SaveChangesAsync();
            return block;
        }

        public async Task<Block> UpdateAsync(Block block)
        {
            _context.Blocks.Update(block);
            await _context.SaveChangesAsync();
            return block;
        }

    }
}
