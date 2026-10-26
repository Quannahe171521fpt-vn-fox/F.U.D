using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class RepairFeedback
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public int StudentId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime SubmittedTime { get; set; }

    public virtual Student Student { get; set; } = null!;

    public virtual IncidentTicket Ticket { get; set; } = null!;
}
