using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Violation
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public int StudentId { get; set; }

    public int RoomId { get; set; }

    public int? ResidencyId { get; set; }

    public int ViolationTypeId { get; set; }

    public int RecordedById { get; set; }

    public int? EvaluatedById { get; set; }

    public DateTime OccurrenceTime { get; set; }

    public string? Description { get; set; }

    public string? InitialLevel { get; set; }

    public string? FinalLevel { get; set; }

    public string? ManagerNote { get; set; }

    public DateTime? ExplanationDeadline { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? FinalizedTime { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<ComplaintRequest> ComplaintRequests { get; set; } = new List<ComplaintRequest>();

    public virtual Manager? EvaluatedBy { get; set; }

    public virtual Guard RecordedBy { get; set; } = null!;

    public virtual Residency? Residency { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual Student Student { get; set; } = null!;

    public virtual ICollection<ViolationEvidence> ViolationEvidences { get; set; } = new List<ViolationEvidence>();

    public virtual ViolationExplanation? ViolationExplanation { get; set; }

    public virtual ViolationType ViolationType { get; set; } = null!;
}
