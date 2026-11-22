using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Utilities;
using FUD.Services.Interfaces.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }

        private int GetRequiredUserId()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                throw new UnauthorizedAccessException("Không xác định được danh tính người dùng. Vui lòng đăng nhập lại.");
            return userId.Value;
        }

        private bool IsManager()
        {
            return User.IsInRole("Manager") || User.IsInRole("Admin");
        }

        // ═════════════════════════════════════════════════════════════
        // UC13: Usages View (Manager, Guard)
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
                var userId = GetCurrentUserId();
                var isManager = IsManager();
                var result = await _ewService.GetUsagesAsync(blockCode, meterType, month, year, termId, billingPeriodId, dormId: null, userId: userId, isManager: isManager);
                return Ok(ApiResponse<IEnumerable<object>>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
                var userId = GetCurrentUserId();
                var isManager = IsManager();
                var result = await _ewService.GetBlockProgressAsync(id, userId, isManager);
                return Ok(ApiResponse<BlockProgressResponseDto>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // M1: Lấy thông tin form nhập tay cho 1 Block (kèm OldReading)
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
                var userId = GetCurrentUserId();
                var isManager = IsManager();
                var result = await _ewService.GetBlockFormDataAsync(blockCode, billingPeriodId, userId, isManager);
                return Ok(ApiResponse<BlockFormDataResponseDto>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
                var userId = GetCurrentUserId();
                var isManager = IsManager();
                var result = await _ewService.GetBlockFormDataAsync(request.BlockCode, request.BillingPeriodId, userId, isManager);
                return Ok(ApiResponse<BlockFormDataResponseDto>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
                var userId = GetCurrentUserId();
                var isManager = IsManager();
                var result = await _ewService.PreviewManualReadingAsync(request, userId, isManager);
                return Ok(ApiResponse<ManualPreviewResponseDto>.Ok(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
        // OCR: Chụp/Tải ảnh công tơ điện nước & tự nhận diện bằng AI
        // ═════════════════════════════════════════════════════════════
        [HttpPost("readings/scan-photo")]
        [Authorize(Roles = "Guard,Manager")]
        [EnableRateLimiting("MeterOcrPolicy")]
        [RequestSizeLimit(12_000_000)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ScanMeterPhoto(
            IFormFile photo,
            [FromForm] string? expectedMeterType,
            [FromForm] decimal? previousValue,
            [FromForm] bool saveEvidence = true,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _ewService.ScanMeterPhotoAsync(
                    photo,
                    expectedMeterType,
                    previousValue,
                    saveEvidence,
                    cancellationToken);
                return Ok(ApiResponse<MeterPhotoScanResponseDto>.Ok(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (TimeoutException ex)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout, ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                var statusCode = ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status429TooManyRequests
                    : StatusCodes.Status503ServiceUnavailable;
                return StatusCode(statusCode, ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // M3: Lưu xác nhận chỉ số nhập tay
        // ═════════════════════════════════════════════════════════════
        [HttpPost("readings/manual/confirm")]
        [Authorize(Roles = "Guard,Manager")]
        public async Task<IActionResult> ConfirmManualReading([FromBody] ManualConfirmRequestDto request)
        {
            try
            {
                var guardUserId = GetRequiredUserId();
                var isManager = IsManager();
                var result = await _ewService.ConfirmManualReadingAsync(guardUserId, request, isManager);
                var message = result.ManagerAlerted
                    ? $"Đã lưu {result.Saved} chỉ số ({result.Flagged} chỉ số bất thường đã gửi cảnh báo)"
                    : $"Đã ghi nhận thành công {result.Saved} chỉ số";

                return Ok(ApiResponse<ManualConfirmResponseDto>.Ok(result, message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
                var userId = GetRequiredUserId();
                var isManager = IsManager();
                await _ewService.UpdateReadingAsync(id, request.CurrentValue, request.Note, userId, isManager);
                return Ok(ApiResponse.Ok("Đã cập nhật chỉ số thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
                var userId = GetRequiredUserId();
                var isManager = IsManager();
                await _ewService.DeleteReadingAsync(id, userId, isManager);
                return Ok(ApiResponse.Ok("Đã xóa chỉ số thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
        }

        private const long MaxExcelBytes = 5 * 1024 * 1024;

        private static async Task<(MemoryStream? Stream, string? Error)> TryReadAndValidateXlsxStreamAsync(IFormFile file)
        {
            if (file.Length > MaxExcelBytes)
            {
                return (null, "File Excel không được vượt quá 5MB");
            }

            using var source = file.OpenReadStream();
            var ms = new MemoryStream();
            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                totalRead += read;
                if (totalRead > MaxExcelBytes)
                {
                    ms.Dispose();
                    return (null, "File Excel không được vượt quá 5MB");
                }
                ms.Write(buffer, 0, read);
            }

            if (totalRead < 4)
            {
                ms.Dispose();
                return (null, "File tải lên không phải định dạng Excel (.xlsx) hợp lệ");
            }

            var bytes = ms.GetBuffer();
            if (bytes[0] != 0x50 || bytes[1] != 0x4B || bytes[2] != 0x03 || bytes[3] != 0x04)
            {
                ms.Dispose();
                return (null, "File tải lên không phải định dạng Excel (.xlsx) hợp lệ");
            }

            ms.Position = 0;
            return (ms, null);
        }

        // ═════════════════════════════════════════════════════════════
        // I1: Preview Import từ file Excel
        // ═════════════════════════════════════════════════════════════
        [HttpPost("import/preview")]
        [Authorize(Roles = "Guard,Manager")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PreviewExcelImport(
            IFormFile file,
            [FromForm] int billingPeriodId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Vui lòng chọn file Excel"));

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file định dạng .xlsx"));

            var (validatedStream, validationError) = await TryReadAndValidateXlsxStreamAsync(file);
            if (validatedStream == null)
                return BadRequest(ApiResponse.Fail(validationError!));

            try
            {
                using (validatedStream)
                {
                    var userId = GetCurrentUserId();
                    var isManager = IsManager();
                    var result = await _ewService.PreviewExcelImportAsync(validatedStream, billingPeriodId, userId, isManager);
                    return Ok(ApiResponse<ExcelPreviewResponseDto>.Ok(result));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // I1-DIRECT: Upload file Excel và LƯU TRỰC TIẾP VÀO DB (1 bước)
        // ═════════════════════════════════════════════════════════════
        [HttpPost("import/upload")]
        [Authorize(Roles = "Guard,Manager")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAndSaveExcel(
            IFormFile file,
            [FromForm] int? billingPeriodId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse.Fail("Vui lòng chọn file Excel"));

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse.Fail("Chỉ chấp nhận file định dạng .xlsx"));

            var (validatedStream, validationError) = await TryReadAndValidateXlsxStreamAsync(file);
            if (validatedStream == null)
                return BadRequest(ApiResponse.Fail(validationError!));

            try
            {
                var guardUserId = GetRequiredUserId();
                var isManager = IsManager();

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
                    {
                        validatedStream.Dispose();
                        return BadRequest(ApiResponse.Fail("Không tìm thấy kỳ thanh toán nào"));
                    }
                    targetPeriodId = activePeriod.Id;
                }

                ExcelPreviewResponseDto preview;
                using (validatedStream)
                {
                    preview = await _ewService.PreviewExcelImportAsync(validatedStream, targetPeriodId, guardUserId, isManager);
                }

                var confirmReq = new ExcelConfirmImportRequestDto
                {
                    BillingPeriodId = targetPeriodId,
                    FileName = file.FileName,
                    ImportOnlyValid = true,
                    OverwriteExisting = true,
                    Rows = preview.Rows
                };

                var result = await _ewService.ConfirmExcelImportAsync(guardUserId, confirmReq, isManager);
                return Ok(ApiResponse<ExcelConfirmImportResponseDto>.Ok(result, $"Đã import và lưu thành công {result.SavedReadings} chỉ số vào database!"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
            try
            {
                var guardUserId = GetRequiredUserId();
                var isManager = IsManager();
                var result = await _ewService.ConfirmExcelImportAsync(guardUserId, request, isManager);
                return Ok(ApiResponse<ExcelConfirmImportResponseDto>.Ok(result, result.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
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
            var userId = GetCurrentUserId();
            var isManager = IsManager();
            var result = await _ewService.GetImportBatchesAsync(billingPeriodId, userId, isManager);
            return Ok(ApiResponse<List<ImportBatchItemDto>>.Ok(result));
        }

        // ═════════════════════════════════════════════════════════════
        // I4: Tải file template Excel mẫu (Guard chỉ lấy phòng thuộc Dorm mình quản lý, Manager lấy tất cả)
        // ═════════════════════════════════════════════════════════════
        [HttpGet("import/template")]
        [Authorize(Roles = "Guard,Manager,Admin")]
        public async Task<IActionResult> DownloadTemplate(
            [FromQuery] int? billingPeriodId,
            [FromQuery] string? dormCode,
            [FromQuery] string? meterType)
        {
            var userId = GetCurrentUserId();
            var isManager = IsManager();
            var bytes = await _ewService.GenerateExcelTemplateAsync(billingPeriodId, dormCode, meterType, userId, isManager);
            var fileName = "Ew_Template.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ═════════════════════════════════════════════════════════════
        // A1: Danh sách cảnh báo chỉ số bất thường (Manager)
        // ═════════════════════════════════════════════════════════════
        [HttpGet("anomalies")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAnomalies(
            [FromQuery] int? billingPeriodId,
            [FromQuery] string? status)
        {
            try
            {
                var result = await _ewService.GetAnomaliesAsync(billingPeriodId, status);
                return Ok(ApiResponse<List<UsageAnomalyItemDto>>.Ok(result));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail($"Lỗi khi tải danh sách cảnh báo bất thường: {ex.Message}"));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // A2: Manager xác nhận (Confirm) hoặc điều chỉnh (Adjust) chỉ số bất thường
        // ═════════════════════════════════════════════════════════════
        [HttpPost("anomalies/{id:int}/resolve")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> ResolveAnomaly(
            [FromRoute] int id,
            [FromBody] ResolveAnomalyRequestDto request)
        {
            try
            {
                var managerUserId = GetRequiredUserId();
                var result = await _ewService.ResolveAnomalyAsync(id, request, managerUserId);
                return Ok(ApiResponse<ResolveAnomalyResponseDto>.Ok(result, result.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail($"Lỗi khi xử lý cảnh báo bất thường: {ex.Message}"));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // B1: Tính phụ trội, chia đều chi phí & gửi thông báo công nợ theo kỳ
        // ═════════════════════════════════════════════════════════════
        [HttpPost("billing-periods/{id:int}/calculate")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> CalculatePeriodSurcharges([FromRoute] int id)
        {
            try
            {
                var result = await _ewService.CalculatePeriodSurchargesAsync(id);
                return Ok(ApiResponse<BillingCalculationResultDto>.Ok(result, result.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail($"Lỗi khi tính phụ trội điện nước: {ex.Message}"));
            }
        }

        // ═════════════════════════════════════════════════════════════
        // UC-Student: Xem chỉ số điện nước & phí phụ trội theo kỳ
        // ═════════════════════════════════════════════════════════════
        [HttpGet("my-terms")]
        [Authorize]
        public async Task<IActionResult> GetMyTerms()
        {
            try
            {
                var studentUserId = GetRequiredUserId();
                var terms = await _ewService.GetStudentTermsAsync(studentUserId);
                return Ok(ApiResponse<List<StudentTermOptionDto>>.Ok(terms));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail($"Lỗi khi tải danh sách học kỳ: {ex.Message}"));
            }
        }

        [HttpGet("my-usage")]
        [Authorize]
        public async Task<IActionResult> GetMyUsage([FromQuery] int? termId)
        {
            try
            {
                var studentUserId = GetRequiredUserId();
                var report = await _ewService.GetStudentEwReportAsync(studentUserId, termId);
                if (report == null)
                {
                    return Ok(ApiResponse<StudentEwReportDto?>.Ok(null, "Sinh viên chưa có hồ sơ lưu trú hoặc chưa check-in trong kỳ này."));
                }
                return Ok(ApiResponse<StudentEwReportDto>.Ok(report));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.Fail($"Lỗi khi tải báo cáo điện nước cá nhân: {ex.Message}"));
            }
        }
    }

    public class UpdateReadingRequestDto
    {
        public decimal CurrentValue { get; set; }
        public string? Note { get; set; }
    }
}
