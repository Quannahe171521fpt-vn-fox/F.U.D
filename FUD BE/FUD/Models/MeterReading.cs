using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class MeterReading
{
    public int Id { get; set; }

    public int? BillingPeriodId { get; set; }

    public int RoomId { get; set; }

    public int? BatchId { get; set; }

    public int? CheckoutRequestId { get; set; }

    public int? TransferRequestId { get; set; }

    public string MeterType { get; set; } = null!;

    public decimal PreviousValue { get; set; }

    public decimal CurrentValue { get; set; }

    public decimal? Consumption { get; set; }

    public string Source { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int RecordedById { get; set; }

    public DateTime RecordedTime { get; set; }

    public string? Note { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ImportBatch? Batch { get; set; }

    public virtual BillingPeriod? BillingPeriod { get; set; }

    public virtual CheckoutRequest? CheckoutRequest { get; set; }

    public virtual Guard RecordedBy { get; set; } = null!;

    public virtual Room Room { get; set; } = null!;

    public virtual BedTransferRequest? TransferRequest { get; set; }

    public virtual ICollection<UsageAnomaly> UsageAnomalies { get; set; } = new List<UsageAnomaly>();
}
