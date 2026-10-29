using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Auth;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FUD.Controllers;

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
    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.LoginWithCredentialsAsync(dto, clientIp);
            SetRefreshTokenCookie(result.RawRefreshToken);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result.Response, "Đăng nhập thành công"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("google-login")]
    [AllowAnonymous]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto dto)
    {
        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.LoginWithGoogleAsync(dto, clientIp);
            SetRefreshTokenCookie(result.RawRefreshToken);
            return Ok(ApiResponse<AuthResponseDto>.Ok(result.Response, "Đăng nhập Google thành công"));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("RefreshPolicy")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("fud_refresh_token", out var rawRefreshToken) || string.IsNullOrEmpty(rawRefreshToken))
            return Unauthorized(ApiResponse.Fail("Không tìm thấy refresh token trong cookie."));

        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var result = await _authService.RotateRefreshTokenAsync(rawRefreshToken, clientIp);
            SetRefreshTokenCookie(result.RawRefreshToken);
            return Ok(ApiResponse<object>.Ok(new { accessToken = result.AccessToken }));
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.Cookies.Delete("fud_refresh_token", GetCookieOptions());
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (Request.Cookies.TryGetValue("fud_refresh_token", out var rawRefreshToken) && !string.IsNullOrEmpty(rawRefreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(rawRefreshToken, clientIp);
        }

        Response.Cookies.Delete("fud_refresh_token", GetCookieOptions());
        return Ok(ApiResponse.Ok("Đăng xuất thành công"));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(ApiResponse.Fail("Token không hợp lệ"));

        var userInfo = await _authService.GetCurrentUserAsync(userId);
        return Ok(ApiResponse<UserInfoDto>.Ok(userInfo));
    }

    private void SetRefreshTokenCookie(string rawRefreshToken)
    {
        Response.Cookies.Append("fud_refresh_token", rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/api/auth"
        });
    }

    private CookieOptions GetCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
        SameSite = SameSiteMode.Lax,
        Path = "/api/auth"
    };
}
