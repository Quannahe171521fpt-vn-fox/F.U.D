using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Refund
{
    public int Id { get; set; }

    public int TransactionId { get; set; }

    public int? RelatedRequestId { get; set; }

    public int? RelatedInvoiceId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedTime { get; set; }

    public DateTime? RefundTime { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Invoice? RelatedInvoice { get; set; }

    public virtual Request? RelatedRequest { get; set; }

    public virtual PaymentTransaction Transaction { get; set; } = null!;
}
