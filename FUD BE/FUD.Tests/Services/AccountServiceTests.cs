using FUD.Common;
using FUD.DTOs.Admin.Account;
using FUD.Models;
using FUD.Repositories.Interfaces.Accounts;
using FUD.Services.Accounts;
using FUD.Services.Interfaces.Accounts;
using FUD.Tests.Helpers;
using Moq;

namespace FUD.Tests.Services
{
    public class AccountServiceTests
    {
        private readonly Mock<IUserRepository> _userRepo = new();
        private readonly Mock<IAccountHistoryRepository> _historyRepo = new();
        private readonly Mock<IRefreshTokenRepository> _refreshTokenRepo = new();
        private readonly Mock<INotificationService> _notificationService = new();
        private readonly FakeTransactionManager _tx = new();

        private AccountService CreateService() => new(
            _userRepo.Object,
            _historyRepo.Object,
            _refreshTokenRepo.Object,
            _tx,
            new FakePasswordHasher(),
            _notificationService.Object);

        private static User BuildUser(int id = 1, string status = AccountConstants.StatusActive, params string[] roles)
        {
            var user = new User
            {
                Id = id,
                Username = "user1",
                FullName = "Nguyễn Văn A",
                Email = "a@fud.local",
                AccountStatus = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TokenVersion = 0
            };

            foreach (var roleName in roles)
            {
                var role = new Role { Id = roleName switch
                {
                    AccountConstants.RoleAdmin => 4,
                    AccountConstants.RoleManager => 2,
                    AccountConstants.RoleGuard => 3,
                    _ => 1
                }, RoleName = roleName };

                user.UserRoleUsers.Add(new UserRole
                {
                    UserId = id,
                    RoleId = role.Id,
                    Role = role,
                    AssignedAt = DateTime.UtcNow
                });
            }

            return user;
        }

