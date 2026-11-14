using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Utilities;
using FUD.Services.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers.Utilities
{
    [Route("api/[controller]")]
    [ApiController]
    public class EwController : ControllerBase
    {
        private readonly IEwService _ewService;

        public EwController(IEwService ewService)
        {
            _ewService = ewService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
                return userId;

            return 3; // Default Guard/Manager trong seed data
        }

        // ═════════════════════════════════════════════════════════════
        // UC13: Usages View (Manager)
        // ═════════════════════════════════════════════════════════════
        [HttpGet("usages")]
        [Authorize(Roles = "Manager, Guard")]
        public async Task<IActionResult> GetUsages(
            [FromQuery] string? blockCode,
            [FromQuery] string? meterType,
            [FromQuery] int? month,
            [FromQuery] int? year,
            [FromQuery] int? termId,
            [FromQuery] int? billingPeriodId)
        {
            try
            {
                var result = await _ewService.GetUsagesAsync(blockCode, meterType, month, year, termId, billingPeriodId);
                return Ok(ApiResponse<IEnumerable<object>>.Ok(result));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail($"Lỗi khi tải dữ liệu sử dụng: {ex.Message}"));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // L1: Danh sách kỳ thanh toán (Lookup)
        // ═════════════════════════════════════════════════════════════
        [HttpGet("billing-periods")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> GetBillingPeriods()
        {
            var result = await _ewService.GetBillingPeriodsAsync();
            return Ok(ApiResponse<List<BillingPeriodLookupDto>>.Ok(result));
        }

        // ═════════════════════════════════════════════════════════════
        // L2: Tiến độ nhập của các block trong kỳ
        // ═════════════════════════════════════════════════════════════
        [HttpGet("billing-periods/{id:int}/blocks")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> GetBlockProgress([FromRoute] int id)
        {
            try
            {
                var result = await _ewService.GetBlockProgressAsync(id);
                return Ok(ApiResponse<BlockProgressResponseDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // M1: Lấy thông tin form nhập tay cho 1 Block (kèm OldReading)
        // Hỗ trợ cả GET query và POST body (cho client gửi payload json)
        // ═════════════════════════════════════════════════════════════
        [HttpGet("readings/block-form")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> GetBlockFormData(
            [FromQuery] string? blockCode,
            [FromQuery] int billingPeriodId)
        {
            if (string.IsNullOrWhiteSpace(blockCode))
                return BadRequest(ApiResponse.Fail("Vui lòng cung cấp mã block/phòng"));

            try
            {
                var result = await _ewService.GetBlockFormDataAsync(blockCode, billingPeriodId);
                return Ok(ApiResponse<BlockFormDataResponseDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        [HttpPost("readings/block-form")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> GetBlockFormDataPost([FromBody] BlockFormDataQueryDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.BlockCode))
                return BadRequest(ApiResponse.Fail("Vui lòng cung cấp mã block/phòng"));

            try
            {
                var result = await _ewService.GetBlockFormDataAsync(request.BlockCode, request.BillingPeriodId);
                return Ok(ApiResponse<BlockFormDataResponseDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // M2: Preview chỉ số nhập tay trước khi lưu
        // ═════════════════════════════════════════════════════════════
        [HttpPost("readings/manual/preview")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> PreviewManualReading([FromBody] ManualPreviewRequestDto request)
        {
            try
            {
                var result = await _ewService.PreviewManualReadingAsync(request);
                return Ok(ApiResponse<ManualPreviewResponseDto>.Ok(result));
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

        // ═════════════════════════════════════════════════════════════
        // M3: Lưu xác nhận chỉ số nhập tay
        // ═════════════════════════════════════════════════════════════
        [HttpPost("readings/manual/confirm")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> ConfirmManualReading([FromBody] ManualConfirmRequestDto request)
        {
            var guardUserId = GetCurrentUserId();

            try
            {
                var result = await _ewService.ConfirmManualReadingAsync(guardUserId, request);
                var message = result.ManagerAlerted
                    ? $"Đã lưu {result.Saved} chỉ số ({result.Flagged} chỉ số bất thường đã gửi cảnh báo)"
                    : $"Đã ghi nhận thành công {result.Saved} chỉ số";

                return Ok(ApiResponse<ManualConfirmResponseDto>.Ok(result, message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // M4: Sửa 1 chỉ số đã ghi nhận
        // ═════════════════════════════════════════════════════════════
        [HttpPut("readings/{id:int}")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> UpdateReading(
            [FromRoute] int id,
            [FromBody] UpdateReadingRequestDto request)
        {
            try
            {
                await _ewService.UpdateReadingAsync(id, request.CurrentValue, request.Note);
                return Ok(ApiResponse.Ok("Đã cập nhật chỉ số thành công"));
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

        // ═════════════════════════════════════════════════════════════
        // M5: Xóa chỉ số
        // ═════════════════════════════════════════════════════════════
        [HttpDelete("readings/{id:int}")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> DeleteReading([FromRoute] int id)
        {
            try
            {
                await _ewService.DeleteReadingAsync(id);
                return Ok(ApiResponse.Ok("Đã xóa chỉ số thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // I1: Preview Import từ file Excel
        // ═════════════════════════════════════════════════════════════
        [HttpPost("import/preview")]
        [Authorize(Roles = "Guard,Manager")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PreviewExcelImport(
            IFormFile file,
            [FromForm] int billingPeriodId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Vui lòng chọn file Excel"));

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file định dạng .xlsx"));

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _ewService.PreviewExcelImportAsync(stream, billingPeriodId);
                return Ok(ApiResponse<ExcelPreviewResponseDto>.Ok(result));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // I1-DIRECT: Upload file Excel và LƯU TRỰC TIẾP VÀO DB (1 bước)
        // Rất tiện cho test Postman hoặc Guard import trực tiếp file
        // ═════════════════════════════════════════════════════════════
        [HttpPost("import/upload")]
        [Authorize(Roles = "Guard,Manager")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndSaveExcel(
            IFormFile file,
            [FromForm] int? billingPeriodId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Vui lòng chọn file Excel"));

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file định dạng .xlsx"));

            var guardUserId = GetCurrentUserId();

            try
            {
                // 1. Xác định kỳ thanh toán
                int targetPeriodId;
                if (billingPeriodId.HasValue && billingPeriodId.Value > 0)
                {
                    targetPeriodId = billingPeriodId.Value;
                }
                else
                {
                    var periods = await _ewService.GetBillingPeriodsAsync();
                    var activePeriod = periods.FirstOrDefault(p => p.Status == "Open") ?? periods.FirstOrDefault();
                    if (activePeriod == null)
                        return BadRequest(ApiResponse.Fail("Không tìm thấy kỳ thanh toán nào"));
                    targetPeriodId = activePeriod.Id;
                }

                // 2. Đọc file & Preview dữ liệu
                ExcelPreviewResponseDto preview;
                using (var stream = file.OpenReadStream())
                {
                    preview = await _ewService.PreviewExcelImportAsync(stream, targetPeriodId);
                }

                // 3. Tự động xác nhận lưu toàn bộ dữ liệu hợp lệ vào DB
                var confirmReq = new ExcelConfirmImportRequestDto
                {
                    BillingPeriodId = targetPeriodId,
                    FileName = file.FileName,
                    ImportOnlyValid = true,
                    OverwriteExisting = true,
                    Rows = preview.Rows
                };

                var result = await _ewService.ConfirmExcelImportAsync(guardUserId, confirmReq);
                return Ok(ApiResponse<ExcelConfirmImportResponseDto>.Ok(result, $"Đã import và lưu thành công {result.SavedReadings} chỉ số vào database!"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // I2: Xác nhận lưu kết quả Import Excel (Bước 2 của quy trình Preview -> Confirm)
        // ═════════════════════════════════════════════════════════════
        [HttpPost("import/confirm")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> ConfirmExcelImport([FromBody] ExcelConfirmImportRequestDto request)
        {
            var guardUserId = GetCurrentUserId();

            try
            {
                var result = await _ewService.ConfirmExcelImportAsync(guardUserId, request);
                return Ok(ApiResponse<ExcelConfirmImportResponseDto>.Ok(result, result.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
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

        // ═════════════════════════════════════════════════════════════
        // I3: Lịch sử các đợt Import
        // ═════════════════════════════════════════════════════════════
        [HttpGet("import/batches")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> GetImportBatches([FromQuery] int? billingPeriodId)
        {
            var result = await _ewService.GetImportBatchesAsync(billingPeriodId);
            return Ok(ApiResponse<List<ImportBatchItemDto>>.Ok(result));
        }

        // ═════════════════════════════════════════════════════════════
        // I4: Tải file template Excel mẫu (1 nút bấm - Đầy đủ tất cả Block & Cả Điện + Nước)
        // ═════════════════════════════════════════════════════════════
        [HttpGet("import/template")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadTemplate()
        {
            var bytes = await _ewService.GenerateExcelTemplateAsync(null, null, null);
            var fileName = "Ew_Template.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }

    public class UpdateReadingRequestDto
    {
        public decimal CurrentValue { get; set; }
        public string? Note { get; set; }
    }
}
