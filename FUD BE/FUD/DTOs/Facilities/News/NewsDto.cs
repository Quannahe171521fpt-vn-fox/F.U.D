namespace FUD.DTOs.Facilities.News
{
    public class NewsDto
    {
        public int Id { get; set; }
        public int AuthorId { get; set; }
        public string? AuthorName { get; set; }
        public int? ApprovedById { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = "News";
        public string Status { get; set; } = "Draft"; // 'Draft','Pending approval','Returned','Published','Archived'
        public DateTime? PublishedTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? HiddenAt { get; set; }
        public List<NewsAttachmentDto> Attachments { get; set; } = new();
    }

    public class NewsAttachmentDto
    {
        public int Id { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? FileType { get; set; }
        public string? FileName { get; set; }
        public string? FileSize { get; set; }
    }

    public class NewsQueryDto
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? Category { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class CreateNewsDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = "News";
        public string Action { get; set; } = "Draft"; // "Draft" or "Publish"
        public List<string>? AttachmentUrls { get; set; }
    }

    public class UpdateNewsDto
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = "News";
        public string? Action { get; set; } // "Draft" or "Publish"
        public List<string>? AttachmentUrls { get; set; }
    }
}

