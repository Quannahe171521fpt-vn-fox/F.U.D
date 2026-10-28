using FUD.Common;
using FUD.DTOs.Admin.Account;
using FUD.Infrastructure.Security;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Repositories.Interfaces.Accounts;
using FUD.Services.Interfaces.Accounts;

namespace FUD.Services.Accounts
{
    public class AccountService : IAccountService
    {
        private readonly IUserRepository _userRepo;
        private readonly IAccountHistoryRepository _historyRepo;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly ITransactionManager _transactionManager;
        private readonly IPasswordHasher _passwordHasher;
        private readonly INotificationService _notificationService;

        public AccountService(
            IUserRepository userRepo,
            IAccountHistoryRepository historyRepo,
            IRefreshTokenRepository refreshTokenRepo,
            ITransactionManager transactionManager,
            IPasswordHasher passwordHasher,
            INotificationService notificationService)
        {
            _userRepo = userRepo;
            _historyRepo = historyRepo;
            _refreshTokenRepo = refreshTokenRepo;
            _transactionManager = transactionManager;
            _passwordHasher = passwordHasher;
            _notificationService = notificationService;
        }

        // ── Đọc ───────────────────────────────────────────────────

        public async Task<PagedResult<AccountListItemDto>> GetAllAsync(AccountQueryDto query)
        {
            var (items, total) = await _userRepo.GetAllAsync(query);

            return new PagedResult<AccountListItemDto>
            {
                Items = items.Select(MapToListItem).ToList(),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<AccountDetailDto?> GetByIdAsync(int id)
        {
            var user = await _userRepo.GetByIdWithRolesAsync(id);
            if (user == null)
                return null;

            var statusHistories = await _historyRepo.GetStatusHistoriesAsync(id) ?? new List<AccountStatusHistory>();
            var roleHistories = await _historyRepo.GetRoleHistoriesAsync(id) ?? new List<RoleChangeHistory>();

            return new AccountDetailDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Status = user.AccountStatus,
                Roles = GetRoleNames(user),
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                SuspendedAt = user.SuspendedAt,
                SuspensionReason = user.SuspensionReason,
                SuspendedById = user.SuspendedById,
                SuspendedByName = user.SuspendedBy?.FullName,
                StatusHistories = statusHistories.Select(h => new AccountStatusHistoryDto
                {
                    Id = h.Id,
                    OldStatus = h.OldStatus,
                    NewStatus = h.NewStatus,
                    Reason = h.Reason,
                    Notes = h.Notes,
                    ChangedById = h.ChangedById,
                    ChangedByName = h.ChangedBy?.FullName,
                    ChangedAt = h.ChangedAt,
                    NotificationStatus = h.NotificationStatus
                }).ToList(),
                RoleHistories = roleHistories.Select(h => new RoleChangeHistoryDto
                {
                    Id = h.Id,
                    Action = h.Action,
                    OldRoles = h.OldRoles,
                    NewRoles = h.NewRoles,
                    ChangedById = h.ChangedById,
                    ChangedByName = h.ChangedBy?.FullName,
                    ChangedAt = h.ChangedAt
                }).ToList()
            };
        }

        public async Task<List<RoleOptionDto>> GetRoleOptionsAsync()
        {
            var roles = await _userRepo.GetAllRolesAsync();
            return roles
                .Where(r => AccountConstants.AllRoles.Contains(r.RoleName))
                .OrderBy(r => Array.IndexOf(AccountConstants.AllRoles, r.RoleName))
                .Select(r => new RoleOptionDto { Id = r.Id, Name = r.RoleName, Description = r.Description })
                .ToList();
        }

        // ── Create Account ────────────────────────────────────────

        public async Task<AccountDetailDto> CreateAsync(CreateAccountDto dto, int adminId)
        {
            var username = dto.Username.Trim();
            var email = dto.Email.Trim();

            if (await _userRepo.IsUsernameExistsAsync(username))
                throw new ConflictException($"Username '{username}' đã tồn tại");

            if (await _userRepo.IsEmailExistsAsync(email))
                throw new ConflictException($"Email '{email}' đã tồn tại");

            var role = (await _userRepo.GetRolesByNamesAsync(new[] { dto.RoleName })).FirstOrDefault();
            if (role == null)
                throw new ValidationException($"Vai trò '{dto.RoleName}' không tồn tại trong hệ thống");

            var now = DateTime.UtcNow;
            var user = new User
            {
                Code = string.IsNullOrWhiteSpace(dto.Username) ? $"U{now:yyyyMMddHHmmss}" : username,
                Username = username,
                FullName = dto.FullName.Trim(),
                Email = email,
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                Gender = "Other",
                PasswordHash = _passwordHasher.Hash(dto.Password),
                AccountStatus = AccountConstants.StatusActive,
                CreatedAt = now,
                UpdatedAt = now,
                TokenVersion = 0
            };

            await using var tx = await _transactionManager.BeginTransactionAsync();
            try
            {
                var created = await _userRepo.CreateAsync(user);

                await _userRepo.AddUserRolesAsync(new[]
                {
                    new UserRole
                    {
                        UserId = created.Id,
                        RoleId = role.Id,
                        AssignedAt = now,
                        AssignedById = adminId
                    }
                });

                await _historyRepo.AddRoleChangeHistoryAsync(new RoleChangeHistory
                {
                    UserId = created.Id,
                    Action = AccountConstants.RoleActionGrant,
                    OldRoles = null,
                    NewRoles = role.RoleName,
                    ChangedById = adminId,
                    ChangedAt = now
                });

                await _userRepo.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            var result = await GetByIdAsync(user.Id);
            return result ?? throw new NotFoundException($"Không tìm thấy tài khoản vừa tạo (id = {user.Id})");
        }

        // ── Assign Roles ──────────────────────────────────────────

        public async Task<AccountDetailDto> AssignRolesAsync(int userId, AssignRolesDto dto, int adminId)
        {
            var user = await _userRepo.GetByIdWithRolesAsync(userId)
                ?? throw new NotFoundException($"Tài khoản {userId} không tồn tại");

            var requestedNames = dto.RoleNames
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct()
                .ToList();

            if (requestedNames.Count == 0)
                throw new ValidationException("Phải giữ lại ít nhất một vai trò");

            var invalid = requestedNames.Where(n => !AccountConstants.AllRoles.Contains(n)).ToList();
            if (invalid.Count > 0)
                throw new ValidationException(
                    $"Vai trò không hợp lệ: {string.Join(", ", invalid)}. " +
                    $"Chỉ chấp nhận: {string.Join(", ", AccountConstants.AllRoles)}");

            var currentRoles = await _userRepo.GetUserRolesAsync(userId);
            var currentNames = currentRoles.Select(ur => ur.Role.RoleName).OrderBy(n => n).ToList();

            var targetRoles = await _userRepo.GetRolesByNamesAsync(requestedNames);
            if (targetRoles.Count != requestedNames.Count)
                throw new ValidationException("Một hoặc nhiều vai trò không tồn tại trong hệ thống");

            var targetNames = targetRoles.Select(r => r.RoleName).OrderBy(n => n).ToList();

            // Không có thay đổi → trả về nguyên trạng, không ghi lịch sử.
            if (currentNames.SequenceEqual(targetNames))
                return await GetByIdAsync(userId) ?? throw new NotFoundException($"Tài khoản {userId} không tồn tại");

            var hasAdminNow = currentNames.Contains(AccountConstants.RoleAdmin);
            var hasAdminTarget = targetNames.Contains(AccountConstants.RoleAdmin);

            // Admin không được tự nâng quyền: tự thêm vai trò Admin cho chính mình
            // (trường hợp admin hiện tại chưa có role Admin).
            if (userId == adminId && !hasAdminNow && hasAdminTarget)
                throw new ForbiddenException("Bạn không thể tự cấp quyền Admin cho chính mình");

            // Admin không được tự thu hồi quyền Admin của chính mình
            if (userId == adminId && hasAdminNow && !hasAdminTarget)
                throw new ForbiddenException("Bạn không thể tự thu hồi quyền Admin của chính mình");

            // Không thu hồi quyền Admin của Admin cuối cùng (kể cả khi admin khác thao tác).
            if (hasAdminNow && !hasAdminTarget)
            {
                var adminCount = await _userRepo.CountAdminsAsync(excludeUserId: userId);
                if (adminCount == 0)
                    throw new ConflictException("Không thể thu hồi quyền Admin của Admin cuối cùng trong hệ thống");
            }

            var now = DateTime.UtcNow;

            var toRemove = currentRoles.Where(ur => !targetNames.Contains(ur.Role.RoleName)).ToList();
            var toAdd = targetRoles.Where(r => !currentNames.Contains(r.RoleName)).ToList();

            await using var tx = await _transactionManager.BeginTransactionAsync();
            try
            {
                if (toRemove.Count > 0)
                    await _userRepo.RemoveUserRolesAsync(toRemove);

                if (toAdd.Count > 0)
                {
                    await _userRepo.AddUserRolesAsync(toAdd.Select(r => new UserRole
                    {
                        UserId = userId,
                        RoleId = r.Id,
                        AssignedAt = now,
                        AssignedById = adminId
                    }));
                }

                await _historyRepo.AddRoleChangeHistoryAsync(new RoleChangeHistory
                {
                    UserId = userId,
                    Action = ResolveAction(currentNames, targetNames),
                    OldRoles = string.Join(", ", currentNames),
                    NewRoles = string.Join(", ", targetNames),
                    ChangedById = adminId,
                    ChangedAt = now
                });

                // Làm mới phiên: token cũ không còn mang đúng quyền.
                user.TokenVersion += 1;
                user.UpdatedAt = now;
                await _userRepo.UpdateAsync(user);

                await _userRepo.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return await GetByIdAsync(userId) ?? throw new NotFoundException($"Tài khoản {userId} không tồn tại");
        }

        // ── Suspend Account ───────────────────────────────────────

        public async Task<AccountStatusResultDto> SuspendAsync(int userId, SuspendAccountDto dto, int adminId)
        {
            var user = await _userRepo.GetByIdWithRolesAsync(userId)
                ?? throw new NotFoundException($"Tài khoản {userId} không tồn tại");

            if (user.AccountStatus == AccountConstants.StatusSuspended)
                throw new ConflictException($"Tài khoản '{user.Username}' đã bị tạm ngưng trước đó");

            if (userId == adminId)
                throw new ForbiddenException("Bạn không thể tự tạm ngưng tài khoản của chính mình");

            if (string.IsNullOrWhiteSpace(dto.Notes) && dto.Reason == AccountConstants.ReasonOther)
                throw new ValidationException("Vui lòng nhập ghi chú khi chọn lý do 'Khác'");

            var roles = GetRoleNames(user);
            if (roles.Contains(AccountConstants.RoleAdmin))
            {
                var adminCount = await _userRepo.CountAdminsAsync(excludeUserId: userId);
                if (adminCount == 0)
                    throw new ConflictException("Không thể tạm ngưng tài khoản của Admin cuối cùng trong hệ thống");
            }

            var now = DateTime.UtcNow;
            var oldStatus = user.AccountStatus;

            await using var tx = await _transactionManager.BeginTransactionAsync();
            AccountStatusHistory history;
            try
            {
                user.AccountStatus = AccountConstants.StatusSuspended;
                user.SuspensionReason = dto.Reason;
                user.SuspendedAt = now;
                user.SuspendedById = adminId;
                user.UpdatedAt = now;
                user.TokenVersion += 1;

                await _userRepo.UpdateAsync(user);

                history = await _historyRepo.AddStatusHistoryAsync(new AccountStatusHistory
                {
                    UserId = userId,
                    OldStatus = oldStatus,
                    NewStatus = AccountConstants.StatusSuspended,
                    Reason = dto.Reason,
                    Notes = dto.Notes,
                    ChangedById = adminId,
                    ChangedAt = now,
                    NotificationStatus = AccountConstants.NotificationNone
                });

                // Thu hồi toàn bộ refresh token của tài khoản bị tạm ngưng.
                await _refreshTokenRepo.RevokeAllForUserAsync(userId, adminId);

                await _userRepo.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            // Gửi email sau khi commit: lỗi email KHÔNG rollback trạng thái tài khoản.
            var sent = await _notificationService.SendAccountSuspendedAsync(
                user.Email, user.FullName, dto.Reason, dto.Notes);

            history.NotificationStatus = sent
                ? AccountConstants.NotificationSent
                : AccountConstants.NotificationFailed;
            await _historyRepo.UpdateStatusHistoryAsync(history);

            return new AccountStatusResultDto
            {
                Id = user.Id,
                Username = user.Username,
                Status = user.AccountStatus,
                Roles = roles,
                SuspendedAt = user.SuspendedAt,
                SuspensionReason = user.SuspensionReason,
                NotificationStatus = history.NotificationStatus,
                Message = sent
                    ? $"Đã tạm ngưng tài khoản '{user.Username}' và gửi thông báo email."
                    : $"Đã tạm ngưng tài khoản '{user.Username}'. Gửi email thất bại, sẽ gửi lại sau."
            };
        }

        // ── Restore Account ───────────────────────────────────────

        public async Task<AccountStatusResultDto> RestoreAsync(int userId, RestoreAccountDto dto, int adminId)
        {
            var user = await _userRepo.GetByIdWithRolesAsync(userId)
                ?? throw new NotFoundException($"Tài khoản {userId} không tồn tại");

            if (user.AccountStatus != AccountConstants.StatusSuspended)
                throw new ConflictException($"Tài khoản '{user.Username}' không ở trạng thái tạm ngưng");

            var roles = GetRoleNames(user);
            var now = DateTime.UtcNow;
            var oldStatus = user.AccountStatus;

            await using var tx = await _transactionManager.BeginTransactionAsync();
            AccountStatusHistory history;
            try
            {
                user.AccountStatus = AccountConstants.StatusActive;
                user.SuspensionReason = null;
                user.SuspendedAt = null;
                user.SuspendedById = null;
                user.UpdatedAt = now;
                user.TokenVersion += 1;

                await _userRepo.UpdateAsync(user);

                history = await _historyRepo.AddStatusHistoryAsync(new AccountStatusHistory
                {
                    UserId = userId,
                    OldStatus = oldStatus,
                    NewStatus = AccountConstants.StatusActive,
                    Reason = dto.Reason,
                    Notes = null,
                    ChangedById = adminId,
                    ChangedAt = now,
                    NotificationStatus = AccountConstants.NotificationNone
                });

                await _userRepo.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            // Không tự động cấp quyền Admin khi khôi phục — giữ nguyên vai trò hiện có.
            var sent = await _notificationService.SendAccountRestoredAsync(
                user.Email, user.FullName, dto.Reason);

            history.NotificationStatus = sent
                ? AccountConstants.NotificationSent
                : AccountConstants.NotificationFailed;
            await _historyRepo.UpdateStatusHistoryAsync(history);

            return new AccountStatusResultDto
            {
                Id = user.Id,
                Username = user.Username,
                Status = user.AccountStatus,
                Roles = roles,
                SuspendedAt = user.SuspendedAt,
                SuspensionReason = user.SuspensionReason,
                NotificationStatus = history.NotificationStatus,
                Message = sent
                    ? $"Đã khôi phục tài khoản '{user.Username}' và gửi thông báo email."
                    : $"Đã khôi phục tài khoản '{user.Username}'. Gửi email thất bại, sẽ gửi lại sau."
            };
        }

        // ── Helpers ───────────────────────────────────────────────

        private static string ResolveAction(List<string> oldNames, List<string> newNames)
        {
            var added = newNames.Except(oldNames).Any();
            var removed = oldNames.Except(newNames).Any();

            if (added && removed) return AccountConstants.RoleActionAdjust;
            if (added) return AccountConstants.RoleActionGrant;
            return AccountConstants.RoleActionRevoke;
        }

        private static List<string> GetRoleNames(User user)
        {
            if (user.UserRoleUsers == null)
                return new List<string>();

            return user.UserRoleUsers
                .Where(ur => ur.Role != null)
                .Select(ur => ur.Role.RoleName)
                .OrderBy(n => Array.IndexOf(AccountConstants.AllRoles, n) < 0
                    ? int.MaxValue
                    : Array.IndexOf(AccountConstants.AllRoles, n))
                .ToList();
        }

        private static AccountListItemDto MapToListItem(User u) => new()
        {
            Id = u.Id,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            Phone = u.Phone,
            Status = u.AccountStatus,
            Roles = GetRoleNames(u),
            CreatedAt = u.CreatedAt,
            SuspendedAt = u.SuspendedAt,
            SuspensionReason = u.SuspensionReason
        };
    }
}
