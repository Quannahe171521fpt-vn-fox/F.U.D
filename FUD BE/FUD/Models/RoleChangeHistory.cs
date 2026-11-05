using System;

namespace FUD.Models;

/// <summary>Lịch sử thay đổi vai trò của tài khoản (Grant | Adjust | Revoke).</summary>
public partial class RoleChangeHistory
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Loại hành động (Grant | Adjust | Revoke).</summary>
    public string Action { get; set; } = null!;

    /// <summary>Danh sách vai trò trước khi thay đổi, phân tách bằng dấu phẩy.</summary>
    public string? OldRoles { get; set; }

    /// <summary>Danh sách vai trò sau khi thay đổi, phân tách bằng dấu phẩy.</summary>
    public string? NewRoles { get; set; }

    /// <summary>Id của Admin thực hiện thay đổi.</summary>
    public int ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User? ChangedByUser { get; set; }
}
