using FUD.Common;
using FUD.DTOs;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FUD.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var result = await _authService.LoginAsync(dto, cancellationToken);
            return Ok(ApiResponse<object>.Ok(result, "Đăng nhập thành công"));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse.Fail("Dữ liệu không hợp lệ",
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()));

            var result = await _authService.RefreshAsync(dto.RefreshToken, cancellationToken);
            return Ok(ApiResponse<object>.Ok(result, "Làm mới token thành công"));
        }
    }
}
