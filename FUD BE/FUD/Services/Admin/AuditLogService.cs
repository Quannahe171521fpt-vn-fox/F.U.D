using System;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Account;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FUD.Services.Admin;

/// <summary>
/// Triển khai dịch vụ tra cứu nhật ký kiểm toán hệ thống cho Admin.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepo;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        IAuditLogRepository auditLogRepo,
        ILogger<AuditLogService> logger)
    {
        _auditLogRepo = auditLogRepo;
        _logger = logger;
    }

    public async Task<PagedResult<AuditLogItemDto>> GetLogsAsync(AuditLogQueryDto query)
    {
        var (items, totalCount) = await _auditLogRepo.GetUnifiedLogsAsync(query);

        return new PagedResult<AuditLogItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page <= 0 ? 1 : query.Page,
            PageSize = query.PageSize <= 0 ? 20 : query.PageSize
        };
    }

    public async Task<AuditLogDetailDto?> GetDetailAsync(int id, string? logType)
    {
        return await _auditLogRepo.GetDetailAsync(id, logType);
    }
}

