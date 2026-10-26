using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class AuditLog
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public int? ImpersonatedStudentId { get; set; }

    public string ActionType { get; set; } = null!;

    public string? TargetEntity { get; set; }

    public string? TargetId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Student? ImpersonatedStudent { get; set; }

    public virtual User? User { get; set; }
}
