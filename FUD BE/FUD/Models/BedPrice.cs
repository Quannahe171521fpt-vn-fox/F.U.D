using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class BedPrice
{
    public int Id { get; set; }

    public int BedId { get; set; }

    public int TermId { get; set; }

    public int? TargetMonth { get; set; }

    public decimal UnitPrice { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly EffectiveTo { get; set; }

    public virtual Bed Bed { get; set; } = null!;

    public virtual Term Term { get; set; } = null!;
}
