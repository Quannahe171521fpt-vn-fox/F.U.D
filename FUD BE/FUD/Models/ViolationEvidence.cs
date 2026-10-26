using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class ViolationEvidence
{
    public int Id { get; set; }

    public int ViolationId { get; set; }

    public int? ExplanationId { get; set; }

    public string Source { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public virtual ViolationExplanation? Explanation { get; set; }

    public virtual Violation Violation { get; set; } = null!;
}
