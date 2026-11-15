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
            catch (InvalidOperationException ex) when (ex.Message == "TERM_NOT_EDITABLE")
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Không thể chỉnh sửa giá: Học kỳ đang mở đăng ký hoặc đã kết thúc."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }
        [HttpPost("save-utility")]
        public async Task<IActionResult> SaveUtilityPrices([FromBody] SaveUtilityPricesRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ"));

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int managerUserId)) managerUserId = 2;

            try
            {
                var result = await _pricingService.SaveUtilityPricesAsync(managerUserId, request);
                return Ok(ApiResponse<TermPricingResponseDto>.Ok(result, "Lưu giá điện/nước thành công"));
            }
            catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) when (ex.Message == "TERM_NOT_EDITABLE")
            { return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail("Học kỳ đang mở hoặc đã kết thúc, không thể chỉnh sửa.")); }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }
    }
}

