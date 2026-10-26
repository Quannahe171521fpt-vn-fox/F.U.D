using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Student
{
    public int UserId { get; set; }

    public string? Major { get; set; }

    public string? Cohort { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<IncidentTicket> IncidentTickets { get; set; } = new List<IncidentTicket>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<RepairFeedback> RepairFeedbacks { get; set; } = new List<RepairFeedback>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<Residency> Residencies { get; set; } = new List<Residency>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<ViolationExplanation> ViolationExplanations { get; set; } = new List<ViolationExplanation>();

    public virtual ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
