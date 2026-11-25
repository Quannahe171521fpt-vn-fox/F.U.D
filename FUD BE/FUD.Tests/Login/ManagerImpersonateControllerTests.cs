using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers;
using FUD.DTOs.Auth;
using FUD.Infrastructure.Auth;
using FUD.Models;
using FUD.Services;
using FUD.Services.Interfaces;
using FUD.Settings.pattern;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FUD.Tests.Login;

/// <summary>
/// Unit tests cho ManagerImpersonateController (Tầng Controller - Đăng nhập hộ sinh viên)
/// và JwtTokenGenerator.GenerateImpersonationToken.
/// Tuân thủ quy tắc AAA (Arrange - Act - Assert) và chuẩn kiểm thử chuyên nghiệp.
/// </summary>
public class ManagerImpersonateControllerTests
{
    private readonly Mock<IManagerImpersonateService> _impersonateServiceMock = new();

    private ManagerImpersonateController CreateSut(
        int? managerId = 10,
        string claimType = ClaimTypes.NameIdentifier,
        string? rawClaimValue = null,
        bool isImpersonating = false,
        string remoteIp = "192.168.1.50",
        string userAgent = "Chrome/130")
    {
        var controller = new ManagerImpersonateController(_impersonateServiceMock.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, AccountConstants.Roles.Manager)
        };

        if (isImpersonating)
        {
            claims.Add(new Claim("imp", "true"));
        }

