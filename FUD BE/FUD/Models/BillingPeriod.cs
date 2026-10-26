using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class BillingPeriod
{
    public int Id { get; set; }

    public int TermId { get; set; }

    public int TargetMonth { get; set; }

    public int TargetYear { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CalculatedTime { get; set; }

    public virtual ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();

    public virtual ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();

    public virtual Term Term { get; set; } = null!;

    public virtual ICollection<UtilityBill> UtilityBills { get; set; } = new List<UtilityBill>();
}
