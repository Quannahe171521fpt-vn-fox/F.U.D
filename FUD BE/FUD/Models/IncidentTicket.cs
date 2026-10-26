using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class IncidentTicket
{
    public int Id { get; set; }

    public string TicketCode { get; set; } = null!;

    public int StudentId { get; set; }

    public int RoomId { get; set; }

    public int? EquipmentId { get; set; }

    public int? AssignedById { get; set; }

    public int? AssignedGuardId { get; set; }

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ManagerNote { get; set; }

    public string? HandlingResult { get; set; }

    public string? ReopenReason { get; set; }

    public string? EscalationReason { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? DoneTime { get; set; }

    public DateTime? ClosedTime { get; set; }

    public bool AutoClosed { get; set; }

    public virtual Manager? AssignedBy { get; set; }

    public virtual Guard? AssignedGuard { get; set; }

    public virtual Equipment? Equipment { get; set; }

    public virtual RepairFeedback? RepairFeedback { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;

    public virtual ICollection<TicketPhoto> TicketPhotos { get; set; } = new List<TicketPhoto>();

    public virtual ICollection<TicketStatusHistory> TicketStatusHistories { get; set; } = new List<TicketStatusHistory>();
}
