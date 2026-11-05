using FUD.Common;
using FUD.DTOs.Pricing;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers
{
    [Route("api/manage/terms")]
    [ApiController]
    [Authorize(Roles = "Manager,Admin")]
    public class TermManagementController : ControllerBase
    {
        private readonly ITermService _termService;

        public TermManagementController(ITermService termService)
        {
            _termService = termService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTerms()
        {
            var terms = await _termService.GetAllTermsAsync();
            return Ok(ApiResponse<List<TermSummaryDto>>.Ok(terms));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetTermDetail(int id)
        {
            try
            {
                var term = await _termService.GetTermDetailAsync(id);
                return Ok(ApiResponse<TermDetailDto>.Ok(term));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTerm([FromBody] CreateTermRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            try
            {
                var created = await _termService.CreateTermAsync(request);
                return StatusCode(StatusCodes.Status201Created,
                    ApiResponse<TermDetailDto>.Ok(created, "Tạo học kỳ mới thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateTerm(int id, [FromBody] UpdateTermRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            try
            {
                var updated = await _termService.UpdateTermAsync(id, request);
                return Ok(ApiResponse<TermDetailDto>.Ok(updated, "Cập nhật thông tin học kỳ thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTerm(int id)
        {
            try
            {
                await _termService.DeleteTermAsync(id);
                return Ok(ApiResponse.Ok("Xóa học kỳ thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("{id:int}/schedule")]
        public async Task<IActionResult> SaveRegistrationSchedule(int id, [FromBody] SaveRegistrationScheduleRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            if (request.TermId != id)
            {
                request.TermId = id;
            }

            try
            {
                var result = await _termService.SaveRegistrationScheduleAsync(id, request);
                return Ok(ApiResponse<TermDetailDto>.Ok(result, "Cập nhật lịch trình đăng ký thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPatch("{id:int}/periods/{periodId:int}/toggle")]
        public async Task<IActionResult> TogglePeriodActive(int id, int periodId, [FromQuery] bool active)
        {
            try
            {
                await _termService.TogglePeriodActiveAsync(id, periodId, active);
                var message = active ? "Kích hoạt đợt đăng ký thành công" : "Tắt đợt đăng ký thành công";
                return Ok(ApiResponse.Ok(message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }
    }
}
