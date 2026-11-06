using System;
using System.Collections.Generic;

namespace FUD.Models;

/// <summary>Lịch sử thay đổi trạng thái tài khoản (Suspend / Restore).</summary>
public partial class AccountStatusHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Trạng thái trước khi thay đổi (Pending | Active | Suspended).</summary>
    public string OldStatus { get; set; } = null!;

    /// <summary>Trạng thái sau khi thay đổi (Pending | Active | Suspended).</summary>
    public string NewStatus { get; set; } = null!;

    /// <summary>Lý do thay đổi (violation | graduation | termination | other | ...).</summary>
    public string? Reason { get; set; }

    /// <summary>Ghi chú bổ sung của Admin.</summary>
    public string? Notes { get; set; }

    /// <summary>Id của Admin thực hiện thay đổi.</summary>
    public int ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    /// <summary>Trạng thái gửi thông báo (None | Sent | Failed).</summary>
    public string NotificationStatus { get; set; } = AccountConstants.NotificationStatus.None;

    public virtual User User { get; set; } = null!;

    public virtual User? ChangedByUser { get; set; }
}
