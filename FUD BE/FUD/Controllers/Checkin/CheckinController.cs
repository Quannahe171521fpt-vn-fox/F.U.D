using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Checkin;
using FUD.Services.Interfaces.Checkin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Checkin
{
    [Route("api/checkin")]
    [ApiController]
    [Authorize]
    public class CheckinController : ControllerBase
    {
        private readonly ICheckinService _checkinService;

        public CheckinController(ICheckinService checkinService)
        {
            _checkinService = checkinService;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("nameid");

            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        /// <summary>
        /// Danh sách sinh viên có đơn Đặt phòng (Booking/Residency) ở trạng thái chờ nhận phòng
        /// </summary>
        [HttpGet("pending-students")]
        [Authorize(Roles = "Guard,Manager,Admin")]
        public async Task<IActionResult> GetPendingStudents(
            [FromQuery] int? blockId = null,
            [FromQuery] int? roomId = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var isGuardOnly = User.IsInRole("Guard") && !User.IsInRole("Manager") && !User.IsInRole("Admin");
                int? guardUserId = isGuardOnly ? GetCurrentUserId() : null;
                if (isGuardOnly && !guardUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));
                }

                var list = await _checkinService.GetPendingStudentsAsync(blockId, roomId, search, guardUserId);
                return Ok(ApiResponse<List<PendingCheckinStudentDto>>.Ok(list));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
        }

        /// <summary>
        /// Lấy danh sách thiết bị thực tế của phòng (và giường của SV) từ bảng Equipments trong Database
        /// </summary>
        [HttpGet("room-equipments/{roomId:int}")]
        [Authorize(Roles = "Guard,Manager,Admin")]
        public async Task<IActionResult> GetRoomEquipments(int roomId, [FromQuery] int? bedId = null)
        {
            try
            {
                var isGuardOnly = User.IsInRole("Guard") && !User.IsInRole("Manager") && !User.IsInRole("Admin");
                int? guardUserId = isGuardOnly ? GetCurrentUserId() : null;
                if (isGuardOnly && !guardUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));
                }

                var list = await _checkinService.GetRoomEquipmentsAsync(roomId, bedId, guardUserId);
                return Ok(ApiResponse<List<RoomEquipmentCheckinDto>>.Ok(list));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
        }

        /// <summary>
        /// Bảo vệ hoàn tất lập biên bản bàn giao tài sản Check-in
        /// </summary>
        [HttpPost("complete")]
        [Authorize(Roles = "Guard")]
        public async Task<IActionResult> CompleteCheckin([FromBody] CreateCheckinHandoverDto dto)
        {
            try
            {
                var guardUserId = GetCurrentUserId();
                if (!guardUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));
                }

                var result = await _checkinService.CompleteCheckinAsync(guardUserId.Value, dto);
                return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(result, "Đã hoàn tất lập biên bản bàn giao tài sản Check-in."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi thực hiện Check-in: {ex.Message}"));
            }
        }

        /// <summary>
        /// Lịch sử biên bản bàn giao tài sản Check-in
        /// </summary>
        [HttpGet("history")]
        [Authorize(Roles = "Guard,Manager,Admin")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int? blockId = null,
            [FromQuery] int? roomId = null,
            [FromQuery] string? search = null)
        {
            try
            {
                var isGuardOnly = User.IsInRole("Guard") && !User.IsInRole("Manager") && !User.IsInRole("Admin");
                int? guardUserId = isGuardOnly ? GetCurrentUserId() : null;
                if (isGuardOnly && !guardUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));
                }

                var list = await _checkinService.GetCheckinHistoryAsync(blockId, roomId, search, guardUserId);
                return Ok(ApiResponse<List<CheckinHandoverResponseDto>>.Ok(list));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
        }

        /// <summary>
        /// Sinh viên xem danh sách các biên bản bàn giao nhận phòng (Check-in) của chính mình
        /// </summary>
        [HttpGet("my-checkins")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyCheckins()
        {
            var studentUserId = GetCurrentUserId();
            if (!studentUserId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ Token."));
            }

            var list = await _checkinService.GetStudentCheckinsAsync(studentUserId.Value);
            return Ok(ApiResponse<List<CheckinHandoverResponseDto>>.Ok(list));
        }

        /// <summary>
        /// Xem chi tiết một biên bản bàn giao Check-in
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize(Roles = "Student,Guard,Manager,Admin")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var callerUserId = GetCurrentUserId();
                var isManagerOrAdmin = User.IsInRole("Manager") || User.IsInRole("Admin");
                var isGuard = User.IsInRole("Guard");

                var detail = await _checkinService.GetCheckinByIdAsync(id, callerUserId, isManagerOrAdmin, isGuard);
                if (detail == null)
                {
                    return NotFound(ApiResponse.Fail("Không tìm thấy biên bản bàn giao."));
                }
                return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(detail));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
        }
    }
}
