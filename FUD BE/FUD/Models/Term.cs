using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Term
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string AcademicYear { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<BedPrice> BedPrices { get; set; } = new List<BedPrice>();

    public virtual ICollection<BillingPeriod> BillingPeriods { get; set; } = new List<BillingPeriod>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual PriceList? PriceList { get; set; }

    public virtual ICollection<RegistrationPeriod> RegistrationPeriods { get; set; } = new List<RegistrationPeriod>();

    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();

    public virtual ICollection<Residency> Residencies { get; set; } = new List<Residency>();

    public virtual ICollection<UtilityPrice> UtilityPrices { get; set; } = new List<UtilityPrice>();
}
