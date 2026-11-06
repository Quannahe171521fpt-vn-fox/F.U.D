using System;
using System.Collections.Generic;

namespace FUD.DTOs.Account;

/// <summary>Query cho GET /api/admin/users — Tìm kiếm, lọc và phân trang danh sách tài khoản.</summary>
public class AccountQueryDto
{
    /// <summary>Từ khóa tìm kiếm trên Username / FullName / Email.</summary>
    public string? Search { get; set; }

    /// <summary>Lọc theo trạng thái: Pending | Active | Suspended.</summary>
    public string? Status { get; set; }

    /// <summary>Lọc theo vai trò: Student | Manager | Guard | Admin.</summary>
    public string? Role { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
