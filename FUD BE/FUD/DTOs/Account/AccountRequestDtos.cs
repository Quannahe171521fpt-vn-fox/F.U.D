using System;
using System.Collections.Generic;

namespace FUD.DTOs.Account;

/// <summary>Body cho POST /api/admin/users — Tạo tài khoản mới thủ công bởi Admin.</summary>
public class CreateAccountDto
{
    /// <summary>Tên đăng nhập (khớp với Users.Code, duy nhất trong hệ thống).</summary>
    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu khởi tạo: tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số.</summary>
    public string Password { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Giới tính: Male | Female.</summary>
    public string? Gender { get; set; }

    /// <summary>Danh sách vai trò ban đầu (Student | Manager | Guard | Admin).</summary>
    public List<string> Roles { get; set; } = new List<string>();
}

/// <summary>Body cho PUT /api/admin/users/{id}/roles — Cấp / chỉnh sửa / thu hồi vai trò.</summary>
public class AssignRolesDto
{
    /// <summary>Danh sách vai trò mong muốn sau khi cập nhật. Gửi mảng rỗng để thu hồi toàn bộ vai trò.</summary>
    public List<string> Roles { get; set; } = new List<string>();
}

/// <summary>Body cho POST /api/admin/users/{id}/suspend — Tạm ngưng tài khoản.</summary>
public class SuspendAccountDto
{
    /// <summary>Lý do tạm ngưng: violation | graduation | termination | other.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Ghi chú bổ sung (không bắt buộc).</summary>
    public string? Notes { get; set; }
}

/// <summary>Body cho POST /api/admin/users/{id}/restore — Khôi phục tài khoản.</summary>
public class RestoreAccountDto
{
    /// <summary>Lý do khôi phục (không bắt buộc).</summary>
    public string? Reason { get; set; }
}
