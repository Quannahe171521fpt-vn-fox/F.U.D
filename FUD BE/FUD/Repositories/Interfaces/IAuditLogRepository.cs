using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Account;

namespace FUD.Repositories.Interfaces;

/// <summary>
/// Data Access Layer cho nhật ký kiểm toán hệ thống (Audit Logs, Role Change, Account Status).
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Lấy danh sách nhật ký kiểm toán hợp nhất từ các bảng với bộ lọc và phân trang.
    /// </summary>
    Task<(IEnumerable<AuditLogItemDto> Items, int TotalCount)> GetUnifiedLogsAsync(AuditLogQueryDto query);

    /// <summary>
    /// Lấy chi tiết một bản ghi kiểm toán theo Id và loại nhật ký.
    /// </summary>
    Task<AuditLogDetailDto?> GetDetailAsync(int id, string? logType);
}

