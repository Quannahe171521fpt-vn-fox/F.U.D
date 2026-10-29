using FUD.Common;
using FUD.DTOs.Facilities.News;

namespace FUD.Services.Interfaces.Facilities
{
    public interface INewsService
    {
        Task<PagedResult<NewsDto>> GetAllAsync(NewsQueryDto query);
        Task<NewsDto?> GetByIdAsync(int id);
        Task<IEnumerable<NewsDto>> GetPublishedAsync();
        Task<NewsDto> CreateAsync(CreateNewsDto dto, int? userId = null);
        Task<NewsDto> UpdateAsync(int id, UpdateNewsDto dto, int? userId = null);
        Task DeleteAsync(int id, int? userId = null);
        Task PublishAsync(int id, int? userId = null);
        Task UnpublishAsync(int id, int? userId = null);
    }
}

