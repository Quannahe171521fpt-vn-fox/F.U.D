using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class PriceItem
{
    public int Id { get; set; }

    public int PriceListId { get; set; }

    public int DormId { get; set; }

    public int RoomTypeId { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Dorm Dorm { get; set; } = null!;

    public virtual PriceList PriceList { get; set; } = null!;

    public virtual RoomType RoomType { get; set; } = null!;
}