        private void SetupRoles(params string[] roles)
        {
            var roleEntities = roles.Select(r => new Role
            {
                Id = r switch
                {
                    AccountConstants.RoleAdmin => 4,
                    AccountConstants.RoleManager => 2,
                    AccountConstants.RoleGuard => 3,
                    _ => 1
                },
                RoleName = r
            }).ToList();

            _userRepo.Setup(r => r.GetRolesByNamesAsync(It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync(roleEntities);
        }

        // ── Create Account ────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithValidData_CreatesUserWithHashedPasswordAndInitialRole()
        {
            var adminId = 99;
            var dto = new CreateAccountDto
            {
                Username = "student01",
                FullName = "Trần Văn B",
                Email = "b@fud.local",
                Password = "Pass1234",
                RoleName = AccountConstants.RoleStudent
            };

            _userRepo.Setup(r => r.IsUsernameExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsEmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
            SetupRoles(AccountConstants.RoleStudent);

            User? created = null;
            _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>()))
                .Callback<User>(u => { created = u; u.Id = 10; })
                .ReturnsAsync((User u) => u);

            _userRepo.Setup(r => r.GetByIdWithRolesAsync(10)).ReturnsAsync(BuildUser(10, AccountConstants.StatusActive, AccountConstants.RoleStudent));
            _historyRepo.Setup(r => r.GetStatusHistoriesAsync(It.IsAny<int>())).ReturnsAsync(new List<AccountStatusHistory>());
            _historyRepo.Setup(r => r.GetRoleHistoriesAsync(It.IsAny<int>())).ReturnsAsync(new List<RoleChangeHistory>());
            _historyRepo.Setup(r => r.AddRoleChangeHistoryAsync(It.IsAny<RoleChangeHistory>()))
                .ReturnsAsync((RoleChangeHistory h) => h);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var service = CreateService();
            var result = await service.CreateAsync(dto, adminId);

            Assert.NotNull(created);
            Assert.Equal("student01", created!.Username);
            Assert.Equal(AccountConstants.StatusActive, created.AccountStatus);
            Assert.Equal("hashed:Pass1234", created.PasswordHash);
            Assert.NotEqual("Pass1234", created.PasswordHash);
            Assert.Equal(1, _tx.Committed);
            Assert.Equal(10, result.Id);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateUsername_ThrowsConflict()
        {
            var dto = new CreateAccountDto
            {
                Username = "student01",
                FullName = "Trần Văn B",
                Email = "b@fud.local",
                Password = "Pass1234",
                RoleName = AccountConstants.RoleStudent
            };

            _userRepo.Setup(r => r.IsUsernameExistsAsync("student01", It.IsAny<int?>())).ReturnsAsync(true);

            var service = CreateService();
            var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(dto, 1));
            Assert.Contains("student01", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateEmail_ThrowsConflict()
        {
            var dto = new CreateAccountDto
            {
                Username = "student02",
                FullName = "Trần Văn B",
                Email = "b@fud.local",
                Password = "Pass1234",
                RoleName = AccountConstants.RoleStudent
            };

            _userRepo.Setup(r => r.IsUsernameExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsEmailExistsAsync("b@fud.local", It.IsAny<int?>())).ReturnsAsync(true);

            var service = CreateService();
            await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(dto, 1));
        }

        [Fact]
        public async Task CreateAsync_WithInvalidRole_ThrowsValidation()
        {
            var dto = new CreateAccountDto
            {
                Username = "student03",
                FullName = "Trần Văn B",
                Email = "c@fud.local",
                Password = "Pass1234",
                RoleName = "SuperAdmin"
            };

            _userRepo.Setup(r => r.IsUsernameExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
            _userRepo.Setup(r => r.IsEmailExistsAsync(It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(false);
            SetupRoles(); // không có role khớp

            var service = CreateService();
            await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(dto, 1));
        }

        // ── Assign Roles ──────────────────────────────────────────

        [Fact]
        public async Task AssignRolesAsync_GrantRole_WritesHistoryAndBumpsTokenVersion()
        {
            var user = BuildUser(5, AccountConstants.StatusActive, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(5)).ReturnsAsync(user);
            _userRepo.Setup(r => r.GetUserRolesAsync(5)).ReturnsAsync(user.UserRoleUsers.ToList());
            SetupRoles(AccountConstants.RoleStudent, AccountConstants.RoleGuard);
            _historyRepo.Setup(r => r.AddRoleChangeHistoryAsync(It.IsAny<RoleChangeHistory>()))
                .ReturnsAsync((RoleChangeHistory h) => h);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(5)).ReturnsAsync(user);

            var service = CreateService();
            var dto = new AssignRolesDto
            {
                RoleNames = new List<string> { AccountConstants.RoleStudent, AccountConstants.RoleGuard }
            };

            var result = await service.AssignRolesAsync(5, dto, 1);

            _historyRepo.Verify(h => h.AddRoleChangeHistoryAsync(It.Is<RoleChangeHistory>(x =>
                x.UserId == 5 && x.Action == AccountConstants.RoleActionGrant)), Times.Once);
            Assert.Equal(1, user.TokenVersion);
            Assert.Equal(1, _tx.Committed);
            Assert.NotNull(result);
        }

        [Fact]
        public async Task AssignRolesAsync_RevokeRole_WritesRevokeHistory()
        {
            var user = BuildUser(6, AccountConstants.StatusActive, AccountConstants.RoleStudent, AccountConstants.RoleGuard);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(6)).ReturnsAsync(user);
            _userRepo.Setup(r => r.GetUserRolesAsync(6)).ReturnsAsync(user.UserRoleUsers.ToList());
            SetupRoles(AccountConstants.RoleStudent);
            _historyRepo.Setup(h => h.AddRoleChangeHistoryAsync(It.IsAny<RoleChangeHistory>()))
                .ReturnsAsync((RoleChangeHistory h) => h);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            var service = CreateService();
            var dto = new AssignRolesDto { RoleNames = new List<string> { AccountConstants.RoleStudent } };

            await service.AssignRolesAsync(6, dto, 1);

            _historyRepo.Verify(h => h.AddRoleChangeHistoryAsync(It.Is<RoleChangeHistory>(x =>
                x.UserId == 6 && x.Action == AccountConstants.RoleActionRevoke)), Times.Once);
        }

        [Fact]
        public async Task AssignRolesAsync_SelfGrantAdmin_ThrowsForbidden()
        {
            var adminId = 7;
            var user = BuildUser(adminId, AccountConstants.StatusActive, AccountConstants.RoleManager);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(adminId)).ReturnsAsync(user);
            _userRepo.Setup(r => r.GetUserRolesAsync(adminId)).ReturnsAsync(user.UserRoleUsers.ToList());
            SetupRoles(AccountConstants.RoleManager, AccountConstants.RoleAdmin);

            var service = CreateService();
            var dto = new AssignRolesDto
            {
                RoleNames = new List<string> { AccountConstants.RoleManager, AccountConstants.RoleAdmin }
            };

            await Assert.ThrowsAsync<ForbiddenException>(() => service.AssignRolesAsync(adminId, dto, adminId));
        }

        [Fact]
        public async Task AssignRolesAsync_SelfRevokeAdmin_ThrowsForbidden()
        {
            var adminId = 8;
            var user = BuildUser(adminId, AccountConstants.StatusActive, AccountConstants.RoleAdmin);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(adminId)).ReturnsAsync(user);
            _userRepo.Setup(r => r.GetUserRolesAsync(adminId)).ReturnsAsync(user.UserRoleUsers.ToList());
            SetupRoles(AccountConstants.RoleManager);

            var service = CreateService();
            var dto = new AssignRolesDto { RoleNames = new List<string> { AccountConstants.RoleManager } };

            await Assert.ThrowsAsync<ForbiddenException>(() => service.AssignRolesAsync(adminId, dto, adminId));
        }

