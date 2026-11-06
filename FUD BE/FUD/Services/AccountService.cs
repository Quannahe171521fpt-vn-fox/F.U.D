using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.Common;
using FUD.DTOs.Account;
using FUD.Infrastructure.Auth;
using FUD.Infrastructure.Email;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace FUD.Services;

/// <summary>
/// Service Layer cho module Account Management.
/// Chứa toàn bộ quy tắc nghiệp vụ: Create Account, Assign Roles, Suspend Account, Restore Account.
/// </summary>
public class AccountService : IAccountService
{
    private readonly IAccountRepository _accountRepo;
    private readonly IAccountHistoryRepository _historyRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IPasswordHasher _passwordHasher;
    private readonly INotificationService _notificationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IAccountRepository accountRepo,
        IAccountHistoryRepository historyRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IPasswordHasher passwordHasher,
        INotificationService notificationService,
        IMemoryCache cache,
        ILogger<AccountService> logger)
    {
        _accountRepo = accountRepo;
        _historyRepo = historyRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _passwordHasher = passwordHasher;
        _notificationService = notificationService;
        _cache = cache;
        _logger = logger;
    }

    // ─────────────────────────────────────────────
    // READ
    // ─────────────────────────────────────────────

    public async Task<PagedResult<AccountDto>> GetAllAsync(AccountQueryDto query)
    {
        var (items, total) = await _accountRepo.GetAllAsync(query);

        return new PagedResult<AccountDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total,
            Page = query.Page <= 0 ? 1 : query.Page,
            PageSize = query.PageSize <= 0 ? 20 : query.PageSize
        };
    }

    public async Task<AccountDetailDto?> GetByIdAsync(int userId)
    {
        var user = await _accountRepo.GetByIdAsync(userId);
        if (user == null)
            return null;

        var statusHistory = await _historyRepo.GetStatusHistoryAsync(userId);
        var roleHistory = await _historyRepo.GetRoleHistoryAsync(userId);

        var dto = new AccountDetailDto();
        CopyBase(dto, user);

        dto.StatusHistory = statusHistory.Select(h => new AccountStatusHistoryDto
        {
            Id = h.Id,
            OldStatus = h.OldStatus,
            NewStatus = h.NewStatus,
            Reason = h.Reason,
            Notes = h.Notes,
            ChangedBy = h.ChangedBy,
            ChangedByName = h.ChangedByUser?.FullName,
            ChangedAt = h.ChangedAt,
            NotificationStatus = h.NotificationStatus
        }).ToList();

        dto.RoleHistory = roleHistory.Select(h => new RoleChangeHistoryDto
        {
            Id = h.Id,
            Action = h.Action,
            OldRoles = h.OldRoles,
            NewRoles = h.NewRoles,
            ChangedBy = h.ChangedBy,
            ChangedByName = h.ChangedByUser?.FullName,
            ChangedAt = h.ChangedAt
        }).ToList();

        return dto;
    }

    // ─────────────────────────────────────────────
    // CREATE ACCOUNT
    // ─────────────────────────────────────────────

    public async Task<AccountDto> CreateAsync(CreateAccountDto dto, int adminUserId)
    {
        var username = dto.Username.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();

        // 1. Kiểm tra trùng Username / Email
        if (await _accountRepo.ExistsByUsernameOrEmailAsync(username, email))
            throw new InvalidOperationException($"Username '{username}' hoặc email '{email}' đã tồn tại trong hệ thống.");

        // 2. Chỉ được chọn vai trò trong danh sách cho phép
        var requestedRoles = dto.Roles
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allowedRoles = AccountConstants.Roles.All;
        if (requestedRoles.Any(r => !allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Vai trò không hợp lệ. Chỉ chấp nhận: Student, Manager, Guard, Admin.");

        var roles = await _accountRepo.GetRolesByNamesAsync(requestedRoles);
        if (roles.Count != requestedRoles.Count)
            throw new InvalidOperationException("Một hoặc nhiều vai trò không tồn tại trong hệ thống.");

        // 3. Tạo tài khoản — trạng thái mặc định Active
        var user = new User
        {
            Code = username,
            FullName = dto.FullName.Trim(),
            Email = email,
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            Gender = string.IsNullOrWhiteSpace(dto.Gender) ? "Male" : dto.Gender.Trim(),
            PasswordHash = _passwordHasher.Hash(dto.Password),
            AccountStatus = AccountConstants.Statuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using var transaction = await _accountRepo.BeginTransactionAsync();
        try
        {
            await _accountRepo.AddAsync(user);
            await _accountRepo.SaveChangesAsync();

            // 4. Gán vai trò ban đầu
            await _accountRepo.AddUserRolesAsync(user.Id, roles, adminUserId);
            await _accountRepo.SaveChangesAsync();

            // 5. Ghi lịch sử vai trò
            await _historyRepo.AddRoleChangeHistoryAsync(new RoleChangeHistory
            {
                UserId = user.Id,
                Action = AccountConstants.RoleActions.Grant,
                OldRoles = null,
                NewRoles = string.Join(", ", roles.Select(r => r.RoleName)),
                ChangedBy = adminUserId,
                ChangedAt = DateTime.UtcNow
            });
            await _historyRepo.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        InvalidateUserCache(user.Id);

        _logger.LogInformation("Admin {AdminId} đã tạo tài khoản {Username} với vai trò {Roles}",
            adminUserId, username, string.Join(", ", roles.Select(r => r.RoleName)));

        var created = await _accountRepo.GetByIdAsync(user.Id);
        return MapToDto(created ?? user);
    }

    // ─────────────────────────────────────────────
    // ASSIGN ROLES
    // ─────────────────────────────────────────────

    public async Task<AccountDto> AssignRolesAsync(int userId, AssignRolesDto dto, int adminUserId)
    {
        var user = await _accountRepo.GetTrackedByIdAsync(userId)
                   ?? throw new KeyNotFoundException($"Tài khoản id {userId} không tồn tại.");

        var oldRoleNames = await _accountRepo.GetUserRoleNamesAsync(userId);

        var newRoleNames = dto.Roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var allowedRoles = AccountConstants.Roles.All;
        if (newRoleNames.Any(r => !allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Vai trò không hợp lệ. Chỉ chấp nhận: Student, Manager, Guard, Admin.");

        // Quy tắc 1: Admin không tự nâng quyền cho chính mình
        var isSelf = adminUserId == userId;
        var selfHadAdmin = oldRoleNames.Any(r => r.Equals(AccountConstants.Roles.Admin, StringComparison.OrdinalIgnoreCase));
        var selfHasAdmin = newRoleNames.Any(r => r.Equals(AccountConstants.Roles.Admin, StringComparison.OrdinalIgnoreCase));

        if (isSelf && !selfHadAdmin && selfHasAdmin)
            throw new ForbiddenException("Bạn không thể tự nâng quyền Admin cho chính mình.");

        // Quy tắc 2: Admin không tự thu hồi quyền Admin của chính mình
        if (isSelf && selfHadAdmin && !selfHasAdmin)
            throw new ForbiddenException("Bạn không thể tự thu hồi quyền Admin của chính mình.");

        // Quy tắc 3: Không cho thu hồi quyền Admin của Admin cuối cùng
        // (chỉ kiểm tra khi tài khoản đang Active — nếu đã bị tạm ngưng thì việc thu hồi không làm hệ thống mất Admin)
        if (selfHadAdmin && !selfHasAdmin
            && user.AccountStatus == AccountConstants.Statuses.Active)
        {
            var remainingActiveAdmins = await _accountRepo.CountActiveAdminsAsync(excludeUserId: userId);
            if (remainingActiveAdmins == 0)
                throw new InvalidOperationException("Không thể thu hồi quyền Admin của Admin cuối cùng trong hệ thống.");
        }

        // Không có thay đổi → không làm gì
        if (oldRoleNames.Count == newRoleNames.Count
            && oldRoleNames.All(r => newRoleNames.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            return MapToDto(user);
        }

        var roles = await _accountRepo.GetRolesByNamesAsync(newRoleNames);
        if (roles.Count != newRoleNames.Count)
            throw new InvalidOperationException("Một hoặc nhiều vai trò không tồn tại trong hệ thống.");

        // Áp dụng thay đổi: xóa role không còn giữ, thêm role mới
        var existingRoleIds = user.UserRoleUsers.Select(ur => ur.RoleId).ToList();

        var action = oldRoleNames.Count == 0
            ? AccountConstants.RoleActions.Grant
            : newRoleNames.Count == 0
                ? AccountConstants.RoleActions.Revoke
                : AccountConstants.RoleActions.Adjust;

        await using var transaction = await _accountRepo.BeginTransactionAsync();
        try
        {
            await _accountRepo.RemoveUserRolesAsync(userId, roles.Select(r => r.Id));
            await _accountRepo.SaveChangesAsync();

            var toAdd = roles.Where(r => !existingRoleIds.Contains(r.Id)).ToList();
            if (toAdd.Count > 0)
            {
                await _accountRepo.AddUserRolesAsync(userId, toAdd, adminUserId);
                await _accountRepo.SaveChangesAsync();
            }

            // Ghi lịch sử thay đổi vai trò
            await _historyRepo.AddRoleChangeHistoryAsync(new RoleChangeHistory
            {
                UserId = userId,
                Action = action,
                OldRoles = string.Join(", ", oldRoleNames),
                NewRoles = string.Join(", ", newRoleNames),
                ChangedBy = adminUserId,
                ChangedAt = DateTime.UtcNow
            });
            await _historyRepo.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // Quyền mới có hiệu lực thật: làm mới cache + buộc client làm mới token
        InvalidateUserCache(userId);

        _logger.LogInformation("Admin {AdminId} đã {Action} vai trò cho tài khoản {UserId}: [{Old}] -> [{New}]",
            adminUserId, action, userId, string.Join(", ", oldRoleNames), string.Join(", ", newRoleNames));

        var updated = await _accountRepo.GetByIdAsync(userId);
        return MapToDto(updated ?? user);
    }

    // ─────────────────────────────────────────────
    // SUSPEND ACCOUNT
    // ─────────────────────────────────────────────

    public async Task<AccountDto> SuspendAsync(int userId, SuspendAccountDto dto, int adminUserId)
    {
        var user = await _accountRepo.GetTrackedByIdAsync(userId)
                   ?? throw new KeyNotFoundException($"Tài khoản id {userId} không tồn tại.");

        // 1. Admin không tự khóa chính mình
        if (adminUserId == userId)
            throw new ForbiddenException("Bạn không thể tạm ngưng tài khoản của chính mình.");

        // 2. Không khóa tài khoản đã bị khóa
        if (user.AccountStatus == AccountConstants.Statuses.Suspended)
            throw new InvalidOperationException("Tài khoản đã bị tạm ngưng trước đó.");

        // 3. Không khóa Admin cuối cùng
        var userRoleNames = await _accountRepo.GetUserRoleNamesAsync(userId);
        if (userRoleNames.Any(r => r.Equals(AccountConstants.Roles.Admin, StringComparison.OrdinalIgnoreCase)))
        {
            var remainingActiveAdmins = await _accountRepo.CountActiveAdminsAsync(excludeUserId: userId);
            if (remainingActiveAdmins == 0)
                throw new InvalidOperationException("Không thể tạm ngưng tài khoản của Admin cuối cùng trong hệ thống.");
        }

        var oldStatus = user.AccountStatus;
        var reason = dto.Reason.Trim().ToLowerInvariant();
        AccountStatusHistory history;

        await using var transaction = await _accountRepo.BeginTransactionAsync();
        try
        {
            // 4. Chỉ đổi Status — tuyệt đối không xóa bản ghi
            user.AccountStatus = AccountConstants.Statuses.Suspended;
            user.SuspensionReason = reason;
            user.SuspendedAt = DateTime.UtcNow;
            user.SuspendedById = adminUserId;
            user.UpdatedAt = DateTime.UtcNow;
            await _accountRepo.SaveChangesAsync();

            // 5. Ghi lịch sử trạng thái
            history = new AccountStatusHistory
            {
                UserId = userId,
                OldStatus = oldStatus,
                NewStatus = AccountConstants.Statuses.Suspended,
                Reason = reason,
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                ChangedBy = adminUserId,
                ChangedAt = DateTime.UtcNow,
                NotificationStatus = AccountConstants.NotificationStatus.None
            };
            await _historyRepo.AddStatusHistoryAsync(history);
            await _historyRepo.SaveChangesAsync();

            // 6. Thu hồi toàn bộ refresh token (kết thúc phiên)
            await _refreshTokenRepo.RevokeAllUserTokensAsync(userId, "AccountSuspended");

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // 7. Đồng bộ cache trạng thái để chặn ngay ở Middleware Pipeline
        InvalidateUserCache(userId);

        // 8. Gửi email — thất bại KHÔNG rollback trạng thái, chỉ ghi NotificationStatus = Failed
        var sent = await _notificationService.SendAccountSuspendedAsync(
            user.Email, user.FullName, reason, dto.Notes);

        await _historyRepo.UpdateNotificationStatusAsync(
            history.Id,
            sent ? AccountConstants.NotificationStatus.Sent : AccountConstants.NotificationStatus.Failed);

        _logger.LogInformation("Admin {AdminId} đã tạm ngưng tài khoản {UserId} (lý do: {Reason}, email: {Sent})",
            adminUserId, userId, reason, sent);

        var updated = await _accountRepo.GetByIdAsync(userId);
        return MapToDto(updated ?? user);
    }

    // ─────────────────────────────────────────────
    // RESTORE ACCOUNT
    // ─────────────────────────────────────────────

    public async Task<AccountDto> RestoreAsync(int userId, RestoreAccountDto dto, int adminUserId)
    {
        var user = await _accountRepo.GetTrackedByIdAsync(userId)
                   ?? throw new KeyNotFoundException($"Tài khoản id {userId} không tồn tại.");

        // 1. Chỉ khôi phục tài khoản đang Suspended
        if (user.AccountStatus != AccountConstants.Statuses.Suspended)
            throw new InvalidOperationException("Tài khoản không ở trạng thái tạm ngưng, không thể khôi phục.");

        var oldStatus = user.AccountStatus;
        var reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        AccountStatusHistory history;

        await using var transaction = await _accountRepo.BeginTransactionAsync();
        try
        {
            // 2. Đổi Status về Active — KHÔNG tự động cấp quyền Admin
            user.AccountStatus = AccountConstants.Statuses.Active;
            user.SuspensionReason = null;
            user.SuspendedAt = null;
            user.SuspendedById = null;
            user.UpdatedAt = DateTime.UtcNow;
            await _accountRepo.SaveChangesAsync();

            // 3. Ghi lịch sử trạng thái
            history = new AccountStatusHistory
            {
                UserId = userId,
                OldStatus = oldStatus,
                NewStatus = AccountConstants.Statuses.Active,
                Reason = reason ?? "restore",
                Notes = null,
                ChangedBy = adminUserId,
                ChangedAt = DateTime.UtcNow,
                NotificationStatus = AccountConstants.NotificationStatus.None
            };
            await _historyRepo.AddStatusHistoryAsync(history);
            await _historyRepo.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        // 4. Đồng bộ cache trạng thái
        InvalidateUserCache(userId);

        // 5. Gửi email — thất bại KHÔNG rollback, chỉ ghi NotificationStatus = Failed
        var sent = await _notificationService.SendAccountRestoredAsync(user.Email, user.FullName, reason);

        await _historyRepo.UpdateNotificationStatusAsync(
            history.Id,
            sent ? AccountConstants.NotificationStatus.Sent : AccountConstants.NotificationStatus.Failed);

        _logger.LogInformation("Admin {AdminId} đã khôi phục tài khoản {UserId} (email: {Sent})",
            adminUserId, userId, sent);

        var updated = await _accountRepo.GetByIdAsync(userId);
        return MapToDto(updated ?? user);
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    /// <summary>Xóa cache trạng thái/vai trò để thay đổi có hiệu lực ngay ở Middleware Pipeline.</summary>
    private void InvalidateUserCache(int userId)
    {
        _cache.Remove($"user_status_{userId}");
        _cache.Remove($"user_roles_{userId}");
    }

    private static AccountDto MapToDto(User user)
    {
        var dto = new AccountDto();
        CopyBase(dto, user);
        return dto;
    }

    private static void CopyBase(AccountDto dto, User user)
    {
        var roleNames = user.UserRoleUsers
            .Select(ur => ur.Role.RoleName)
            .ToList();

        dto.Id = user.Id;
        dto.Username = user.Code;
        dto.FullName = user.FullName;
        dto.Email = user.Email;
        dto.Phone = user.Phone;
        dto.Gender = user.Gender;
        dto.Status = user.AccountStatus;
        dto.SuspensionReason = user.SuspensionReason;
        dto.SuspendedAt = user.SuspendedAt;
        dto.SuspendedById = user.SuspendedById;
        dto.CreatedAt = user.CreatedAt;
        dto.UpdatedAt = user.UpdatedAt;
        dto.Roles = roleNames;
        dto.PrimaryRole = RoleHelper.ResolvePrimaryRole(roleNames);
    }
}
