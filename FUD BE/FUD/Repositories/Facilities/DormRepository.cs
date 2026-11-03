using FUD.DTOs.Facilities.Dorm;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Facilities
{
    public class DormRepository : IDormRepository
    {
        private readonly FudContext _context;

        public DormRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<Dorm> Items, int TotalCount)> GetAllAsync(DormQueryDto query)
        {
            var q = _context.Dorms
                .Include(d => d.Campus)
                .Include(d => d.Blocks)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.CampusName))
                q = q.Where(d => d.Campus.Name.Contains(query.CampusName));

            if (!string.IsNullOrWhiteSpace(query.DormName))
                q = q.Where(d => d.Name.Contains(query.DormName) || d.Code.Contains(query.DormName));

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(d => d.Status == query.Status);

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderBy(d => d.Campus.Name)
                .ThenBy(d => d.Code)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Dorm?> GetByIdAsync(int id)
        {
            return await _context.Dorms
                .Include(d => d.Campus)
                .Include(d => d.Blocks)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<bool> IsCodeUniqueAsync(int campusId, string code, int? excludeId = null)
        {
            return !await _context.Dorms
                .AnyAsync(d => d.CampusId == campusId
                            && d.Code == code
                            && (excludeId == null || d.Id != excludeId));
        }

        public async Task<bool> HasActiveBlocksAsync(int dormId)
        {
            return await _context.Blocks
                .AnyAsync(b => b.DormId == dormId && b.Status == "Active");
        }

        public async Task<bool> CampusExistsAsync(int campusId)
        {
            return await _context.Campuses
                .AnyAsync(c => c.Id == campusId);
        }

        public async Task<Dorm> CreateAsync(Dorm dorm)
        {
            _context.Dorms.Add(dorm);
            await _context.SaveChangesAsync();
            return dorm;
        }

        public async Task<Dorm> UpdateAsync(Dorm dorm)
        {
            _context.Dorms.Update(dorm);
            await _context.SaveChangesAsync();
            return dorm;
        }

        public async Task<bool> SoftDeleteAsync(int dormId)
        {
            var dorm = await _context.Dorms.FirstOrDefaultAsync(d => d.Id == dormId);
            if (dorm == null) return false;

            // TODO: Thêm lại DeletedAt sau khi cập nhật database/model
            dorm.Status = "Inactive";
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
