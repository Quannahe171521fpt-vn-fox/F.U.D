using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Request
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int TermId { get; set; }

    public string RequestType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public string? RejectReason { get; set; }

    public string? CancelReason { get; set; }

    public bool CancelledBySystem { get; set; }

    public int? ProcessedById { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? ProcessedTime { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual BedTransferRequest? BedTransferRequest { get; set; }

    public virtual BookingRequest? BookingRequest { get; set; }

    public virtual CheckoutRequest? CheckoutRequest { get; set; }

    public virtual ComplaintRequest? ComplaintRequest { get; set; }

    public virtual Handover? Handover { get; set; }

    public virtual Manager? ProcessedBy { get; set; }

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual ICollection<RequestLog> RequestLogs { get; set; } = new List<RequestLog>();

    public virtual ICollection<Residency> Residencies { get; set; } = new List<Residency>();

    public virtual Student Student { get; set; } = null!;

    public virtual Term Term { get; set; } = null!;
}
