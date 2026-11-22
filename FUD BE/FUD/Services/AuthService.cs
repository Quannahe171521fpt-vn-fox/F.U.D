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
using FUD.Settings.pattern;
using Google.Apis.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly JwtSettings _jwtSettings;

    private int AccessTokenMinutes => _jwtSettings.AccessTokenExpirationMinutes > 0 ? _jwtSettings.AccessTokenExpirationMinutes : 15;
    private int IdleTimeoutMinutes => _jwtSettings.IdleTimeoutMinutes > 0 ? _jwtSettings.IdleTimeoutMinutes : 30;
    private int AbsoluteSessionHours => _jwtSettings.AbsoluteSessionHours > 0 ? _jwtSettings.AbsoluteSessionHours : 24;

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshRepo,
        IPasswordHasher hasher,
        IJwtTokenGenerator jwtGen,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<AuthService> logger,
        IOptions<JwtSettings> jwtOptions)
    {
        _userRepo = userRepo;
        _refreshRepo = refreshRepo;
        _hasher = hasher;
        _jwtGen = jwtGen;
        _config = config;
        _cache = cache;
        _logger = logger;
        _jwtSettings = jwtOptions.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
    }

    public async Task<(AuthResponseDto Response, string RawRefreshToken, DateTime CookieExpiresAt)> LoginWithCredentialsAsync(LoginRequestDto dto, string clientIp)
    {
        var user = await _userRepo.GetByAccountAsync(dto.Account);

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

        // 4. Sinh viên bắt buộc đăng nhập bằng Google, không đăng nhập bằng mật khẩu
        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        if (roles.Count == 1 && roles.Contains("Student"))
        {
            throw new UnauthorizedAccessException("Tài khoản sinh viên vui lòng đăng nhập bằng Google.");
        }

        return await GenerateAuthResultAsync(user, clientIp);
    }

    public async Task<(AuthResponseDto Response, string RawRefreshToken, DateTime CookieExpiresAt)> LoginWithGoogleAsync(GoogleLoginRequestDto dto, string clientIp)
    {
        var expectedClientId = _config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(expectedClientId))
        {
            _logger.LogError("Google:ClientId is not configured.");
            throw new UnauthorizedAccessException("Xác thực Google chưa được cấu hình hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(dto?.IdToken))
        {
            throw new UnauthorizedAccessException("Xác thực Google không hợp lệ hoặc đã hết hạn.");
        }

        string email;
        string subject;
        bool emailVerified;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { expectedClientId }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
            email = payload.Email;
            subject = payload.Subject;
            emailVerified = payload.EmailVerified;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GoogleJsonWebSignature.ValidateAsync failed");
            throw new UnauthorizedAccessException("Xác thực Google không hợp lệ hoặc đã hết hạn.");
        }

        if (!emailVerified)
            throw new UnauthorizedAccessException("Email Google chưa được xác thực.");

        email = email.ToLowerInvariant();

        // Tìm User theo GoogleId hoặc Email
        var user = await _userRepo.GetByGoogleIdAsync(subject)
                   ?? await _userRepo.GetByEmailAsync(email);

        if (user == null)
        {
            throw new UnauthorizedAccessException("Tài khoản chưa được kích hoạt trong hệ thống KTX. Vui lòng liên hệ Ban Quản lý.");
        }

        if (user.AccountStatus == "Banned" || user.AccountStatus == "Locked")
        {
            throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa.");
        }

        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        if (!roles.Contains("Student") || roles.Any(r => r is "Admin" or "Manager" or "Guard"))
        {
            throw new UnauthorizedAccessException("Chỉ tài khoản sinh viên mới được đăng nhập bằng Google.");
        }

        // Liên kết GoogleId nếu trước đó chỉ có Email
        if (string.IsNullOrEmpty(user.GoogleId))
        {
            await _userRepo.UpdateGoogleIdAsync(user.Id, subject);
            user.GoogleId = subject;
        }

        return await GenerateAuthResultAsync(user, clientIp);
    }

    public async Task<(RefreshResponseDto Response, string? RawRefreshToken, DateTime? CookieExpiresAt)> RotateRefreshTokenAsync(string rawRefreshToken, string clientIp)
    {
        var tokenHash = _jwtGen.HashToken(rawRefreshToken);
        var existingToken = await _refreshRepo.GetByTokenHashAsync(tokenHash);

        if (existingToken == null)
            throw new UnauthorizedAccessException("Phiên truy cập không tồn tại hoặc không hợp lệ.");

        var now = DateTime.UtcNow;

        // Xử lý Token đã bị thu hồi
        if (existingToken.RevokedAt.HasValue)
        {
            var graceResponse = await TryBuildGracePeriodResponseAsync(existingToken, now);
            if (graceResponse != null)
            {
                return (graceResponse, null, null);
            }

            // Quá Grace Period -> Phát hiện Reuse Attack -> Thu hồi toàn bộ chuỗi token!
            await _refreshRepo.RevokeAllUserTokensAsync(existingToken.UserId, clientIp);
            _logger.LogWarning("Token Reuse Attack detected for UserId: {UserId} from IP: {Ip}", existingToken.UserId, clientIp);
            throw new UnauthorizedAccessException("Phát hiện phiên truy cập bất thường. Tất cả các phiên đã bị thu hồi vì lý do an toàn.");
        }

        // Kiểm tra Hạn mức (ExpiresAt & AbsoluteExpiresAt)
        if (now > existingToken.ExpiresAt || now > existingToken.AbsoluteExpiresAt)
        {
            throw new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn tối đa. Vui lòng đăng nhập lại.");
        }

        var user = existingToken.User;
        if (user.AccountStatus != "Active" || user.DeletedAt != null)
        {
            await _refreshRepo.RevokeAllUserTokensAsync(user.Id, clientIp);
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa hoặc không còn tồn tại.");
        }

        // Sinh token mới và thực hiện rotation nguyên tử (capped ở AbsoluteExpiresAt)
        var absoluteExpiresAt = DateTime.SpecifyKind(existingToken.AbsoluteExpiresAt, DateTimeKind.Utc);
        var idleExpiresAt = DateTime.SpecifyKind(now.AddMinutes(IdleTimeoutMinutes), DateTimeKind.Utc);
        var newExpiresAt = idleExpiresAt < absoluteExpiresAt ? idleExpiresAt : absoluteExpiresAt;

        var (newRaw, newTokenHash) = _jwtGen.GenerateRefreshToken();
        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            CreatedAt = now,
            ExpiresAt = newExpiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
            CreatedByIp = clientIp
        };

        var rotated = await _refreshRepo.TryRotateAsync(existingToken, newRefreshToken, clientIp);
        if (!rotated)
        {
            var freshToken = await _refreshRepo.GetByTokenHashNoTrackingAsync(tokenHash);
            if (freshToken != null && freshToken.RevokedAt.HasValue)
            {
                var graceResponse = await TryBuildGracePeriodResponseAsync(freshToken, DateTime.UtcNow);
                if (graceResponse != null)
                {
                    return (graceResponse, null, null);
                }
            }
            throw new UnauthorizedAccessException("Phiên truy cập không hợp lệ hoặc đang được làm mới đồng thời. Vui lòng thử lại.");
        }

        var userRoles = user.UserRoleUsers.Select(ur => ur.Role.RoleName);
        var accessToken = _jwtGen.GenerateAccessToken(user, userRoles);

        return (new RefreshResponseDto
        {
            AccessToken = accessToken,
            ExpiresIn = AccessTokenMinutes * 60,
            IdleTimeoutMinutes = IdleTimeoutMinutes,
            AbsoluteExpiresAt = absoluteExpiresAt
        }, newRaw, newExpiresAt);
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
        if (user == null || user.AccountStatus == "Banned" || user.AccountStatus == "Locked")
            throw new UnauthorizedAccessException("Người dùng không tồn tại hoặc đã bị khóa.");

        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();

        int? dormId = null;
        string? dormName = null;
        if (roles.Contains("Guard"))
        {
            var guardDorm = await _userRepo.GetGuardDormInfoAsync(userId);
            dormId = guardDorm?.DormId;
            dormName = guardDorm?.DormName;
        }

        return new UserInfoDto
        {
            Id = user.Id,
            Code = user.Code,
            FullName = user.FullName,
            Email = user.Email,
            Gender = user.Gender,
            PrimaryRole = RoleHelper.ResolvePrimaryRole(roles),
            Roles = roles,
            AccountStatus = user.AccountStatus,
            SuspensionReason = user.SuspensionReason,
            DormId = dormId,
            DormName = dormName
        };
    }

    private async Task<RefreshResponseDto?> TryBuildGracePeriodResponseAsync(RefreshToken revokedToken, DateTime now)
    {
        if (!revokedToken.RevokedAt.HasValue
            || (now - revokedToken.RevokedAt.Value) > TimeSpan.FromSeconds(30)
            || string.IsNullOrEmpty(revokedToken.ReplacedByTokenHash))
        {
            return null;
        }

        var replacedToken = await _refreshRepo.GetByTokenHashNoTrackingAsync(revokedToken.ReplacedByTokenHash);
        if (replacedToken == null
            || replacedToken.RevokedAt != null
            || now > replacedToken.ExpiresAt
            || now > replacedToken.AbsoluteExpiresAt
            || replacedToken.User.AccountStatus != "Active"
            || replacedToken.User.DeletedAt != null)
        {
            return null;
        }

        var roles = replacedToken.User.UserRoleUsers.Select(ur => ur.Role.RoleName);
        var newAccToken = _jwtGen.GenerateAccessToken(replacedToken.User, roles);
        return new RefreshResponseDto
        {
            AccessToken = newAccToken,
            ExpiresIn = AccessTokenMinutes * 60,
            IdleTimeoutMinutes = IdleTimeoutMinutes,
            AbsoluteExpiresAt = DateTime.SpecifyKind(replacedToken.AbsoluteExpiresAt, DateTimeKind.Utc)
        };
    }

    private async Task<(AuthResponseDto Response, string RawRefreshToken, DateTime CookieExpiresAt)> GenerateAuthResultAsync(User user, string clientIp)
    {
        var roles = user.UserRoleUsers.Select(ur => ur.Role.RoleName).ToList();
        var accessToken = _jwtGen.GenerateAccessToken(user, roles);
        var (rawRefresh, refreshHash) = _jwtGen.GenerateRefreshToken();

        var now = DateTime.UtcNow;
        var absoluteExpiresAt = DateTime.SpecifyKind(now.AddHours(AbsoluteSessionHours), DateTimeKind.Utc);
        var idleExpiresAt = DateTime.SpecifyKind(now.AddMinutes(IdleTimeoutMinutes), DateTimeKind.Utc);
        var refreshExpiresAt = idleExpiresAt < absoluteExpiresAt ? idleExpiresAt : absoluteExpiresAt;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
            CreatedByIp = clientIp
        };
        await _refreshRepo.CreateAsync(refreshToken);

        int? dormId = null;
        string? dormName = null;
        if (roles.Contains("Guard"))
        {
            var guardDorm = await _userRepo.GetGuardDormInfoAsync(user.Id);
            dormId = guardDorm?.DormId;
            dormName = guardDorm?.DormName;
        }

        var response = new AuthResponseDto
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = AccessTokenMinutes * 60,
            IdleTimeoutMinutes = IdleTimeoutMinutes,
            AbsoluteExpiresAt = absoluteExpiresAt,
            User = new UserInfoDto
            {
                Id = user.Id,
                Code = user.Code,
                FullName = user.FullName,
                Email = user.Email,
                Gender = user.Gender,
                PrimaryRole = RoleHelper.ResolvePrimaryRole(roles),
                Roles = roles,
                AccountStatus = user.AccountStatus,
                SuspensionReason = user.SuspensionReason,
                DormId = dormId,
                DormName = dormName
            }
        };

        return (response, rawRefresh, refreshExpiresAt);
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
