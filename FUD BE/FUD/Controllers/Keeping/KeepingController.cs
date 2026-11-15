using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Keeping;
using FUD.Services.Interfaces.Keeping;
using FUD.Services.Keeping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FUD.Controllers.Keeping
{
    /// <summary>
    /// UC02 – Giữ phòng (sinh viên giữ giường hiện tại cho học kỳ kế tiếp).
    /// Giai đoạn 1: không có thanh toán, xác nhận ngay.
    /// </summary>
    [Route("api/keeping")]
    [ApiController]
    [Authorize(Roles = "Student")]
    public class KeepingController : ControllerBase
    {
        private readonly IKeepingEligibilityService _eligibilityService;
        private readonly IKeepingService _keepingService;

        public KeepingController(
            IKeepingEligibilityService eligibilityService,
            IKeepingService keepingService)
        {
            _eligibilityService = eligibilityService;
            _keepingService = keepingService;
        }

        /// <summary>
        /// Kiểm tra điều kiện giữ phòng: thông tin chỗ ở hiện tại, học kỳ kế tiếp, phí giữ (để hiển thị)
        /// và khoảng thời gian mở kỳ giữ phòng.
        /// </summary>
        [HttpGet("eligibility")]
        public async Task<IActionResult> GetEligibility()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            var result = await _eligibilityService.CheckEligibilityAsync(studentId);
            return Ok(ApiResponse<KeepingEligibilityResponseDto>.Ok(result));
        }

        /// <summary>
        /// Sinh yêu cầu giữ phòng cho học kỳ kế tiếp.
        /// Giai đoạn 1: xác nhận ngay, tạo Request + Residency + thông báo.
        /// </summary>
        [HttpPost("requests")]
        public async Task<IActionResult> CreateKeepingRequest([FromBody] CreateKeepingRequestDto dto)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            try
            {
                var result = await _keepingService.CreateAsync(studentId, dto);
                return Created($"/api/keeping/requests/{result.RequestId}",
                    ApiResponse<KeepingRequestResponseDto>.Ok(result, "Giữ phòng thành công"));
            }
            catch (ArgumentException ex)
            {
                // Chưa đồng ý nội quy → 400
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (KeepingNotEligibleException ex)
            {
                // Không đủ điều kiện → 422 kèm reasonCode ổn định (KEEP_xxx / NOT_RESIDENT...).
                return UnprocessableEntity(ApiResponse.Fail(ex.Message));
            }
            catch (KeepingConflictException ex)
            {
                // Xung đột dữ liệu (race) → 409
                return Conflict(ApiResponse.Fail(ex.Message));
            }
            catch (DbUpdateException)
            {
                // Vi phạm unique index (race giữa 2 request đồng thời) → 409
                return Conflict(ApiResponse.Fail(
                    "Yêu cầu giữ phòng đã tồn tại. Vui lòng tải lại trang để kiểm tra."));
            }
        }

        /// <summary>Chi tiết yêu cầu giữ phòng. Chỉ chủ sở hữu được xem.</summary>
        [HttpGet("requests/{id:int}")]
        public async Task<IActionResult> GetKeepingRequest(int id)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            var request = await _keepingService.GetRequestAsync(id);
            if (request == null)
            {
                return NotFound(ApiResponse.Fail($"Không tìm thấy yêu cầu giữ phòng với mã {id}"));
            }

            var owner = await _keepingService.GetRequestOwnerIdAsync(id);
            if (owner != studentId)
            {
                return Forbid();
            }

            return Ok(ApiResponse<KeepingRequestResponseDto>.Ok(request));
        }

        private int GetCurrentStudentId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var studentId))
            {
                return 0;
            }

            return studentId;
        }
    }
}
