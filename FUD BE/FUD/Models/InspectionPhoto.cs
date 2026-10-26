using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class InspectionPhoto
{
    public int Id { get; set; }

    public int InspectionRequestId { get; set; }

    public string FileUrl { get; set; } = null!;

    public virtual InspectionRequest InspectionRequest { get; set; } = null!;
}
