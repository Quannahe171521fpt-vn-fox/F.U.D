using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class VwStudentOutstanding
{
    public int StudentId { get; set; }

    public decimal OutstandingAmount { get; set; }

    public int OverdueInvoiceCount { get; set; }
}
