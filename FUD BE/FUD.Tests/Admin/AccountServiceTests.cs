using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using FUD.Common;
using FUD.DTOs.Account;
using FUD.Infrastructure.Auth;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services.Admin;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FUD.Tests.Admin;

/// <summary>
/// Unit tests cho AccountService — tuân thủ quy tắc AAA (Arrange / Act / Assert).
/// Toàn bộ dependency được mock bằng Moq; không kết nối DB thực.
/// </summary>
public class AccountServiceTests
{
    // ─── Shared mocks ───────────────────────────────────────────────────────────
    private readonly Mock<IAccountRepository>        _accountRepoMock     = new();
    private readonly Mock<IAccountHistoryRepository> _historyRepoMock     = new();
    private readonly Mock<IRefreshTokenRepository>   _refreshTokenRepoMock= new();
    private readonly Mock<IPasswordHasher>           _passwordHasherMock  = new();
    private readonly Mock<INotificationService>      _notificationMock    = new();
    private readonly Mock<IMemoryCache>              _cacheMock           = new();
    private readonly Mock<ILogger<AccountService>>   _loggerMock          = new();

    private AccountService CreateSut() => new(
        _accountRepoMock.Object,
        _historyRepoMock.Object,
        _refreshTokenRepoMock.Object,
        _passwordHasherMock.Object,
        _notificationMock.Object,
        _cacheMock.Object,
        _loggerMock.Object);

    // Helper: mock IAccountTransaction (CommitAsync / RollbackAsync no-op)
    private static Mock<IAccountTransaction> MockTransaction()
    {
        var tx = new Mock<IAccountTransaction>();
        tx.Setup(t => t.CommitAsync()).Returns(Task.CompletedTask);
        tx.Setup(t => t.RollbackAsync()).Returns(Task.CompletedTask);
        tx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return tx;
    }

    // Helper: tạo User mẫu có roles
    private static User SampleUser(int id = 1, string status = "Active", params string[] roleNames)
    {
        return new User
        {
            Id            = id,
            Code          = $"user{id}",
            FullName      = $"User {id}",
            Email         = $"user{id}@test.com",
            AccountStatus = status,
            CreatedAt     = DateTime.UtcNow,
            UpdatedAt     = DateTime.UtcNow,
            UserRoleUsers = roleNames.Select(r => new UserRole
            {
                Role = new Role { RoleName = r }
            }).ToList()
        };
    }

