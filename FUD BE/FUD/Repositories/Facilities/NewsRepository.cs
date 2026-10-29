using FUD.DTOs.Facilities.News;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories.Facilities
{
    public class NewsRepository : INewsRepository
    {
        private readonly FudContext _context;

        public NewsRepository(FudContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<News> Items, int TotalCount)> GetAllAsync(NewsQueryDto query)
        {
            var q = _context.News
                .Include(n => n.Author)
                    .ThenInclude(m => m.User)
                .Include(n => n.NewsAttachments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim().ToLower();
                q = q.Where(n => n.Title.ToLower().Contains(s) || n.Content.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
            {
                q = q.Where(n => n.Status == query.Status);
            }

            if (!string.IsNullOrWhiteSpace(query.Category) && query.Category != "All")
            {
                q = q.Where(n => n.Category == query.Category);
            }

            var totalCount = await q.CountAsync();

            var items = await q
                .OrderByDescending(n => n.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<News?> GetByIdAsync(int id)
        {
            return await _context.News
                .Include(n => n.Author)
                    .ThenInclude(m => m.User)
                .Include(n => n.NewsAttachments)
                .FirstOrDefaultAsync(n => n.Id == id);
        }

        public async Task<IEnumerable<News>> GetPublishedAsync()
        {
            return await _context.News
                .Include(n => n.Author)
                    .ThenInclude(m => m.User)
                .Include(n => n.NewsAttachments)
                .Where(n => n.Status == "Published")
                .OrderByDescending(n => n.PublishedTime)
                .ToListAsync();
        }

        public async Task<News> AddAsync(News news)
        {
            await _context.News.AddAsync(news);
            await _context.SaveChangesAsync();
            return news;
        }

        public async Task UpdateAsync(News news)
        {
            _context.News.Update(news);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var news = await _context.News.Include(n => n.NewsAttachments).FirstOrDefaultAsync(n => n.Id == id);
            if (news != null)
            {
                _context.NewsAttachments.RemoveRange(news.NewsAttachments);
                _context.News.Remove(news);
                await _context.SaveChangesAsync();
            }
        }

        public async Task AddAttachmentAsync(NewsAttachment attachment)
        {
            await _context.NewsAttachments.AddAsync(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAttachmentsByNewsIdAsync(int newsId)
        {
            var atts = await _context.NewsAttachments.Where(a => a.NewsId == newsId).ToListAsync();
            _context.NewsAttachments.RemoveRange(atts);
            await _context.SaveChangesAsync();
        }
    }
}

