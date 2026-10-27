using FUD.Common;
using FUD.DTOs.Facilities.Room;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/rooms")]
    [ApiController]
    public class RoomsController : ControllerBase
    {
        private readonly IRoomService _roomService;

        public RoomsController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] RoomQueryDto query)
        {
            var result = await _roomService.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var room = await _roomService.GetByIdAsync(id);
            if (room == null)
                return NotFound(ApiResponse.Fail($"Phòng {id} không tồn tại"));
            return Ok(ApiResponse<object>.Ok(room));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoomDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var created = await _roomService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id },
                    ApiResponse<object>.Ok(created, "Tạo phòng thành công"));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateRoomDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _roomService.UpdateAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật phòng thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return Conflict(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPatch("{id:int}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateRoomStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            try
            {
                var updated = await _roomService.UpdateStatusAsync(id, dto);
                return Ok(ApiResponse<object>.Ok(updated, "Cập nhật trạng thái phòng thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

    }
}
