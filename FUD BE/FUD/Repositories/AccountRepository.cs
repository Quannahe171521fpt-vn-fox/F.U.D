using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Account;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

/// <summary>Repository cho module Account Management (EF Core).</summary>
public class AccountRepository : IAccountRepository
{
    private readonly FudContext _context;

    public AccountRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int userId)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
    }

    public async Task<User?> GetTrackedByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null);
    }

    public async Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(AccountQueryDto query)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        var q = _context.Users
            .AsNoTracking()
            .Include(u => u.UserRoleUsers)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var keyword = query.Search.Trim();
            q = q.Where(u => u.Code.Contains(keyword)
                         || u.FullName.Contains(keyword)
                         || u.Email.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            q = q.Where(u => u.AccountStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role.Trim();
            q = q.Where(u => u.UserRoleUsers.Any(ur => ur.Role.RoleName == role));
        }

        var totalCount = await q.CountAsync();

        var items = await q
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, int? excludeUserId = null)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedUsername = username?.Trim() ?? string.Empty;

        return await _context.Users
            .AnyAsync(u => u.DeletedAt == null
                        && (u.Code == normalizedUsername || u.Email.ToLower() == normalizedEmail)
                        && (excludeUserId == null || u.Id != excludeUserId));
    }

    public async Task<List<Role>> GetRolesByNamesAsync(IEnumerable<string> roleNames)
    {
        var names = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct()
            .ToList();

        if (names.Count == 0)
            return new List<Role>();

        return await _context.Roles
            .AsNoTracking()
            .Where(r => names.Contains(r.RoleName))
            .ToListAsync();
    }

    public async Task<List<string>> GetUserRoleNamesAsync(int userId)
    {
        var names = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.RoleName)
            .ToListAsync();

        return names;
    }

    public async Task<int> CountActiveAdminsAsync(int? excludeUserId = null)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null
                     && u.AccountStatus == AccountConstants.Statuses.Active
                     && u.UserRoleUsers.Any(ur => ur.Role.RoleName == AccountConstants.Roles.Admin))
            .CountAsync(u => excludeUserId == null || u.Id != excludeUserId);
    }

    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        return user;
    }

    public async Task AddUserRolesAsync(int userId, IEnumerable<Role> roles, int? assignedById)
    {
        foreach (var role in roles)
        {
            await _context.UserRoles.AddAsync(new UserRole
            {
                UserId = userId,
                RoleId = role.Id,
                AssignedById = assignedById,
                AssignedAt = DateTime.UtcNow
            });
        }
    }

    public async Task RemoveUserRolesAsync(int userId, IEnumerable<int> keepRoleIds)
    {
        var keep = keepRoleIds.ToList();
        var toRemove = await _context.UserRoles
            .Where(ur => ur.UserId == userId && !keep.Contains(ur.RoleId))
            .ToListAsync();

        if (toRemove.Count > 0)
            _context.UserRoles.RemoveRange(toRemove);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<IAccountTransaction> BeginTransactionAsync()
    {
        var tx = await _context.Database.BeginTransactionAsync();
        return new AccountTransaction(tx);
    }

    /// <summary>Wrapper cho IDbContextTransaction, giữ Service Layer không phụ thuộc EF Core.</summary>
    private sealed class AccountTransaction : IAccountTransaction
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;
        private bool _disposed;

        public AccountTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync()
        {
            await _transaction.CommitAsync();
        }

        public async Task RollbackAsync()
        {
            await _transaction.RollbackAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            await _transaction.DisposeAsync();
        }
    }
}
