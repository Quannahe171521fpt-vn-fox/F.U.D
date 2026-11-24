using FUD.DTOs.Facilities.Bed;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Facilities
{
    public class BedRepository : IBedRepository
    {
        private readonly FudContext _context;

        public BedRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<Bed> Items, int TotalCount)> GetAllAsync(BedQueryDto query)
        {
            var q = _context.Beds
                .Include(b => b.Room)
                    .ThenInclude(r => r.Block)
                .AsQueryable();

            if (query.RoomId.HasValue)
                q = q.Where(b => b.RoomId == query.RoomId.Value);

            if (!string.IsNullOrWhiteSpace(query.BedCode))
                q = q.Where(b => b.BedCode.Contains(query.BedCode));

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(b => b.Status == query.Status);

            var total = await q.CountAsync();

            var items = await q
                .OrderBy(b => b.Room.RoomNumber)
                .ThenBy(b => b.BedCode)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<Bed?> GetByIdAsync(int id)
        {
            return await _context.Beds
                .Include(b => b.Room)
                    .ThenInclude(r => r.Block)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<bool> RoomExistsAsync(int roomId)
        {
            return await _context.Rooms.AnyAsync(r => r.Id == roomId);
        }

        public async Task<Room?> GetRoomWithTypeAndBedsAsync(int roomId)
        {
            return await _context.Rooms
                .AsNoTracking()
                .Include(r => r.RoomType)
                .Include(r => r.Beds)
                .FirstOrDefaultAsync(r => r.Id == roomId);
        }

        public async Task<bool> IsBedCodeUniqueAsync(int roomId, string bedCode, int? excludeId = null)
        {
            return !await _context.Beds.AnyAsync(b =>
                b.RoomId  == roomId &&
                b.BedCode == bedCode &&
                (excludeId == null || b.Id != excludeId));
        }

        public async Task<bool> HasActiveResidencyAsync(int bedId)
        {
            return await _context.Beds.AnyAsync(b =>
                b.Id == bedId &&
                (b.Status == "Occupied" ||
                 b.Status == "Holding"  ||
                 b.Status == "Pending Transfer"));
        }

        public async Task<Bed> CreateAsync(Bed bed)
        {
            _context.Beds.Add(bed);
            await _context.SaveChangesAsync();
            return bed;
        }

        public async Task<Bed> UpdateAsync(Bed bed)
        {
            _context.Beds.Update(bed);
            await _context.SaveChangesAsync();
            return bed;
        }
    }
}

