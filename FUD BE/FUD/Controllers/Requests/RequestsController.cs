using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FUD.DTOs.Requests;
using FUD.Services.Interfaces.Requests;

namespace FUD.Controllers.Requests
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RequestsController : ControllerBase
    {
        private readonly IRequestService _requestService;

        public RequestsController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        /// <summary>
        /// Lấy tất cả danh sách đơn yêu cầu (Manager/Admin)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAll([FromQuery] string? requestType, [FromQuery] string? status)
        {
            var result = await _requestService.GetAllRequestsAsync(requestType, status);
            return Ok(result);
        }

        /// <summary>
        /// Sinh viên xem danh sách các đơn yêu cầu của chính mình
        /// </summary>
        [HttpGet("my-requests")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var studentId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            var result = await _requestService.GetStudentRequestsAsync(studentId);
            return Ok(result);
        }

        /// <summary>
        /// Lấy ngữ cảnh chuyển giường (chỗ ở hiện tại, điều kiện gửi đơn và danh sách giường trống phù hợp giới tính)
        /// </summary>
        [HttpGet("bed-transfer/context")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetBedTransferContext()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var studentId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            var result = await _requestService.GetBedTransferContextAsync(studentId);
            return Ok(result);
        }

        /// <summary>
        /// Sinh viên gửi đơn yêu cầu (Booking, BedTransfer, Checkout, Complaint, Others)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Create([FromForm] CreateRequestDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var studentId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            try
            {
                var result = await _requestService.CreateRequestAsync(studentId, dto);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Danh sách Bảo vệ để Quản lý phân công kiểm tra phòng khi duyệt chuyển giường (Bước 1)
        /// </summary>
        [HttpGet("guards")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetGuards([FromQuery] int? dormId)
        {
            try
            {
                var result = await _requestService.GetGuardsAsync(dormId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Danh sách nhiệm vụ kiểm tra phòng cũ & chốt điện nước cho Bảo vệ được phân công
        /// </summary>
        [HttpGet("bed-transfer/guard-tasks")]
        [Authorize(Roles = "Guard")]
        public async Task<IActionResult> GetGuardTransferTasks()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var guardUserId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            try
            {
                var result = await _requestService.GetGuardTransferInspectionsAsync(guardUserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Bảo vệ gửi biên bản kiểm tra thiết bị phòng cũ & chỉ số điện nước cuối cho đơn chuyển giường
        /// </summary>
        [HttpPost("{id}/transfer-inspection")]
        [Authorize(Roles = "Guard")]
        public async Task<IActionResult> SubmitTransferInspection(int id, [FromBody] SubmitTransferInspectionDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var guardUserId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            try
            {
                var result = await _requestService.SubmitTransferInspectionAsync(id, guardUserId, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Quản lý (Manager) xử lý phê duyệt hoặc từ chối đơn yêu cầu
        /// </summary>
        [HttpPost("{id}/process")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> Process(int id, [FromBody] ProcessRequestDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var managerId))
            {
                return Unauthorized(new { message = "Không xác định được danh tính người dùng." });
            }

            try
            {
                var result = await _requestService.ProcessRequestAsync(id, managerId, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

