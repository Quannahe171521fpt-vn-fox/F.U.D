using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Admin
{
    public int UserId { get; set; }

    public virtual User User { get; set; } = null!;
}
