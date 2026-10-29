using FUD.Common;
using FUD.DTOs.Facilities.Equipment;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;

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

        public async Task<EquipmentDto> UpdateAsync(int id, UpdateEquipmentDto dto, int? userId = null)
        {
            var equip = await _repo.GetByIdAsync(id);
            if (equip == null) throw new KeyNotFoundException($"Thiết bị id {id} không tồn tại");

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

            var hasOpen = await _repo.HasOpenTicketAsync(id);
            if (hasOpen)
            {
                throw new InvalidOperationException("Thiết bị đang có phiếu báo hỏng/sửa chữa chưa hoàn tất, không thể thanh lý (Equipment in use)");
            }

            var oldCond = equip.Condition;
            equip.Condition = "Liquidated";
            await _repo.UpdateAsync(equip);

            await LogAuditAsync(userId, "LIQUIDATE", "Equipment", id.ToString(), oldCond, "Liquidated");
        }

        public async Task<IEnumerable<TicketDto>> GetRepairHistoryAsync(int id)
        {
            var tickets = await _repo.GetTicketsByEquipmentAsync(id);
            return tickets.Select(t => new TicketDto
            {
                Id = t.Id,
                TicketCode = t.TicketCode,
                Description = t.Description,
                Status = t.Status,
                HandlingResult = t.HandlingResult,
                CreatedTime = t.CreatedTime,
                DoneTime = t.DoneTime
            });
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
                InUseCount = c.Equipment.Count(e => e.Condition != "Liquidated")
            });
        }

        public async Task<EquipmentCatalogDto> CreateCatalogAsync(CreateEquipmentCatalogDto dto)
        {
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
            var blockCode = e.Room?.Block?.Code ?? "";
            if (e.Bed != null && e.Room != null)
            {
                loc = $"Giường {e.Bed.BedCode}/{blockCode}{e.Room.RoomNumber}";
            }
            else if (e.Room != null)
            {
                loc = $"Phòng {blockCode}{e.Room.RoomNumber}";
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
                PurchaseDate = e.PurchaseDate,
                Note = e.Note,
                HasOpenTicket = e.IncidentTickets != null && e.IncidentTickets.Any(t => t.Status != "Closed" && t.Status != "Done")
            };
        }
    }
}
