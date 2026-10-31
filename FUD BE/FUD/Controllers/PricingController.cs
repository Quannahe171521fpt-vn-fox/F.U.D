using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Pricing;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers
{
    [Route("api/pricing")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class PricingController : ControllerBase
    {
        private readonly IPricingService _pricingService;

        public PricingController(IPricingService pricingService)
        {
            _pricingService = pricingService;
        }

        [HttpGet("terms")]
        public async Task<IActionResult> GetTerms()
        {
            var terms = await _pricingService.GetTermsLookupAsync();
            return Ok(ApiResponse<List<TermLookupDto>>.Ok(terms));
        }

        [HttpGet("terms/{termId:int}")]
        public async Task<IActionResult> GetPricingByTerm(int termId)
        {
            try
            {
                var pricing = await _pricingService.GetPricingByTermAsync(termId);
                return Ok(ApiResponse<TermPricingResponseDto>.Ok(pricing));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("save")]
        public async Task<IActionResult> SavePricing([FromBody] SavePricingRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int managerUserId))
            {
                // Fallback nếu chạy test hoặc dev không có token chuẩn
                managerUserId = 2; // Default Manager id trong seed data
            }

            try
            {
                var result = await _pricingService.SavePricingAsync(managerUserId, request);
                return Ok(ApiResponse<TermPricingResponseDto>.Ok(result, "Lưu bảng giá thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex) when (ex.Message == "REGISTRATION_ALREADY_STARTED")
            {
                // Trả về mã đặc biệt để Frontend kích hoạt Dialog Warning (2.3.2)
                return StatusCode(StatusCodes.Status409Conflict, ApiResponse.Fail("Đã bắt đầu đăng ký: Cần xác nhận áp dụng bảng giá mới chỉ cho đợt phòng mới."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }
    }
}

