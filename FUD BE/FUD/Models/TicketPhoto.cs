using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class TicketPhoto
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public string PhotoType { get; set; } = null!;

    public string FileUrl { get; set; } = null!;

    public DateTime UploadTime { get; set; }

    public int UploaderId { get; set; }

    public virtual IncidentTicket Ticket { get; set; } = null!;

    public virtual User Uploader { get; set; } = null!;
}
