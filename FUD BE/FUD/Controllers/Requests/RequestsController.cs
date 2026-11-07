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
        public async Task<IActionResult> GetAll([FromQuery] string? requestType, [FromQuery] string? status)
        {
            var result = await _requestService.GetAllRequestsAsync(requestType, status);
            return Ok(result);
        }

        /// <summary>
        /// Sinh viên xem danh sách các đơn yêu cầu của chính mình
        /// </summary>
        [HttpGet("my-requests")]
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
        /// Sinh viên gửi đơn yêu cầu (Booking, BedTransfer, Checkout, Complaint, Others)
        /// </summary>
        [HttpPost]
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
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Quản lý (Manager) xử lý phê duyệt hoặc từ chối đơn yêu cầu
        /// </summary>
        [HttpPost("{id}/process")]
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

