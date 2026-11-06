using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Violations;
using FUD.Services.Interfaces.Violations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Violations
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViolationsController : ControllerBase
    {
        private readonly IViolationService _violationService;

        public ViolationsController(IViolationService violationService)
        {
            _violationService = violationService;
        }

        /// <summary>
        /// Lấy danh mục các loại vi phạm (Violation Types)
        /// </summary>
        [HttpGet("types")]
        [AllowAnonymous]
        public async Task<IActionResult> GetViolationTypes()
        {
            var types = await _violationService.GetViolationTypesAsync();
            return Ok(ApiResponse<List<ViolationTypeDto>>.Ok(types));
        }

        /// <summary>
        /// Lấy danh sách sinh viên đang lưu trú theo phòng
        /// </summary>
        [HttpGet("room-students/{roomId:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStudentsByRoom(int roomId)
        {
            var students = await _violationService.GetStudentsByRoomAsync(roomId);
            return Ok(ApiResponse<List<RoomStudentDto>>.Ok(students));
        }

        /// <summary>
        /// Tìm kiếm sinh viên theo MSSV hoặc mã định danh
        /// </summary>
        [HttpGet("search-student")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchStudent([FromQuery] string studentCode, [FromQuery] int? roomId = null)
        {
            if (string.IsNullOrWhiteSpace(studentCode))
            {
                return BadRequest(ApiResponse.Fail("Vui lòng nhập MSSV để tìm kiếm."));
            }

            var student = await _violationService.SearchStudentByCodeAsync(studentCode, roomId);
            if (student == null)
            {
                return NotFound(ApiResponse.Fail($"Không tìm thấy sinh viên với mã '{studentCode}'."));
            }

            return Ok(ApiResponse<RoomStudentDto>.Ok(student));
        }

        /// <summary>
        /// Lấy User Id từ Claims linh hoạt NameIdentifier, sub, nameid
        /// </summary>
        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("nameid");

            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        /// <summary>
        /// UC22: Ghi nhận vi phạm phòng/sinh viên kèm upload ảnh Cloudinary
        /// Yêu cầu xác thực Token cán bộ (Guard)
        /// </summary>
        [HttpPost("record")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> RecordViolation([FromForm] CreateViolationRequestDto request)
        {
            try
            {
                var guardUserId = GetCurrentUserId();
                if (!guardUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ thực hiện từ Token."));
                }

                var result = await _violationService.RecordViolationsAsync(request, guardUserId.Value);
                return Ok(ApiResponse<List<ViolationItemDto>>.Ok(result, "Ghi nhận vi phạm thành công. Đã tạo biên bản lưu vào hệ thống."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi ghi nhận vi phạm: {ex.Message}"));
            }
        }

        /// <summary>
        /// Lấy danh sách hồ sơ vi phạm (Manager & Guard xem toàn bộ hoặc lọc)
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetViolations([FromQuery] int? roomId = null, [FromQuery] int? studentId = null, [FromQuery] string? status = null)
        {
            var list = await _violationService.GetViolationsAsync(roomId, studentId, status);
            return Ok(ApiResponse<List<ViolationItemDto>>.Ok(list));
        }

        /// <summary>
        /// Lấy chi tiết một biên bản vi phạm
        /// </summary>
        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetViolationById(int id)
        {
            var violation = await _violationService.GetViolationByIdAsync(id);
            if (violation == null)
            {
                return NotFound(ApiResponse.Fail("Không tìm thấy hồ sơ vi phạm."));
            }
            return Ok(ApiResponse<ViolationItemDto>.Ok(violation));
        }

        /// <summary>
        /// UC19: Sinh viên xem danh sách thông báo vi phạm gửi tới mình
        /// </summary>
        [HttpGet("student/notices")]
        [Authorize]
        public async Task<IActionResult> GetStudentNotices()
        {
            var studentUserId = GetCurrentUserId();
            if (!studentUserId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail("Không xác thực được sinh viên."));
            }

            var notices = await _violationService.GetStudentNoticesAsync(studentUserId.Value);
            return Ok(ApiResponse<List<ViolationItemDto>>.Ok(notices));
        }

        /// <summary>
        /// UC21: Sinh viên tra cứu lịch sử vi phạm cá nhân
        /// </summary>
        [HttpGet("student/history")]
        [Authorize]
        public async Task<IActionResult> GetStudentHistory([FromQuery] string? term = null, [FromQuery] string? status = null)
        {
            var studentUserId = GetCurrentUserId();
            if (!studentUserId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail("Không xác thực được sinh viên."));
            }

            var history = await _violationService.GetStudentHistoryAsync(studentUserId.Value, term, status);
            return Ok(ApiResponse<List<ViolationItemDto>>.Ok(history));
        }

        /// <summary>
        /// UC23: Quản lý (Manager) đánh giá mức độ vi phạm ban đầu và gửi thông báo tới sinh viên
        /// </summary>
        [HttpPost("{id:int}/evaluate")]
        [Authorize]
        public async Task<IActionResult> EvaluateViolation(int id, [FromBody] EvaluateViolationRequestDto request)
        {
            try
            {
                var managerUserId = GetCurrentUserId();
                if (!managerUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác thực được danh tính cán bộ quản lý."));
                }

                var result = await _violationService.EvaluateViolationAsync(id, managerUserId.Value, request);
                return Ok(ApiResponse<ViolationItemDto>.Ok(result, "Đã đánh giá mức độ vi phạm và ban hành thông báo!"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi đánh giá vi phạm: {ex.Message}"));
            }
        }

        /// <summary>
        /// Mở khóa đình chỉ book phòng / giữ phòng cho sinh viên
        /// </summary>
        [HttpPost("unsuspend-student/{studentId:int}")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> UnsuspendStudent(int studentId, [FromBody] UnsuspendStudentDto? dto)
        {
            try
            {
                var managerUserId = GetCurrentUserId();
                if (!managerUserId.HasValue)
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính người dùng từ Token."));
                }

                await _violationService.UnsuspendStudentAsync(studentId, managerUserId.Value, dto?.Note);
                return Ok(ApiResponse.Ok("Đã mở khóa quyền đăng ký book phòng và giữ phòng cho sinh viên thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Lỗi hệ thống: " + ex.Message));
            }
        }
    }
}
