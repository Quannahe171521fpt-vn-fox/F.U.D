using System.Security.Claims;
using FUD.Common;
using FUD.DTOs.Payments;
using FUD.Services.Interfaces.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public StudentPaymentsController(IStudentPaymentService paymentService)
        {
            _paymentService = paymentService;
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
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi tải danh sách hóa đơn: {ex.Message}"));
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
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi tra cứu hóa đơn: {ex.Message}"));
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
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi tạo giao dịch thanh toán: {ex.Message}"));
            }
        }

        /// <summary>
        /// Giả lập callback thành công từ cổng thanh toán: giao dịch -> Success,
        /// hóa đơn -> Paid và chạy side-effects (Booking/Transfer/Handover + thông báo).
        /// </summary>
        [HttpPost("{id:int}/mock-confirm")]
        public async Task<IActionResult> MockConfirm(int id, [FromBody] MockConfirmPaymentDto? dto)
        {
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
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi xác nhận thanh toán: {ex.Message}"));
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
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail($"Đã xảy ra lỗi khi hủy giao dịch: {ex.Message}"));
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
