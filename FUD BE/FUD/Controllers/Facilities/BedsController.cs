using FUD.Common;
using FUD.DTOs.Facilities.Bed;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/beds")]
    [ApiController]
    [Authorize(Roles = "Manager")]
    public class BedsController : ControllerBase
    {
        private readonly IBedService _bedService;

        public BedsController(IBedService bedService)
        {
            _bedService = bedService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] BedQueryDto query)
        {
            var result = await _bedService.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var bed = await _bedService.GetByIdAsync(id);
            if (bed == null)
                return NotFound(ApiResponse.Fail($"Giường {id} không tồn tại"));
            return Ok(ApiResponse<object>.Ok(bed));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBedDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            try
            {
                var created = await _bedService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id },
                    ApiResponse<object>.Ok(created, "Tạo giường thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBedDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            try
            {
                var updated = await _bedService.UpdateAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật giường thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBedStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            try
            {
                var updated = await _bedService.UpdateStatusAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật trạng thái giường thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }
    }
}

