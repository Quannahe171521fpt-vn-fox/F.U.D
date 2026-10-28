using System;
using System.Collections.Generic;

namespace FUD.Models;

/// <summary>
/// Lịch sử thay đổi vai trò (Grant / Adjust / Revoke).
/// OldRoles / NewRoles lưu danh sách tên vai trò cách nhau bởi dấu phẩy.
/// </summary>
public partial class RoleChangeHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Grant | Adjust | Revoke</summary>
    public string Action { get; set; } = null!;

    public string? OldRoles { get; set; }

    public string? NewRoles { get; set; }

    public int ChangedById { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual User ChangedBy { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
