using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class BookingRequest
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int TermId { get; set; }

    public int BedId { get; set; }

    public string BookingKind { get; set; } = null!;

    public int? KeepFromResidencyId { get; set; }

    public bool AgreementToRegulations { get; set; }

    public DateTime PaymentDeadline { get; set; }

    public string Status { get; set; } = null!;

    public string? RejectReason { get; set; }

    public string? CancelReason { get; set; }

    public bool CancelledBySystem { get; set; }

    public int? ProcessedById { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? ProcessedTime { get; set; }

    public virtual Bed Bed { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Residency? KeepFromResidency { get; set; }

    public virtual Manager? ProcessedBy { get; set; }

    public virtual ICollection<Residency> Residencies { get; set; } = new List<Residency>();

    public virtual Student Student { get; set; } = null!;

    public virtual Term Term { get; set; } = null!;
}
