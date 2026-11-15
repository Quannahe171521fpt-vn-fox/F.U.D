using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers;
using FUD.DTOs.Auth;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services;
using FUD.Services.Interfaces;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FUD.Tests.Login;

public class AuthRefreshTokenTests
{
    private static JwtSettings CreateDefaultSettings() => new()
    {
        Key = "test_secret_key_that_is_at_least_32_bytes_long",
        Issuer = "FUD-API",
        Audience = "FUD-Client",
        AccessTokenExpirationMinutes = 15,
        IdleTimeoutMinutes = 30,
        AbsoluteSessionHours = 24
    };

    private static AuthService CreateAuthService(
        out Mock<IUserRepository> userRepoMock,
        out Mock<IRefreshTokenRepository> refreshRepoMock,
        out Mock<IPasswordHasher> hasherMock,
        out Mock<IJwtTokenGenerator> jwtGenMock,
        JwtSettings? customSettings = null)
    {
        userRepoMock = new Mock<IUserRepository>();
        refreshRepoMock = new Mock<IRefreshTokenRepository>();
        hasherMock = new Mock<IPasswordHasher>();
        jwtGenMock = new Mock<IJwtTokenGenerator>();
        var configMock = new Mock<IConfiguration>();
        var cacheMock = new Mock<IMemoryCache>();
        var loggerMock = new Mock<ILogger<AuthService>>();

        return new AuthService(
            userRepoMock.Object,
            refreshRepoMock.Object,
            hasherMock.Object,
            jwtGenMock.Object,
            configMock.Object,
            cacheMock.Object,
            loggerMock.Object,
            Options.Create(customSettings ?? CreateDefaultSettings()));
    }

    private static User CreateActiveManager(int id = 1) => new()
    {
        Id = id,
        Code = $"MGR{id:000}",
        FullName = "Manager Test",
        Email = $"manager{id}@fpt.edu.vn",
        PasswordHash = "hashed_pwd",
        AccountStatus = "Active",
        UserRoleUsers = new List<UserRole>
        {
            new()
            {
                UserId = id,
                RoleId = 2,
                Role = new Role { Id = 2, RoleName = "Manager" }
            }
        }
    };

