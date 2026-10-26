using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class RoomTypeEquipment
{
    public int RoomTypeId { get; set; }

    public int CatalogId { get; set; }

    public int StandardQuantity { get; set; }

    public virtual EquipmentCatalog Catalog { get; set; } = null!;

    public virtual RoomType RoomType { get; set; } = null!;
}
