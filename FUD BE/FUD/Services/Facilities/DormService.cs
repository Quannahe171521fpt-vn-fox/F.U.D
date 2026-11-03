using FUD.Common;
using FUD.DTOs.Facilities.Dorm;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;

namespace FUD.Services.Facilities
{
    public class DormService : IDormService
    {
        private readonly IDormRepository _dormRepo;

        public DormService(IDormRepository dormRepo)
        {
            _dormRepo = dormRepo;
        }

        public async Task<PagedResult<DormDto>> GetAllAsync(DormQueryDto query)
        {
            var (items, total) = await _dormRepo.GetAllAsync(query);
            return new PagedResult<DormDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<DormDto?> GetByIdAsync(int id)
        {
            var dorm = await _dormRepo.GetByIdAsync(id);
            return dorm == null ? null : MapToDto(dorm);
        }

        public async Task<DormDto> CreateAsync(CreateDormDto dto)
        {
            // Kiểm tra campus tồn tại
            if (!await _dormRepo.CampusExistsAsync(dto.CampusId))
                throw new KeyNotFoundException($"Campus {dto.CampusId} không tồn tại");

            // Kiểm tra code duy nhất trong cùng campus
            if (!await _dormRepo.IsCodeUniqueAsync(dto.CampusId, dto.Code))
                throw new InvalidOperationException($"Mã KTX '{dto.Code}' đã tồn tại trong Campus này");

            var dorm = new Dorm
            {
                CampusId = dto.CampusId,
                Code = dto.Code,
                Name = dto.Name,
                Address = dto.Address,
                Status = "Active"
            };

            var created = await _dormRepo.CreateAsync(dorm);
            var result = await _dormRepo.GetByIdAsync(created.Id);
            return MapToDto(result!);
        }

        public async Task<DormDto> UpdateAsync(int id, UpdateDormDto dto)
        {
            var dorm = await _dormRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"KTX {id} không tồn tại");

            dorm.Name = dto.Name;
            dorm.Address = dto.Address;

            await _dormRepo.UpdateAsync(dorm);
            var result = await _dormRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task<DormDto> UpdateStatusAsync(int id, UpdateDormStatusDto dto)
        {
            var dorm = await _dormRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"KTX {id} không tồn tại");

            dorm.Status = dto.Status;
            await _dormRepo.UpdateAsync(dorm);
            var result = await _dormRepo.GetByIdAsync(id);
            return MapToDto(result!);
        }

        public async Task SoftDeleteAsync(int id)
        {
            var dorm = await _dormRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"KTX {id} không tồn tại");

            // Không xóa cứng nếu còn tòa (Block) đang hoạt động
            if (await _dormRepo.HasActiveBlocksAsync(id))
                throw new InvalidOperationException(
                    $"Không thể xóa KTX '{dorm.Name}' vì còn {dorm.Blocks?.Count ?? 0} tòa đang hoạt động. " +
                    "Vui lòng xóa hoặc vô hiệu hóa tất cả các tòa trước.");

            await _dormRepo.SoftDeleteAsync(id);
        }

        private static DormDto MapToDto(Dorm d) => new()
        {
            Id = d.Id,
            CampusId = d.CampusId,
            CampusName = d.Campus?.Name ?? string.Empty,
            Code = d.Code,
            Name = d.Name,
            Address = d.Address,
            Status = d.Status,
            TotalBlocks = d.Blocks?.Count ?? 0
        };
    }
}
