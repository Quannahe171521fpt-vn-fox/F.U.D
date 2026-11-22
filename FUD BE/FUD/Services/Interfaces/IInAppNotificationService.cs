using System.Collections.Generic;
using System.Threading.Tasks;
using FUD.DTOs.Notifications;

namespace FUD.Services.Interfaces;

public interface IInAppNotificationService
{
    Task<NotificationListResponseDto> GetMyNotificationsAsync(
        int userId,
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20,
        string? readStatus = null,
        string? referenceType = null,
        string? search = null);

    Task<UnreadCountResponseDto> GetUnreadCountAsync(int userId);

    Task<MarkNotificationReadResponseDto> MarkAsReadAsync(int userId, int notificationId);

    Task<MarkAllReadResponseDto> MarkAllAsReadAsync(int userId);

    Task NotifyUserAsync(
        int userId, string title, string? content,
        string? referenceType = null, string? referenceId = null,
        bool skipEmail = false);

    Task NotifyUsersAsync(
        IEnumerable<int> userIds, string title, string? content,
        string? referenceType = null, string? referenceId = null,
        bool skipEmail = false);

    Task NotifyRolesAsync(
        IEnumerable<string> roles, string title, string? content,
        string? referenceType = null, string? referenceId = null);

    Task NotifyDormGuardsAsync(
        int dormId, string title, string? content,
        string? referenceType = null, string? referenceId = null);
}
