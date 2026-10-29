using FUD.DTOs.Facilities.News;
using FUD.Models;

namespace FUD.Repositories.Interfaces.Facilities
{
    public interface INewsRepository
    {
        Task<(IEnumerable<News> Items, int TotalCount)> GetAllAsync(NewsQueryDto query);
        Task<News?> GetByIdAsync(int id);
        Task<IEnumerable<News>> GetPublishedAsync();
        Task<News> AddAsync(News news);
        Task UpdateAsync(News news);
        Task DeleteAsync(int id);
        Task AddAttachmentAsync(NewsAttachment attachment);
        Task RemoveAttachmentsByNewsIdAsync(int newsId);
    }
}

