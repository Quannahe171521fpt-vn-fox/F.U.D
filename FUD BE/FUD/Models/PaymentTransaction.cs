using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class PaymentTransaction
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public string Method { get; set; } = null!;

    public string GatewayReference { get; set; } = null!;

    public string? QrLink { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public string? ReceiptNumber { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime ExpiryTime { get; set; }

    public DateTime? PaidTime { get; set; }

    public DateTime? CallbackTime { get; set; }

    public string? GatewayPayload { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual Student Student { get; set; } = null!;

    public virtual ICollection<TransactionInvoice> TransactionInvoices { get; set; } = new List<TransactionInvoice>();
}
