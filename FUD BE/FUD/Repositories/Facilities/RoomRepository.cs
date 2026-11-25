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
                    .ThenInclude(b => b.Residencies)
                        .ThenInclude(res => res.Handover)
                .AsQueryable();

            if (query.DormId.HasValue)
                q = q.Where(r => r.Block.DormId == query.DormId.Value);

            if (query.BlockId.HasValue)
                q = q.Where(r => r.BlockId == query.BlockId.Value);

            if (!string.IsNullOrWhiteSpace(query.BlockName))
            {
                var blockKw = query.BlockName.Trim().ToLower();
                q = q.Where(r => r.Block.Name.ToLower().Contains(blockKw) || r.Block.Code.ToLower().Contains(blockKw));
            }

            if (!string.IsNullOrWhiteSpace(query.RoomNumber))
            {
                var roomKw = query.RoomNumber.Trim().ToLower();
                q = q.Where(r => r.RoomNumber.ToLower().Contains(roomKw));
            }

            if (!string.IsNullOrWhiteSpace(query.ExactRoomNumber))
            {
                var exactRoom = query.ExactRoomNumber.Trim().ToLower();
                q = q.Where(r => r.RoomNumber.ToLower() == exactRoom);
            }

            if (query.FloorLevel.HasValue)
                q = q.Where(r => r.FloorLevel == query.FloorLevel.Value);

            if (query.RoomTypeId.HasValue)
                q = q.Where(r => r.RoomTypeId == query.RoomTypeId.Value);

            if (!string.IsNullOrWhiteSpace(query.GenderType))
                q = q.Where(r => r.Block.GenderType == query.GenderType);

            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(r => r.Status == query.Status);

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderBy(r => r.Block.Code)
                .ThenBy(r => r.FloorLevel)
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
                    .ThenInclude(b => b.Residencies)
                        .ThenInclude(res => res.Handover)
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

        public async Task<bool> IsRoomNumberUniqueOnFloorAsync(int blockId, int floorLevel, string roomNumber, int? excludeId = null)
        {
            return !await _context.Rooms
                .AnyAsync(r => r.BlockId == blockId
                            && r.FloorLevel == floorLevel
                            && r.RoomNumber == roomNumber
                            && (excludeId == null || r.Id != excludeId));
        }

        public async Task<bool> HasActiveBedsAsync(int roomId)
        {
            return await _context.Beds
                .AnyAsync(b => b.RoomId == roomId
                            && (b.Status == "Occupied"
                                || b.Status == "Holding"
                                || b.Status == "Pending Transfer"
                                || b.Residencies.Any(r => r.Status == "Active")));
        }

        public async Task<IEnumerable<Bed>> GetBedsByRoomIdAsync(int roomId)
        {
            return await _context.Beds
                .Where(b => b.RoomId == roomId)
                .ToListAsync();
        }

        public async Task<IEnumerable<RoomType>> GetRoomTypesAsync()
        {
            return await _context.RoomTypes
                .OrderBy(rt => rt.BedCapacity)
                .ToListAsync();
        }

        public async Task<int?> GetBlockNumberOfFloorsAsync(int blockId)
        {
            return await _context.Blocks
                .Where(b => b.Id == blockId)
                .Select(b => b.FloorLevel)
                .FirstOrDefaultAsync();
        }

        public async Task<int?> GetGuardDormIdAsync(int guardUserId)
        {
            return await _context.Guards
                .AsNoTracking()
                .Where(g => g.UserId == guardUserId)
                .Select(g => (int?)g.DormId)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Block>> GetBlocksWithRoomsAsync(int? dormId = null)
        {
            var q = _context.Blocks
                .AsNoTracking()
                .Include(b => b.Dorm)
                .Include(b => b.Rooms)
                .AsQueryable();

            if (dormId.HasValue)
                q = q.Where(b => b.DormId == dormId.Value);

            return await q
                .OrderBy(b => b.Dorm.Name)
                .ThenBy(b => b.Code)
                .ToListAsync();
        }

        public async Task<Room?> GetRoomDetailWithResidentsAsync(int roomId)
        {
            return await _context.Rooms
                .AsNoTracking()
                .Include(r => r.Block)
                    .ThenInclude(b => b.Dorm)
                .Include(r => r.RoomType)
                .Include(r => r.Beds)
                    .ThenInclude(b => b.Residencies)
                        .ThenInclude(res => res.Student)
                            .ThenInclude(s => s.User)
                .Include(r => r.Beds)
                    .ThenInclude(b => b.Residencies)
                        .ThenInclude(res => res.Term)
                .Include(r => r.Beds)
                    .ThenInclude(b => b.Residencies)
                        .ThenInclude(res => res.Handover)
                .FirstOrDefaultAsync(r => r.Id == roomId);
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
