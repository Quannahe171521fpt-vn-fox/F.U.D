using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Account;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Admin;

/// <summary>
/// API xem và tra cứu nhật ký kiểm toán hệ thống (Audit Logs) cho Ban Quản trị.
/// </summary>
[Route("api/admin/audit-logs")]
[ApiController]
[Authorize(Policy = "RequireAdmin")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AdminAuditController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// GET /api/admin/audit-logs
    /// Lấy danh sách nhật ký kiểm toán hợp nhất (phân trang, lọc theo loại, ngày, từ khóa).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] AuditLogQueryDto query)
    {
        var result = await _auditLogService.GetLogsAsync(query);
        return Ok(ApiResponse<PagedResult<AuditLogItemDto>>.Ok(result));
    }

    /// <summary>
    /// GET /api/admin/audit-logs/{id}?logType={logType}
    /// Lấy thông tin chi tiết một bản ghi kiểm toán.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id, [FromQuery] string? logType)
    {
        var detail = await _auditLogService.GetDetailAsync(id, logType);
        if (detail == null)
            return NotFound(ApiResponse.Fail($"Không tìm thấy nhật ký kiểm toán id {id}"));

        return Ok(ApiResponse<AuditLogDetailDto>.Ok(detail));
    }
}

