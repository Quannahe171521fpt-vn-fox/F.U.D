using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Violations;
using FUD.Services.Interfaces.Violations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers
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
        /// Kiểm tra hồ sơ trùng lặp: cùng sinh viên, loại vi phạm và thời gian
        /// </summary>
        [HttpGet("check-duplicate")]
        [AllowAnonymous]
        public async Task<IActionResult> CheckDuplicate([FromQuery] string studentIds, [FromQuery] int violationTypeId, [FromQuery] DateTime occurrenceTime)
        {
            var ids = (studentIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            var result = await _violationService.CheckDuplicateAsync(ids, violationTypeId, occurrenceTime);
            return Ok(ApiResponse<DuplicateViolationCheckResultDto>.Ok(result));
        }

        /// <summary>
        /// Ghi nhận vi phạm phòng/sinh viên kèm upload ảnh Cloudinary
        /// Yêu cầu xác thực Token cán bộ (Guard)
        /// </summary>
        [HttpPost("record")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> RecordViolation([FromForm] CreateViolationRequestDto request)
        {
            try
            {
                // Trích xuất chính xác userId của Guard đang đăng nhập từ JWT Token Claim (NameIdentifier)
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var guardUserId))
                {
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ thực hiện từ Token đăng nhập. Vui lòng đăng nhập lại!"));
                }

                var result = await _violationService.RecordViolationsAsync(request, guardUserId);
                return Ok(ApiResponse<List<ViolationItemDto>>.Ok(result, "Ghi nhận vi phạm thành công. Đã tạo biên bản lưu vào hệ thống."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex) when (ex.Message.StartsWith("DUPLICATE_VIOLATION:"))
            {
                return Conflict(ApiResponse.Fail(ex.Message.Replace("DUPLICATE_VIOLATION:", "")));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Fail($"Lỗi khi ghi nhận vi phạm: {ex.Message}"));
            }
        }

        /// <summary>
        /// Lấy danh sách vi phạm đã ghi nhận (cho Bảo vệ / Ban quản lý)
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetViolations([FromQuery] int? roomId = null, [FromQuery] int? studentId = null)
        {
            var violations = await _violationService.GetViolationsAsync(roomId, studentId);
            return Ok(ApiResponse<List<ViolationItemDto>>.Ok(violations));
        }
    }
}
