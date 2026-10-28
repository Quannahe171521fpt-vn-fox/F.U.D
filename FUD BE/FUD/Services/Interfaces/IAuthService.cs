using FUD.DTOs;
using FUD.Services.Interfaces;

namespace FUD.Services.Interfaces
{
    public interface IAuthService
    {
        /// <summary>Đăng nhập bằng username/email + mật khẩu. Tài khoản không Active sẽ bị từ chối.</summary>
        Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

        /// <summary>Đổi access token bằng refresh token.</summary>
        Task<AuthResultDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    }
}