    [Fact]
    public void JwtSettings_BindsFromEnvironmentSection_WithoutAppsettingsAndPreservesDefaults()
    {
        var defaultSettings = new JwtSettings();
        defaultSettings.Issuer.Should().Be("FUD-API");
        defaultSettings.Audience.Should().Be("FUD-Client");
        defaultSettings.AccessTokenExpirationMinutes.Should().Be(15);
        defaultSettings.IdleTimeoutMinutes.Should().Be(30);
        defaultSettings.AbsoluteSessionHours.Should().Be(24);

        var envConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenExpirationMinutes"] = "20",
                ["Jwt:IdleTimeoutMinutes"] = "45",
                ["Jwt:AbsoluteSessionHours"] = "12"
            })
            .Build();

        var boundSettings = new JwtSettings();
        envConfig.GetSection("Jwt").Bind(boundSettings);

        boundSettings.AccessTokenExpirationMinutes.Should().Be(20);
        boundSettings.IdleTimeoutMinutes.Should().Be(45);
        boundSettings.AbsoluteSessionHours.Should().Be(12);
        boundSettings.Issuer.Should().Be("FUD-API");
        boundSettings.Audience.Should().Be("FUD-Client");
    }

    [Fact]
    public void JwtTokenGenerator_HonorsConfiguredAccessTokenExpirationMinutes()
    {
        var settings = CreateDefaultSettings();
        settings.AccessTokenExpirationMinutes = 10;
        var generator = new JwtTokenGenerator(Options.Create(settings));
        var user = CreateActiveManager();

        var before = DateTime.UtcNow;
        var jwt = generator.GenerateAccessToken(user, new[] { "Manager" });
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        parsed.Issuer.Should().Be("FUD-API");
        parsed.Audiences.Should().ContainSingle("FUD-Client");
        parsed.ValidTo.Should().BeCloseTo(before.AddMinutes(10), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_PersistsIdleAndAbsoluteCaps_AndHonorsCustomOptions()
    {
        var customSettings = new JwtSettings
        {
            Key = "test_secret_key_that_is_at_least_32_bytes_long",
            AccessTokenExpirationMinutes = 10,
            IdleTimeoutMinutes = 40,
            AbsoluteSessionHours = 12
        };
        var service = CreateAuthService(out var userRepo, out var refreshRepo, out var hasher, out var jwtGen, customSettings);
        var user = CreateActiveManager();
        var dto = new LoginRequestDto { Account = user.Email, Password = "Secret123" };

        userRepo.Setup(r => r.GetByAccountAsync(dto.Account)).ReturnsAsync(user);
        hasher.Setup(h => h.Verify(dto.Password, user.PasswordHash!)).Returns(true);
        jwtGen.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("access_token");
        jwtGen.Setup(j => j.GenerateRefreshToken()).Returns(("raw_token", "hash_token"));

        RefreshToken? persisted = null;
        refreshRepo.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => persisted = t)
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;
        var result = await service.LoginWithCredentialsAsync(dto, "127.0.0.1");

        result.Response.ExpiresIn.Should().Be(600);
        result.Response.IdleTimeoutMinutes.Should().Be(40);
        result.Response.AbsoluteExpiresAt.Should().BeCloseTo(before.AddHours(12), TimeSpan.FromSeconds(5));
        result.CookieExpiresAt.Should().BeCloseTo(before.AddMinutes(40), TimeSpan.FromSeconds(5));

        persisted.Should().NotBeNull();
        persisted!.ExpiresAt.Should().BeCloseTo(before.AddMinutes(40), TimeSpan.FromSeconds(5));
        persisted.AbsoluteExpiresAt.Should().BeCloseTo(before.AddHours(12), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_ActiveToken_SlidesIdleTimeoutAndCapsAtAbsoluteExpiresAt()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var now = DateTime.UtcNow;
        var tightAbsoluteCap = now.AddMinutes(10);
        var existing = new RefreshToken
        {
            Id = 10,
            UserId = user.Id,
            TokenHash = "old_hash",
            CreatedAt = now.AddHours(-23).AddMinutes(-50),
            ExpiresAt = now.AddMinutes(10),
            AbsoluteExpiresAt = tightAbsoluteCap,
            User = user
        };

        jwtGen.Setup(j => j.HashToken("old_raw")).Returns("old_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(existing);
        jwtGen.Setup(j => j.GenerateRefreshToken()).Returns(("new_raw", "new_hash"));
        jwtGen.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("new_access");

        RefreshToken? inserted = null;
        refreshRepo.Setup(r => r.TryRotateAsync(existing, It.IsAny<RefreshToken>(), "127.0.0.1"))
            .Callback<RefreshToken, RefreshToken, string>((_, rep, _) => inserted = rep)
            .ReturnsAsync(true);

        var result = await service.RotateRefreshTokenAsync("old_raw", "127.0.0.1");

        result.Response.AccessToken.Should().Be("new_access");
        result.Response.ExpiresIn.Should().Be(900);
        result.Response.IdleTimeoutMinutes.Should().Be(30);
        result.RawRefreshToken.Should().Be("new_raw");
        result.CookieExpiresAt.Should().BeCloseTo(tightAbsoluteCap, TimeSpan.FromSeconds(2));
        inserted.Should().NotBeNull();
        inserted!.ExpiresAt.Should().BeOnOrBefore(tightAbsoluteCap.AddMilliseconds(500));
        inserted.AbsoluteExpiresAt.Should().BeCloseTo(tightAbsoluteCap, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_IdleExpired_ThrowsWithoutRotating()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var existing = new RefreshToken
        {
            Id = 11,
            UserId = user.Id,
            TokenHash = "expired_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            AbsoluteExpiresAt = DateTime.UtcNow.AddHours(10),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("expired_raw")).Returns("expired_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("expired_hash")).ReturnsAsync(existing);

        var act = () => service.RotateRefreshTokenAsync("expired_raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        refreshRepo.Verify(r => r.TryRotateAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_AbsoluteExpired_ThrowsEvenIfIdleUnexpired()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var existing = new RefreshToken
        {
            Id = 12,
            UserId = user.Id,
            TokenHash = "abs_expired_hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            AbsoluteExpiresAt = DateTime.UtcNow.AddSeconds(-5),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("abs_raw")).Returns("abs_expired_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("abs_expired_hash")).ReturnsAsync(existing);

        var act = () => service.RotateRefreshTokenAsync("abs_raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        refreshRepo.Verify(r => r.TryRotateAsync(It.IsAny<RefreshToken>(), It.IsAny<RefreshToken>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_GracePeriodWithin30Seconds_ReturnsNullRefreshTokenAndDoesNotRevokeAll()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var now = DateTime.UtcNow;
        var revoked = new RefreshToken
        {
            Id = 13,
            UserId = user.Id,
            TokenHash = "old_hash",
            RevokedAt = now.AddSeconds(-5),
            ReplacedByTokenHash = "replacement_hash",
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };
        var replacement = new RefreshToken
        {
            Id = 14,
            UserId = user.Id,
            TokenHash = "replacement_hash",
            RevokedAt = null,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("old_raw")).Returns("old_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(revoked);
        refreshRepo.Setup(r => r.GetByTokenHashNoTrackingAsync("replacement_hash")).ReturnsAsync(replacement);
        jwtGen.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("grace_access_token");

        var result = await service.RotateRefreshTokenAsync("old_raw", "127.0.0.1");

        result.Response.AccessToken.Should().Be("grace_access_token");
        result.RawRefreshToken.Should().BeNull();
        result.CookieExpiresAt.Should().BeNull();
        refreshRepo.Verify(r => r.RevokeAllUserTokensAsync(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_GracePeriodWithRevokedOrExpiredReplacement_RevokesAllAndThrows()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var now = DateTime.UtcNow;
        var revoked = new RefreshToken
        {
            Id = 15,
            UserId = user.Id,
            TokenHash = "old_hash",
            RevokedAt = now.AddSeconds(-5),
            ReplacedByTokenHash = "replacement_hash",
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };
        var expiredReplacement = new RefreshToken
        {
            Id = 16,
            UserId = user.Id,
            TokenHash = "replacement_hash",
            RevokedAt = now.AddSeconds(-1),
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("old_raw")).Returns("old_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(revoked);
        refreshRepo.Setup(r => r.GetByTokenHashNoTrackingAsync("replacement_hash")).ReturnsAsync(expiredReplacement);

        var act = () => service.RotateRefreshTokenAsync("old_raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Phát hiện phiên truy cập bất thường*");
        refreshRepo.Verify(r => r.RevokeAllUserTokensAsync(user.Id, "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_ReuseAttackAfter30Seconds_RevokesAllUserTokensAndThrows()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var now = DateTime.UtcNow;
        var revoked = new RefreshToken
        {
            Id = 17,
            UserId = user.Id,
            TokenHash = "old_hash",
            RevokedAt = now.AddSeconds(-35),
            ReplacedByTokenHash = "replacement_hash",
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("old_raw")).Returns("old_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(revoked);

        var act = () => service.RotateRefreshTokenAsync("old_raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Phát hiện phiên truy cập bất thường*");
        refreshRepo.Verify(r => r.RevokeAllUserTokensAsync(user.Id, "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_ConcurrentRaceLosesTryRotate_ServesGracePeriodFromFreshNoTrackingLookup()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var now = DateTime.UtcNow;
        var trackedInitial = new RefreshToken
        {
            Id = 18,
            UserId = user.Id,
            TokenHash = "race_hash",
            RevokedAt = null,
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };
        var freshRevoked = new RefreshToken
        {
            Id = 18,
            UserId = user.Id,
            TokenHash = "race_hash",
            RevokedAt = now.AddSeconds(-1),
            ReplacedByTokenHash = "winner_hash",
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };
        var winnerReplacement = new RefreshToken
        {
            Id = 19,
            UserId = user.Id,
            TokenHash = "winner_hash",
            RevokedAt = null,
            ExpiresAt = now.AddMinutes(30),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("race_raw")).Returns("race_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("race_hash")).ReturnsAsync(trackedInitial);
        jwtGen.Setup(j => j.GenerateRefreshToken()).Returns(("loser_raw", "loser_hash"));
        refreshRepo.Setup(r => r.TryRotateAsync(trackedInitial, It.IsAny<RefreshToken>(), "127.0.0.1")).ReturnsAsync(false);
        refreshRepo.Setup(r => r.GetByTokenHashNoTrackingAsync("race_hash")).ReturnsAsync(freshRevoked);
        refreshRepo.Setup(r => r.GetByTokenHashNoTrackingAsync("winner_hash")).ReturnsAsync(winnerReplacement);
        jwtGen.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("race_grace_access");

        var result = await service.RotateRefreshTokenAsync("race_raw", "127.0.0.1");

        result.Response.AccessToken.Should().Be("race_grace_access");
        result.RawRefreshToken.Should().BeNull();
        result.CookieExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_ConcurrentRaceUnrecoverable_ThrowsUnauthorized()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var user = CreateActiveManager();
        var now = DateTime.UtcNow;
        var trackedInitial = new RefreshToken
        {
            Id = 20,
            UserId = user.Id,
            TokenHash = "race_hash",
            RevokedAt = null,
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = user
        };

        jwtGen.Setup(j => j.HashToken("race_raw")).Returns("race_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("race_hash")).ReturnsAsync(trackedInitial);
        jwtGen.Setup(j => j.GenerateRefreshToken()).Returns(("loser_raw", "loser_hash"));
        refreshRepo.Setup(r => r.TryRotateAsync(trackedInitial, It.IsAny<RefreshToken>(), "127.0.0.1")).ReturnsAsync(false);
        refreshRepo.Setup(r => r.GetByTokenHashNoTrackingAsync("race_hash")).ReturnsAsync((RefreshToken?)null);

        var act = () => service.RotateRefreshTokenAsync("race_raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Phiên truy cập không hợp lệ hoặc đang được làm mới đồng thời. Vui lòng thử lại.");
    }

    [Fact]
    public async Task RotateRefreshTokenAsync_InactiveOrDeletedUser_RevokesAllTokensAndThrows()
    {
        var service = CreateAuthService(out _, out var refreshRepo, out _, out var jwtGen);
        var suspendedUser = CreateActiveManager();
        suspendedUser.AccountStatus = "Suspended";
        var now = DateTime.UtcNow;
        var existing = new RefreshToken
        {
            Id = 21,
            UserId = suspendedUser.Id,
            TokenHash = "valid_hash",
            ExpiresAt = now.AddMinutes(20),
            AbsoluteExpiresAt = now.AddHours(10),
            User = suspendedUser
        };

        jwtGen.Setup(j => j.HashToken("valid_raw")).Returns("valid_hash");
        refreshRepo.Setup(r => r.GetByTokenHashAsync("valid_hash")).ReturnsAsync(existing);

        var act = () => service.RotateRefreshTokenAsync("valid_raw", "127.0.0.1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Tài khoản đã bị khóa hoặc không còn tồn tại.");
        refreshRepo.Verify(r => r.RevokeAllUserTokensAsync(suspendedUser.Id, "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task AuthController_Refresh_GracePeriod_DoesNotEmitSetCookie()
    {
        var authServiceMock = new Mock<IAuthService>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "fud_refresh_token=old_token";
        var controller = new AuthController(authServiceMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var dto = new RefreshResponseDto
        {
            AccessToken = "grace_access",
            ExpiresIn = 900,
            IdleTimeoutMinutes = 30,
            AbsoluteExpiresAt = DateTime.UtcNow.AddHours(24)
        };
        authServiceMock.Setup(s => s.RotateRefreshTokenAsync("old_token", It.IsAny<string>()))
            .ReturnsAsync((dto, (string?)null, (DateTime?)null));

        var result = await controller.Refresh();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ApiResponse<RefreshResponseDto>>().Subject;
        payload.Data!.AccessToken.Should().Be("grace_access");
        httpContext.Response.Headers.ContainsKey("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task AuthController_Refresh_RotatedToken_EmitsCookieWithExpectedFlagsAndExpiry()
    {
        var authServiceMock = new Mock<IAuthService>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.IsHttps = true;
        httpContext.Request.Host = new HostString("dorm.fpt.edu.vn");
        httpContext.Request.Headers["Cookie"] = "fud_refresh_token=old_token";
        var controller = new AuthController(authServiceMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var cookieExpiry = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var dto = new RefreshResponseDto
        {
            AccessToken = "new_access",
            ExpiresIn = 900,
            IdleTimeoutMinutes = 30,
            AbsoluteExpiresAt = cookieExpiry.AddHours(20)
        };
        authServiceMock.Setup(s => s.RotateRefreshTokenAsync("old_token", It.IsAny<string>()))
            .ReturnsAsync((dto, "rotated_raw_token", (DateTime?)cookieExpiry));

        var result = await controller.Refresh();

        result.Should().BeOfType<OkObjectResult>();
        var setCookie = httpContext.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain("fud_refresh_token=rotated_raw_token");
        setCookie.Should().Contain("secure");
        setCookie.Should().Contain("httponly");
        setCookie.Should().Contain("path=/api/auth");
        setCookie.Should().Contain("expires=Mon, 16 Nov 2026 12:00:00 GMT");
    }

    [Fact]
    public async Task AuthController_Refresh_Unauthorized_Returns401AndDeletesCookie()
    {
        var authServiceMock = new Mock<IAuthService>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "fud_refresh_token=bad_token";
        var controller = new AuthController(authServiceMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        authServiceMock.Setup(s => s.RotateRefreshTokenAsync("bad_token", It.IsAny<string>()))
            .ThrowsAsync(new UnauthorizedAccessException("Phiên đăng nhập đã hết hạn tối đa."));

        var result = await controller.Refresh();

        result.Should().BeOfType<UnauthorizedObjectResult>();
        var setCookie = httpContext.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain("fud_refresh_token=");
        setCookie.Should().Contain("expires=Thu, 01 Jan 1970");
    }
}
