using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.Models;

namespace FUD.Repositories.Interfaces;

public interface INotificationRepository
{
    Task<(List<Notification> Items, int TotalCount, int UnreadCount)> GetUserNotificationsAsync(
        int userId,
        bool unreadOnly,
        int page,
        int pageSize,
        string? readStatus = null,
        string? referenceType = null,
        string? search = null);

    Task<int> GetUnreadCountAsync(int userId);

    Task<(Notification? Notification, int UnreadCount)> MarkAsReadAsync(
        int notificationId, int userId, DateTime readTime);

    Task<int> MarkAllAsReadAsync(int userId, DateTime readTime);

    Task AddRangeAsync(IEnumerable<Notification> notifications);

    Task<List<int>> GetActiveUserIdsByRolesAsync(IEnumerable<string> roleNames);

    Task<List<int>> GetActiveGuardUserIdsByDormAsync(int dormId);
}
