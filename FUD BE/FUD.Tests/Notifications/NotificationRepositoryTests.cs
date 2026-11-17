using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Models;
using FUD.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FUD.Tests.Notifications;

public class NotificationRepositoryTests
{
    private static FudContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FudContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FudContext(options);
    }

    private static User CreateUser(int id, string code, string status = "Active", DateTime? deletedAt = null) => new()
    {
        Id = id,
        Code = code,
        FullName = $"User {code}",
        Email = $"{code.ToLowerInvariant()}@fpt.edu.vn",
        Gender = "Male",
        AccountStatus = status,
        CreatedAt = DateTime.UtcNow,
        DeletedAt = deletedAt
    };

    [Fact]
    public async Task GetUserNotificationsAsync_FiltersByUserAndInAppChannelAndOrdersNewestFirst()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var baseTime = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "Old", SentTime = baseTime.AddMinutes(-10), ReadFlag = true },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "Middle", SentTime = baseTime, ReadFlag = false },
            new Notification { Id = 3, UserId = 10, Channel = "InApp", Title = "Newest", SentTime = baseTime, ReadFlag = false },
            new Notification { Id = 4, UserId = 10, Channel = "Email", Title = "EmailOnly", SentTime = baseTime.AddMinutes(5), ReadFlag = false },
            new Notification { Id = 5, UserId = 20, Channel = "InApp", Title = "OtherUser", SentTime = baseTime.AddMinutes(5), ReadFlag = false }
        );
        await context.SaveChangesAsync();

        var (items, totalCount, unreadCount) = await repo.GetUserNotificationsAsync(10, unreadOnly: false, page: 1, pageSize: 2);

        totalCount.Should().Be(3);
        unreadCount.Should().Be(2);
        items.Should().HaveCount(2);
        items[0].Id.Should().Be(3);
        items[1].Id.Should().Be(2);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_WhenUnreadOnly_FiltersItemsButKeepsTotalUnreadCount()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var now = DateTime.UtcNow;

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "ReadOne", SentTime = now, ReadFlag = true },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "UnreadOne", SentTime = now.AddMinutes(1), ReadFlag = false }
        );
        await context.SaveChangesAsync();

        var (items, totalCount, unreadCount) = await repo.GetUserNotificationsAsync(10, unreadOnly: true, page: 1, pageSize: 10);

        totalCount.Should().Be(1);
        unreadCount.Should().Be(1);
        items.Should().ContainSingle(i => i.Id == 2);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_FiltersByReadStatusAndPreservesGlobalUnreadCount()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var now = DateTime.UtcNow;

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "ReadOne", SentTime = now, ReadFlag = true },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "UnreadOne", SentTime = now.AddMinutes(1), ReadFlag = false },
            new Notification { Id = 3, UserId = 10, Channel = "InApp", Title = "UnreadTwo", SentTime = now.AddMinutes(2), ReadFlag = false }
        );
        await context.SaveChangesAsync();

        var (readItems, readTotal, readUnread) = await repo.GetUserNotificationsAsync(
            10, unreadOnly: true, page: 1, pageSize: 10, readStatus: "read");

        readTotal.Should().Be(1);
        readUnread.Should().Be(2);
        readItems.Should().ContainSingle(i => i.Id == 1);

        var (allItems, allTotal, _) = await repo.GetUserNotificationsAsync(
            10, unreadOnly: true, page: 1, pageSize: 10, readStatus: "all");

        allTotal.Should().Be(3);
        allItems.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_FiltersByReferenceTypeCaseInsensitivelyWithoutChangingUnreadCount()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var now = DateTime.UtcNow;

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "Book1", ReferenceType = "Booking", SentTime = now, ReadFlag = false },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "Req1", ReferenceType = "Request", SentTime = now.AddMinutes(1), ReadFlag = false },
            new Notification { Id = 3, UserId = 10, Channel = "InApp", Title = "Book2", ReferenceType = "Booking", SentTime = now.AddMinutes(2), ReadFlag = true }
        );
        await context.SaveChangesAsync();

        var (items, totalCount, unreadCount) = await repo.GetUserNotificationsAsync(
            10, unreadOnly: false, page: 1, pageSize: 10, readStatus: "all", referenceType: "booking");

        totalCount.Should().Be(2);
        unreadCount.Should().Be(2);
        items.Select(i => i.Id).Should().Equal(3, 1);

        var (_, allTypesTotal, _) = await repo.GetUserNotificationsAsync(
            10, unreadOnly: false, page: 1, pageSize: 10, readStatus: "all", referenceType: "all");
        allTypesTotal.Should().Be(3);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_FiltersBySearchAcrossTitleAndNullableContentWithPagination()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var now = DateTime.UtcNow;

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "Hóa đơn phòng 101", Content = null, ReferenceType = "Invoice", SentTime = now.AddMinutes(-3), ReadFlag = true },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "Nhắc nhở", Content = "Vui lòng thanh toán HÓA ĐƠN kỳ mới", ReferenceType = "Invoice", SentTime = now.AddMinutes(-2), ReadFlag = false },
            new Notification { Id = 3, UserId = 10, Channel = "InApp", Title = "Hóa đơn điện nước", Content = "Tháng 11", ReferenceType = "Utility", SentTime = now.AddMinutes(-1), ReadFlag = true },
            new Notification { Id = 4, UserId = 10, Channel = "InApp", Title = "Tin tức chung", Content = "Thông báo nghỉ lễ", ReferenceType = "News", SentTime = now, ReadFlag = false }
        );
        await context.SaveChangesAsync();

        var (searchItems, searchTotal, searchUnread) = await repo.GetUserNotificationsAsync(
            10, unreadOnly: false, page: 1, pageSize: 10, readStatus: "all", referenceType: null, search: "  hóa đơn ");

        searchTotal.Should().Be(3);
        searchUnread.Should().Be(2);
        searchItems.Select(i => i.Id).Should().Equal(3, 2, 1);

        var (combinedItems, combinedTotal, combinedUnread) = await repo.GetUserNotificationsAsync(
            10, unreadOnly: false, page: 1, pageSize: 1, readStatus: "read", referenceType: "Invoice", search: "hóa đơn");

        combinedTotal.Should().Be(1);
        combinedUnread.Should().Be(2);
        combinedItems.Should().ContainSingle(i => i.Id == 1);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenOwned_UpdatesReadFlagAndReturnsRemainingUnreadCount()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var readAt = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "N1", SentTime = readAt.AddHours(-2), ReadFlag = false },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "N2", SentTime = readAt.AddHours(-1), ReadFlag = false }
        );
        await context.SaveChangesAsync();

        var (entity, unreadCount) = await repo.MarkAsReadAsync(1, 10, readAt);

        entity.Should().NotBeNull();
        entity!.ReadFlag.Should().BeTrue();
        entity.ReadTime.Should().Be(readAt);
        unreadCount.Should().Be(1);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenAlreadyRead_IsIdempotentAndPreservesReadTime()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var originalReadTime = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);

        context.Notifications.Add(new Notification
        {
            Id = 1,
            UserId = 10,
            Channel = "InApp",
            Title = "N1",
            SentTime = originalReadTime.AddHours(-1),
            ReadFlag = true,
            ReadTime = originalReadTime
        });
        await context.SaveChangesAsync();

        var (entity, unreadCount) = await repo.MarkAsReadAsync(1, 10, originalReadTime.AddHours(2));

        entity.Should().NotBeNull();
        entity!.ReadTime.Should().Be(originalReadTime);
        unreadCount.Should().Be(0);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenOwnedByAnotherUser_ReturnsNullWithoutMutating()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);

        context.Notifications.Add(new Notification
        {
            Id = 7,
            UserId = 99,
            Channel = "InApp",
            Title = "Foreign",
            SentTime = DateTime.UtcNow,
            ReadFlag = false
        });
        await context.SaveChangesAsync();

        var (entity, _) = await repo.MarkAsReadAsync(7, userId: 10, DateTime.UtcNow);

        entity.Should().BeNull();
        var dbRow = await context.Notifications.FindAsync(7);
        dbRow!.ReadFlag.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_InMemoryFallback_MarksOnlyTargetUsersUnreadInAppItems()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);
        var now = DateTime.UtcNow;

        context.Notifications.AddRange(
            new Notification { Id = 1, UserId = 10, Channel = "InApp", Title = "A", SentTime = now, ReadFlag = false },
            new Notification { Id = 2, UserId = 10, Channel = "InApp", Title = "B", SentTime = now, ReadFlag = false },
            new Notification { Id = 3, UserId = 10, Channel = "Email", Title = "Email", SentTime = now, ReadFlag = false },
            new Notification { Id = 4, UserId = 20, Channel = "InApp", Title = "Other", SentTime = now, ReadFlag = false }
        );
        await context.SaveChangesAsync();

        var marked = await repo.MarkAllAsReadAsync(10, now);

        marked.Should().Be(2);
        (await repo.GetUnreadCountAsync(10)).Should().Be(0);
        (await repo.GetUnreadCountAsync(20)).Should().Be(1);
    }

    [Fact]
    public async Task GetActiveUserIdsByRolesAndDormGuards_FilterInactiveAndDeletedUsers()
    {
        await using var context = CreateInMemoryContext();
        var repo = new NotificationRepository(context);

        var mgrRole = new Role { Id = 1, RoleName = "Manager" };
        var guardRole = new Role { Id = 2, RoleName = "Guard" };
        var dorm1 = new Dorm { Id = 1, CampusId = 1, Code = "A", Name = "Dom A", Status = "Active" };
        var dorm2 = new Dorm { Id = 2, CampusId = 1, Code = "B", Name = "Dom B", Status = "Active" };

        var mgrActive = CreateUser(1, "MGR1", "Active");
        var mgrSuspended = CreateUser(2, "MGR2", "Suspended");
        var mgrDeleted = CreateUser(3, "MGR3", "Active", DateTime.UtcNow);
        var grdDorm1Active = CreateUser(4, "GRD1", "Active");
        var grdDorm1Suspended = CreateUser(5, "GRD2", "Suspended");
        var grdDorm2Active = CreateUser(6, "GRD3", "Active");

        context.Roles.AddRange(mgrRole, guardRole);
        context.Dorms.AddRange(dorm1, dorm2);
        context.Users.AddRange(mgrActive, mgrSuspended, mgrDeleted, grdDorm1Active, grdDorm1Suspended, grdDorm2Active);
        context.UserRoles.AddRange(
            new UserRole { UserId = 1, RoleId = 1, AssignedAt = DateTime.UtcNow },
            new UserRole { UserId = 2, RoleId = 1, AssignedAt = DateTime.UtcNow },
            new UserRole { UserId = 3, RoleId = 1, AssignedAt = DateTime.UtcNow },
            new UserRole { UserId = 4, RoleId = 2, AssignedAt = DateTime.UtcNow }
        );
        context.Guards.AddRange(
            new Guard { UserId = 4, DormId = 1 },
            new Guard { UserId = 5, DormId = 1 },
            new Guard { UserId = 6, DormId = 2 }
        );
        await context.SaveChangesAsync();

        var managerIds = await repo.GetActiveUserIdsByRolesAsync(new[] { "Manager" });
        var dorm1GuardIds = await repo.GetActiveGuardUserIdsByDormAsync(1);

        managerIds.Should().Equal(1);
        dorm1GuardIds.Should().Equal(4);
    }
}
