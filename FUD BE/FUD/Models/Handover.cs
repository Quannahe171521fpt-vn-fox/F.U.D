using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Handover
{
    public int Id { get; set; }

    public string HandoverType { get; set; } = null!;

    public int? RequestId { get; set; }

    public int? ResidencyId { get; set; }

    public int RoomId { get; set; }

    public int GuardId { get; set; }

    public bool BelongingsRemainingFlag { get; set; }

    public string? BelongingsNote { get; set; }

    public bool HasDamageOrMissingFlag { get; set; }

    public decimal TotalCompensation { get; set; }

    public string Status { get; set; } = null!;

    public bool StudentAcceptedFlag { get; set; }

    public DateTime? StudentAcceptedTime { get; set; }

    public int? ManagerReviewedById { get; set; }

    public DateTime? ManagerReviewedTime { get; set; }

    public string? ManagerNote { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? CompletedTime { get; set; }

    public virtual ICollection<ComplaintRequest> ComplaintRequests { get; set; } = new List<ComplaintRequest>();

    public virtual Guard Guard { get; set; } = null!;

    public virtual ICollection<HandoverItem> HandoverItems { get; set; } = new List<HandoverItem>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Manager? ManagerReviewedBy { get; set; }

    public virtual Request? Request { get; set; }

    public virtual Residency? Residency { get; set; }

    public virtual Room Room { get; set; } = null!;
}
