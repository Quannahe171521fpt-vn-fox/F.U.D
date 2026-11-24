using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Payments;
using FUD.Services.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FUD.Controllers.Payments
{
    /// <summary>
    /// UC-Payment – Thanh toán của Sinh viên (VietQR).
    ///
    /// Luồng: Choose Paid Item -> tạo mã QR -> quét/thanh toán -> xác nhận (mock) -> biên lai.
    /// </summary>
    [Route("api/student/payments")]
    [ApiController]
    [Authorize(Roles = "Student")]
    public class StudentPaymentsController : ControllerBase
    {
        private readonly IStudentPaymentService _paymentService;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public StudentPaymentsController(
            IStudentPaymentService paymentService,
            IWebHostEnvironment env,
            IConfiguration configuration)
        {
            _paymentService = paymentService;
            _env = env;
            _configuration = configuration;
        }

        /// <summary>
        /// Danh sách hóa đơn đang nợ (Unpaid / Overdue / Processing) kèm chi tiết Invoice_Items.
        /// Dùng cho màn hình "Chọn khoản thanh toán".
        /// </summary>
        [HttpGet("unpaid-invoices")]
        public async Task<IActionResult> GetUnpaidInvoices()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _paymentService.GetPayableInvoicesAsync(studentId);
                return Ok(ApiResponse<StudentInvoiceListResponseDto>.Ok(result));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Tìm hóa đơn theo nguồn phát sinh (Booking/Keeping/Utility/BedTransfer/Checkout).
        /// Frontend dùng để auto-tick hóa đơn khi URL có ?sourceType=...&amp;sourceId=...
        /// </summary>
        [HttpGet("by-source")]
        public async Task<IActionResult> GetInvoiceBySource([FromQuery] string sourceType, [FromQuery] int sourceId)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            if (string.IsNullOrWhiteSpace(sourceType) || sourceId <= 0)
            {
                return BadRequest(ApiResponse.Fail("Cần cung cấp sourceType và sourceId hợp lệ."));
            }

            try
            {
                var result = await _paymentService.FindInvoiceBySourceAsync(studentId, sourceType, sourceId);
                if (result == null)
                {
                    return NotFound(ApiResponse.Fail(
                        "Không tìm thấy hóa đơn đang nợ tương ứng với nguồn đã chọn."));
                }

                return Ok(ApiResponse<StudentInvoiceDto>.Ok(result));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Tạo giao dịch VietQR cho 1 hoặc nhiều hóa đơn được chọn.
        /// Các hóa đơn sẽ bị khóa (Status = Processing) để chống thanh toán trùng.
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreateTransaction([FromBody] CreatePaymentTransactionDto dto)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));
            }

            try
            {
                var result = await _paymentService.CreateTransactionAsync(studentId, dto);
                return Created($"/api/student/payments/{result.Id}",
                    ApiResponse<PaymentTransactionDto>.Ok(result, "Tạo mã QR thanh toán thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Giả lập callback thành công từ cổng thanh toán: giao dịch -> Success,
        /// hóa đơn -> Paid và chạy side-effects (Booking/Transfer/Handover + thông báo).
        /// </summary>
        [HttpPost("{id:int}/mock-confirm")]
        public async Task<IActionResult> MockConfirm(int id, [FromBody] MockConfirmPaymentDto? dto)
        {
            var allowMock = _env.IsDevelopment()
                && _configuration.GetValue<bool>("Payments:EnableMockConfirm");
            if (!allowMock)
            {
                return NotFound(ApiResponse.Fail("Endpoint không khả dụng trong môi trường hiện tại."));
            }

            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _paymentService.MockConfirmAsync(studentId, id, dto);
                return Ok(ApiResponse<PaymentSuccessDto>.Ok(result, "Thanh toán thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Hủy giao dịch QR: giao dịch -> Cancelled và mở khóa các hóa đơn
        /// (về Overdue nếu quá hạn, ngược lại Unpaid).
        /// </summary>
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> CancelTransaction(int id, [FromBody] CancelPaymentDto? dto)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _paymentService.CancelTransactionAsync(studentId, id, dto);
                return Ok(ApiResponse<PaymentCancelResultDto>.Ok(result, "Đã hủy giao dịch thanh toán"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse.Fail(ex.Message));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Lịch sử các giao dịch và hóa đơn đã thanh toán của sinh viên.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _paymentService.GetPaymentHistoryAsync(studentId);
                return Ok(ApiResponse<List<PaymentHistoryItemDto>>.Ok(result));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Kiểm tra trạng thái giao dịch QR theo thời gian thực (dùng cho Frontend polling chờ SePay Webhook).
        /// </summary>
        [HttpGet("{id:int}/status")]
        public async Task<IActionResult> GetTransactionStatus(int id)
        {
            var studentId = GetCurrentStudentId();
            if (studentId == 0)
            {
                return Unauthorized(ApiResponse.Fail(
                    "Không xác định được danh tính sinh viên từ token đăng nhập. Vui lòng đăng nhập lại!"));
            }

            try
            {
                var result = await _paymentService.GetTransactionStatusAsync(studentId, id);
                return Ok(ApiResponse<PaymentTransactionStatusDto>.Ok(result));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau."));
            }
        }

        /// <summary>
        /// Endpoint nhận Webhook tự động từ SePay (hỗ trợ cả Test Mode "Mô phỏng giao dịch" và Live Mode).
        /// URL cấu hình trên SePay:
        /// - https://fud-ktx.vercel.app/api/student/payments/sepay-webhook
        /// - hoặc http://fudapi.somee.com/api/student/payments/sepay-webhook
        /// </summary>
        [AllowAnonymous]
        [EnableRateLimiting("SePayWebhookPolicy")]
        [HttpPost("sepay-webhook")]
        [HttpPost("/api/sepay/webhook")]
        public async Task<IActionResult> SePayWebhook([FromBody] SePayWebhookPayloadDto? payload)
        {
            if (payload == null)
            {
                return BadRequest(new { success = false, message = "Payload trống." });
            }

            try
            {
                var authHeader = Request.Headers.Authorization.FirstOrDefault();
                var (handled, message, receipt) = await _paymentService.ProcessSePayWebhookAsync(payload, authHeader);
                return Ok(new
                {
                    success = true,
                    handled,
                    message,
                    receiptNumber = receipt?.ReceiptNumber,
                    transactionId = receipt?.TransactionId
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "Đã xảy ra lỗi hệ thống khi xử lý webhook." });
            }
        }

        /// <summary>
        /// Lấy UserId của sinh viên đang đăng nhập từ token.
        /// </summary>
        private int GetCurrentStudentId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var studentId))
            {
                return 0;
            }

            return studentId;
        }
    }
}
