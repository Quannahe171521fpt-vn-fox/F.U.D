using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class RoomType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int BedCapacity { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<PriceItem> PriceItems { get; set; } = new List<PriceItem>();

    public virtual ICollection<RoomTypeEquipment> RoomTypeEquipments { get; set; } = new List<RoomTypeEquipment>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
}
