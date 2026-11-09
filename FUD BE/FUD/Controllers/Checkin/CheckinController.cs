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
        public async Task<IActionResult> GetPendingStudents(
            [FromQuery] int? blockId = null,
            [FromQuery] int? roomId = null,
            [FromQuery] string? search = null)
        {
            var list = await _checkinService.GetPendingStudentsAsync(blockId, roomId, search);
            return Ok(ApiResponse<List<PendingCheckinStudentDto>>.Ok(list));
        }

        /// <summary>
        /// Lấy danh sách thiết bị thực tế của phòng (và giường của SV) từ bảng Equipments trong Database
        /// </summary>
        [HttpGet("room-equipments/{roomId:int}")]
        public async Task<IActionResult> GetRoomEquipments(int roomId, [FromQuery] int? bedId = null)
        {
            var list = await _checkinService.GetRoomEquipmentsAsync(roomId, bedId);
            return Ok(ApiResponse<List<RoomEquipmentCheckinDto>>.Ok(list));
        }

        /// <summary>
        /// Bảo vệ hoàn tất lập biên bản bàn giao tài sản Check-in
        /// </summary>
        [HttpPost("complete")]
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
        public async Task<IActionResult> GetHistory(
            [FromQuery] int? blockId = null,
            [FromQuery] int? roomId = null,
            [FromQuery] string? search = null)
        {
            var list = await _checkinService.GetCheckinHistoryAsync(blockId, roomId, search);
            return Ok(ApiResponse<List<CheckinHandoverResponseDto>>.Ok(list));
        }

        /// <summary>
        /// Sinh viên xem danh sách các biên bản bàn giao nhận phòng (Check-in) của chính mình
        /// </summary>
        [HttpGet("my-checkins")]
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
        public async Task<IActionResult> GetById(int id)
        {
            var detail = await _checkinService.GetCheckinByIdAsync(id);
            if (detail == null)
            {
                return NotFound(ApiResponse.Fail("Không tìm thấy biên bản bàn giao."));
            }
            return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(detail));
        }
    }
}
