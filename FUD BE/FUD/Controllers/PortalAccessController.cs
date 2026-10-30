using System.Security.Claims;
using FUD.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers;

[Route("api/portals")]
[ApiController]
[Authorize]
public class PortalAccessController : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult GetAdminPortalData()
    {
        return Ok(ApiResponse<object>.Ok(new { message = "Chào mừng Admin! Dữ liệu quản trị hệ thống toàn quyền." }));
    }

    [HttpGet("manager")]
    [Authorize(Policy = "RequireManager")]
    public IActionResult GetManagerPortalData()
    {
        return Ok(ApiResponse<object>.Ok(new { message = "Dữ liệu Ban Quản lý KTX: Duyệt phòng, cơ sở vật chất, tin tức." }));
    }

    [HttpGet("guard")]
    [Authorize(Policy = "RequireGuard")]
    public IActionResult GetGuardPortalData()
    {
        return Ok(ApiResponse<object>.Ok(new { message = "Dữ liệu Đội Bảo vệ: Ghi số điện nước, kiểm tra an ninh." }));
    }

    [HttpGet("student")]
    [Authorize(Policy = "RequireStudent")]
    public IActionResult GetStudentPortalData()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(ApiResponse<object>.Ok(new { 
            message = "Dữ liệu Sinh viên nội trú.", 
            studentId = currentUserId 
        }));
    }
}
