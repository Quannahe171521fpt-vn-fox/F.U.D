using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class Bed
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public string BedCode { get; set; } = null!;

    public string? PositionInRoom { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<BedPrice> BedPrices { get; set; } = new List<BedPrice>();

    public virtual ICollection<BedTransferRequest> BedTransferRequestFromBeds { get; set; } = new List<BedTransferRequest>();

    public virtual ICollection<BedTransferRequest> BedTransferRequestToBeds { get; set; } = new List<BedTransferRequest>();

    public virtual ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();

    public virtual ICollection<Residency> Residencies { get; set; } = new List<Residency>();

    public virtual Room Room { get; set; } = null!;
}
