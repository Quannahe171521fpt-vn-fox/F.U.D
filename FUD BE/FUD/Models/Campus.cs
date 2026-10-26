using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Campus
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public virtual ICollection<Dorm> Dorms { get; set; } = new List<Dorm>();
}
