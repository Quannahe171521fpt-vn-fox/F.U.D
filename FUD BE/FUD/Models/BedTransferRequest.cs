using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class BedTransferRequest
{
    public int RequestId { get; set; }

    public int FromResidencyId { get; set; }

    public int FromBedId { get; set; }

    public int ToBedId { get; set; }

    public string TransferReason { get; set; } = null!;

    public int? ReviewedById { get; set; }

    public DateTime? ReviewedTime { get; set; }

    public int? AssignedGuardId { get; set; }

    public DateOnly? TransferDate { get; set; }

    public decimal? OldBedDailyRate { get; set; }

    public decimal? NewBedDailyRate { get; set; }

    public int? DaysStayed { get; set; }

    public int? DaysRemaining { get; set; }

    public decimal? AmountAlreadyPaid { get; set; }

    public decimal? OldBedAmount { get; set; }

    public decimal? NewBedAmount { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? PriceDifference { get; set; }

    public int? NewResidencyId { get; set; }

    public virtual Guard? AssignedGuard { get; set; }

    public virtual Bed FromBed { get; set; } = null!;

    public virtual Residency FromResidency { get; set; } = null!;

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();

    public virtual Residency? NewResidency { get; set; }

    public virtual Request Request { get; set; } = null!;

    public virtual Manager? ReviewedBy { get; set; }

    public virtual Bed ToBed { get; set; } = null!;
}
