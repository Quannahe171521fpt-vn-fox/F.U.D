using System;
using System.Collections.Generic;

namespace FUD.Models;

public partial class User
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Gender { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? GoogleId { get; set; }

    public string AccountStatus { get; set; } = null!;

    public string? SuspensionReason { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public int? SuspendedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEnd { get; set; }

    public virtual Admin? Admin { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual Guard? Guard { get; set; }

    public virtual ICollection<User> InverseSuspendedBy { get; set; } = new List<User>();

    public virtual Manager? Manager { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<RequestLog> RequestLogs { get; set; } = new List<RequestLog>();

    public virtual Student? Student { get; set; }

    public virtual User? SuspendedBy { get; set; }

    public virtual ICollection<TicketPhoto> TicketPhotos { get; set; } = new List<TicketPhoto>();

    public virtual ICollection<TicketStatusHistory> TicketStatusHistories { get; set; } = new List<TicketStatusHistory>();

    public virtual ICollection<UserRole> UserRoleAssignedBies { get; set; } = new List<UserRole>();

    public virtual ICollection<UserRole> UserRoleUsers { get; set; } = new List<UserRole>();
}
