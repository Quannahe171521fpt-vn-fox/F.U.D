using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Invoice
{
    public int Id { get; set; }

    public int StudentId { get; set; }

    public int TermId { get; set; }

    public string InvoiceNumber { get; set; } = null!;

    public string InvoiceType { get; set; } = null!;

    public int? BookingRequestId { get; set; }

    public int? TransferRequestId { get; set; }

    public int? UtilityBillId { get; set; }

    public int? HandoverId { get; set; }

    public int? ParentInvoiceId { get; set; }

    public string? AdjustmentKind { get; set; }

    public decimal Amount { get; set; }

    public DateTime IssuedTime { get; set; }

    public DateOnly DueDate { get; set; }

    public DateTime? PaidTime { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual BookingRequest? BookingRequest { get; set; }

    public virtual ICollection<ComplaintRequest> ComplaintRequests { get; set; } = new List<ComplaintRequest>();

    public virtual Handover? Handover { get; set; }

    public virtual ICollection<Invoice> InverseParentInvoice { get; set; } = new List<Invoice>();

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public virtual Invoice? ParentInvoice { get; set; }

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();

    public virtual Student Student { get; set; } = null!;

    public virtual Term Term { get; set; } = null!;

    public virtual ICollection<TransactionInvoice> TransactionInvoices { get; set; } = new List<TransactionInvoice>();

    public virtual BedTransferRequest? TransferRequest { get; set; }

    public virtual UtilityBill? UtilityBill { get; set; }
}
