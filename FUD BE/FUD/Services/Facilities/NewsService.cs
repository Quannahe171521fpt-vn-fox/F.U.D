using FUD.Common;
using FUD.DTOs.Facilities.News;
using FUD.Models;
using FUD.Repositories.Interfaces.Facilities;
using FUD.Services.Interfaces.Facilities;
using Microsoft.EntityFrameworkCore;

namespace FUD.Services.Facilities
{
    public class NewsService : INewsService
    {
        private readonly INewsRepository _repo;
        private readonly FudContext _context;

        public NewsService(INewsRepository repo, FudContext context)
        {
            _repo = repo;
            _context = context;
        }

        public async Task<PagedResult<NewsDto>> GetAllAsync(NewsQueryDto query)
        {
            var (items, total) = await _repo.GetAllAsync(query);

            var newsList = items.ToList();
            var newsIdsStr = newsList.Select(n => n.Id.ToString()).ToList();

            // Lấy nhật ký AuditLogs liên quan đến các bản tin để lấy ngày tạo và ngày ẩn
            var logs = await _context.AuditLogs
                .Where(a => a.TargetEntity == "News" && newsIdsStr.Contains(a.TargetId!))
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            var dtos = newsList.Select(n =>
            {
                var dto = MapToDto(n);
                var relatedLogs = logs.Where(l => l.TargetId == n.Id.ToString()).ToList();

                var createLog = relatedLogs.FirstOrDefault(l => l.ActionType == "CREATE");
                dto.CreatedAt = createLog?.CreatedAt ?? n.PublishedTime;

                var unpublishLog = relatedLogs.LastOrDefault(l => l.ActionType == "UNPUBLISH");
                if (n.Status == "Draft" && unpublishLog != null)
                {
                    dto.HiddenAt = unpublishLog.CreatedAt;
                }

                return dto;
            });

            return new PagedResult<NewsDto>
            {
                Items = dtos,
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<NewsDto?> GetByIdAsync(int id)
        {
            var item = await _repo.GetByIdAsync(id);
            return item == null ? null : MapToDto(item);
        }

        public async Task<IEnumerable<NewsDto>> GetPublishedAsync()
        {
            var items = await _repo.GetPublishedAsync();
            return items.Select(MapToDto);
        }

        public async Task<NewsDto> CreateAsync(CreateNewsDto dto, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
            {
                throw new ArgumentException("Tiêu đề và nội dung bản tin không được để trống (Empty title or content)");
            }

            var isPublish = dto.Action?.Equals("Publish", StringComparison.OrdinalIgnoreCase) == true
                || dto.Action?.Equals("Published", StringComparison.OrdinalIgnoreCase) == true;
            var authorId = userId ?? 2; // Default Manager ID 2 in seed

            var news = new News
            {
                AuthorId = authorId,
                Title = dto.Title.Trim(),
                Content = dto.Content.Trim(),
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "News" : dto.Category,
                Status = isPublish ? "Published" : "Draft",
                PublishedTime = isPublish ? DateTime.UtcNow : null
            };

            var created = await _repo.AddAsync(news);

            // Add attachments
            if (dto.AttachmentUrls != null && dto.AttachmentUrls.Count > 0)
            {
                foreach (var url in dto.AttachmentUrls)
                {
                    var ext = Path.GetExtension(url).TrimStart('.').ToLower();
                    await _repo.AddAttachmentAsync(new NewsAttachment
                    {
                        NewsId = created.Id,
                        FileUrl = url,
                        FileType = ext
                    });
                }
            }

            // Notification to students if published
            if (isPublish)
            {
                await NotifyStudentsAsync(created);
            }

            // Audit Log
            await LogAuditAsync(authorId, "CREATE", "News", created.Id.ToString(), null, $"Tạo tin: {created.Title}");

            var full = await _repo.GetByIdAsync(created.Id);
            return MapToDto(full ?? created);
        }

        public async Task<NewsDto> UpdateAsync(int id, UpdateNewsDto dto, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Content))
            {
                throw new ArgumentException("Tiêu đề và nội dung bản tin không được để trống (Empty title or content)");
            }

            var news = await _repo.GetByIdAsync(id);
            if (news == null) throw new KeyNotFoundException($"Bản tin id {id} không tồn tại");

            var oldVal = $"Title: {news.Title}, Status: {news.Status}";

            news.Title = dto.Title.Trim();
            news.Content = dto.Content.Trim();
            news.Category = string.IsNullOrWhiteSpace(dto.Category) ? news.Category : dto.Category;

            if (!string.IsNullOrWhiteSpace(dto.Action))
            {
                if (dto.Action.Equals("Publish", StringComparison.OrdinalIgnoreCase) || dto.Action.Equals("Published", StringComparison.OrdinalIgnoreCase))
                {
                    news.Status = "Published";
                    news.PublishedTime ??= DateTime.UtcNow;
                }
                else if (dto.Action.Equals("Draft", StringComparison.OrdinalIgnoreCase))
                {
                    news.Status = "Draft";
                    news.PublishedTime = null;
                }
            }

            await _repo.UpdateAsync(news);

            // Replace attachments if provided
            if (dto.AttachmentUrls != null)
            {
                await _repo.RemoveAttachmentsByNewsIdAsync(id);
                foreach (var url in dto.AttachmentUrls)
                {
                    var ext = Path.GetExtension(url).TrimStart('.').ToLower();
                    await _repo.AddAttachmentAsync(new NewsAttachment
                    {
                        NewsId = id,
                        FileUrl = url,
                        FileType = ext
                    });
                }
            }

            var newVal = $"Title: {news.Title}, Status: {news.Status}";
            await LogAuditAsync(userId ?? news.AuthorId, "UPDATE", "News", id.ToString(), oldVal, newVal);

            var full = await _repo.GetByIdAsync(id);
            return MapToDto(full ?? news);
        }

