using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Booking;
using FUD.Services.Interfaces.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Booking;

[Route("api/booking")]
[ApiController]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    // ── Student Endpoints ─────────────────────────────────────────

    [HttpGet("eligibility")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetEligibility()
    {
        var studentId = GetCurrentUserId();
        if (studentId == 0)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ token đăng nhập."));
        }

        var result = await _bookingService.CheckEligibilityAsync(studentId);
        return Ok(ApiResponse<BookingEligibilityResponseDto>.Ok(result));
    }

    [HttpGet("available-dorms")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetAvailableDorms()
    {
        var studentId = GetCurrentUserId();
        if (studentId == 0)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ token đăng nhập."));
        }

        var result = await _bookingService.GetAvailableDormsAsync(studentId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("available-blocks")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetAvailableBlocks([FromQuery] int dormId)
    {
        var studentId = GetCurrentUserId();
        if (studentId == 0)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ token đăng nhập."));
        }

        var result = await _bookingService.GetAvailableBlocksAsync(studentId, dormId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("available-rooms")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetAvailableRooms([FromQuery] int blockId)
    {
        var result = await _bookingService.GetAvailableRoomsAsync(blockId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("available-beds")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetAvailableBeds([FromQuery] int roomId)
    {
        var result = await _bookingService.GetAvailableBedsAsync(roomId);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("requests")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateBookingRequest([FromBody] CreateBookingRequestDto dto)
    {
        var studentId = GetCurrentUserId();
        if (studentId == 0)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ token đăng nhập."));
        }

        try
        {
            var result = await _bookingService.CreateBookingAsync(studentId, dto);
            return Ok(ApiResponse<BookingRequestResponseDto>.Ok(result, "Đặt phòng thành công! Đơn của bạn đang chờ Ban Quản lý xét duyệt."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse.Fail(ex.Message));
        }
    }

    // ── Manager Endpoints ─────────────────────────────────────────

    [HttpGet("manager/requests")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> GetManagerRequests([FromQuery] string? status)
    {
        var result = await _bookingService.GetAllForManagerAsync(status);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("manager/requests/{id:int}/process")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> ProcessBooking([FromRoute] int id, [FromBody] ProcessBookingDto dto)
    {
        var managerId = GetCurrentUserId();
        if (managerId == 0)
        {
            return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người quản lý."));
        }

        try
        {
            if (string.Equals(dto.Decision, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _bookingService.ApproveAsync(id, managerId);
                return Ok(ApiResponse<BookingRequestManagerDto>.Ok(result, "Đã phê duyệt đơn đặt phòng và cập nhật hồ sơ lưu trú thành công!"));
            }
            else if (string.Equals(dto.Decision, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                var result = await _bookingService.RejectAsync(id, managerId, dto.RejectReason ?? "Không đủ điều kiện");
                return Ok(ApiResponse<BookingRequestManagerDto>.Ok(result, "Đã từ chối đơn đặt phòng!"));
            }
            else
            {
                return BadRequest(ApiResponse.Fail("Quyết định xử lý không hợp lệ. Chỉ chấp nhận 'Approved' hoặc 'Rejected'."));
            }
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse.Fail(ex.Message));
        }
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? User.FindFirstValue("nameid");

        if (string.IsNullOrWhiteSpace(claim) || !int.TryParse(claim, out var id))
        {
            return 0;
        }

        return id;
    }
}

