using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class HandoverItem
{
    public int Id { get; set; }

    public int HandoverId { get; set; }

    public int EquipmentId { get; set; }

    public string ItemCondition { get; set; } = null!;

    public string? Note { get; set; }

    public decimal CompensationAmount { get; set; }

    public virtual Equipment Equipment { get; set; } = null!;

    public virtual Handover Handover { get; set; } = null!;

    public virtual ICollection<HandoverPhoto> HandoverPhotos { get; set; } = new List<HandoverPhoto>();
}
