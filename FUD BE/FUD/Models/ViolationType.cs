using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class ViolationType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Violation> Violations { get; set; } = new List<Violation>();
}
