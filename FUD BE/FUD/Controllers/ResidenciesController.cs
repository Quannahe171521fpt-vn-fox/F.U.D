using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Residencies;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers
{
    [Route("api/residencies")]
    [ApiController]
    [Authorize]
    public class ResidenciesController : ControllerBase
    {
        private readonly IResidencyService _residencyService;

        public ResidenciesController(IResidencyService residencyService)
        {
            _residencyService = residencyService;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("nameid");

            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        /// <summary>
        /// Sinh viên xem danh sách lịch sử lưu trú (Residency History) của chính mình qua các học kỳ
        /// </summary>
        [HttpGet("my-history")]
        public async Task<IActionResult> GetMyHistory(
            [FromQuery] string? status = null,
            [FromQuery] string? term = null,
            [FromQuery] string? registrationType = null,
            [FromQuery] string? search = null)
        {
            var studentUserId = GetCurrentUserId();
            if (!studentUserId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ Token."));
            }

            var query = new ResidencyHistoryQueryDto
            {
                Status = status,
                Term = term,
                RegistrationType = registrationType,
                Search = search
            };

            var list = await _residencyService.GetStudentResidencyHistoryAsync(studentUserId.Value, query);
            return Ok(ApiResponse<List<ResidencyHistoryItemDto>>.Ok(list));
        }

        /// <summary>
        /// Sinh viên xem chi tiết một đợt lưu trú của chính mình (kèm biên bản bàn giao, chuyển giường, trả phòng, hóa đơn)
        /// </summary>
        [HttpGet("my-history/{id:int}")]
        public async Task<IActionResult> GetMyHistoryDetail(int id)
        {
            var studentUserId = GetCurrentUserId();
            if (!studentUserId.HasValue)
            {
                return Unauthorized(ApiResponse.Fail("Không xác định được danh tính sinh viên từ Token."));
            }

            var detail = await _residencyService.GetStudentResidencyDetailAsync(studentUserId.Value, id);
            if (detail == null)
            {
                return NotFound(ApiResponse.Fail("Không tìm thấy hồ sơ lưu trú hoặc bạn không có quyền truy cập."));
            }

            return Ok(ApiResponse<ResidencyHistoryDetailDto>.Ok(detail));
        }
    }
}
