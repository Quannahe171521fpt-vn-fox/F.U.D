using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class TicketStatusHistory
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = null!;

    public string? Note { get; set; }

    public int? ChangedById { get; set; }

    public DateTime ChangeTime { get; set; }

    public virtual User? ChangedBy { get; set; }

    public virtual IncidentTicket Ticket { get; set; } = null!;
}
