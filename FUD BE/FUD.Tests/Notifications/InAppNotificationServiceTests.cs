using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Notifications;

public class InAppNotificationServiceTests
{
    private readonly Mock<INotificationRepository> _repoMock = new();
    private readonly Mock<ILogger<InAppNotificationService>> _loggerMock = new();

    private InAppNotificationService CreateSut() => new(_repoMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetMyNotificationsAsync_NormalizesInvalidPageAndClampsPageSize()
    {
        _repoMock.Setup(r => r.GetUserNotificationsAsync(10, true, 1, 50, "unread", null, null))
            .ReturnsAsync((new List<Notification>(), 0, 0));

        var sut = CreateSut();
        var result = await sut.GetMyNotificationsAsync(10, unreadOnly: true, page: 0, pageSize: 200);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
        _repoMock.Verify(r => r.GetUserNotificationsAsync(10, true, 1, 50, "unread", null, null), Times.Once);
    }

    [Fact]
    public async Task GetMyNotificationsAsync_NormalizesReadStatusReferenceTypeAndSearch()
    {
        _repoMock.Setup(r => r.GetUserNotificationsAsync(
                10, It.IsAny<bool>(), 1, 20, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((new List<Notification>(), 0, 0));

        var sut = CreateSut();
        var overlongSearch = "  " + new string('x', 240) + "  ";

        await sut.GetMyNotificationsAsync(
            10,
            unreadOnly: true,
            page: 1,
            pageSize: 20,
            readStatus: " READ ",
            referenceType: " booking ",
            search: overlongSearch);

        _repoMock.Verify(r => r.GetUserNotificationsAsync(
            10,
            true,
            1,
            20,
            "read",
            "Booking",
            It.Is<string>(s => s.Length == 200)), Times.Once);

        await sut.GetMyNotificationsAsync(
            10,
            unreadOnly: false,
            page: 1,
            pageSize: 20,
            readStatus: "invalid",
            referenceType: " ALL ",
            search: "   ");

        _repoMock.Verify(r => r.GetUserNotificationsAsync(
            10,
            false,
            1,
            20,
            "all",
            null,
            null), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotFoundOrNotOwned_ThrowsKeyNotFoundException()
    {
        _repoMock.Setup(r => r.MarkAsReadAsync(99, 10, It.IsAny<DateTime>()))
            .ReturnsAsync(((Notification?)null, 0));

        var sut = CreateSut();

        var act = async () => await sut.MarkAsReadAsync(10, 99);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenValid_ReturnsDtoWithUpdatedUnreadCount()
    {
        var readTime = DateTime.UtcNow;
        var entity = new Notification
        {
            Id = 5,
            UserId = 10,
            Channel = "InApp",
            Title = "Test",
            ReadFlag = true,
            ReadTime = readTime,
            SentTime = readTime.AddMinutes(-10)
        };
        _repoMock.Setup(r => r.MarkAsReadAsync(5, 10, It.IsAny<DateTime>()))
            .ReturnsAsync((entity, 3));

        var sut = CreateSut();
        var result = await sut.MarkAsReadAsync(10, 5);

        result.Id.Should().Be(5);
        result.ReadFlag.Should().BeTrue();
        result.UnreadCount.Should().Be(3);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ReturnsMarkedCountAndZeroUnread()
    {
        _repoMock.Setup(r => r.MarkAllAsReadAsync(10, It.IsAny<DateTime>()))
            .ReturnsAsync(4);

        var sut = CreateSut();
        var result = await sut.MarkAllAsReadAsync(10);

        result.MarkedCount.Should().Be(4);
        result.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task NotifyUserAsync_TruncatesLongTitleAndUsesInAppUtcNow()
    {
        List<Notification>? captured = null;
        _repoMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()))
            .Callback<IEnumerable<Notification>>(items => captured = items.ToList())
            .Returns(Task.CompletedTask);

        var longTitle = new string('A', 240);
        var sut = CreateSut();

        await sut.NotifyUserAsync(15, longTitle, "Noi dung", "Booking", "123");

        captured.Should().NotBeNull();
        captured!.Should().ContainSingle();
        captured[0].UserId.Should().Be(15);
        captured[0].Channel.Should().Be("InApp");
        captured[0].Title.Should().HaveLength(200);
        captured[0].ReadFlag.Should().BeFalse();
        captured[0].ReferenceType.Should().Be("Booking");
        captured[0].ReferenceId.Should().Be("123");
    }

    [Fact]
    public async Task NotifyUserAsync_WhenRepositoryThrows_DoesNotPropagateException()
    {
        _repoMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var sut = CreateSut();

        var act = async () => await sut.NotifyUserAsync(15, "Title", "Body");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyRolesAsync_DeduplicatesUsersAndIgnoresEmptyRoles()
    {
        List<Notification>? captured = null;
        _repoMock.Setup(r => r.GetActiveUserIdsByRolesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<int> { 2, 2, 5, -1, 0 });
        _repoMock.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Notification>>()))
            .Callback<IEnumerable<Notification>>(items => captured = items.ToList())
            .Returns(Task.CompletedTask);

        var sut = CreateSut();

        await sut.NotifyRolesAsync(Array.Empty<string>(), "Skip", "Skip");
        _repoMock.Verify(r => r.GetActiveUserIdsByRolesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);

        await sut.NotifyRolesAsync(new[] { "Manager", "Manager" }, "Title", "Body");
        captured.Should().NotBeNull();
        captured!.Select(n => n.UserId).Should().Equal(2, 5);
    }
}
