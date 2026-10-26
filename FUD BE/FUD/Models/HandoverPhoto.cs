using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class HandoverPhoto
{
    public int Id { get; set; }

    public int HandoverItemId { get; set; }

    public string FileUrl { get; set; } = null!;

    public virtual HandoverItem HandoverItem { get; set; } = null!;
}
