using System;
using System.Collections.Generic;

namespace FUD.DTOs.Account;

/// <summary>Thông tin tài khoản trả về cho API (không bao giờ chứa PasswordHash).</summary>
public class AccountDto
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Gender { get; set; }

    /// <summary>Trạng thái tài khoản: Pending | Active | Suspended.</summary>
    public string Status { get; set; } = string.Empty;

    public string? SuspensionReason { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public int? SuspendedById { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Danh sách vai trò hệ thống hiện tại của tài khoản.</summary>
    public List<string> Roles { get; set; } = new List<string>();

    /// <summary>Vai trò chính (được dùng để hiển thị, theo thứ tự ưu tiên Admin &gt; Manager &gt; Guard &gt; Student).</summary>
    public string PrimaryRole { get; set; } = string.Empty;
}

/// <summary>Chi tiết tài khoản kèm lịch sử thay đổi trạng thái và vai trò.</summary>
public class AccountDetailDto : AccountDto
{
    public List<AccountStatusHistoryDto> StatusHistory { get; set; } = new List<AccountStatusHistoryDto>();

    public List<RoleChangeHistoryDto> RoleHistory { get; set; } = new List<RoleChangeHistoryDto>();
}

/// <summary>Lịch sử thay đổi trạng thái tài khoản.</summary>
public class AccountStatusHistoryDto
{
    public int Id { get; set; }

    public string OldStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public int ChangedBy { get; set; }

    public string? ChangedByName { get; set; }

    public DateTime ChangedAt { get; set; }

    public string NotificationStatus { get; set; } = string.Empty;
}

/// <summary>Lịch sử thay đổi vai trò tài khoản.</summary>
public class RoleChangeHistoryDto
{
    public int Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? OldRoles { get; set; }

    public string? NewRoles { get; set; }

    public int ChangedBy { get; set; }

    public string? ChangedByName { get; set; }

    public DateTime ChangedAt { get; set; }
}