        [Fact]
        public async Task AssignRolesAsync_RevokeAdminOfLastAdmin_ThrowsConflict()
        {
            var targetId = 20;
            var user = BuildUser(targetId, AccountConstants.StatusActive, AccountConstants.RoleAdmin);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(targetId)).ReturnsAsync(user);
            _userRepo.Setup(r => r.GetUserRolesAsync(targetId)).ReturnsAsync(user.UserRoleUsers.ToList());
            SetupRoles(AccountConstants.RoleManager);
            _userRepo.Setup(r => r.CountAdminsAsync(targetId)).ReturnsAsync(0);

            var service = CreateService();
            var dto = new AssignRolesDto { RoleNames = new List<string> { AccountConstants.RoleManager } };

            await Assert.ThrowsAsync<ConflictException>(() => service.AssignRolesAsync(targetId, dto, 1));
        }

        [Fact]
        public async Task AssignRolesAsync_WithInvalidRoleName_ThrowsValidation()
        {
            var user = BuildUser(9, AccountConstants.StatusActive, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(9)).ReturnsAsync(user);
            _userRepo.Setup(r => r.GetUserRolesAsync(9)).ReturnsAsync(user.UserRoleUsers.ToList());

            var service = CreateService();
            var dto = new AssignRolesDto { RoleNames = new List<string> { "Wizard" } };

            await Assert.ThrowsAsync<ValidationException>(() => service.AssignRolesAsync(9, dto, 1));
        }

        // ── Suspend Account ───────────────────────────────────────

