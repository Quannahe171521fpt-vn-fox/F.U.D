using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Room
{
    public int Id { get; set; }

    public int BlockId { get; set; }

    public int RoomTypeId { get; set; }

    public string RoomNumber { get; set; } = null!;

    public int? FloorLevel { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Bed> Beds { get; set; } = new List<Bed>();

    public virtual Block Block { get; set; } = null!;

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();

    public virtual ICollection<Handover> Handovers { get; set; } = new List<Handover>();

    public virtual ICollection<IncidentTicket> IncidentTickets { get; set; } = new List<IncidentTicket>();

    public virtual ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();

    public virtual RoomType RoomType { get; set; } = null!;

    public virtual ICollection<UtilityBill> UtilityBills { get; set; } = new List<UtilityBill>();

    public virtual ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
