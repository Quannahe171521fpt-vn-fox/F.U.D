using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Auth;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Admin;

/// <summary>
/// Unit tests cho AuthService — tuân thủ quy tắc AAA.
/// LoginWithGoogleAsync phụ thuộc GoogleJsonWebSignature (static) không thể mock trực tiếp
/// → chỉ test các nhánh logic sau khi payload đã được parse (thông qua cấu trúc test riêng).
/// </summary>
public class AuthServiceTests
{
    // ─── Shared mocks ───────────────────────────────────────────────────────────
    private readonly Mock<IUserRepository>          _userRepoMock      = new();
    private readonly Mock<IRefreshTokenRepository>  _refreshRepoMock   = new();
    private readonly Mock<IPasswordHasher>          _hasherMock        = new();
    private readonly Mock<IJwtTokenGenerator>       _jwtGenMock        = new();
    private readonly Mock<IConfiguration>           _configMock        = new();
    private readonly Mock<IMemoryCache>             _cacheMock         = new();
    private readonly Mock<ILogger<AuthService>>     _loggerMock        = new();

    private AuthService CreateSut() => new(
        _userRepoMock.Object,
        _refreshRepoMock.Object,
        _hasherMock.Object,
        _jwtGenMock.Object,
        _configMock.Object,
        _cacheMock.Object,
        _loggerMock.Object);

    // Helper: User mẫu
    private static User SampleUser(int id = 1, string status = "Active", params string[] roleNames)
    {
        return new User
        {
            Id            = id,
            Code          = $"user{id}",
            FullName      = $"User {id}",
            Email         = $"user{id}@test.com",
            PasswordHash  = "hashed_password",
            AccountStatus = status,
            UserRoleUsers = roleNames.Select(r => new UserRole
            {
                Role = new Role { RoleName = r }
            }).ToList()
        };
    }

