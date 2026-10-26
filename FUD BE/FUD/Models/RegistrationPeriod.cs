using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class RegistrationPeriod
{
    public int Id { get; set; }

    public int TermId { get; set; }

    public string PeriodType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool ActiveFlag { get; set; }

    public virtual Term Term { get; set; } = null!;
}
