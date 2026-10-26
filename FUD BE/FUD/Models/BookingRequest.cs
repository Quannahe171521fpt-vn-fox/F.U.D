using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class BookingRequest
{
    public int RequestId { get; set; }

    public int BedId { get; set; }

    public string BookingKind { get; set; } = null!;

    public int? KeepFromResidencyId { get; set; }

    public bool AgreementToRegulations { get; set; }

    public DateTime PaymentDeadline { get; set; }

    public virtual Bed Bed { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Residency? KeepFromResidency { get; set; }

    public virtual Request Request { get; set; } = null!;
}
