using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class RequestLog
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime ChangeTime { get; set; }

    public int? ChangedById { get; set; }

    public virtual User? ChangedBy { get; set; }

    public virtual Request Request { get; set; } = null!;
}
