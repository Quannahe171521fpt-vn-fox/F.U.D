using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class ImportBatch
{
    public int Id { get; set; }

    public int BillingPeriodId { get; set; }

    public int BlockId { get; set; }

    public int GuardId { get; set; }

    public string Method { get; set; } = null!;

    public string? FileName { get; set; }

    public int TotalRows { get; set; }

    public int ErrorRows { get; set; }

    public DateTime ImportTime { get; set; }

    public virtual BillingPeriod BillingPeriod { get; set; } = null!;

    public virtual Block Block { get; set; } = null!;

    public virtual Guard Guard { get; set; } = null!;

    public virtual ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();
}
