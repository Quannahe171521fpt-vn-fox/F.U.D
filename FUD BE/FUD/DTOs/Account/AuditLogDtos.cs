using System;
using System.Collections.Generic;

namespace FUD.DTOs.Account;

/// <summary>
/// Query parameters cho GET /api/admin/audit-logs.
/// </summary>
public class AuditLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Loại nhật ký: All | Impersonation | RoleChange | AccountStatus | System
    /// </summary>
    public string? LogType { get; set; }

    /// <summary>
    /// Lọc theo ID của người thực hiện hành vi (Actor).
    /// </summary>
    public int? ActorId { get; set; }

    /// <summary>
    /// Lọc theo ngày bắt đầu (UTC/local ISO string).
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Lọc theo ngày kết thúc (UTC/local ISO string).
    /// </summary>
    public DateTime? ToDate { get; set; }

    /// <summary>
    /// Tìm kiếm theo tên/mã người thực hiện, đối tượng, ghi chú, loại hành động...
    /// </summary>
    public string? Search { get; set; }
}

/// <summary>
/// Thông tin nhật ký kiểm toán hợp nhất từ các bảng hệ thống.
/// </summary>
public class AuditLogItemDto
{
    public int Id { get; set; }

    /// <summary>
    /// "Impersonation" | "RoleChange" | "AccountStatus" | "System"
    /// </summary>
    public string LogType { get; set; } = string.Empty;

    public int? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? ActorCode { get; set; }

    public int? TargetUserId { get; set; }
    public string? TargetUserName { get; set; }
    public string? TargetUserCode { get; set; }

    public string? ActionType { get; set; }
    public string? TargetEntity { get; set; }
    public string? TargetId { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Chi tiết mở rộng của nhật ký kiểm toán.
/// </summary>
public class AuditLogDetailDto : AuditLogItemDto
{
    public string? RawPayload { get; set; }
}

