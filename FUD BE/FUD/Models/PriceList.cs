using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class PriceList
{
    public int Id { get; set; }

    public int TermId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreationDate { get; set; }

    public int CreatedById { get; set; }

    public DateTime? PublishedTime { get; set; }

    public virtual Manager CreatedBy { get; set; } = null!;

    public virtual ICollection<PriceItem> PriceItems { get; set; } = new List<PriceItem>();

    public virtual Term Term { get; set; } = null!;
}