    // ─── GetAllAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResult_WithCorrectMapping()
    {
        // Arrange
        var users = new List<User>
        {
            SampleUser(1, "Active", "Admin"),
            SampleUser(2, "Active", "Student")
        };
        var query = new AccountQueryDto { Page = 1, PageSize = 10 };
        _accountRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((users, 2));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_WhenPageIsZero_NormalizesToOne()
    {
        // Arrange
        var query = new AccountQueryDto { Page = 0, PageSize = 10 };
        _accountRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((new List<User>(), 0));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_WhenPageSizeIsNegative_NormalizesToTwenty()
    {
        // Arrange
        var query = new AccountQueryDto { Page = 1, PageSize = -1 };
        _accountRepoMock.Setup(r => r.GetAllAsync(query))
            .ReturnsAsync((new List<User>(), 0));

        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync(query);

        // Assert
        result.PageSize.Should().Be(20);
    }

    // ─── GetByIdAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetByIdAsync(99))
            .ReturnsAsync((User?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(99);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_MapsHistoryCorrectly()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Admin");
        var statusHistory = new List<AccountStatusHistory>
        {
            new() { Id = 1, OldStatus = "Active", NewStatus = "Suspended", Reason = "test" }
        };
        var roleHistory = new List<RoleChangeHistory>
        {
            new() { Id = 1, Action = "Grant", OldRoles = null, NewRoles = "Admin" }
        };

        _accountRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(user);
        _historyRepoMock.Setup(r => r.GetStatusHistoryAsync(1)).ReturnsAsync(statusHistory);
        _historyRepoMock.Setup(r => r.GetRoleHistoryAsync(1)).ReturnsAsync(roleHistory);

        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.StatusHistory.Should().HaveCount(1);
        result.RoleHistory.Should().HaveCount(1);
    }

    // ─── CreateAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WhenUsernameOrEmailExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateAccountDto
        {
            Username = "existing",
            Email    = "existing@test.com",
            FullName = "Test",
            Password = "pass",
            Roles    = new List<string> { "Student" }
        };
        _accountRepoMock
            .Setup(r => r.ExistsByUsernameOrEmailAsync("existing", "existing@test.com", It.IsAny<int?>()))
            .ReturnsAsync(true);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.CreateAsync(dto, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đã tồn tại*");
    }

    [Fact]
    public async Task CreateAsync_WhenInvalidRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateAccountDto
        {
            Username = "newuser",
            Email    = "new@test.com",
            FullName = "New",
            Password = "pass",
            Roles    = new List<string> { "SuperAdmin" }
        };
        _accountRepoMock
            .Setup(r => r.ExistsByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(false);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.CreateAsync(dto, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Vai trò không hợp lệ*");
    }

    [Fact]
    public async Task CreateAsync_WhenValid_CreatesUserAndRecordsHistory()
    {
        // Arrange
        var dto = new CreateAccountDto
        {
            Username = "newuser",
            Email    = "new@test.com",
            FullName = "New User",
            Password = "StrongPass!1",
            Roles    = new List<string> { "Student" }
        };
        var studentRole = new Role { Id = 10, RoleName = "Student" };
        var createdUser = SampleUser(5, "Active", "Student");
        var txMock      = MockTransaction();

        _accountRepoMock
            .Setup(r => r.ExistsByUsernameOrEmailAsync("newuser", "new@test.com", It.IsAny<int?>()))
            .ReturnsAsync(false);
        _accountRepoMock
            .Setup(r => r.GetRolesByNamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Role> { studentRole });
        _passwordHasherMock.Setup(h => h.Hash("StrongPass!1")).Returns("hashed");
        _accountRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(txMock.Object);
        _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<User>())).ReturnsAsync(new User());
        _accountRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _accountRepoMock
            .Setup(r => r.AddUserRolesAsync(It.IsAny<int>(), It.IsAny<IEnumerable<Role>>(), It.IsAny<int?>()))
            .Returns(Task.CompletedTask);
        _historyRepoMock
            .Setup(r => r.AddRoleChangeHistoryAsync(It.IsAny<RoleChangeHistory>()))
            .Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _accountRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(createdUser);
        _cacheMock.Setup(c => c.Remove(It.IsAny<object>()));

        var sut = CreateSut();

        // Act
        var result = await sut.CreateAsync(dto, adminUserId: 1);

        // Assert
        result.Should().NotBeNull();
        _accountRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
        _historyRepoMock.Verify(r => r.AddRoleChangeHistoryAsync(It.IsAny<RoleChangeHistory>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenRepoThrows_RollsBackTransaction()
    {
        // Arrange
        var dto = new CreateAccountDto
        {
            Username = "newuser",
            Email    = "new@test.com",
            FullName = "New",
            Password = "pass",
            Roles    = new List<string> { "Student" }
        };
        var txMock = MockTransaction();

        _accountRepoMock
            .Setup(r => r.ExistsByUsernameOrEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(false);
        _accountRepoMock
            .Setup(r => r.GetRolesByNamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(new List<Role> { new() { Id = 1, RoleName = "Student" } });
        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed");
        _accountRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(txMock.Object);
        _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<User>())).ThrowsAsync(new Exception("DB error"));

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.CreateAsync(dto, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("DB error");
        txMock.Verify(t => t.RollbackAsync(), Times.Once);
    }

    // ─── AssignRolesAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRolesAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(99)).ReturnsAsync((User?)null);
        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.AssignRolesAsync(99,
            new AssignRolesDto { Roles = new List<string> { "Student" } }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AssignRolesAsync_WhenInvalidRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = SampleUser(2, "Active", "Student");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(2)).ReturnsAsync(new List<string> { "Student" });

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.AssignRolesAsync(2,
            new AssignRolesDto { Roles = new List<string> { "Ghost" } }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Vai trò không hợp lệ*");
    }

    [Fact]
    public async Task AssignRolesAsync_WhenAdminElevatesSelf_ThrowsForbiddenException()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Manager");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(1)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(1)).ReturnsAsync(new List<string> { "Manager" });

        var sut = CreateSut();

        // Act — admin tự nâng mình lên Admin
        Func<Task> act = () => sut.AssignRolesAsync(1,
            new AssignRolesDto { Roles = new List<string> { "Admin" } }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*tự nâng quyền Admin*");
    }

    [Fact]
    public async Task AssignRolesAsync_WhenAdminRevokesOwnAdmin_ThrowsForbiddenException()
    {
        // Arrange
        var user = SampleUser(1, "Active", "Admin");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(1)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(1)).ReturnsAsync(new List<string> { "Admin" });

        var sut = CreateSut();

        // Act — admin tự thu hồi Admin của mình
        Func<Task> act = () => sut.AssignRolesAsync(1,
            new AssignRolesDto { Roles = new List<string> { "Manager" } }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*tự thu hồi quyền Admin*");
    }

    [Fact]
    public async Task AssignRolesAsync_WhenRevokingLastAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = SampleUser(2, "Active", "Admin");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(2)).ReturnsAsync(new List<string> { "Admin" });
        // CountActiveAdminsAsync has optional param excludeUserId → use It.IsAny
        _accountRepoMock.Setup(r => r.CountActiveAdminsAsync(It.IsAny<int?>())).ReturnsAsync(0);

        var sut = CreateSut();

        // Act — thu hồi Admin của admin khác khi chỉ còn 1 admin
        Func<Task> act = () => sut.AssignRolesAsync(2,
            new AssignRolesDto { Roles = new List<string> { "Manager" } }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Admin cuối cùng*");
    }

    [Fact]
    public async Task AssignRolesAsync_WhenRolesUnchanged_DoesNotRecordHistory()
    {
        // Arrange
        var user = SampleUser(2, "Active", "Student");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(2)).ReturnsAsync(new List<string> { "Student" });

        var sut = CreateSut();

        // Act
        var result = await sut.AssignRolesAsync(2,
            new AssignRolesDto { Roles = new List<string> { "Student" } }, adminUserId: 1);

        // Assert
        _historyRepoMock.Verify(r => r.AddRoleChangeHistoryAsync(It.IsAny<RoleChangeHistory>()), Times.Never);
    }

    // ─── SuspendAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SuspendAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(99)).ReturnsAsync((User?)null);
        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SuspendAsync(99, new SuspendAccountDto { Reason = "test" }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SuspendAsync_WhenAdminSuspendsSelf_ThrowsForbiddenException()
    {
        // Arrange
        var user = SampleUser(1, "Active");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(1)).ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SuspendAsync(1, new SuspendAccountDto { Reason = "test" }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*chính mình*");
    }

    [Fact]
    public async Task SuspendAsync_WhenAlreadySuspended_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = SampleUser(2, "Suspended");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SuspendAsync(2, new SuspendAccountDto { Reason = "test" }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*đã bị tạm ngưng*");
    }

    [Fact]
    public async Task SuspendAsync_WhenSuspendingLastAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = SampleUser(2, "Active", "Admin");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(2)).ReturnsAsync(new List<string> { "Admin" });
        _accountRepoMock.Setup(r => r.CountActiveAdminsAsync(It.IsAny<int?>())).ReturnsAsync(0);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.SuspendAsync(2, new SuspendAccountDto { Reason = "test" }, adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Admin cuối cùng*");
    }

    [Fact]
    public async Task SuspendAsync_WhenValid_RevokesTokensAndSendsEmail()
    {
        // Arrange
        var user   = SampleUser(2, "Active", "Student");
        var txMock = MockTransaction();

        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(2)).ReturnsAsync(new List<string> { "Student" });
        _accountRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(txMock.Object);
        _accountRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>())).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _refreshTokenRepoMock.Setup(r => r.RevokeAllUserTokensAsync(2, "AccountSuspended")).Returns(Task.CompletedTask);
        _notificationMock
            .Setup(n => n.SendAccountSuspendedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        _historyRepoMock.Setup(r => r.UpdateNotificationStatusAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _accountRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(SampleUser(2, "Suspended", "Student"));
        _cacheMock.Setup(c => c.Remove(It.IsAny<object>()));

        var sut = CreateSut();

        // Act
        var result = await sut.SuspendAsync(2, new SuspendAccountDto { Reason = "vi phạm nội quy" }, adminUserId: 1);

        // Assert
        _refreshTokenRepoMock.Verify(r => r.RevokeAllUserTokensAsync(2, "AccountSuspended"), Times.Once);
        _notificationMock.Verify(
            n => n.SendAccountSuspendedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task SuspendAsync_WhenEmailFails_UpdatesNotificationStatusToFailed()
    {
        // Arrange
        var user   = SampleUser(2, "Active", "Student");
        var txMock = MockTransaction();

        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.GetUserRoleNamesAsync(2)).ReturnsAsync(new List<string> { "Student" });
        _accountRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(txMock.Object);
        _accountRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>())).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _refreshTokenRepoMock.Setup(r => r.RevokeAllUserTokensAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _notificationMock
            .Setup(n => n.SendAccountSuspendedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(false);
        _historyRepoMock.Setup(r => r.UpdateNotificationStatusAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _accountRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(SampleUser(2, "Suspended", "Student"));
        _cacheMock.Setup(c => c.Remove(It.IsAny<object>()));

        var sut = CreateSut();

        // Act
        await sut.SuspendAsync(2, new SuspendAccountDto { Reason = "test" }, adminUserId: 1);

        // Assert
        _historyRepoMock.Verify(r => r.UpdateNotificationStatusAsync(It.IsAny<int>(), "Failed"), Times.Once);
    }

    // ─── RestoreAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RestoreAsync_WhenUserNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(99)).ReturnsAsync((User?)null);
        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RestoreAsync(99, new RestoreAccountDto(), adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RestoreAsync_WhenNotSuspended_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = SampleUser(2, "Active");
        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);

        var sut = CreateSut();

        // Act
        Func<Task> act = () => sut.RestoreAsync(2, new RestoreAccountDto(), adminUserId: 1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*không ở trạng thái tạm ngưng*");
    }

    [Fact]
    public async Task RestoreAsync_WhenValid_SetsStatusToActiveAndSendsEmail()
    {
        // Arrange
        var user   = SampleUser(2, "Suspended", "Student");
        var txMock = MockTransaction();

        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(txMock.Object);
        _accountRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>())).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _notificationMock
            .Setup(n => n.SendAccountRestoredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        _historyRepoMock.Setup(r => r.UpdateNotificationStatusAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _accountRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(SampleUser(2, "Active", "Student"));
        _cacheMock.Setup(c => c.Remove(It.IsAny<object>()));

        var sut = CreateSut();

        // Act
        var result = await sut.RestoreAsync(2, new RestoreAccountDto { Reason = "Đã xử lý xong" }, adminUserId: 1);

        // Assert
        _notificationMock.Verify(
            n => n.SendAccountRestoredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task RestoreAsync_WhenEmailFails_UpdatesNotificationStatusToFailed()
    {
        // Arrange
        var user   = SampleUser(2, "Suspended", "Student");
        var txMock = MockTransaction();

        _accountRepoMock.Setup(r => r.GetTrackedByIdAsync(2)).ReturnsAsync(user);
        _accountRepoMock.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(txMock.Object);
        _accountRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>())).Returns(Task.CompletedTask);
        _historyRepoMock.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _notificationMock
            .Setup(n => n.SendAccountRestoredAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(false);
        _historyRepoMock.Setup(r => r.UpdateNotificationStatusAsync(It.IsAny<int>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _accountRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(SampleUser(2, "Active", "Student"));
        _cacheMock.Setup(c => c.Remove(It.IsAny<object>()));

        var sut = CreateSut();

        // Act
        await sut.RestoreAsync(2, new RestoreAccountDto(), adminUserId: 1);

        // Assert
        _historyRepoMock.Verify(r => r.UpdateNotificationStatusAsync(It.IsAny<int>(), "Failed"), Times.Once);
    }
}
