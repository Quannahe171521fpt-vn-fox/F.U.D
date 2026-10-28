using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class EquipmentCatalog
{
    public int Id { get; set; }

    public string Category { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool ActiveFlag { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();

    public virtual ICollection<RoomTypeEquipment> RoomTypeEquipments { get; set; } = new List<RoomTypeEquipment>();
}
