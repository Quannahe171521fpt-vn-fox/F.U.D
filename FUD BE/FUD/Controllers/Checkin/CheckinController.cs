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

        /// <summary>
        /// Bước Manager 1 (Troubleshooting): Quản lý xử lý sự cố khi biên bản có thiết bị hư hỏng/thiếu
        /// và chuyển biên bản sang trạng thái chờ Sinh viên ký xác nhận (Pending Acceptance).
        /// </summary>
        [HttpPost("{id:int}/manager-troubleshoot")]
        public async Task<IActionResult> ManagerTroubleshoot(int id, [FromBody] ManagerTroubleshootCheckinDto? dto)
        {
            try
            {
                var managerUserId = GetCurrentUserId();
                if (!managerUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính Quản lý từ Token."));
                }

                var result = await _checkinService.TroubleshootByManagerAsync(managerUserId.Value, id, dto);
                return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(result, "Quản lý đã xử lý sự cố thiết bị (Troubleshooting) và chuyển cho Sinh viên ký xác nhận."));
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
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi xử lý sự cố thiết bị: {ex.Message}"));
            }
        }

        /// <summary>
        /// Bước Student (Sign to confirm): Sinh viên ký xác nhận biên bản bàn giao phòng.
        /// </summary>
        [HttpPost("{id:int}/student-sign")]
        public async Task<IActionResult> StudentSignConfirm(int id)
        {
            try
            {
                var studentUserId = GetCurrentUserId();
                if (!studentUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính Sinh viên từ Token."));
                }

                var result = await _checkinService.StudentSignConfirmAsync(studentUserId.Value, id);
                return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(result, "Sinh viên đã ký xác nhận biên bản bàn giao (Sign to confirm)."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi ký xác nhận biên bản: {ex.Message}"));
            }
        }

        /// <summary>
        /// Bước Guard (Confirm check-in -> System: Save record &amp; Bed -- Occupied):
        /// Sau khi Sinh viên đã ký xác nhận, Bảo vệ bấm Confirm check-in để lưu chính thức và kích hoạt giường Occupied.
        /// </summary>
        [HttpPost("{id:int}/guard-confirm")]
        public async Task<IActionResult> GuardConfirmCheckin(int id)
        {
            try
            {
                var guardUserId = GetCurrentUserId();
                if (!guardUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính Bảo vệ từ Token."));
                }

                var result = await _checkinService.GuardConfirmCheckinAsync(guardUserId.Value, id);
                return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(result, "Bảo vệ đã xác nhận Check-in (Save record & Bed -> Occupied)."));
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
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi xác nhận Check-in: {ex.Message}"));
            }
        }

        /// <summary>
        /// Bước Manager 2 (Check record -> End): Quản lý kiểm tra biên bản bàn giao sau khi lưu và kết thúc quy trình.
        /// </summary>
        [HttpPost("{id:int}/manager-check")]
        public async Task<IActionResult> ManagerCheckRecord(int id, [FromBody] ManagerCheckRecordDto? dto)
        {
            try
            {
                var managerUserId = GetCurrentUserId();
                if (!managerUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính Quản lý từ Token."));
                }

                var result = await _checkinService.ManagerCheckRecordAsync(managerUserId.Value, id, dto);
                return Ok(ApiResponse<CheckinHandoverResponseDto>.Ok(result, "Ban Quản lý đã kiểm tra và duyệt hồ sơ biên bản Check-in (Check record)."));
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
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi kiểm tra biên bản Check-in: {ex.Message}"));
            }
        }
    }
}
