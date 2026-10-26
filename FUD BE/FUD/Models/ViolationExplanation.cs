using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class ViolationExplanation
{
    public int Id { get; set; }

    public int ViolationId { get; set; }

    public int StudentId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime SubmissionTime { get; set; }

    public virtual Student Student { get; set; } = null!;

    public virtual Violation Violation { get; set; } = null!;

    public virtual ICollection<ViolationEvidence> ViolationEvidences { get; set; } = new List<ViolationEvidence>();
}
