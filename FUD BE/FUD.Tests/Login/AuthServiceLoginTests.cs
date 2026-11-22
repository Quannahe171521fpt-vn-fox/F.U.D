using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.DTOs.Auth;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services;
using FUD.Settings.pattern;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FUD.Tests.Login;

public class AuthServiceLoginTests
{
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IRefreshTokenRepository> _refreshRepoMock;
    private readonly Mock<IPasswordHasher> _hasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtGenMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _authService;

    public AuthServiceLoginTests()
    {
        _userRepoMock = new Mock<IUserRepository>();
        _refreshRepoMock = new Mock<IRefreshTokenRepository>();
        _hasherMock = new Mock<IPasswordHasher>();
        _jwtGenMock = new Mock<IJwtTokenGenerator>();
        _configMock = new Mock<IConfiguration>();
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        var jwtOptions = Options.Create(new JwtSettings
        {
            Key = "test_secret_key_that_is_at_least_32_bytes_long",
            Issuer = "FUD-API",
            Audience = "FUD-Client",
            AccessTokenExpirationMinutes = 15,
            IdleTimeoutMinutes = 30,
            AbsoluteSessionHours = 24
        });

        _authService = new AuthService(
            _userRepoMock.Object,
            _refreshRepoMock.Object,
            _hasherMock.Object,
            _jwtGenMock.Object,
            _configMock.Object,
            _cacheMock.Object,
            _loggerMock.Object,
            jwtOptions);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_ValidCredentialsManager_ReturnsAuthResponseAndSetsRefreshToken()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "ValidPassword123" };
        const string clientIp = "192.168.1.10";

