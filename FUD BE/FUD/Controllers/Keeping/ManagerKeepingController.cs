using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Keeping;
using FUD.Services.Interfaces.Keeping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Keeping
{
    /// <summary>
    /// UC02 – Duyệt yêu cầu giữ phòng (Ban Quản lý).
    /// Chỉ nhận các yêu cầu đã được sinh viên thanh toán phí giữ phòng
    /// (Booking_Request: BookingKind = 'Keeping' và Status = 'Pending').
    /// </summary>
    [Route("api/keeping/manager")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerKeepingController : ControllerBase
    {
        private readonly IKeepingService _keepingService;

        public ManagerKeepingController(IKeepingService keepingService)
        {
            _keepingService = keepingService;
        }

        /// <summary>
        /// Danh sách yêu cầu giữ phòng đang chờ Ban Quản lý duyệt (đã thanh toán xong).
        /// </summary>
        [HttpGet("requests")]
        public async Task<IActionResult> GetPendingForManager()
        {
            try
            {
                var result = await _keepingService.GetPendingForManagerAsync();
                return Ok(ApiResponse<List<KeepingRequestManagerDto>>.Ok(result));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi tải danh sách yêu cầu giữ phòng: {ex.Message}"));
            }
        }

        /// <summary>
        /// Ban Quản lý phê duyệt yêu cầu giữ phòng đã thanh toán.
        /// Duyệt xong: Booking_Request → 'Approved' + tạo cư trú kỳ kế tiếp.
        /// </summary>
        [HttpPost("requests/{id:int}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var managerId = GetCurrentUserId();
            if (managerId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính người quản lý từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _keepingService.ApproveByManagerAsync(id, managerId);
                return Ok(ApiResponse<KeepingRequestResponseDto>.Ok(result,
                    "Đã phê duyệt yêu cầu giữ phòng và cập nhật cư trú kỳ kế tiếp thành công!"));
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
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi phê duyệt yêu cầu giữ phòng: {ex.Message}"));
            }
        }

        /// <summary>
        /// Ban Quản lý từ chối yêu cầu giữ phòng đã thanh toán.
        /// </summary>
        [HttpPost("requests/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] ProcessKeepingRequestDto dto)
        {
            var managerId = GetCurrentUserId();
            if (managerId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính người quản lý từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _keepingService.RejectByManagerAsync(id, managerId, dto.RejectReason ?? "");
                return Ok(ApiResponse<KeepingRequestResponseDto>.Ok(result, "Đã từ chối yêu cầu giữ phòng!"));
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
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi từ chối yêu cầu giữ phòng: {ex.Message}"));
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return 0;
            }

            return userId;
        }
    }
}
