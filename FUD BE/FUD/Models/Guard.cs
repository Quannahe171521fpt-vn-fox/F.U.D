using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Guard
{
    public int UserId { get; set; }

    public virtual ICollection<BedTransferRequest> BedTransferRequests { get; set; } = new List<BedTransferRequest>();

    public virtual ICollection<CheckoutRequest> CheckoutRequests { get; set; } = new List<CheckoutRequest>();

    public virtual ICollection<Handover> Handovers { get; set; } = new List<Handover>();

    public virtual ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();

    public virtual ICollection<IncidentTicket> IncidentTickets { get; set; } = new List<IncidentTicket>();

    public virtual ICollection<InspectionRequest> InspectionRequests { get; set; } = new List<InspectionRequest>();

    public virtual ICollection<MeterReading> MeterReadings { get; set; } = new List<MeterReading>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
