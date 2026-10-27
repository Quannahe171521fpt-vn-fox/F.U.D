using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common.Extensions;
using FUD.DTOs.Auth;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Đăng nhập bằng Email/Tài khoản và Mật khẩu
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu đăng nhập không hợp lệ", errors = ModelState });
            }

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();

                var result = await _authService.LoginAsync(request, ipAddress, userAgent);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống trong quá trình đăng nhập.", error = ex.Message });
            }
        }

        /// <summary>
        /// Đăng nhập bằng tài khoản Google (OAuth 2.0 / IdToken)
        /// </summary>
        [HttpPost("google")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginGoogle([FromBody] GoogleLoginRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Google Token không được để trống" });
            }

            try
            {
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers["User-Agent"].ToString();

                var result = await _authService.LoginGoogleAsync(request, ipAddress, userAgent);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống trong quá trình xác thực Google.", error = ex.Message });
            }
        }

        /// <summary>
        /// Làm mới Access Token thông qua Refresh Token
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu Refresh Token không hợp lệ" });
            }

            try
            {
                var result = await _authService.RefreshTokenAsync(request);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi hệ thống khi làm mới phiên đăng nhập.", error = ex.Message });
            }
        }

        /// <summary>
        /// Lấy thông tin người dùng hiện tại (kèm vai trò và giới tính để FE tự động lọc)
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.GetUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Phiên làm việc không hợp lệ hoặc đã hết hạn." });
            }

            try
            {
                var profile = await _authService.GetCurrentUserProfileAsync(userId.Value);
                return Ok(new { success = true, user = profile });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Lỗi khi lấy thông tin người dùng.", error = ex.Message });
            }
        }
    }
}
