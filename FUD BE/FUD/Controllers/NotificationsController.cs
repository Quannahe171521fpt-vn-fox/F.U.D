using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Notifications;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize(Roles = "Student,Manager,Guard")]
public class NotificationsController : ControllerBase
{
    private readonly IInAppNotificationService _notificationService;

    public NotificationsController(IInAppNotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private int? GetCurrentUserId()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("nameid");

        return int.TryParse(userIdStr, out var userId) ? userId : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? readStatus = null,
        [FromQuery] string? referenceType = null,
        [FromQuery] string? search = null)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _notificationService.GetMyNotificationsAsync(
            userId.Value, unreadOnly, page, pageSize, readStatus, referenceType, search);
        return Ok(ApiResponse<NotificationListResponseDto>.Ok(result));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _notificationService.GetUnreadCountAsync(userId.Value);
        return Ok(ApiResponse<UnreadCountResponseDto>.Ok(result));
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người dùng."));
        }

        try
        {
            var result = await _notificationService.MarkAsReadAsync(userId.Value, id);
            return Ok(ApiResponse<MarkNotificationReadResponseDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _notificationService.MarkAllAsReadAsync(userId.Value);
        return Ok(ApiResponse<MarkAllReadResponseDto>.Ok(result));
    }
}
