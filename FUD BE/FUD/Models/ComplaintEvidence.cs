using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class ComplaintEvidence
{
    public int Id { get; set; }

    public int ComplaintRequestId { get; set; }

    public string FileUrl { get; set; } = null!;

    public virtual ComplaintRequest ComplaintRequest { get; set; } = null!;
}
