using System.Threading.Tasks;
using FUD.DTOs.Auth;

namespace FUD.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null);
        Task<AuthResponseDto> LoginGoogleAsync(GoogleLoginRequestDto request, string? ipAddress = null, string? userAgent = null);
        Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);
        Task<UserDto> GetCurrentUserProfileAsync(int userId);
    }
}
