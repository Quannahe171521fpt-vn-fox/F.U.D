using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers;
using FUD.DTOs.Auth;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.Login;

public class AuthControllerLoginTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly AuthController _controller;
    private readonly DefaultHttpContext _httpContext;

    public AuthControllerLoginTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _httpContext = new DefaultHttpContext();
        _httpContext.Request.Host = new HostString("localhost");
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        _controller = new AuthController(_authServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkResultWithAuthResponseAndSetsCookie()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "manager@fpt.edu.vn", Password = "Password123" };
        var authResponse = new AuthResponseDto
        {
            AccessToken = "valid_access_token",
            TokenType = "Bearer",
            ExpiresIn = 900,
            User = new UserInfoDto
            {
                Id = 1,
                Code = "MGR001",
                FullName = "Manager",
                Email = "manager@fpt.edu.vn",
                PrimaryRole = "Manager",
                Roles = new List<string> { "Manager" },
                AccountStatus = "Active"
            }
        };
        const string rawRefreshToken = "test_raw_refresh_token";

        _authServiceMock.Setup(s => s.LoginWithCredentialsAsync(dto, "127.0.0.1"))
                        .ReturnsAsync((authResponse, rawRefreshToken));

        // Act
        var result = await _controller.Login(dto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var apiResponse = okResult.Value as ApiResponse<AuthResponseDto>;
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeTrue();
        apiResponse.Message.Should().Be("Đăng nhập thành công");
        apiResponse.Data!.AccessToken.Should().Be("valid_access_token");

        // Verify Set-Cookie header contains fud_refresh_token
        _httpContext.Response.Headers.Should().ContainKey("Set-Cookie");
        var setCookieHeader = _httpContext.Response.Headers["Set-Cookie"].ToString();
        setCookieHeader.Should().Contain("fud_refresh_token=" + rawRefreshToken);
    }

    [Fact]
    public async Task Login_UnauthorizedCredentials_ReturnsUnauthorizedResultWithErrorMessage()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "wrong@fpt.edu.vn", Password = "WrongPassword" };
        const string expectedMessage = "Tên đăng nhập hoặc mật khẩu không chính xác.";

        _authServiceMock.Setup(s => s.LoginWithCredentialsAsync(dto, "127.0.0.1"))
                        .ThrowsAsync(new UnauthorizedAccessException(expectedMessage));

        // Act
        var result = await _controller.Login(dto);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        var unauthorizedResult = result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        var apiResponse = unauthorizedResult.Value as ApiResponse;
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public async Task Login_TooManyRequests_ReturnsStatusCode429WithErrorMessage()
    {
        // Arrange
        var dto = new LoginRequestDto { Account = "locked@fpt.edu.vn", Password = "Password123" };
        const string rateLimitMessage = "Tài khoản tạm thời bị khóa do nhập sai nhiều lần.";

        _authServiceMock.Setup(s => s.LoginWithCredentialsAsync(dto, "127.0.0.1"))
                        .ThrowsAsync(new InvalidOperationException(rateLimitMessage));

        // Act
        var result = await _controller.Login(dto);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);

        var apiResponse = objectResult.Value as ApiResponse;
        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Be(rateLimitMessage);
    }
}
