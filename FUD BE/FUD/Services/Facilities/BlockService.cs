using FUD.Common;
using FUD.DTOs.Facilities.Block;
using FUD.DTOs.Facilities.Room;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;

namespace FUD.Services.Facilities
{
    public class BlockService : IBlockService
    {
        private readonly IBlockRepository _blockRepo;

        public BlockService(IBlockRepository blockRepo)
        {
            _blockRepo = blockRepo;
        }

        public async Task<PagedResult<BlockDto>> GetAllAsync(BlockQueryDto query)
        {
            var (items, total) = await _blockRepo.GetAllAsync(query);
            return new PagedResult<BlockDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<BlockDto?> GetByIdAsync(int id)
        {
            var block = await _blockRepo.GetByIdAsync(id);
            return block == null ? null : MapToDto(block);
        }

        public async Task<IEnumerable<RoomDto>> GetRoomsByBlockIdAsync(int blockId)
        {
            var block = await _blockRepo.GetByIdWithRoomsAsync(blockId);
            if (block == null) throw new KeyNotFoundException($"Block {blockId} không tồn tại");

            return block.Rooms.Select(r => new RoomDto
            {
                Id = r.Id,
                BlockId = r.BlockId,
                BlockName = block.Name,
                BlockGenderType = block.GenderType,
                RoomTypeId = r.RoomTypeId,
                RoomTypeName = r.RoomType?.Name ?? string.Empty,
                BedCapacity = r.RoomType?.BedCapacity ?? 0,
                RoomNumber = r.RoomNumber,
                FloorLevel = r.FloorLevel,
                Status = r.Status,
                TotalBeds = r.Beds.Count,
                FreeBeds = r.Beds.Count(b => b.Status == "Free")
            });
        }

        public async Task<BlockDto> CreateAsync(CreateBlockDto dto)
        {
            if (!await _blockRepo.IsCodeUniqueAsync(dto.DormId, dto.Code))
                throw new InvalidOperationException($"Code '{dto.Code}' đã tồn tại trong Dorm này");

            var block = new Block
            {
                DormId = dto.DormId,
                Code = dto.Code,
                Name = dto.Name,
                NumberOfFloors = dto.NumberOfFloors,
                GenderType = dto.GenderType,
                Status = "Active"
            };

            var created = await _blockRepo.CreateAsync(block);
            var result = await _blockRepo.GetByIdAsync(created.Id);
            return MapToDto(result!);
        }

        public async Task<BlockDto> UpdateAsync(int id, UpdateBlockDto dto)
        {
            var block = await _blockRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Block {id} không tồn tại");

            block.Name = dto.Name;
            block.NumberOfFloors = dto.NumberOfFloors;

            await _blockRepo.UpdateAsync(block);
            var result = await _blockRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<BlockDto> UpdateStatusAsync(int id, UpdateBlockStatusDto dto)
        {
            var block = await _blockRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Block {id} không tồn tại");

            block.Status = dto.Status;
            await _blockRepo.UpdateAsync(block);
            var result = await _blockRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<BlockDto> UpdateGenderAsync(int id, UpdateBlockGenderDto dto)
        {
            var block = await _blockRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Block {id} không tồn tại");

            if (await _blockRepo.HasActiveResidenciesAsync(id))
                throw new InvalidOperationException("Không thể đổi giới tính khi block còn sinh viên đang cư trú");

            block.GenderType = dto.GenderType;
            await _blockRepo.UpdateAsync(block);
            var result = await _blockRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        private static BlockDto MapToDto(Block b) => new()
        {
            Id = b.Id,
            DormId = b.DormId,
            DormName = b.Dorm?.Name ?? string.Empty,
            Code = b.Code,
            Name = b.Name,
            NumberOfFloors = b.NumberOfFloors,
            GenderType = b.GenderType,
            Status = b.Status,
            TotalRooms = b.Rooms?.Count ?? 0
        };
    }
}
