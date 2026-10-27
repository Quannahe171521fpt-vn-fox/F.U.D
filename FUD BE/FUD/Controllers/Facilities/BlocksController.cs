using FUD.Common;
using FUD.DTOs.Facilities.Block;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/blocks")]
    [ApiController]
    public class BlocksController : ControllerBase
    {
        private readonly IBlockService _blockService;

        public BlocksController(IBlockService blockService)
        {
            _blockService = blockService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] BlockQueryDto query)
        {
            var result = await _blockService.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var block = await _blockService.GetByIdAsync(id);
            if (block == null)
                return NotFound(ApiResponse.Fail($"Block {id} không tồn tại"));
            return Ok(ApiResponse<object>.Ok(block));
        }

        [HttpGet("{id:int}/rooms")]
        public async Task<IActionResult> GetRooms(int id)
        {
            try
            {
                var rooms = await _blockService.GetRoomsByBlockIdAsync(id);
                return Ok(ApiResponse<object>.Ok(rooms));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBlockDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var created = await _blockService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id },
                    ApiResponse<object>.Ok(created, "Tạo block thành công"));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateBlockDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _blockService.UpdateAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật block thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBlockStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _blockService.UpdateStatusAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật trạng thái block thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPatch("{id:int}/gender")]
        public async Task<IActionResult> UpdateGender(int id, [FromBody] UpdateBlockGenderDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _blockService.UpdateGenderAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Đổi giới tính block thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        }

    }
}
