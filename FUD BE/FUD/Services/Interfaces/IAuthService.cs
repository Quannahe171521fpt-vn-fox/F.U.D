using System.Threading.Tasks;
using FUD.DTOs.Auth;

namespace FUD.Services.Interfaces;

public interface IAuthService
{
    Task<(AuthResponseDto Response, string RawRefreshToken)> LoginWithCredentialsAsync(LoginRequestDto dto, string clientIp);
    Task<(AuthResponseDto Response, string RawRefreshToken)> LoginWithGoogleAsync(GoogleLoginRequestDto dto, string clientIp);
    Task<(string AccessToken, string RawRefreshToken)> RotateRefreshTokenAsync(string rawRefreshToken, string clientIp);
    Task RevokeRefreshTokenAsync(string rawRefreshToken, string clientIp);
    Task<UserInfoDto> GetCurrentUserAsync(int userId);
}
