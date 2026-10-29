using FUD.Common;
using FUD.DTOs.Facilities.News;
using FUD.Services.Interfaces.Facilities;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Facilities
{
    [Route("api/facilities/news")]
    [ApiController]
    public class NewsController : ControllerBase
    {
        private readonly INewsService _service;

        public NewsController(INewsService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] NewsQueryDto query)
        {
            var result = await _service.GetAllAsync(query);
            return Ok(ApiResponse<PagedResult<NewsDto>>.Ok(result));
        }

        [HttpGet("published")]
        public async Task<IActionResult> GetPublished()
        {
            var list = await _service.GetPublishedAsync();
            return Ok(ApiResponse<IEnumerable<NewsDto>>.Ok(list));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null) return NotFound(ApiResponse.Fail($"Bản tin {id} không tồn tại"));
            return Ok(ApiResponse<NewsDto>.Ok(item));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNewsDto dto)
        {
            try
            {
                var created = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<NewsDto>.Ok(created, "Tạo bản tin thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateNewsDto dto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto);
                return Ok(ApiResponse<NewsDto>.Ok(updated, "Cập nhật bản tin thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
            catch (ArgumentException ex) { return BadRequest(ApiResponse.Fail(ex.Message)); }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // Quy tắc nghiệp vụ: Bản tin không được xóa cứng (hard delete), chỉ có chức năng ẩn (chuyển sang nháp / gỡ xuất bản)
                await _service.UnpublishAsync(id);
                return Ok(ApiResponse.Ok("Đã ẩn bản tin thành công (chuyển về bản nháp, không xóa vĩnh viễn)"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPost("{id:int}/publish")]
        public async Task<IActionResult> Publish(int id)
        {
            try
            {
                await _service.PublishAsync(id);
                return Ok(ApiResponse.Ok("Xuất bản tin tức thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }

        [HttpPost("{id:int}/unpublish")]
        public async Task<IActionResult> Unpublish(int id)
        {
            try
            {
                await _service.UnpublishAsync(id);
                return Ok(ApiResponse.Ok("Gỡ xuất bản tin tức thành công"));
            }
            catch (KeyNotFoundException ex) { return NotFound(ApiResponse.Fail(ex.Message)); }
        }
    }
}

