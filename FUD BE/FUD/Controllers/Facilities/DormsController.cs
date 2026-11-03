using FUD.Common;
using FUD.DTOs.Facilities.Dorm;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/dorms")]
    [ApiController]
    public class DormsController : ControllerBase
    {
        private readonly IDormService _dormService;

        public DormsController(IDormService dormService)
        {
            _dormService = dormService;
        }

        /// <summary>GET /api/dorms — Lấy danh sách KTX (có phân trang, tìm kiếm)</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DormQueryDto query)
        {
            var result = await _dormService.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(result));
        }

        /// <summary>GET /api/dorms/{id} — Chi tiết KTX</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dorm = await _dormService.GetByIdAsync(id);
            if (dorm == null)
                return NotFound(ApiResponse.Fail($"KTX {id} không tồn tại"));
            return Ok(ApiResponse<object>.Ok(dorm));
        }

        /// <summary>POST /api/dorms — Tạo KTX mới [Manager/Admin]</summary>
        [HttpPost]
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> Create([FromBody] CreateDormDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var created = await _dormService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id },
                    ApiResponse<object>.Ok(created, "Tạo KTX thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>PUT /api/dorms/{id} — Cập nhật thông tin KTX [Manager/Admin]</summary>
        [HttpPut("{id:int}")]
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateDormDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _dormService.UpdateAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật KTX thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>PATCH /api/dorms/{id}/status — Đổi trạng thái Active/Inactive [Manager/Admin]</summary>
        [HttpPatch("{id:int}/status")]
        [Authorize(Policy = "RequireManager")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateDormStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _dormService.UpdateStatusAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật trạng thái KTX thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        /// <summary>
        /// DELETE /api/dorms/{id} — Xóa mềm KTX [Admin only]
        /// Chỉ soft-delete (set DeletedAt). Nếu còn tòa (Block) thì trả 409 Conflict.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Policy = "RequireAdmin")]
        public async Task<IActionResult> SoftDelete(int id)
        {
            try
            {
                await _dormService.SoftDeleteAsync(id);
                return Ok(ApiResponse.Ok("Đã xóa KTX thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        }
    }
}
