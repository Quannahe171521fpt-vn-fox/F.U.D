using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Account;

namespace FUD.Services.Interfaces;

/// <summary>
/// Service Layer cho nhật ký kiểm toán hệ thống.
/// </summary>
public interface IAuditLogService
{
    Task<PagedResult<AuditLogItemDto>> GetLogsAsync(AuditLogQueryDto query);
    Task<AuditLogDetailDto?> GetDetailAsync(int id, string? logType);
}

