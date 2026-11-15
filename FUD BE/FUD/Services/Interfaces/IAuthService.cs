using System;
using System.Threading.Tasks;
using FUD.DTOs.Auth;

namespace FUD.Services.Interfaces;

public interface IAuthService
{
    Task<(AuthResponseDto Response, string RawRefreshToken, DateTime CookieExpiresAt)> LoginWithCredentialsAsync(LoginRequestDto dto, string clientIp);
    Task<(AuthResponseDto Response, string RawRefreshToken, DateTime CookieExpiresAt)> LoginWithGoogleAsync(GoogleLoginRequestDto dto, string clientIp);
    Task<(RefreshResponseDto Response, string? RawRefreshToken, DateTime? CookieExpiresAt)> RotateRefreshTokenAsync(string rawRefreshToken, string clientIp);
    Task RevokeRefreshTokenAsync(string rawRefreshToken, string clientIp);
    Task<UserInfoDto> GetCurrentUserAsync(int userId);
}
