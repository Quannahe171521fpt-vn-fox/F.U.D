using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class TransactionInvoice
{
    public int TransactionId { get; set; }

    public int InvoiceId { get; set; }

    public decimal AllocatedAmount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual PaymentTransaction Transaction { get; set; } = null!;
}
