using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Channel { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Content { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public bool ReadFlag { get; set; }

    public DateTime? ReadTime { get; set; }

    public DateTime SentTime { get; set; }

    public virtual User User { get; set; } = null!;
}
