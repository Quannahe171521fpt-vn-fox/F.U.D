using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Equipment
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public int? BedId { get; set; }

    public int CatalogId { get; set; }

    public string AssetCode { get; set; } = null!;

    public string Condition { get; set; } = null!;

    public DateOnly? PurchaseDate { get; set; }

    public string? Note { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Bed? Bed { get; set; }

    public virtual EquipmentCatalog Catalog { get; set; } = null!;

    public virtual ICollection<HandoverItem> HandoverItems { get; set; } = new List<HandoverItem>();

    public virtual ICollection<IncidentTicket> IncidentTickets { get; set; } = new List<IncidentTicket>();

    public virtual Room Room { get; set; } = null!;
}
