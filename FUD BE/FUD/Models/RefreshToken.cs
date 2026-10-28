using System;
using System.Collections.Generic;

namespace FUD.Models;

/// <summary>
/// Refresh token dùng để thu hồi phiên khi tài khoản bị Suspend.
/// Chỉ lưu hash (SHA-256) của token, không lưu bản rõ.
/// </summary>
public partial class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public int? RevokedById { get; set; }

    public virtual User User { get; set; } = null!;
}
