using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Dorm
{
    public int Id { get; set; }

    public int CampusId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Address { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Block> Blocks { get; set; } = new List<Block>();

    public virtual Campus Campus { get; set; } = null!;

    public virtual ICollection<PriceItem> PriceItems { get; set; } = new List<PriceItem>();
}
