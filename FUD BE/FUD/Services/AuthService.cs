using FUD.Common;
using FUD.DTOs;
using FUD.Infrastructure.Auth;
using FUD.Infrastructure.Security;
using FUD.Models;
using FUD.Repositories.Interfaces.Accounts;
using FUD.Services.Interfaces;
using FUD.Settings.pattern;
using Microsoft.Extensions.Options;

namespace FUD.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly JwtSettings _jwtSettings;

        public AuthService(
            IUserRepository userRepo,
            IRefreshTokenRepository refreshTokenRepo,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator,
            IOptions<JwtSettings> jwtSettings)
        {
            _userRepo = userRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userRepo.GetByUsernameOrEmailAsync(request.UsernameOrEmail)
                ?? throw new Common.ValidationException("Tài khoản hoặc mật khẩu không đúng");

            if (string.IsNullOrEmpty(user.PasswordHash)
                || !_passwordHasher.Verify(request.Password, user.PasswordHash))
                throw new Common.ValidationException("Tài khoản hoặc mật khẩu không đúng");

            if (user.AccountStatus == AccountConstants.StatusSuspended)
                throw new Common.ForbiddenException("Tài khoản đã bị tạm ngưng. Vui lòng liên hệ quản trị viên.");

            if (user.AccountStatus != AccountConstants.StatusActive)
                throw new Common.ForbiddenException("Tài khoản chưa được kích hoạt.");

            return await IssueTokensAsync(user);
        }

        public async Task<AuthResultDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var hash = _tokenGenerator.HashToken(refreshToken);
            var stored = await _refreshTokenRepo.GetByHashAsync(hash);

            if (stored == null || stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
                throw new Common.ForbiddenException("Refresh token không hợp lệ hoặc đã hết hạn");

            var user = await _userRepo.GetByIdWithRolesAsync(stored.UserId)
                ?? throw new Common.NotFoundException("Tài khoản không tồn tại");

            if (user.AccountStatus != AccountConstants.StatusActive)
                throw new Common.ForbiddenException("Tài khoản không còn hoạt động.");

            // Thu hồi token cũ (rotate)
            await _refreshTokenRepo.RevokeAsync(hash, user.Id);

            return await IssueTokensAsync(user);
        }

        private async Task<AuthResultDto> IssueTokensAsync(User user)
        {
            var roles = user.UserRoleUsers?
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role.RoleName)
                .ToList() ?? new List<string>();

            var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(
                user.Id, user.Username, roles, user.TokenVersion);

            var refreshToken = _tokenGenerator.GenerateRefreshToken();

            await _refreshTokenRepo.CreateAsync(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = _tokenGenerator.HashToken(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays),
                CreatedAt = DateTime.UtcNow
            });

            return new AuthResultDto
            {
                UserId = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Status = user.AccountStatus,
                Roles = roles,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };
        }
    }
}
