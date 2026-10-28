using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class UtilityPrice
{
    public int Id { get; set; }

    public int TermId { get; set; }

    public string MeterType { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Term Term { get; set; } = null!;
}
