using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class ComplaintRequest
{
    public int RequestId { get; set; }

    public string TargetType { get; set; } = null!;

    public int? HandoverId { get; set; }

    public int? InvoiceId { get; set; }

    public int? ViolationId { get; set; }

    public string Content { get; set; } = null!;

    public string? ResolutionNote { get; set; }

    public virtual ICollection<ComplaintEvidence> ComplaintEvidences { get; set; } = new List<ComplaintEvidence>();

    public virtual Handover? Handover { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual Request Request { get; set; } = null!;

    public virtual Violation? Violation { get; set; }
}
