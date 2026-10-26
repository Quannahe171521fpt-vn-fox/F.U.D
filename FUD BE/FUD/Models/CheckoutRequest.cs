using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class CheckoutRequest
{
    public int RequestId { get; set; }

    public int ResidencyId { get; set; }

    public DateOnly ExpectedCheckOutDate { get; set; }

    public int? ReviewedById { get; set; }

    public DateTime? ReviewedTime { get; set; }

    public int? AssignedGuardId { get; set; }

    public bool IsExceptionalCase { get; set; }

    public string? ExceptionNote { get; set; }

    public virtual Guard? AssignedGuard { get; set; }

    public virtual ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();

    public virtual Request Request { get; set; } = null!;

    public virtual Residency Residency { get; set; } = null!;

    public virtual Manager? ReviewedBy { get; set; }
}