        [Fact]
        public async Task SuspendAsync_ActiveAccount_SetsStatusWritesHistoryAndRevokesTokens()
        {
            var user = BuildUser(30, AccountConstants.StatusActive, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(30)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            AccountStatusHistory? saved = null;
            _historyRepo.Setup(h => h.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Callback<AccountStatusHistory>(x => saved = x)
                .ReturnsAsync((AccountStatusHistory x) => x);
            _historyRepo.Setup(h => h.UpdateStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Returns(Task.CompletedTask);

            _notificationService.Setup(n => n.SendAccountSuspendedAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = CreateService();
            var dto = new SuspendAccountDto { Reason = AccountConstants.ReasonViolation, Notes = "Vi phạm nhiều lần" };

            var result = await service.SuspendAsync(30, dto, 1);

            Assert.Equal(AccountConstants.StatusSuspended, user.AccountStatus);
            Assert.Equal(AccountConstants.ReasonViolation, user.SuspensionReason);
            Assert.NotNull(user.SuspendedAt);
            Assert.NotNull(saved);
            Assert.Equal(AccountConstants.StatusActive, saved!.OldStatus);
            Assert.Equal(AccountConstants.StatusSuspended, saved.NewStatus);
            Assert.Equal(AccountConstants.NotificationSent, result.NotificationStatus);
            Assert.Equal(1, _tx.Committed);
            _refreshTokenRepo.Verify(r => r.RevokeAllForUserAsync(30, It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task SuspendAsync_AlreadySuspended_ThrowsConflict()
        {
            var user = BuildUser(31, AccountConstants.StatusSuspended, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(31)).ReturnsAsync(user);

            var service = CreateService();
            var dto = new SuspendAccountDto { Reason = AccountConstants.ReasonViolation };

            await Assert.ThrowsAsync<ConflictException>(() => service.SuspendAsync(31, dto, 1));
        }

        [Fact]
        public async Task SuspendAsync_SelfSuspend_ThrowsForbidden()
        {
            var adminId = 40;
            var user = BuildUser(adminId, AccountConstants.StatusActive, AccountConstants.RoleAdmin);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(adminId)).ReturnsAsync(user);

            var service = CreateService();
            var dto = new SuspendAccountDto { Reason = AccountConstants.ReasonOther, Notes = "test" };

            await Assert.ThrowsAsync<ForbiddenException>(() => service.SuspendAsync(adminId, dto, adminId));
        }

        [Fact]
        public async Task SuspendAsync_LastAdmin_ThrowsConflict()
        {
            var targetId = 41;
            var user = BuildUser(targetId, AccountConstants.StatusActive, AccountConstants.RoleAdmin);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(targetId)).ReturnsAsync(user);
            _userRepo.Setup(r => r.CountAdminsAsync(targetId)).ReturnsAsync(0);

            var service = CreateService();
            var dto = new SuspendAccountDto { Reason = AccountConstants.ReasonTermination };

            await Assert.ThrowsAsync<ConflictException>(() => service.SuspendAsync(targetId, dto, 1));
        }

        [Fact]
        public async Task SuspendAsync_EmailFails_StillSuspendsAndMarksNotificationFailed()
        {
            var user = BuildUser(32, AccountConstants.StatusActive, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(32)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _historyRepo.Setup(h => h.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .ReturnsAsync((AccountStatusHistory x) => x);
            _historyRepo.Setup(h => h.UpdateStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Returns(Task.CompletedTask);

            _notificationService.Setup(n => n.SendAccountSuspendedAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var service = CreateService();
            var dto = new SuspendAccountDto { Reason = AccountConstants.ReasonGraduation };

            var result = await service.SuspendAsync(32, dto, 1);

            Assert.Equal(AccountConstants.StatusSuspended, user.AccountStatus);
            Assert.Equal(AccountConstants.NotificationFailed, result.NotificationStatus);
            Assert.Equal(1, _tx.Committed);
        }

        // ── Restore Account ───────────────────────────────────────

        [Fact]
        public async Task RestoreAsync_SuspendedAccount_SetsActiveAndWritesHistory()
        {
            var user = BuildUser(50, AccountConstants.StatusSuspended, AccountConstants.RoleStudent);
            user.SuspensionReason = AccountConstants.ReasonViolation;
            user.SuspendedAt = DateTime.UtcNow.AddDays(-3);

            _userRepo.Setup(r => r.GetByIdWithRolesAsync(50)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);

            AccountStatusHistory? saved = null;
            _historyRepo.Setup(h => h.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Callback<AccountStatusHistory>(x => saved = x)
                .ReturnsAsync((AccountStatusHistory x) => x);
            _historyRepo.Setup(h => h.UpdateStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Returns(Task.CompletedTask);

            _notificationService.Setup(n => n.SendAccountRestoredAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = CreateService();
            var dto = new RestoreAccountDto { Reason = "Đã xác minh vi phạm được giải quyết" };

            var result = await service.RestoreAsync(50, dto, 1);

            Assert.Equal(AccountConstants.StatusActive, user.AccountStatus);
            Assert.Null(user.SuspensionReason);
            Assert.Null(user.SuspendedAt);
            Assert.NotNull(saved);
            Assert.Equal(AccountConstants.StatusSuspended, saved!.OldStatus);
            Assert.Equal(AccountConstants.StatusActive, saved.NewStatus);
            Assert.Equal(AccountConstants.NotificationSent, result.NotificationStatus);
        }

        [Fact]
        public async Task RestoreAsync_NotSuspended_ThrowsConflict()
        {
            var user = BuildUser(51, AccountConstants.StatusActive, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(51)).ReturnsAsync(user);

            var service = CreateService();
            var dto = new RestoreAccountDto { Reason = "Khôi phục" };

            await Assert.ThrowsAsync<ConflictException>(() => service.RestoreAsync(51, dto, 1));
        }

        [Fact]
        public async Task RestoreAsync_EmailFails_StillRestoresAndMarksNotificationFailed()
        {
            var user = BuildUser(52, AccountConstants.StatusSuspended, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(52)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _historyRepo.Setup(h => h.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .ReturnsAsync((AccountStatusHistory x) => x);
            _historyRepo.Setup(h => h.UpdateStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Returns(Task.CompletedTask);

            _notificationService.Setup(n => n.SendAccountRestoredAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var service = CreateService();
            var dto = new RestoreAccountDto { Reason = "Kiểm tra hệ thống hoàn tất" };

            var result = await service.RestoreAsync(52, dto, 1);

            Assert.Equal(AccountConstants.StatusActive, user.AccountStatus);
            Assert.Equal(AccountConstants.NotificationFailed, result.NotificationStatus);
        }

        [Fact]
        public async Task RestoreAsync_DoesNotGrantAdminRoleAutomatically()
        {
            var user = BuildUser(53, AccountConstants.StatusSuspended, AccountConstants.RoleStudent);
            _userRepo.Setup(r => r.GetByIdWithRolesAsync(53)).ReturnsAsync(user);
            _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(true);
            _historyRepo.Setup(h => h.AddStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .ReturnsAsync((AccountStatusHistory x) => x);
            _historyRepo.Setup(h => h.UpdateStatusHistoryAsync(It.IsAny<AccountStatusHistory>()))
                .Returns(Task.CompletedTask);
            _notificationService.Setup(n => n.SendAccountRestoredAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var service = CreateService();
            var result = await service.RestoreAsync(53, new RestoreAccountDto { Reason = "Hoàn tất kiểm tra" }, 1);

            Assert.DoesNotContain(AccountConstants.RoleAdmin, result.Roles);
            Assert.Single(result.Roles);
        }
    }
}
