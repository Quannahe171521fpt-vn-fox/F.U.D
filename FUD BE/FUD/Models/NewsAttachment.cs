using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class NewsAttachment
{
    public int Id { get; set; }

    public int NewsId { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? FileType { get; set; }

    public virtual News News { get; set; } = null!;
}
