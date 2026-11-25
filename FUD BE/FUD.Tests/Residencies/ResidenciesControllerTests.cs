using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers;
using FUD.DTOs.Residencies;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.Residencies;

public class ResidenciesControllerTests
{
    private static ResidenciesController CreateController(
        Mock<IResidencyService> serviceMock,
        params Claim[] claims)
    {
        var identity = claims.Length > 0
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();

        return new ResidenciesController(serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    [Fact]
    public async Task GetMyHistory_WithValidNameIdentifierClaim_ForwardsQueryAndReturnsOkWithList()
    {
        var serviceMock = new Mock<IResidencyService>();
        var items = new List<ResidencyHistoryItemDto>
        {
            new() { Id = 1, ResidencyCode = "RES-0001", Status = "Active", TermName = "Fall" }
        };

        ResidencyHistoryQueryDto? capturedQuery = null;
        serviceMock
            .Setup(s => s.GetStudentResidencyHistoryAsync(10, It.IsAny<ResidencyHistoryQueryDto>()))
            .Callback<int, ResidencyHistoryQueryDto?>((_, q) => capturedQuery = q)
            .ReturnsAsync(items);

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "10"));

        var result = await controller.GetMyHistory(
            status: "Active",
            term: "Fall",
            registrationType: "Booking",
            search: "A101");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var api = ok.Value.Should().BeOfType<ApiResponse<List<ResidencyHistoryItemDto>>>().Subject;
        api.Success.Should().BeTrue();
        api.Data.Should().HaveCount(1);

        capturedQuery.Should().NotBeNull();
        capturedQuery!.Status.Should().Be("Active");
        capturedQuery.Term.Should().Be("Fall");
        capturedQuery.RegistrationType.Should().Be("Booking");
        capturedQuery.Search.Should().Be("A101");
    }

    [Fact]
    public async Task GetMyHistory_WithSubClaimFallback_ExtractsStudentIdAndReturnsOk()
    {
        var serviceMock = new Mock<IResidencyService>();
        serviceMock
            .Setup(s => s.GetStudentResidencyHistoryAsync(25, It.IsAny<ResidencyHistoryQueryDto>()))
            .ReturnsAsync(new List<ResidencyHistoryItemDto>());

        var controller = CreateController(serviceMock, new Claim("sub", "25"));

        var result = await controller.GetMyHistory();

        result.Should().BeOfType<OkObjectResult>();
        serviceMock.Verify(s => s.GetStudentResidencyHistoryAsync(25, It.IsAny<ResidencyHistoryQueryDto>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-an-int")]
    public async Task GetMyHistory_WhenUserIdClaimMissingOrInvalid_Returns401Unauthorized(string? claimValue)
    {
        var serviceMock = new Mock<IResidencyService>();
        var controller = claimValue == null
            ? CreateController(serviceMock)
            : CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, claimValue));

        var result = await controller.GetMyHistory();

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var api = unauthorized.Value.Should().BeOfType<ApiResponse>().Subject;
        api.Success.Should().BeFalse();
        api.Message.Should().Contain("Không xác định được danh tính sinh viên");
        serviceMock.Verify(s => s.GetStudentResidencyHistoryAsync(It.IsAny<int>(), It.IsAny<ResidencyHistoryQueryDto>()), Times.Never);
    }

    [Fact]
    public async Task GetMyHistory_WhenStudentHasNoHistory_ReturnsOkWithEmptyList()
    {
        var serviceMock = new Mock<IResidencyService>();
        serviceMock
            .Setup(s => s.GetStudentResidencyHistoryAsync(12, It.IsAny<ResidencyHistoryQueryDto>()))
            .ReturnsAsync(new List<ResidencyHistoryItemDto>());

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "12"));

        var result = await controller.GetMyHistory();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var api = ok.Value.Should().BeOfType<ApiResponse<List<ResidencyHistoryItemDto>>>().Subject;
        api.Success.Should().BeTrue();
        api.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyHistoryDetail_WhenResidencyExists_ReturnsOkWithDetail()
    {
        var serviceMock = new Mock<IResidencyService>();
        var detail = new ResidencyHistoryDetailDto
        {
            Id = 10,
            ResidencyCode = "RES-0010",
            StudentId = 10,
            Status = "Active"
        };
        serviceMock.Setup(s => s.GetStudentResidencyDetailAsync(10, 10)).ReturnsAsync(detail);

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "10"));

        var result = await controller.GetMyHistoryDetail(10);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        var api = ok.Value.Should().BeOfType<ApiResponse<ResidencyHistoryDetailDto>>().Subject;
        api.Success.Should().BeTrue();
        api.Data!.Id.Should().Be(10);
    }

    [Fact]
    public async Task GetMyHistoryDetail_WhenNotFoundOrNotOwned_Returns404NotFound()
    {
        var serviceMock = new Mock<IResidencyService>();
        serviceMock.Setup(s => s.GetStudentResidencyDetailAsync(10, 999)).ReturnsAsync((ResidencyHistoryDetailDto?)null);

        var controller = CreateController(serviceMock, new Claim(ClaimTypes.NameIdentifier, "10"));

        var result = await controller.GetMyHistoryDetail(999);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var api = notFound.Value.Should().BeOfType<ApiResponse>().Subject;
        api.Success.Should().BeFalse();
        api.Message.Should().Contain("Không tìm thấy hồ sơ lưu trú");
    }

    [Fact]
    public async Task GetMyHistoryDetail_WhenUserIdClaimMissing_Returns401Unauthorized()
    {
        var serviceMock = new Mock<IResidencyService>();
        var controller = CreateController(serviceMock);

        var result = await controller.GetMyHistoryDetail(10);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        serviceMock.Verify(s => s.GetStudentResidencyDetailAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void ResidenciesController_HasClassLevelAuthorizeAndExpectedRoute()
    {
        var authorizeAttr = typeof(ResidenciesController).GetCustomAttribute<AuthorizeAttribute>(inherit: false);
        var routeAttr = typeof(ResidenciesController).GetCustomAttribute<RouteAttribute>(inherit: false);

        authorizeAttr.Should().NotBeNull();
        routeAttr.Should().NotBeNull();
        routeAttr!.Template.Should().Be("api/residencies");
    }
}
