using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Auth;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;
using Google.Apis.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FUD.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _jwtGen;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshRepo,
        IPasswordHasher hasher,
        IJwtTokenGenerator jwtGen,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<AuthService> logger)
    {
        _userRepo = userRepo;
        _refreshRepo = refreshRepo;
        _hasher = hasher;
        _jwtGen = jwtGen;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public async Task<(AuthResponseDto Response, string RawRefreshToken)> LoginWithCredentialsAsync(LoginRequestDto dto, string clientIp)
    {
        var user = await _userRepo.GetByIdentifierAsync(dto.Identifier);

        // 1. Chống User Enumeration & Timing Attack
        if (user == null)
        {
            _hasher.VerifyDummy();
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // 2. Xác minh mật khẩu
        if (string.IsNullOrEmpty(user.PasswordHash) || !_hasher.Verify(dto.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        // 3. Kiểm tra trạng thái Active
        if (user.AccountStatus != "Active")
        {
            throw new UnauthorizedAccessException("Tài khoản của bạn chưa được kích hoạt hoặc đã bị khóa.");
        }

        return await GenerateAuthResultAsync(user, clientIp);
    }

    public async Task<(AuthResponseDto Response, string RawRefreshToken)> LoginWithGoogleAsync(GoogleLoginRequestDto dto, string clientIp)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google token validation failed");
            throw new UnauthorizedAccessException("Xác thực Google không hợp lệ hoặc đã hết hạn.");
        }

        if (!payload.EmailVerified)
            throw new UnauthorizedAccessException("Email Google chưa được xác thực.");

        // Kiểm tra domain FPT Education
        var email = payload.Email.ToLowerInvariant();
        if (!email.EndsWith("@fpt.edu.vn") && !email.EndsWith("@fe.edu.vn"))
        {
            throw new UnauthorizedAccessException("Hệ thống chỉ chấp nhận tài khoản Google thuộc tổ chức FPT Education.");
        }

        // Tìm User theo GoogleId hoặc Email
        var user = await _userRepo.GetByGoogleIdAsync(payload.Subject) 
                   ?? await _userRepo.GetByEmailAsync(email);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Tài khoản chưa được kích hoạt trong hệ thống KTX. Vui lòng liên hệ Ban Quản lý.");
        }

        if (user.AccountStatus != "Active")
        {
            throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa.");
        }

        // Liên kết GoogleId nếu trước đó chỉ có Email
        if (string.IsNullOrEmpty(user.GoogleId))
        {
            await _userRepo.UpdateGoogleIdAsync(user.Id, payload.Subject);
            user.GoogleId = payload.Subject;
        }

        return await GenerateAuthResultAsync(user, clientIp);
    }

    public async Task<(string AccessToken, string RawRefreshToken)> RotateRefreshTokenAsync(string rawRefreshToken, string clientIp)
    {
        var tokenHash = _jwtGen.HashToken(rawRefreshToken);
        var existingToken = await _refreshRepo.GetByTokenHashAsync(tokenHash);

        if (existingToken == null)
            throw new UnauthorizedAccessException("Phiên truy cập không tồn tại hoặc không hợp lệ.");

        // Xử lý Token đã bị thu hồi
        if (existingToken.RevokedAt.HasValue)
        {
            // Grace Period: 30 giây kể từ khi token bị rotate
            var elapsedSinceRevoke = DateTime.UtcNow - existingToken.RevokedAt.Value;
            if (elapsedSinceRevoke <= TimeSpan.FromSeconds(30) && !string.IsNullOrEmpty(existingToken.ReplacedByTokenHash))
            {
                var replacedToken = await _refreshRepo.GetByTokenHashAsync(existingToken.ReplacedByTokenHash);
                if (replacedToken != null && replacedToken.User.AccountStatus == "Active")
                {
                    var roles = replacedToken.User.UserRoleUsers.Select(ur => ur.Role.RoleName);
                    var newAccToken = _jwtGen.GenerateAccessToken(replacedToken.User, roles);
                    return (newAccToken, rawRefreshToken);
                }
            }

            // Quá Grace Period -> Phát hiện Reuse Attack -> Thu hồi toàn bộ chuỗi token!
            await _refreshRepo.RevokeAllUserTokensAsync(existingToken.UserId, clientIp);
            _logger.LogWarning("Token Reuse Attack detected for UserId: {UserId} from IP: {Ip}", existingToken.UserId, clientIp);
            throw new UnauthorizedAccessException("Phát hiện phiên truy cập bất thường. Tất cả các phiên đã bị thu hồi vì lý do an toàn.");
        }

        // Kiểm tra Hạn mức (ExpiresAt & AbsoluteExpiresAt)
        if (DateTime.UtcNow > existingToken.ExpiresAt || DateTime.UtcNow > existingToken.AbsoluteExpiresAt)
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn tối đa. Vui lòng đăng nhập lại.");
        }

        var user = existingToken.User;
        if (user.AccountStatus != "Active" || user.DeletedAt != null)
        {
            await _refreshRepo.RevokeAllUserTokensAsync(user.Id, clientIp);
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa hoặc không còn tồn tại.");
        }

        // Sinh token mới và đánh dấu thu hồi token cũ
        var (newRaw, newTokenHash) = _jwtGen.GenerateRefreshToken();
        await _refreshRepo.RevokeAsync(existingToken, clientIp, newTokenHash);

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            AbsoluteExpiresAt = existingToken.AbsoluteExpiresAt, // Giữ nguyên Absolute thời điểm đầu
            CreatedByIp = clientIp
        };
        await _refreshRepo.CreateAsync(newRefreshToken);

        var userRoles = user.UserRoleUsers.Select(ur => ur.Role.RoleName);
        var accessToken = _jwtGen.GenerateAccessToken(user, userRoles);

        return (accessToken, newRaw);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken, string clientIp)
    {
        var tokenHash = _jwtGen.HashToken(rawRefreshToken);
        var token = await _refreshRepo.GetByTokenHashAsync(tokenHash);
        if (token != null && token.RevokedAt == null)
        {
            await _refreshRepo.RevokeAsync(token, clientIp);
        }
    }

    public async Task<UserInfoDto> GetCurrentUserAsync(int userId)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || user.AccountStatus != "Active")
            throw new UnauthorizedAccessException("Người dùng không tồn tại hoặc đã bị khóa.");

        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        return new UserInfoDto
        {
            Id = user.Id,
            Code = user.Code,
            FullName = user.FullName,
            Email = user.Email,
            PrimaryRole = RoleHelper.ResolvePrimaryRole(roles),
            Roles = roles
        };
    }

    private async Task<(AuthResponseDto Response, string RawRefreshToken)> GenerateAuthResultAsync(User user, string clientIp)
    {
        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        var accessToken = _jwtGen.GenerateAccessToken(user, roles);
        var (rawRefresh, refreshHash) = _jwtGen.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedByIp = clientIp
        };
        await _refreshRepo.CreateAsync(refreshToken);

        var response = new AuthResponseDto
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = 900,
            User = new UserInfoDto
            {
                Id = user.Id,
                Code = user.Code,
                FullName = user.FullName,
                Email = user.Email,
                PrimaryRole = RoleHelper.ResolvePrimaryRole(roles),
                Roles = roles
            }
        };

        return (response, rawRefresh);
    }
}

public static class RoleHelper
{
    public static string ResolvePrimaryRole(IEnumerable<string> roles)
    {
        var list = roles.ToList();
        if (list.Contains("Admin")) return "Admin";
        if (list.Contains("Manager")) return "Manager";
        if (list.Contains("Guard")) return "Guard";
        if (list.Contains("Student")) return "Student";
        return list.FirstOrDefault() ?? "Student";
    }
}
