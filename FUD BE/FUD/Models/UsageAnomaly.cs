using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class UsageAnomaly
{
    public int Id { get; set; }

    public int ReadingId { get; set; }

    public string FlagReason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Resolution { get; set; }

    public string? ManagerNote { get; set; }

    public int? ResolvedById { get; set; }

    public DateTime? ResolvedTime { get; set; }

    public DateTime CreatedTime { get; set; }

    public virtual ICollection<InspectionRequest> InspectionRequests { get; set; } = new List<InspectionRequest>();

    public virtual MeterReading Reading { get; set; } = null!;

    public virtual Manager? ResolvedBy { get; set; }
}