        var user = new User
        {
            Id = 1,
            Code = "MGR001",
            FullName = "Nguyen Van Manager",
            Email = "manager@fpt.edu.vn",
            PasswordHash = "hashed_valid_password",
            AccountStatus = "Active",
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 1,
                    RoleId = 2,
                    Role = new Role { Id = 2, RoleName = "Manager" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash))
                   .Returns(true);
        _jwtGenMock.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>()))
                   .Returns("fake_jwt_access_token");
        _jwtGenMock.Setup(j => j.GenerateRefreshToken())
                   .Returns(("raw_refresh_token", "hash_refresh_token"));
        _refreshRepoMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
                        .Returns(Task.CompletedTask);

        // Act
        var before = DateTime.UtcNow;
        var result = await _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        result.Should().NotBeNull();
        result.RawRefreshToken.Should().Be("raw_refresh_token");
        result.CookieExpiresAt.Should().BeCloseTo(before.AddMinutes(30), TimeSpan.FromSeconds(5));
        result.Response.AccessToken.Should().Be("fake_jwt_access_token");
        result.Response.TokenType.Should().Be("Bearer");
        result.Response.ExpiresIn.Should().Be(900);
        result.Response.IdleTimeoutMinutes.Should().Be(30);
        result.Response.AbsoluteExpiresAt.Should().BeCloseTo(before.AddHours(24), TimeSpan.FromSeconds(5));
        result.Response.User.Id.Should().Be(user.Id);
        result.Response.User.Code.Should().Be("MGR001");
        result.Response.User.PrimaryRole.Should().Be("Manager");
        result.Response.User.Roles.Should().ContainSingle("Manager");

        _refreshRepoMock.Verify(r => r.CreateAsync(It.Is<RefreshToken>(t =>
            t.UserId == user.Id &&
            t.TokenHash == "hash_refresh_token" &&
            t.CreatedByIp == clientIp &&
            t.ExpiresAt >= before.AddMinutes(29) &&
            t.ExpiresAt <= before.AddMinutes(31) &&
            t.AbsoluteExpiresAt >= before.AddHours(23).AddMinutes(59) &&
            t.AbsoluteExpiresAt <= before.AddHours(24).AddMinutes(1))), Times.Once);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_UserNotFound_CallsVerifyDummyAndThrowsUnauthorizedAccessException()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "notfound@fpt.edu.vn", Password = "AnyPassword" };
        const string clientIp = "192.168.1.10";

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync((User?)null);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tên đăng nhập hoặc mật khẩu không chính xác.");
        _hasherMock.Verify(h => h.VerifyDummy(), Times.Once);
        _hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "WrongPassword" };
        const string clientIp = "192.168.1.10";

        var user = new User
        {
            Id = 1,
            Email = "manager@fpt.edu.vn",
            PasswordHash = "hashed_password",
            AccountStatus = "Active"
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash))
                   .Returns(false);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tên đăng nhập hoặc mật khẩu không chính xác.");
        _jwtGenMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LoginWithCredentialsAsync_NullOrEmptyPasswordHash_ThrowsUnauthorizedAccessException(string? passwordHash)
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "user@fpt.edu.vn", Password = "Password123" };
        const string clientIp = "192.168.1.10";

        var user = new User
        {
            Id = 2,
            Email = "user@fpt.edu.vn",
            PasswordHash = passwordHash,
            AccountStatus = "Active"
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync(user);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tên đăng nhập hoặc mật khẩu không chính xác.");
        _hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("Inactive")]
    [InlineData("Locked")]
    [InlineData("Banned")]
    public async Task LoginWithCredentialsAsync_AccountInactiveOrLocked_ThrowsUnauthorizedAccessException(string status)
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "staff@fpt.edu.vn", Password = "Password123" };
        const string clientIp = "192.168.1.10";

        var user = new User
        {
            Id = 3,
            Email = "staff@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = status,
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 3,
                    RoleId = 3,
                    Role = new Role { Id = 3, RoleName = "Guard" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash))
                   .Returns(true);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tài khoản của bạn chưa được kích hoạt hoặc đã bị khóa.");
        _jwtGenMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_StudentOnlyRole_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "student@fpt.edu.vn", Password = "Password123" };
        const string clientIp = "192.168.1.10";

        var user = new User
        {
            Id = 4,
            Email = "student@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = "Active",
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 4,
                    RoleId = 4,
                    Role = new Role { Id = 4, RoleName = "Student" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash))
                   .Returns(true);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Tài khoản sinh viên vui lòng đăng nhập bằng Google.");
        _jwtGenMock.Verify(j => j.GenerateAccessToken(It.IsAny<User>(), It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_MultiRoleIncludingStudentAndStaff_AllowsLoginAndSetsManagerRole()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "admin@fpt.edu.vn", Password = "Password123" };
        const string clientIp = "192.168.1.10";

        var user = new User
        {
            Id = 5,
            Code = "ADM001",
            FullName = "Admin User",
            Email = "admin@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = "Active",
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 5,
                    RoleId = 4,
                    Role = new Role { Id = 4, RoleName = "Student" }
                },
                new UserRole
                {
                    UserId = 5,
                    RoleId = 1,
                    Role = new Role { Id = 1, RoleName = "Admin" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account))
                     .ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash))
                   .Returns(true);
        _jwtGenMock.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>()))
                   .Returns("fake_admin_jwt");
        _jwtGenMock.Setup(j => j.GenerateRefreshToken())
                   .Returns(("raw_admin_refresh", "hash_admin_refresh"));
        _refreshRepoMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>()))
                        .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.LoginWithCredentialsAsync(dto, clientIp);

        // Assert
        result.Should().NotBeNull();
        result.Response.User.PrimaryRole.Should().Be("Admin");
        result.Response.User.Roles.Should().BeEquivalentTo(new[] { "Student", "Admin" });
        _jwtGenMock.Verify(j => j.GenerateAccessToken(user, It.Is<IEnumerable<string>>(roles => roles.Contains("Admin") && roles.Contains("Student"))), Times.Once);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenUnsignedOrForgedJwtProvided_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        const string clientId = "test-google-client-id.apps.googleusercontent.com";
        _configMock.Setup(c => c["Google:ClientId"]).Returns(clientId);

        static string Base64UrlEncode(string input) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(input))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        var header = Base64UrlEncode("{\"alg\":\"RS256\",\"typ\":\"JWT\",\"kid\":\"forged\"}");
        var payload = Base64UrlEncode(
            $"{{\"iss\":\"https://accounts.google.com\",\"aud\":\"{clientId}\",\"sub\":\"forged-sub-123\",\"email\":\"admin@fpt.edu.vn\",\"email_verified\":true,\"exp\":4102444800}}");
        var fakeSignature = Base64UrlEncode("forged-invalid-signature");
        var forgedJwt = $"{header}.{payload}.{fakeSignature}";

        var dto = new GoogleLoginRequestDto { IdToken = forgedJwt };

        // Act
        var act = () => _authService.LoginWithGoogleAsync(dto, "192.168.1.10");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Xác thực Google không hợp lệ hoặc đã hết hạn.");
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        _userRepoMock.Verify(r => r.GetByGoogleIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenGoogleClientIdMissing_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _configMock.Setup(c => c["Google:ClientId"]).Returns(string.Empty);
        var dto = new GoogleLoginRequestDto { IdToken = "header.payload.signature" };

        // Act
        var act = () => _authService.LoginWithGoogleAsync(dto, "192.168.1.10");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Xác thực Google chưa được cấu hình hợp lệ.");
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        _userRepoMock.Verify(r => r.GetByGoogleIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithGoogleAsync_WhenTokenEmpty_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        _configMock.Setup(c => c["Google:ClientId"]).Returns("test-google-client-id.apps.googleusercontent.com");
        var dto = new GoogleLoginRequestDto { IdToken = string.Empty };

        // Act
        var act = () => _authService.LoginWithGoogleAsync(dto, "192.168.1.10");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Xác thực Google không hợp lệ hoặc đã hết hạn.");
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        _userRepoMock.Verify(r => r.GetByGoogleIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenAccountStatusSuspended_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            Id = 101,
            Code = "HE170101",
            FullName = "Suspended Student",
            Email = "suspended@fpt.edu.vn",
            AccountStatus = AccountConstants.Statuses.Suspended,
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 101,
                    RoleId = 4,
                    Role = new Role { Id = 4, RoleName = "Student" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(101)).ReturnsAsync(user);

        // Act
        var act = () => _authService.GetCurrentUserAsync(101);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Người dùng không tồn tại hoặc đã bị khóa.");
    }

    [Fact]
    public async Task GetCurrentUserAsync_WhenAccountStatusPending_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var user = new User
        {
            Id = 102,
            Code = "HE170102",
            FullName = "Pending Student",
            Email = "pending@fpt.edu.vn",
            AccountStatus = AccountConstants.Statuses.Pending,
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 102,
                    RoleId = 4,
                    Role = new Role { Id = 4, RoleName = "Student" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(102)).ReturnsAsync(user);

        // Act
        var act = () => _authService.GetCurrentUserAsync(102);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
                 .WithMessage("Người dùng không tồn tại hoặc đã bị khóa.");
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_WhenWrongPassword_IncrementsFailedLoginCount()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "WrongPassword" };
        var user = new User
        {
            Id = 201,
            Email = "manager@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = "Active",
            FailedLoginCount = 1
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash)).Returns(false);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepoMock.Verify(r => r.AtomicIncrementFailedLoginAsync(201, null), Times.Once);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_OnFifthFailedAttempt_SetsLockoutEnd()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "WrongPassword" };
        var user = new User
        {
            Id = 202,
            Email = "manager@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = "Active",
            FailedLoginCount = 4
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash)).Returns(false);

        var before = DateTime.UtcNow;

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _userRepoMock.Verify(
            r => r.AtomicIncrementFailedLoginAsync(
                202,
                It.Is<DateTime?>(d => d.HasValue && d.Value >= before.AddMinutes(14) && d.Value <= before.AddMinutes(16))),
            Times.Once);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_WhenAccountLockedOut_ThrowsInvalidOperationExceptionWithoutVerifyingPassword()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "ValidPassword123" };
        var user = new User
        {
            Id = 203,
            Email = "manager@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = "Active",
            FailedLoginCount = 5,
            LockoutEnd = DateTime.UtcNow.AddMinutes(10)
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account)).ReturnsAsync(user);

        // Act
        var act = () => _authService.LoginWithCredentialsAsync(dto, "127.0.0.1");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*tạm thời bị khóa*");
        _hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginWithCredentialsAsync_WhenValidPasswordAfterFailures_ResetsFailedLoginCount()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "ValidPassword123" };
        var user = new User
        {
            Id = 204,
            Code = "MGR204",
            FullName = "Manager",
            Email = "manager@fpt.edu.vn",
            PasswordHash = "valid_hash",
            AccountStatus = "Active",
            FailedLoginCount = 3,
            UserRoleUsers = new List<UserRole>
            {
                new UserRole
                {
                    UserId = 204,
                    RoleId = 2,
                    Role = new Role { Id = 2, RoleName = "Manager" }
                }
            }
        };

        _userRepoMock.Setup(r => r.GetByAccountAsync(dto.Account)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(dto.Password, user.PasswordHash)).Returns(true);
        _jwtGenMock.Setup(j => j.GenerateAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("jwt_token");
        _jwtGenMock.Setup(j => j.GenerateRefreshToken()).Returns(("raw_ref", "hash_ref"));
        _refreshRepoMock.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        // Act
        await _authService.LoginWithCredentialsAsync(dto, "127.0.0.1");

        // Assert
        _userRepoMock.Verify(r => r.AtomicResetFailedLoginAsync(204), Times.Once);
    }
}
