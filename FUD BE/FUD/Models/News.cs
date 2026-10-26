using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class News
{
    public int Id { get; set; }

    public int AuthorId { get; set; }

    public int? ApprovedById { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string Category { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? PublishedTime { get; set; }

    public virtual Manager? ApprovedBy { get; set; }

    public virtual Manager Author { get; set; } = null!;

    public virtual ICollection<NewsAttachment> NewsAttachments { get; set; } = new List<NewsAttachment>();
}
