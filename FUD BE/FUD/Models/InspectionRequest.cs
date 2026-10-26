using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class InspectionRequest
{
    public int Id { get; set; }

    public int AnomalyId { get; set; }

    public int CreatedById { get; set; }

    public int AssignedGuardId { get; set; }

    public string? ManagerNote { get; set; }

    public string? GuardNote { get; set; }

    public decimal? VerifiedValue { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedTime { get; set; }

    public DateTime? CompletedTime { get; set; }

    public DateTime? ApprovedTime { get; set; }

    public virtual UsageAnomaly Anomaly { get; set; } = null!;

    public virtual Guard AssignedGuard { get; set; } = null!;

    public virtual Manager CreatedBy { get; set; } = null!;

    public virtual ICollection<InspectionPhoto> InspectionPhotos { get; set; } = new List<InspectionPhoto>();
}
