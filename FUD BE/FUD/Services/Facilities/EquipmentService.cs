using FUD.Common;
using FUD.DTOs.Facilities.Equipment;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Facilities
{
    public class EquipmentService : IEquipmentService
    {
        private readonly IEquipmentRepository _repo;
        private readonly FudContext _context;

        public EquipmentService(IEquipmentRepository repo, FudContext context)
        {
            _repo = repo;
            _context = context;
        }

        public async Task<PagedResult<EquipmentDto>> GetAllAsync(EquipmentQueryDto query)
        {
            var (items, total) = await _repo.GetAllAsync(query);
            return new PagedResult<EquipmentDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<EquipmentDto?> GetByIdAsync(int id)
        {
            var item = await _repo.GetByIdAsync(id);
            return item == null ? null : MapToDto(item);
        }

        public async Task<EquipmentDto> CreateAsync(CreateEquipmentDto dto, int? userId = null)
        {
            var existing = await _repo.GetByAssetCodeAsync(dto.AssetCode);
            if (existing != null)
            {
                throw new InvalidOperationException($"Mã thiết bị '{dto.AssetCode}' đã tồn tại trong hệ thống (Duplicate Asset Code)");
            }

            var equip = new Equipment
            {
                AssetCode = dto.AssetCode,
                CatalogId = dto.CatalogId,
                RoomId = dto.RoomId,
                BedId = dto.BedId,
                Condition = string.IsNullOrWhiteSpace(dto.Condition) ? "Good" : dto.Condition,
                PurchaseDate = dto.PurchaseDate,
                Note = dto.Note
            };

            var created = await _repo.AddAsync(equip);

            // Audit Log
            await LogAuditAsync(userId, "CREATE", "Equipment", created.Id.ToString(), null, $"Tạo thiết bị {created.AssetCode}");

            var full = await _repo.GetByIdAsync(created.Id);
            return MapToDto(full ?? created);
        }

        private const string HiddenMarker = "[HIDDEN]";

        private static bool IsEquipmentHidden(Equipment e) =>
            !string.IsNullOrEmpty(e.Note) && e.Note.Contains(HiddenMarker, StringComparison.Ordinal);

        public async Task<EquipmentDto> UpdateAsync(int id, UpdateEquipmentDto dto, int? userId = null)
        {
            var equip = await _repo.GetByIdAsync(id);
            if (equip == null) throw new KeyNotFoundException($"Thiết bị id {id} không tồn tại");
            if (IsEquipmentHidden(equip))
                throw new InvalidOperationException("Thiết bị đang bị ẩn, không thể chỉnh sửa.");

            var oldVal = $"Catalog: {equip.CatalogId}, Room: {equip.RoomId}, Bed: {equip.BedId}, Condition: {equip.Condition}";

            equip.CatalogId = dto.CatalogId;
            equip.RoomId = dto.RoomId;
            equip.BedId = dto.BedId;
            equip.Condition = dto.Condition;
            equip.PurchaseDate = dto.PurchaseDate;
            equip.Note = dto.Note;

            await _repo.UpdateAsync(equip);

            var newVal = $"Catalog: {equip.CatalogId}, Room: {equip.RoomId}, Bed: {equip.BedId}, Condition: {equip.Condition}";
            await LogAuditAsync(userId, "UPDATE", "Equipment", id.ToString(), oldVal, newVal);

            var full = await _repo.GetByIdAsync(id);
            return MapToDto(full ?? equip);
        }

        public async Task<EquipmentDto> MoveAsync(int id, MoveEquipmentDto dto, int? userId = null)
        {
            var equip = await _repo.GetByIdAsync(id);
            if (equip == null) throw new KeyNotFoundException($"Thiết bị id {id} không tồn tại");
            if (IsEquipmentHidden(equip))
                throw new InvalidOperationException("Thiết bị đang bị ẩn, không thể di chuyển.");

            var oldLoc = $"Room: {equip.RoomId}, Bed: {equip.BedId}";
            equip.RoomId = dto.RoomId;
            equip.BedId = dto.BedId;

            await _repo.UpdateAsync(equip);

            var newLoc = $"Room: {equip.RoomId}, Bed: {equip.BedId}";
            await LogAuditAsync(userId, "MOVE", "Equipment", id.ToString(), oldLoc, newLoc);

            var full = await _repo.GetByIdAsync(id);
            return MapToDto(full ?? equip);
        }

        public async Task<EquipmentDto> ChangeConditionAsync(int id, ChangeConditionDto dto, int? userId = null)
        {
            var equip = await _repo.GetByIdAsync(id);
            if (equip == null) throw new KeyNotFoundException($"Thiết bị id {id} không tồn tại");
            if (IsEquipmentHidden(equip))
                throw new InvalidOperationException("Thiết bị đang bị ẩn, không thể thay đổi tình trạng.");

            var oldCond = equip.Condition;
            equip.Condition = dto.Condition;

            await _repo.UpdateAsync(equip);

            await LogAuditAsync(userId, "CHANGE_CONDITION", "Equipment", id.ToString(), oldCond, dto.Condition);

            var full = await _repo.GetByIdAsync(id);
            return MapToDto(full ?? equip);
        }

        public async Task LiquidateAsync(int id, int? userId = null)
        {
            var equip = await _repo.GetByIdAsync(id);
            if (equip == null) throw new KeyNotFoundException($"Thiết bị id {id} không tồn tại");

            var wasHidden = IsEquipmentHidden(equip);
            if (wasHidden)
            {
                var cleaned = equip.Note!.Replace(HiddenMarker, string.Empty, StringComparison.Ordinal).Trim();
                equip.Note = string.IsNullOrEmpty(cleaned) ? null : cleaned;
            }
            else
            {
                equip.Note = string.IsNullOrWhiteSpace(equip.Note)
                    ? HiddenMarker
                    : $"{HiddenMarker} {equip.Note.Trim()}";
            }

            await _repo.UpdateAsync(equip);

            await LogAuditAsync(userId, "TOGGLE_VISIBILITY", "Equipment", id.ToString(), wasHidden ? "Hidden" : "Visible", wasHidden ? "Visible" : "Hidden");
        }

        public async Task<IEnumerable<EquipmentCatalogDto>> GetCatalogsAsync()
        {
            var list = await _repo.GetCatalogsAsync();
            return list.Select(c => new EquipmentCatalogDto
            {
                Id = c.Id,
                Category = c.Category,
                Name = c.Name,
                ActiveFlag = c.ActiveFlag,
                InUseCount = c.Equipment.Count(e => !IsEquipmentHidden(e))
            });
        }

        public async Task<EquipmentCatalogDto> CreateCatalogAsync(CreateEquipmentCatalogDto dto)
        {
            // Kiểm tra trùng (Category, Name) trước khi lưu
            var exists = await _context.EquipmentCatalogs
                .AnyAsync(c => c.Category == dto.Category && c.Name == dto.Name);
            if (exists)
                throw new InvalidOperationException($"Loại thiết bị '{dto.Name}' trong nhóm '{dto.Category}' đã tồn tại trong danh mục.");

            var cat = new EquipmentCatalog
            {
                Category = dto.Category,
                Name = dto.Name,
                ActiveFlag = true
            };
            var res = await _repo.AddCatalogAsync(cat);
            return new EquipmentCatalogDto
            {
                Id = res.Id,
                Category = res.Category,
                Name = res.Name,
                ActiveFlag = res.ActiveFlag,
                InUseCount = 0
            };
        }

        public async Task<bool> DeleteCatalogAsync(int id)
        {
            return await _repo.DeleteCatalogAsync(id);
        }

        public async Task<IEnumerable<LocationOptionDto>> GetLocationsAsync()
        {
            return await _repo.GetLocationsAsync();
        }

        private async Task LogAuditAsync(int? userId, string action, string entity, string targetId, string? oldVal, string? newVal)
        {
            try
            {
                var audit = new AuditLog
                {
                    UserId = userId ?? 2,
                    ActionType = action,
                    TargetEntity = entity,
                    TargetId = targetId,
                    OldValue = oldVal,
                    NewValue = newVal,
                    CreatedAt = DateTime.UtcNow
                };
                await _context.AuditLogs.AddAsync(audit);
                await _context.SaveChangesAsync();
            }
            catch
            {
            }
        }

        private static EquipmentDto MapToDto(Equipment e)
        {
            string loc = string.Empty;
            if (e.Room != null)
            {
                var blockCode = e.Room.Block?.Code?.Trim() ?? "";
                var roomNum = (e.Room.RoomNumber ?? "").Trim();
                if (string.IsNullOrWhiteSpace(blockCode) || roomNum.StartsWith(blockCode, StringComparison.OrdinalIgnoreCase))
                {
                    loc = $"Phòng {roomNum}";
                }
                else
                {
                    loc = $"Phòng {blockCode} - {roomNum}";
                }
            }

            return new EquipmentDto
            {
                Id = e.Id,
                CatalogId = e.CatalogId,
                Name = e.Catalog?.Name ?? "Thiết bị",
                Category = e.Catalog?.Category ?? "Chung",
                AssetCode = e.AssetCode,
                RoomId = e.RoomId,
                RoomNumber = e.Room?.RoomNumber,
                BedId = e.BedId,
                BedCode = e.Bed?.BedCode,
                Location = loc,
                Condition = e.Condition,
                IsHidden = IsEquipmentHidden(e),
                PurchaseDate = e.PurchaseDate,
                Note = e.Note,
                HasOpenTicket = false
            };
        }
    }
}
