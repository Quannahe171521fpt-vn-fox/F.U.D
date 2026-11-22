using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.Controllers;
using FUD.DTOs.Notifications;
using FUD.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FUD.Tests.Notifications;

public class NotificationsControllerTests
{
    private readonly Mock<IInAppNotificationService> _serviceMock = new();

    private NotificationsController CreateSut(int? userId = 10, string role = "Student")
    {
        var controller = new NotificationsController(_serviceMock.Object);
        var httpContext = new DefaultHttpContext();

        if (userId.HasValue)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.Value.ToString()),
                new(ClaimTypes.Role, role)
            };
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    [Fact]
    public async Task GetMyNotifications_WithoutValidUserClaim_Returns401Unauthorized()
    {
        var sut = CreateSut(userId: null);

        var result = await sut.GetMyNotifications();

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorized.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var body = unauthorized.Value.Should().BeOfType<ApiResponse>().Subject;
        body.Success.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyNotifications_WithValidUser_Returns200Ok()
    {
        var expected = new NotificationListResponseDto
        {
            Items = new List<NotificationItemDto> { new() { Id = 1, Title = "Test" } },
            UnreadCount = 1,
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };
        _serviceMock.Setup(s => s.GetMyNotificationsAsync(10, false, 1, 20, null, null, null))
            .ReturnsAsync(expected);

        var sut = CreateSut(10);
        var result = await sut.GetMyNotifications();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<NotificationListResponseDto>>().Subject;
        body.Success.Should().BeTrue();
        body.Data!.UnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMyNotifications_ForwardsFilterAndSearchQueryParameters()
    {
        var expected = new NotificationListResponseDto { Page = 2, PageSize = 10, TotalCount = 15, UnreadCount = 4 };
        _serviceMock.Setup(s => s.GetMyNotificationsAsync(10, false, 2, 10, "read", "Booking", "phòng"))
            .ReturnsAsync(expected);

        var sut = CreateSut(10, "Manager");
        var result = await sut.GetMyNotifications(
            unreadOnly: false,
            page: 2,
            pageSize: 10,
            readStatus: "read",
            referenceType: "Booking",
            search: "phòng");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<NotificationListResponseDto>>().Subject;
        body.Data!.Page.Should().Be(2);
        _serviceMock.Verify(
            s => s.GetMyNotificationsAsync(10, false, 2, 10, "read", "Booking", "phòng"),
            Times.Once);
    }

    [Fact]
    public async Task GetUnreadCount_WithValidUser_Returns200Ok()
    {
        _serviceMock.Setup(s => s.GetUnreadCountAsync(10))
            .ReturnsAsync(new UnreadCountResponseDto { UnreadCount = 5 });

        var sut = CreateSut(10, "Manager");
        var result = await sut.GetUnreadCount();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<UnreadCountResponseDto>>().Subject;
        body.Success.Should().BeTrue();
        body.Data!.UnreadCount.Should().Be(5);
    }

    [Fact]
    public async Task MarkAsRead_WhenValid_Returns200Ok()
    {
        _serviceMock.Setup(s => s.MarkAsReadAsync(10, 7))
            .ReturnsAsync(new MarkNotificationReadResponseDto { Id = 7, ReadFlag = true, UnreadCount = 2 });

        var sut = CreateSut(10, "Guard");
        var result = await sut.MarkAsRead(7);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<MarkNotificationReadResponseDto>>().Subject;
        body.Success.Should().BeTrue();
        body.Data!.Id.Should().Be(7);
        body.Data.UnreadCount.Should().Be(2);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotFound_Returns404NotFound()
    {
        _serviceMock.Setup(s => s.MarkAsReadAsync(10, 999))
            .ThrowsAsync(new KeyNotFoundException("Không tìm thấy thông báo."));

        var sut = CreateSut(10);
        var result = await sut.MarkAsRead(999);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var body = notFound.Value.Should().BeOfType<ApiResponse>().Subject;
        body.Success.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllAsRead_WithValidUser_Returns200Ok()
    {
        _serviceMock.Setup(s => s.MarkAllAsReadAsync(10))
            .ReturnsAsync(new MarkAllReadResponseDto { MarkedCount = 3, UnreadCount = 0 });

        var sut = CreateSut(10);
        var result = await sut.MarkAllAsRead();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = ok.Value.Should().BeOfType<ApiResponse<MarkAllReadResponseDto>>().Subject;
        body.Success.Should().BeTrue();
        body.Data!.MarkedCount.Should().Be(3);
        body.Data.UnreadCount.Should().Be(0);
    }
}
