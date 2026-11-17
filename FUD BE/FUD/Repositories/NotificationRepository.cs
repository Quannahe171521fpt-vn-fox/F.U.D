using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.Models;
using FUD.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FUD.Repositories;

public class NotificationRepository : INotificationRepository
{
    private const string InAppChannel = "InApp";
    private readonly FudContext _context;

    public NotificationRepository(FudContext context)
    {
        _context = context;
    }

    public async Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetUserNotificationsAsync(
        int userId,
        bool unreadOnly,
        int page,
        int pageSize,
        string? readStatus = null,
        string? referenceType = null,
        string? search = null)
    {
        var baseQuery = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.Channel == InAppChannel);

        var unreadCount = await baseQuery.CountAsync(n => !n.ReadFlag);

        var effectiveReadStatus = string.IsNullOrWhiteSpace(readStatus)
            ? (unreadOnly ? "unread" : "all")
            : readStatus.Trim().ToLowerInvariant();

        var listQuery = effectiveReadStatus switch
        {
            "unread" => baseQuery.Where(n => !n.ReadFlag),
            "read" => baseQuery.Where(n => n.ReadFlag),
            "all" => baseQuery,
            _ => unreadOnly ? baseQuery.Where(n => !n.ReadFlag) : baseQuery
        };

        if (!string.IsNullOrWhiteSpace(referenceType) &&
            !referenceType.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var refTypeLower = referenceType.Trim().ToLowerInvariant();
            listQuery = listQuery.Where(n => n.ReferenceType != null && n.ReferenceType.ToLower() == refTypeLower);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim().ToLowerInvariant();
            listQuery = listQuery.Where(n =>
                (n.Title != null && n.Title.ToLower().Contains(keyword)) ||
                (n.Content != null && n.Content.ToLower().Contains(keyword)));
        }

        var totalCount = await listQuery.CountAsync();

        var items = await listQuery
            .OrderByDescending(n => n.SentTime)
            .ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount, unreadCount);
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.Channel == InAppChannel && !n.ReadFlag);
    }

    public async Task<(Notification? Notification, int UnreadCount)> MarkAsReadAsync(
        int notificationId, int userId, DateTime readTime)
    {
        var entity = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && n.Channel == InAppChannel);

        if (entity == null)
        {
            return (null, 0);
        }

        if (!entity.ReadFlag)
        {
            entity.ReadFlag = true;
            entity.ReadTime = readTime;
            await _context.SaveChangesAsync();
        }

        var unreadCount = await GetUnreadCountAsync(userId);
        return (entity, unreadCount);
    }

    public async Task<int> MarkAllAsReadAsync(int userId, DateTime readTime)
    {
        var unreadQuery = _context.Notifications
            .Where(n => n.UserId == userId && n.Channel == InAppChannel && !n.ReadFlag);

        if (_context.Database.IsRelational())
        {
            return await unreadQuery.ExecuteUpdateAsync(s => s
                .SetProperty(n => n.ReadFlag, true)
                .SetProperty(n => n.ReadTime, readTime));
        }

        var unreadItems = await unreadQuery.ToListAsync();
        if (unreadItems.Count == 0)
        {
            return 0;
        }

        foreach (var item in unreadItems)
        {
            item.ReadFlag = true;
            item.ReadTime = readTime;
        }

        await _context.SaveChangesAsync();
        return unreadItems.Count;
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications)
    {
        await _context.Notifications.AddRangeAsync(notifications);
        await _context.SaveChangesAsync();
    }

    public async Task<List<int>> GetActiveUserIdsByRolesAsync(IEnumerable<string> roleNames)
    {
        var roles = roleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct()
            .ToList();

        if (roles.Count == 0)
        {
            return new List<int>();
        }

        return await _context.Users
            .AsNoTracking()
            .Where(u => u.DeletedAt == null
                        && u.AccountStatus == AccountConstants.Statuses.Active
                        && u.UserRoleUsers.Any(ur => roles.Contains(ur.Role.RoleName)))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<int>> GetActiveGuardUserIdsByDormAsync(int dormId)
    {
        return await _context.Guards
            .AsNoTracking()
            .Where(g => g.DormId == dormId
                        && g.User.DeletedAt == null
                        && g.User.AccountStatus == AccountConstants.Statuses.Active)
            .Select(g => g.UserId)
            .Distinct()
            .ToListAsync();
    }
}
