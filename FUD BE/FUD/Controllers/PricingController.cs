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

        private bool TryGetCurrentUserId(out int userId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("nameid");
            return int.TryParse(userIdClaim, out userId) && userId > 0;
        }

        [HttpPost("save")]
        public async Task<IActionResult> SavePricing([FromBody] SavePricingRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            if (!TryGetCurrentUserId(out var managerUserId))
            {
                return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ từ token."));
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

            if (!TryGetCurrentUserId(out var managerUserId))
            {
                return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ từ token."));
            }

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