    // ─── LoginWithCredentialsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task LoginWithCredentials_WhenUserNotFound_CallsVerifyDummyAndThrows()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByAccountAsync("unknown")).ReturnsAsync((User?)null);
        _hasherMock.Setup(h => h.VerifyDummy());

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.LoginWithCredentialsAsync(
            new LoginRequestDto { Account = "unknown", Password = "pass" }, clientIp: "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Tên đăng nhập hoặc mật khẩu*");
        _hasherMock.Verify(h => h.VerifyDummy(), Times.Once);
    }

    [Fact]
    public async Task LoginWithCredentials_WhenWrongPassword_ThrowsUnauthorized()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Manager");
        _userRepoMock.Setup(r => r.GetByAccountAsync("manager")).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify("wrong", "hashed_password")).Returns(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.LoginWithCredentialsAsync(
            new LoginRequestDto { Account = "manager", Password = "wrong" }, clientIp: "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Tên đăng nhập hoặc mật khẩu*");
    }

    [Fact]
    public async Task LoginWithCredentials_WhenAccountNotActive_ThrowsUnauthorized()
    {
        // Arrange
        var user = SampleUser(1, "Suspended", "Manager");
        _userRepoMock.Setup(r => r.GetByAccountAsync("manager")).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.LoginWithCredentialsAsync(
            new LoginRequestDto { Account = "manager", Password = "pass" }, clientIp: "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*chưa được kích hoạt hoặc đã bị khóa*");
    }

    [Fact]
    public async Task LoginWithCredentials_WhenStudentOnly_ThrowsUnauthorizedWithGoogleMessage()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Student");
        _userRepoMock.Setup(r => r.GetByAccountAsync("student")).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.LoginWithCredentialsAsync(
            new LoginRequestDto { Account = "student", Password = "pass" }, clientIp: "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*đăng nhập bằng Google*");
    }

    [Fact]
    public async Task LoginWithCredentials_WhenValid_ReturnsTokenPair()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Manager");
        _userRepoMock.Setup(r => r.GetByAccountAsync("manager")).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), "hashed_password")).Returns(true);
        _jwtGenMock.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("access_token");
        _jwtGenMock.Setup(j => j.GenerateRefreshToken()).Returns(("raw_refresh", "refresh_hash"));
        _refreshRepoMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        var (response, rawRefresh) = await sut.LoginWithCredentialsAsync(
            new LoginRequestDto { Account = "manager", Password = "correct" }, clientIp: "127.0.0.1");

        // Assert
        response.AccessToken.Should().Be("access_token");
        rawRefresh.Should().Be("raw_refresh");
        _refreshRepoMock.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    // ─── RotateRefreshTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RotateRefreshToken_WhenTokenNotFound_ThrowsUnauthorized()
    {
        // Arrange
        _jwtGenMock.Setup(j => j.HashToken("bad_token")).Returns("bad_hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("bad_hash")).ReturnsAsync((RefreshToken?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RotateRefreshTokenAsync("bad_token", "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*không tồn tại*");
    }

    [Fact]
    public async Task RotateRefreshToken_WhenRevokedAndInGracePeriod_ReturnsNewAccessTokenWithSameRefresh()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Manager");
        var replacedToken = new RefreshToken
        {
            UserId    = 1,
            TokenHash = "replaced_hash",
            User      = user,
            ExpiresAt         = DateTime.UtcNow.AddDays(7),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        var existingToken = new RefreshToken
        {
            UserId               = 1,
            RevokedAt            = DateTime.UtcNow.AddSeconds(-10), // 10s trước → trong grace period 30s
            ReplacedByTokenHash  = "replaced_hash"
        };

        _jwtGenMock.Setup(j => j.HashToken("old_token")).Returns("old_hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(existingToken);
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("replaced_hash")).ReturnsAsync(replacedToken);
        _jwtGenMock.Setup(j => j.GenerateAccessToken(replacedToken.User, It.IsAny<IEnumerable<string>>())).Returns("new_access");

        var sut = CreateSut();

        // Act
        var (accessToken, rawRefresh) = await sut.RotateRefreshTokenAsync("old_token", "127.0.0.1");

        // Assert
        accessToken.Should().Be("new_access");
        rawRefresh.Should().Be("old_token"); // Trả lại cùng rawRefresh
    }

    [Fact]
    public async Task RotateRefreshToken_WhenRevokedPastGracePeriod_RevokesAllAndThrows()
    {
        // Arrange
        var existingToken = new RefreshToken
        {
            UserId    = 1,
            RevokedAt = DateTime.UtcNow.AddSeconds(-60), // 60s → vượt grace period
            ReplacedByTokenHash = null
        };

        _jwtGenMock.Setup(j => j.HashToken("old_token")).Returns("old_hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(existingToken);
        _refreshRepoMock.Setup(r => r.RevokeAllUserTokensAsync(1, It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RotateRefreshTokenAsync("old_token", "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*bất thường*");
        _refreshRepoMock.Verify(r => r.RevokeAllUserTokensAsync(1, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RotateRefreshToken_WhenExpired_ThrowsUnauthorized()
    {
        // Arrange
        var existingToken = new RefreshToken
        {
            UserId    = 1,
            RevokedAt = null,
            ExpiresAt         = DateTime.UtcNow.AddDays(-1), // đã hết hạn
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _jwtGenMock.Setup(j => j.HashToken("token")).Returns("hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("hash")).ReturnsAsync(existingToken);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RotateRefreshTokenAsync("token", "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*đã hết hạn tối đa*");
    }

    [Fact]
    public async Task RotateRefreshToken_WhenAbsoluteExpired_ThrowsUnauthorized()
    {
        // Arrange
        var existingToken = new RefreshToken
        {
            UserId    = 1,
            RevokedAt = null,
            ExpiresAt         = DateTime.UtcNow.AddDays(7),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(-1) // absolute đã hết
        };

        _jwtGenMock.Setup(j => j.HashToken("token")).Returns("hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("hash")).ReturnsAsync(existingToken);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RotateRefreshTokenAsync("token", "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*đã hết hạn tối đa*");
    }

    [Fact]
    public async Task RotateRefreshToken_WhenUserSuspended_RevokesAllAndThrows()
    {
        // Arrange
        var user = SampleUser(1, "Suspended", "Manager");
        var existingToken = new RefreshToken
        {
            UserId    = 1,
            RevokedAt = null,
            ExpiresAt         = DateTime.UtcNow.AddDays(7),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30),
            User      = user
        };

        _jwtGenMock.Setup(j => j.HashToken("token")).Returns("hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("hash")).ReturnsAsync(existingToken);
        _refreshRepoMock.Setup(r => r.RevokeAllUserTokensAsync(1, It.IsAny<string>())).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RotateRefreshTokenAsync("token", "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*bị khóa*");
        _refreshRepoMock.Verify(r => r.RevokeAllUserTokensAsync(1, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RotateRefreshToken_WhenValid_CreatesNewRefreshTokenAndReturnsTokens()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Manager");
        var existingToken = new RefreshToken
        {
            UserId    = 1,
            RevokedAt = null,
            ExpiresAt         = DateTime.UtcNow.AddDays(7),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(30),
            User      = user
        };

        _jwtGenMock.Setup(j => j.HashToken("old_token")).Returns("old_hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("old_hash")).ReturnsAsync(existingToken);
        _jwtGenMock.Setup(j => j.GenerateRefreshToken()).Returns(("new_raw", "new_hash"));
        _refreshRepoMock.Setup(r => r.RevokeAsync(existingToken, It.IsAny<string>(), "new_hash")).Returns(Task.CompletedTask);
        _refreshRepoMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        _jwtGenMock.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("new_access");

        var sut = CreateSut();

        // Act
        var (accessToken, rawRefresh) = await sut.RotateRefreshTokenAsync("old_token", "127.0.0.1");

        // Assert
        accessToken.Should().Be("new_access");
        rawRefresh.Should().Be("new_raw");
        _refreshRepoMock.Verify(r => r.CreateAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    // ─── RevokeRefreshTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RevokeRefreshToken_WhenTokenNotFound_DoesNotCallRevoke()
    {
        // Arrange
        _jwtGenMock.Setup(j => j.HashToken("bad")).Returns("bad_hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("bad_hash")).ReturnsAsync((RefreshToken?)null);

        var sut = CreateSut();

        // Act
        await sut.RevokeRefreshTokenAsync("bad", "127.0.0.1");

        // Assert
        _refreshRepoMock.Verify(r => r.RevokeAsync(It.IsAny<RefreshToken>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RevokeRefreshToken_WhenAlreadyRevoked_DoesNotCallRevoke()
    {
        // Arrange
        var token = new RefreshToken { RevokedAt = DateTime.UtcNow };
        _jwtGenMock.Setup(j => j.HashToken("token")).Returns("hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("hash")).ReturnsAsync(token);

        var sut = CreateSut();

        // Act
        await sut.RevokeRefreshTokenAsync("token", "127.0.0.1");

        // Assert
        _refreshRepoMock.Verify(r => r.RevokeAsync(It.IsAny<RefreshToken>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RevokeRefreshToken_WhenValid_CallsRevokeOnce()
    {
        // Arrange
        var token = new RefreshToken { RevokedAt = null };
        _jwtGenMock.Setup(j => j.HashToken("token")).Returns("hash");
        _refreshRepoMock.Setup(r => r.GetByTokenHashAsync("hash")).ReturnsAsync(token);
        _refreshRepoMock.Setup(r => r.RevokeAsync(token, "127.0.0.1", It.IsAny<string?>())).Returns(Task.CompletedTask);

        var sut = CreateSut();

        // Act
        await sut.RevokeRefreshTokenAsync("token", "127.0.0.1");

        // Assert
        _refreshRepoMock.Verify(r => r.RevokeAsync(token, "127.0.0.1", It.IsAny<string?>()), Times.Once);
    }

    // ─── GetCurrentUserAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentUser_WhenUserNull_ThrowsUnauthorized()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((User?)null);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.GetCurrentUserAsync(1);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*không tồn tại*");
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserBanned_ThrowsUnauthorized()
    {
        // Arrange
        var user = SampleUser(1, "Banned", "Student");
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.GetCurrentUserAsync(1);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*bị khóa*");
    }

    [Fact]
    public async Task GetCurrentUser_WhenValid_ReturnsCorrectUserInfoDto()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Admin", "Manager");
        _userRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        var result = await sut.GetCurrentUserAsync(1);

        // Assert
        result.Id.Should().Be(1);
        result.Roles.Should().Contain("Admin");
        result.PrimaryRole.Should().Be("Admin");
        result.AccountStatus.Should().Be("Active");
    }
}

