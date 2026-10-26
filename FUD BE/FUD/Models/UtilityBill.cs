using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class UtilityBill
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public int BillingPeriodId { get; set; }

    public decimal? TotalKwh { get; set; }

    public decimal? TotalM3 { get; set; }

    public decimal? ElectricityUnitPrice { get; set; }

    public decimal? WaterUnitPrice { get; set; }

    public decimal? ElectricityCost { get; set; }

    public decimal? WaterCost { get; set; }

    public decimal? TotalCost { get; set; }

    public int? NumberOfResidents { get; set; }

    public decimal? SharePerStudent { get; set; }

    public DateTime? PublishedTime { get; set; }

    public string Status { get; set; } = null!;

    public int? CorrectedFromId { get; set; }

    public string? CorrectionReason { get; set; }

    public virtual BillingPeriod BillingPeriod { get; set; } = null!;

    public virtual UtilityBill? CorrectedFrom { get; set; }

    public virtual ICollection<UtilityBill> InverseCorrectedFrom { get; set; } = new List<UtilityBill>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual Room Room { get; set; } = null!;
}
