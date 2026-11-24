using FUD.Common;
using FUD.DTOs.Facilities.Bed;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;

namespace FUD.Services.Facilities
{
    public class BedService : IBedService
    {
        private readonly IBedRepository _bedRepo;

        public BedService(IBedRepository bedRepo)
        {
            _bedRepo = bedRepo;
        }

        public async Task<PagedResult<BedDto>> GetAllAsync(BedQueryDto query)
        {
            var (items, total) = await _bedRepo.GetAllAsync(query);
            return new PagedResult<BedDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<BedDto?> GetByIdAsync(int id)
        {
            var bed = await _bedRepo.GetByIdAsync(id);
            return bed == null ? null : MapToDto(bed);
        }

        public async Task<BedDto> CreateAsync(CreateBedDto dto)
        {
            var room = await _bedRepo.GetRoomWithTypeAndBedsAsync(dto.RoomId)
                ?? throw new KeyNotFoundException($"Phòng {dto.RoomId} không tồn tại.");

            if (room.RoomType is { BedCapacity: > 0 } roomType)
            {
                var currentBedCount = room.Beds?.Count ?? 0;
                if (currentBedCount >= roomType.BedCapacity)
                {
                    var roomTypeName = !string.IsNullOrWhiteSpace(roomType.Name)
                        ? roomType.Name
                        : $"loại {roomType.BedCapacity} giường";
                    throw new InvalidOperationException(
                        $"Phòng {room.RoomNumber} ({roomTypeName}) đã đủ {currentBedCount}/{roomType.BedCapacity} giường, không thể thêm giường mới.");
                }
            }

            if (!await _bedRepo.IsBedCodeUniqueAsync(dto.RoomId, dto.BedCode))
                throw new InvalidOperationException($"Mã giường '{dto.BedCode}' đã tồn tại trong phòng này.");

            var bed = new Bed
            {
                RoomId = dto.RoomId,
                BedCode = dto.BedCode,
                PositionInRoom = dto.PositionInRoom,
                Status = "Free"
            };

            var created = await _bedRepo.CreateAsync(bed);
            var result = await _bedRepo.GetByIdAsync(created.Id);
            return MapToDto(result!);
        }

        public async Task<BedDto> UpdateAsync(int id, UpdateBedDto dto)
        {
            var bed = await _bedRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Giường {id} không tồn tại.");

            if (!await _bedRepo.IsBedCodeUniqueAsync(bed.RoomId, dto.BedCode, id))
                throw new InvalidOperationException($"Mã giường '{dto.BedCode}' đã tồn tại trong phòng này.");

            bed.BedCode = dto.BedCode;
            bed.PositionInRoom = dto.PositionInRoom;

            await _bedRepo.UpdateAsync(bed);
            var result = await _bedRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<BedDto> UpdateStatusAsync(int id, UpdateBedStatusDto dto)
        {
            var bed = await _bedRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Giường {id} không tồn tại.");

            if (dto.Status == "Maintenance" && await _bedRepo.HasActiveResidencyAsync(id))
            {
                throw new InvalidOperationException(
                    "Không thể chuyển giường sang trạng thái bảo trì vì đang có sinh viên cư trú hoặc giữ chỗ.");
            }

            bed.Status = dto.Status;
            await _bedRepo.UpdateAsync(bed);

            var result = await _bedRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        private static BedDto MapToDto(Bed b) => new()
        {
            Id = b.Id,
            RoomId = b.RoomId,
            RoomNumber = b.Room?.RoomNumber ?? string.Empty,
            BlockName = b.Room?.Block?.Name ?? string.Empty,
            BedCode = b.BedCode,
            PositionInRoom = b.PositionInRoom,
            Status = b.Status
        };
    }
}

