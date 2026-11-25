using FUD.DTOs.Facilities.Equipment;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Facilities
{
    public class EquipmentRepository : IEquipmentRepository
    {
        private readonly FudContext _context;

        public EquipmentRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<Equipment> Items, int TotalCount)> GetAllAsync(EquipmentQueryDto query)
        {
            var q = _context.Equipments
                .Include(e => e.Catalog)
                .Include(e => e.Room)
                    .ThenInclude(r => r.Block)
                .Include(e => e.Bed)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim().ToLower();
                q = q.Where(e => e.AssetCode.ToLower().Contains(s) || e.Catalog.Name.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(query.Condition) && query.Condition != "All")
            {
                q = q.Where(e => e.Condition == query.Condition);
            }

            if (query.RoomId.HasValue)
            {
                q = q.Where(e => e.RoomId == query.RoomId.Value);
            }

            if (query.CatalogId.HasValue)
            {
                q = q.Where(e => e.CatalogId == query.CatalogId.Value);
            }

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderBy(e => e.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Equipment?> GetByIdAsync(int id)
        {
            return await _context.Equipments
                .Include(e => e.Catalog)
                .Include(e => e.Room)
                    .ThenInclude(r => r.Block)
                .Include(e => e.Bed)
                .FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<Equipment?> GetByAssetCodeAsync(string assetCode)
        {
            return await _context.Equipments
                .Include(e => e.Catalog)
                .Include(e => e.Room)
                    .ThenInclude(r => r.Block)
                .Include(e => e.Bed)
                .FirstOrDefaultAsync(e => e.AssetCode == assetCode);
        }

        public async Task<Equipment> AddAsync(Equipment equipment)
        {
            await _context.Equipments.AddAsync(equipment);
            await _context.SaveChangesAsync();
            return equipment;
        }

        public async Task UpdateAsync(Equipment equipment)
        {
            _context.Equipments.Update(equipment);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<EquipmentCatalog>> GetCatalogsAsync()
        {
            return await _context.EquipmentCatalogs
                .Include(c => c.Equipment)
                .ToListAsync();
        }

        public async Task<EquipmentCatalog?> GetCatalogByIdAsync(int id)
        {
            return await _context.EquipmentCatalogs
                .Include(c => c.Equipment)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<EquipmentCatalog> AddCatalogAsync(EquipmentCatalog catalog)
        {
            await _context.EquipmentCatalogs.AddAsync(catalog);
            await _context.SaveChangesAsync();
            return catalog;
        }

        public async Task<bool> DeleteCatalogAsync(int id)
        {
            var item = await _context.EquipmentCatalogs.FindAsync(id);
            if (item == null) return false;
            
            var inUse = await _context.Equipments.AnyAsync(e => e.CatalogId == id);
            if (inUse)
            {
                item.ActiveFlag = false;
            }
            else
            {
                _context.EquipmentCatalogs.Remove(item);
            }
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<LocationOptionDto>> GetLocationsAsync()
        {
            var rooms = await _context.Rooms
                .Include(r => r.Block)
                .OrderBy(r => r.Block.Code)
                .ThenBy(r => r.RoomNumber)
                .ToListAsync();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = new List<LocationOptionDto>();
            foreach (var r in rooms)
            {
                var blockCode = r.Block != null ? r.Block.Code.Trim() : "";
                var roomNum = (r.RoomNumber ?? "").Trim();
                string roomLabel;

                if (string.IsNullOrWhiteSpace(blockCode))
                {
                    roomLabel = $"Phòng {roomNum}";
                }
                else if (roomNum.StartsWith(blockCode, StringComparison.OrdinalIgnoreCase))
                {
                    roomLabel = $"Phòng {roomNum}";
                }
                else
                {
                    roomLabel = $"Phòng {blockCode} - {roomNum}";
                }

                // Bỏ qua nếu label đã được thêm (tránh trùng lặp)
                if (!seen.Add(roomLabel)) continue;

                list.Add(new LocationOptionDto
                {
                    RoomId = r.Id,
                    BedId = null,
                    Label = roomLabel
                });
            }

            return list;
        }
    }
}