        public async Task DeleteAsync(int id, int? userId = null)
        {
            var news = await _repo.GetByIdAsync(id);
            if (news == null) throw new KeyNotFoundException($"Bản tin id {id} không tồn tại");

            // Quy tắc nghiệp vụ: Bản tin không xóa cứng khỏi cơ sở dữ liệu, mà chỉ chuyển sang ẩn (Draft)
            news.Status = "Draft";
            news.PublishedTime = null;
            await _repo.UpdateAsync(news);

            await LogAuditAsync(userId ?? news.AuthorId, "UNPUBLISH", "News", id.ToString(), news.Title, "Bản tin đã được ẩn (chuyển sang nháp)");
        }

        public async Task PublishAsync(int id, int? userId = null)
        {
            var news = await _repo.GetByIdAsync(id);
            if (news == null) throw new KeyNotFoundException($"Bản tin id {id} không tồn tại");

            news.Status = "Published";
            news.PublishedTime = DateTime.UtcNow;
            await _repo.UpdateAsync(news);

            await NotifyStudentsAsync(news);
            await LogAuditAsync(userId ?? news.AuthorId, "PUBLISH", "News", id.ToString(), "Draft", "Published");
        }

        public async Task UnpublishAsync(int id, int? userId = null)
        {
            var news = await _repo.GetByIdAsync(id);
            if (news == null) throw new KeyNotFoundException($"Bản tin id {id} không tồn tại");

            news.Status = "Draft";
            news.PublishedTime = null;
            await _repo.UpdateAsync(news);

            await LogAuditAsync(userId ?? news.AuthorId, "UNPUBLISH", "News", id.ToString(), "Published", "Draft");
        }

        private async Task NotifyStudentsAsync(News news)
        {
            try
            {
                // Find all active students
                var studentUserIds = await _context.Students.Select(s => s.UserId).ToListAsync();
                foreach (var uid in studentUserIds)
                {
                    var noti = new Notification
                    {
                        UserId = uid,
                        Channel = "InApp",
                        Title = $"Bản tin mới: {news.Title}",
                        Content = news.Content.Length > 100 ? news.Content.Substring(0, 97) + "..." : news.Content,
                        ReferenceType = "News",
                        ReferenceId = news.Id.ToString(),
                        ReadFlag = false,
                        SentTime = DateTime.UtcNow
                    };
                    await _context.Notifications.AddAsync(noti);
                }
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Suppress notification dispatch failure
            }
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

        private static NewsDto MapToDto(News n)
        {
            return new NewsDto
            {
                Id = n.Id,
                AuthorId = n.AuthorId,
                AuthorName = n.Author?.User?.FullName ?? "Ban Quản Lý",
                ApprovedById = n.ApprovedById,
                Title = n.Title,
                Content = n.Content,
                Category = n.Category,
                Status = n.Status,
                PublishedTime = n.PublishedTime,
                Attachments = n.NewsAttachments?.Select(a => new NewsAttachmentDto
                {
                    Id = a.Id,
                    FileUrl = a.FileUrl,
                    FileType = a.FileType ?? Path.GetExtension(a.FileUrl).TrimStart('.'),
                    FileName = Path.GetFileName(a.FileUrl),
                    FileSize = "1.0 MB"
                }).ToList() ?? new List<NewsAttachmentDto>()
            };
        }
    }
}

