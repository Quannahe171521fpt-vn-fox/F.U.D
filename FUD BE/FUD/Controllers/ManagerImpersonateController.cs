using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Auth;
using FUD.Services;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers;

/// <summary>
/// Controller hỗ trợ Quản lý (Manager) tra cứu thông tin và đăng nhập hộ sinh viên.
/// </summary>
[Route("api/manager")]
[ApiController]
[Authorize(Policy = "RequireManager")]
public class ManagerImpersonateController : ControllerBase
{
    private readonly IManagerImpersonateService _impersonateService;

    public ManagerImpersonateController(IManagerImpersonateService impersonateService)
    {
        _impersonateService = impersonateService;
    }

    /// <summary>
    /// GET /api/manager/lookup-student?q=email_or_mssv
    /// Tra cứu thông tin đầy đủ của sinh viên theo Email hoặc Mã sinh viên (Code).
    /// </summary>
    [HttpGet("lookup-student")]
    public async Task<IActionResult> LookupStudent([FromQuery] string q)
    {
        try
        {
            var studentData = await _impersonateService.LookupStudentAsync(q);
            return Ok(ApiResponse<StudentLookupDto>.Ok(studentData, "Tìm thấy thông tin sinh viên"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// POST /api/manager/impersonate/{studentId}
    /// Tạo phiên đăng nhập hộ sinh viên cho Ban Quản trị.
    /// </summary>
    [HttpPost("impersonate/{studentId:int}")]
    public async Task<IActionResult> Impersonate(int studentId)
    {
        var isImpersonating = string.Equals(
            User.FindFirstValue("imp"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (isImpersonating)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Không thể thực hiện đăng nhập hộ từ một phiên đang đăng nhập hộ."));
        }

        var managerIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("nameid");

        if (!int.TryParse(managerIdStr, out var managerId))
        {
            return Unauthorized(ApiResponse.Fail("Token quản lý không hợp lệ"));
        }

        var rawIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();
        var rawUserAgent = Request?.Headers.UserAgent.ToString();

        try
        {
            var result = await _impersonateService.ImpersonateStudentAsync(
                managerId,
                studentId,
                isImpersonating,
                rawIp,
                rawUserAgent);

            return Ok(ApiResponse<ImpersonationResponseDto>.Ok(result, "Tạo phiên đăng nhập hộ sinh viên thành công"));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (AuditLogWriteException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail(ex.Message));
        }
    }
}
