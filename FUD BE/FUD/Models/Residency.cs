using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Residency
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int BedId { get; set; }

    public int TermId { get; set; }

    public int? SourceRequestId { get; set; }

    public string? RegistrationType { get; set; }

    public DateOnly? CheckInDate { get; set; }

    public DateOnly? PlannedCheckOutDate { get; set; }

    public DateOnly? ActualCheckOutDate { get; set; }

    public string Status { get; set; } = null!;

    public virtual Bed Bed { get; set; } = null!;

    public virtual ICollection<BedTransferRequest> BedTransferRequestFromResidencies { get; set; } = new List<BedTransferRequest>();

    public virtual ICollection<BedTransferRequest> BedTransferRequestNewResidencies { get; set; } = new List<BedTransferRequest>();

    public virtual ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();

    public virtual ICollection<CheckoutRequest> CheckoutRequests { get; set; } = new List<CheckoutRequest>();

    public virtual Handover? Handover { get; set; }

    public virtual Request? SourceRequest { get; set; }

    public virtual Student Student { get; set; } = null!;

    public virtual Term Term { get; set; } = null!;

    public virtual ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
