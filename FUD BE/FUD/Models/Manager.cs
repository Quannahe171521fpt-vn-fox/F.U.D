using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Manager
{
    public int UserId { get; set; }

    public virtual ICollection<BedTransferRequest> BedTransferRequests { get; set; } = new List<BedTransferRequest>();

    public virtual ICollection<CheckoutRequest> CheckoutRequests { get; set; } = new List<CheckoutRequest>();

    public virtual ICollection<Handover> Handovers { get; set; } = new List<Handover>();

    public virtual ICollection<IncidentTicket> IncidentTickets { get; set; } = new List<IncidentTicket>();

    public virtual ICollection<InspectionRequest> InspectionRequests { get; set; } = new List<InspectionRequest>();

    public virtual ICollection<News> NewsApprovedBies { get; set; } = new List<News>();

    public virtual ICollection<News> NewsAuthors { get; set; } = new List<News>();

    public virtual ICollection<PriceList> PriceLists { get; set; } = new List<PriceList>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<UsageAnomaly> UsageAnomalies { get; set; } = new List<UsageAnomaly>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