        if (rawClaimValue != null)
        {
            claims.Add(new Claim(claimType, rawClaimValue));
        }
        else if (managerId.HasValue)
        {
            claims.Add(new Claim(claimType, managerId.Value.ToString()));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        httpContext.Request.Headers.UserAgent = userAgent;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    #region LookupStudent Tests

    [Fact]
    public async Task LookupStudent_WhenFound_Returns200OkWithStudentLookupDto()
    {
        // Arrange
        var expectedDto = new StudentLookupDto
        {
            Id = 100,
            Code = "HE170100",
            FullName = "Nguyễn Văn Sinh Viên",
            Email = "sv100@fpt.edu.vn",
            AccountStatus = AccountConstants.Statuses.Active,
            PrimaryRole = AccountConstants.Roles.Student,
            Roles = new List<string> { AccountConstants.Roles.Student },
            CurrentResidency = new ImpersonationResidencyDto
            {
                Id = 50,
                Status = "Active",
                DormName = "Dorm A",
                BedCode = "A1-101L-1"
            }
        };

        _impersonateServiceMock.Setup(s => s.LookupStudentAsync("HE170100"))
            .ReturnsAsync(expectedDto);
        var sut = CreateSut();

        // Act
        var result = await sut.LookupStudent("HE170100");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = okResult.Value.Should().BeOfType<ApiResponse<StudentLookupDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Tìm thấy thông tin sinh viên");
        response.Data.Should().BeEquivalentTo(expectedDto);
    }

    [Fact]
    public async Task LookupStudent_WhenServiceThrowsArgumentException_Returns400BadRequest()
    {
        // Arrange
        _impersonateServiceMock.Setup(s => s.LookupStudentAsync("a"))
            .ThrowsAsync(new ArgumentException("Vui lòng nhập tối thiểu 2 ký tự mã sinh viên hoặc email."));
        var sut = CreateSut();

        // Act
        var result = await sut.LookupStudent("a");

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var response = badRequest.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("tối thiểu 2 ký tự");
    }

    [Fact]
    public async Task LookupStudent_WhenServiceThrowsKeyNotFoundException_Returns404NotFound()
    {
        // Arrange
        _impersonateServiceMock.Setup(s => s.LookupStudentAsync("UNKNOWN"))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy sinh viên nào với email hoặc mã số này trong hệ thống."));
        var sut = CreateSut();

        // Act
        var result = await sut.LookupStudent("UNKNOWN");

        // Assert
        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var response = notFound.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Không tìm thấy sinh viên");
    }

    #endregion

    #region Impersonate Tests

    [Fact]
    public async Task Impersonate_WhenCallerHasImpTrueClaim_Returns403ForbiddenWithoutCallingService()
    {
        // Arrange
        var sut = CreateSut(managerId: 10, isImpersonating: true);

        // Act
        var result = await sut.Impersonate(100);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var response = objResult.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("đang đăng nhập hộ");
        _impersonateServiceMock.Verify(
            s => s.ImpersonateStudentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Theory]
    [InlineData(ClaimTypes.NameIdentifier, null)]
    [InlineData(ClaimTypes.NameIdentifier, "not-a-number")]
    public async Task Impersonate_WhenManagerClaimMissingOrInvalid_Returns401Unauthorized(string claimType, string? claimValue)
    {
        // Arrange
        var sut = CreateSut(managerId: null, claimType: claimType, rawClaimValue: claimValue);

        // Act
        var result = await sut.Impersonate(100);

        // Assert
        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var response = unauthorized.Value.Should().BeOfType<ApiResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Token quản lý không hợp lệ");
        _impersonateServiceMock.Verify(
            s => s.ImpersonateStudentAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("nameid")]
    public async Task Impersonate_WhenManagerIdInFallbackClaim_CallsServiceAndReturns200Ok(string fallbackClaimType)
    {
        // Arrange
        var expectedResponse = new ImpersonationResponseDto
        {
            ImpersonationToken = "imp-token-fallback",
            ExpiresIn = 1800,
            Student = new ImpersonatedStudentDto { Id = 110, Code = "HE170110" }
        };

        _impersonateServiceMock
            .Setup(s => s.ImpersonateStudentAsync(25, 110, false, "192.168.1.50", "Chrome/130"))
            .ReturnsAsync(expectedResponse);

        var sut = CreateSut(managerId: 25, claimType: fallbackClaimType);

        // Act
        var result = await sut.Impersonate(110);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = okResult.Value.Should().BeOfType<ApiResponse<ImpersonationResponseDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().BeEquivalentTo(expectedResponse);
        _impersonateServiceMock.Verify(
            s => s.ImpersonateStudentAsync(25, 110, false, "192.168.1.50", "Chrome/130"),
            Times.Once);
    }

    [Fact]
    public async Task Impersonate_WhenServiceThrowsForbiddenException_Returns403Forbidden()
    {
        // Arrange
        _impersonateServiceMock
            .Setup(s => s.ImpersonateStudentAsync(10, 50, false, It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new ForbiddenException("Chỉ được đăng nhập hộ tài khoản có duy nhất vai trò Sinh viên."));
        var sut = CreateSut(managerId: 10);

        // Act
        var result = await sut.Impersonate(50);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Impersonate_WhenServiceThrowsKeyNotFoundException_Returns404NotFound()
    {
        // Arrange
        _impersonateServiceMock
            .Setup(s => s.ImpersonateStudentAsync(10, 999, false, It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new KeyNotFoundException("Sinh viên không tồn tại hoặc không còn hoạt động trên hệ thống."));
        var sut = CreateSut(managerId: 10);

        // Act
        var result = await sut.Impersonate(999);

        // Assert
        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Impersonate_WhenServiceThrowsInvalidOperationException_Returns400BadRequest()
    {
        // Arrange
        _impersonateServiceMock
            .Setup(s => s.ImpersonateStudentAsync(10, 113, false, It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Tài khoản sinh viên đang ở trạng thái Suspended, không thể đăng nhập hộ."));
        var sut = CreateSut(managerId: 10);

        // Act
        var result = await sut.Impersonate(113);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Impersonate_WhenServiceThrowsAuditLogWriteException_Returns500InternalServerError()
    {
        // Arrange
        _impersonateServiceMock
            .Setup(s => s.ImpersonateStudentAsync(10, 51, false, It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new AuditLogWriteException("Không thể ghi nhận nhật ký kiểm toán cho phiên đăng nhập hộ.", new Exception("DB error")));
        var sut = CreateSut(managerId: 10);

        // Act
        var result = await sut.Impersonate(51);

        // Assert
        var objResult = result.Should().BeOfType<ObjectResult>().Subject;
        objResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Impersonate_WhenValidRequest_Returns200OkWithImpersonationResponseDto()
    {
        // Arrange
        var expected = new ImpersonationResponseDto
        {
            ImpersonationToken = "jwt-imp-120",
            ExpiresIn = 1800,
            Student = new ImpersonatedStudentDto
            {
                Id = 120,
                Code = "HE170120",
                FullName = "Nguyễn Văn Sinh Viên",
                DormId = 1,
                DormName = "Dorm A"
            }
        };

        _impersonateServiceMock
            .Setup(s => s.ImpersonateStudentAsync(10, 120, false, "192.168.1.50", "Chrome/130"))
            .ReturnsAsync(expected);

        var sut = CreateSut(managerId: 10);

        // Act
        var result = await sut.Impersonate(120);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var response = okResult.Value.Should().BeOfType<ApiResponse<ImpersonationResponseDto>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Tạo phiên đăng nhập hộ sinh viên thành công");
        response.Data.Should().BeEquivalentTo(expected);
    }

    #endregion

    #region JwtTokenGenerator Impersonation Token Tests

    [Fact]
    public void GenerateImpersonationToken_WhenCalled_EmbedsAllRequiredStudentAndManagerClaims()
    {
        // Arrange
        var jwtOptions = Options.Create(new JwtSettings
        {
            Key = "unit_test_secret_key_for_impersonation_at_least_32_bytes",
            Issuer = "FUD-Test-Issuer",
            Audience = "FUD-Test-Audience"
        });
        var sut = new JwtTokenGenerator(jwtOptions);
        var student = new User
        {
            Id = 200,
            Code = "HE170200",
            FullName = "Phạm Minh Tuấn",
            Email = "tuanpm@fpt.edu.vn"
        };

        // Act
        var rawToken = sut.GenerateImpersonationToken(student, new[] { "Student" }, managerId: 15);

        // Assert
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
        jwt.Issuer.Should().Be("FUD-Test-Issuer");
        jwt.Audiences.Should().Contain("FUD-Test-Audience");
        jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value.Should().Be("200");
        jwt.Claims.First(c => c.Type == "sub").Value.Should().Be("200");
        jwt.Claims.First(c => c.Type == "nameid").Value.Should().Be("200");
        jwt.Claims.First(c => c.Type == "user_code").Value.Should().Be("HE170200");
        jwt.Claims.First(c => c.Type == "email").Value.Should().Be("tuanpm@fpt.edu.vn");
        jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value.Should().Be("Phạm Minh Tuấn");
        jwt.Claims.First(c => c.Type == "impersonated_by").Value.Should().Be("15");
        jwt.Claims.First(c => c.Type == "imp").Value.Should().Be("true");
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().ContainSingle().Which.Should().Be("Student");
    }

    #endregion
}
