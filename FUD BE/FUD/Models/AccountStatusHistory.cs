using System;
using System.Collections.Generic;

namespace FUD.Models;

/// <summary>
/// Lịch sử thay đổi trạng thái tài khoản (Suspend / Restore).
/// Bảng này chỉ ghi thêm, không bao giờ xóa bản ghi.
/// </summary>
public partial class AccountStatusHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string OldStatus { get; set; } = null!;

    public string NewStatus { get; set; } = null!;

    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public int ChangedById { get; set; }

    public DateTime ChangedAt { get; set; }

    /// <summary>None | Sent | Failed</summary>
    public string NotificationStatus { get; set; } = null!;

    public virtual User ChangedBy { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
