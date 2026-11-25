using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Facilities.Room;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/rooms")]
    [ApiController]
    [Authorize(Roles = "Manager,Guard")]
    public class RoomsController : ControllerBase
    {
        private readonly IRoomService _roomService;

        public RoomsController(IRoomService roomService)
        {
            _roomService = roomService;
        }

        private int? GetCurrentUserId()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("nameid");

            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        private bool IsGuardOnly()
        {
            return User.IsInRole("Guard") && !User.IsInRole("Manager");
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] RoomQueryDto query)
        {
            if (IsGuardOnly())
            {
                var guardUserId = GetCurrentUserId();
                if (!guardUserId.HasValue)
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));

                try
                {
                    var guardResult = await _roomService.GetAllForGuardAsync(query, guardUserId.Value);
                    return Ok(ApiResponse<object>.Ok(guardResult));
                }
                catch (UnauthorizedAccessException ex)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
                }
            }

            var result = await _roomService.GetAllAsync(query);
            return Ok(ApiResponse<object>.Ok(result));
        }

        [HttpGet("guard-blocks")]
        [HttpGet("blocks-lookup")]
        public async Task<IActionResult> GetAssignedBlocks()
        {
            try
            {
                int? guardUserId = IsGuardOnly() ? GetCurrentUserId() : null;
                if (IsGuardOnly() && !guardUserId.HasValue)
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));

                var blocks = await _roomService.GetAssignedBlocksLookupAsync(guardUserId);
                return Ok(ApiResponse<object>.Ok(blocks));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
        }

        [HttpGet("{id:int}/detail")]
        public async Task<IActionResult> GetDetail(int id)
        {
            try
            {
                int? guardUserId = IsGuardOnly() ? GetCurrentUserId() : null;
                if (IsGuardOnly() && !guardUserId.HasValue)
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));

                var detail = await _roomService.GetDetailByIdAsync(id, guardUserId);
                if (detail == null)
                    return NotFound(ApiResponse.Fail($"Phòng {id} không tồn tại"));

                return Ok(ApiResponse<object>.Ok(detail));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (IsGuardOnly())
            {
                var guardUserId = GetCurrentUserId();
                if (!guardUserId.HasValue)
                    return Unauthorized(ApiResponse.Fail("Không xác định được danh tính cán bộ bảo vệ từ Token."));

                try
                {
                    var guardRoom = await _roomService.GetByIdForGuardAsync(id, guardUserId.Value);
                    if (guardRoom == null)
                        return NotFound(ApiResponse.Fail($"Phòng {id} không tồn tại"));
                    return Ok(ApiResponse<object>.Ok(guardRoom));
                }
                catch (UnauthorizedAccessException ex)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
                }
            }

            var room = await _roomService.GetByIdAsync(id);
            if (room == null)
                return NotFound(ApiResponse.Fail($"Phòng {id} không tồn tại"));
            return Ok(ApiResponse<object>.Ok(room));
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
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
        [Authorize(Roles = "Manager")]
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
        [Authorize(Roles = "Manager")]
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

        [HttpGet("types")]
        public async Task<IActionResult> GetRoomTypes()
        {
            var types = await _roomService.GetRoomTypesAsync();
            return Ok(ApiResponse<object>.Ok(types));
        }

    }
}
