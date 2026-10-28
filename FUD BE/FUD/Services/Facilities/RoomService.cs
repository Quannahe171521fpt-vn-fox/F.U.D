using FUD.Common;
using FUD.DTOs.Facilities.Room;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;

namespace FUD.Services.Facilities
{
    public class RoomService : IRoomService
    {
        private readonly IRoomRepository _roomRepo;

        public RoomService(IRoomRepository roomRepo)
        {
            _roomRepo = roomRepo;
        }

        public async Task<PagedResult<RoomDto>> GetAllAsync(RoomQueryDto query)
        {
            var (items, total) = await _roomRepo.GetAllAsync(query);
            return new PagedResult<RoomDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<RoomDto?> GetByIdAsync(int id)
        {
            var room = await _roomRepo.GetByIdAsync(id);
            return room == null ? null : MapToDto(room);
        }

        public async Task<RoomDto> CreateAsync(CreateRoomDto dto)
        {
            if (!await _roomRepo.IsRoomNumberUniqueAsync(dto.BlockId, dto.RoomNumber))
                throw new InvalidOperationException($"Phòng '{dto.RoomNumber}' đã tồn tại trong block này");

            var room = new Room
            {
                BlockId = dto.BlockId,
                RoomTypeId = dto.RoomTypeId,
                RoomNumber = dto.RoomNumber,
                FloorLevel = dto.FloorLevel,
                Status = "Active"
            };

            var created = await _roomRepo.CreateAsync(room);
            var result = await _roomRepo.GetByIdAsync(created.Id);
            return MapToDto(result!);
        }

        public async Task<RoomDto> UpdateAsync(int id, UpdateRoomDto dto)
        {
            var room = await _roomRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Phòng {id} không tồn tại");

            if (!await _roomRepo.IsRoomNumberUniqueAsync(room.BlockId, dto.RoomNumber, id))
                throw new InvalidOperationException($"Phòng '{dto.RoomNumber}' đã tồn tại trong block này");

            room.RoomTypeId = dto.RoomTypeId;
            room.RoomNumber = dto.RoomNumber;
            room.FloorLevel = dto.FloorLevel;

            await _roomRepo.UpdateAsync(room);
            var result = await _roomRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<RoomDto> UpdateStatusAsync(int id, UpdateRoomStatusDto dto)
        {
            var room = await _roomRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Phòng {id} không tồn tại");

            room.Status = dto.Status;

            if (dto.Status == "Maintenance")
            {
                var beds = await _roomRepo.GetBedsByRoomIdAsync(id);
                foreach (var bed in beds.Where(b => b.Status == "Free"))
                    bed.Status = "Maintenance";
            }

            await _roomRepo.UpdateAsync(room);
            var result = await _roomRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        private static RoomDto MapToDto(Room r) => new()
        {
            Id = r.Id,
            BlockId = r.BlockId,
            BlockName = r.Block?.Name ?? string.Empty,
            BlockGenderType = r.Block?.GenderType ?? string.Empty,
            RoomTypeId = r.RoomTypeId,
            RoomTypeName = r.RoomType?.Name ?? string.Empty,
            BedCapacity = r.RoomType?.BedCapacity ?? 0,
            RoomNumber = r.RoomNumber,
            FloorLevel = r.FloorLevel,
            Status = r.Status,
            TotalBeds = r.Beds?.Count ?? 0,
            FreeBeds = r.Beds?.Count(b => b.Status == "Free") ?? 0
        };
    }
}
