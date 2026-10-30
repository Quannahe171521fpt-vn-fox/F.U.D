using FUD.Common;
using FUD.DTOs.Facilities.Equipment;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/facilities/equipment")]
    [ApiController]
    public class EquipmentController : ControllerBase
    {
        private readonly IEquipmentService _service;

        public EquipmentController(IEquipmentService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] EquipmentQueryDto query)
        {
            var result = await _service.GetAllAsync(query);
            return Ok(ApiResponse<PagedResult<EquipmentDto>>.Ok(result));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(ApiResponse.Fail($"Thiết bị {id} không tồn tại"));
            return Ok(ApiResponse<EquipmentDto>.Ok(item));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEquipmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ"));

            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<EquipmentDto>.Ok(created, "Tạo thiết bị thành công"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateEquipmentDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ"));

            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                return Ok(ApiResponse<EquipmentDto>.Ok(updated, "Cập nhật thiết bị thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPatch("{id:int}/move")]
        public async Task<IActionResult> Move(int id, [FromBody] MoveEquipmentDto dto)
        {
            try
            {
                var updated = await _service.MoveAsync(id, dto);
                return Ok(ApiResponse<EquipmentDto>.Ok(updated, "Di chuyển thiết bị thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPatch("{id:int}/condition")]
        public async Task<IActionResult> ChangeCondition(int id, [FromBody] ChangeConditionDto dto)
        {
            try
            {
                var updated = await _service.ChangeConditionAsync(id, dto);
                return Ok(ApiResponse<EquipmentDto>.Ok(updated, "Thay đổi tình trạng thiết bị thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPost("{id:int}/liquidate")]
        public async Task<IActionResult> Liquidate(int id)
        {
            try
            {
                await _service.LiquidateAsync(id);
                return Ok(ApiResponse.Ok("Thanh lý thiết bị thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (InvalidOperationException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        }

        [HttpGet("{id:int}/repair-history")]
        public async Task<IActionResult> GetRepairHistory(int id)
        {
            var history = await _service.GetRepairHistoryAsync(id);
            return Ok(ApiResponse<IEnumerable<TicketDto>>.Ok(history));
        }

        [HttpGet("catalogs")]
        public async Task<IActionResult> GetCatalogs()
        {
            var list = await _service.GetCatalogsAsync();
            return Ok(ApiResponse<IEnumerable<EquipmentCatalogDto>>.Ok(list));
        }

        [HttpPost("catalogs")]
        public async Task<IActionResult> CreateCatalog([FromBody] CreateEquipmentCatalogDto dto)
        {
            var created = await _service.CreateCatalogAsync(dto);
            return Ok(ApiResponse<EquipmentCatalogDto>.Ok(created, "Thêm danh mục thiết bị thành công"));
        }

        [HttpDelete("catalogs/{id:int}")]
        public async Task<IActionResult> DeleteCatalog(int id)
        {
            var ok = await _service.DeleteCatalogAsync(id);
            if (!ok) return NotFound(ApiResponse.Fail("Không tìm thấy danh mục thiết bị"));
            return Ok(ApiResponse.Ok("Xóa danh mục thiết bị thành công"));
        }

        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations()
        {
            var list = await _service.GetLocationsAsync();
            return Ok(ApiResponse<IEnumerable<LocationOptionDto>>.Ok(list));
        }
    }
}
