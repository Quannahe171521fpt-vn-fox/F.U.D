using FUD.DTOs.Facilities.Room;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Facilities
{
    public class RoomRepository : IRoomRepository
    {
        private readonly FudContext _context;

        public RoomRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<Room> Items, int TotalCount)> GetAllAsync(RoomQueryDto query)
        {
            var q = _context.Rooms
                .Include(r => r.Block)
                    .ThenInclude(b => b.Dorm)
                .Include(r => r.RoomType)
                .Include(r => r.Beds)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.BlockName))
                q = q.Where(r => r.Block.Name.Contains(query.BlockName) || r.Block.Code.Contains(query.BlockName));

            if (!string.IsNullOrWhiteSpace(query.RoomNumber))
                q = q.Where(r => r.RoomNumber.Contains(query.RoomNumber));

            if (query.RoomTypeId.HasValue)
                q = q.Where(r => r.RoomTypeId == query.RoomTypeId.Value);

            if (!string.IsNullOrWhiteSpace(query.GenderType))
                q = q.Where(r => r.Block.GenderType == query.GenderType);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(r => r.Status == query.Status);

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderBy(r => r.Block.Code)
                .ThenBy(r => r.RoomNumber)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Room?> GetByIdAsync(int id)
        {
            return await _context.Rooms
                .Include(r => r.Block)
                    .ThenInclude(b => b.Dorm)
                .Include(r => r.RoomType)
                .Include(r => r.Beds)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Room?> GetByIdWithBedsAsync(int id)
        {
            return await _context.Rooms
                .Include(r => r.Block)
                    .ThenInclude(b => b.Dorm)
                .Include(r => r.RoomType)
                .Include(r => r.Beds)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<bool> IsRoomNumberUniqueAsync(int blockId, string roomNumber, int? excludeId = null)
        {
            return !await _context.Rooms
                .AnyAsync(r => r.BlockId == blockId
                            && r.RoomNumber == roomNumber
                            && (excludeId == null || r.Id != excludeId));
        }

        public async Task<bool> HasActiveBedsAsync(int roomId)
        {
            return await _context.Beds
                .AnyAsync(b => b.RoomId == roomId
                            && (b.Status == "Occupied" || b.Status == "Holding" || b.Status == "Pending Transfer"));
        }

        public async Task<IEnumerable<Bed>> GetBedsByRoomIdAsync(int roomId)
        {
            return await _context.Beds
                .Where(b => b.RoomId == roomId)
                .ToListAsync();
        }

        public async Task<Room> CreateAsync(Room room)
        {
            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();
            return room;
        }

        public async Task<Room> UpdateAsync(Room room)
        {
            _context.Rooms.Update(room);
            await _context.SaveChangesAsync();
            return room;
        }

    }
}
