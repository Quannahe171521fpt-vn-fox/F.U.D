using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FUD.DTOs.Notifications;
using FUD.Models;
using FUD.Repositories.Interfaces;
using FUD.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FUD.Services;

public class InAppNotificationService : IInAppNotificationService
{
    private const string InAppChannel = "InApp";
    private const int MaxTitleLength = 200;
    private const int MaxReferenceLength = 50;
    private const int MaxSearchLength = 200;

    private readonly INotificationRepository _repo;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(
        INotificationRepository repo,
        ILogger<InAppNotificationService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<NotificationListResponseDto> GetMyNotificationsAsync(
        int userId,
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20,
        string? readStatus = null,
        string? referenceType = null,
        string? search = null)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 50);

        var normalizedReadStatus = readStatus?.Trim().ToLowerInvariant() switch
        {
            "all" => "all",
            "unread" => "unread",
            "read" => "read",
            _ => unreadOnly ? "unread" : "all"
        };

        var trimmedRefType = referenceType?.Trim();
        var normalizedRefType = string.IsNullOrWhiteSpace(trimmedRefType) ||
            trimmedRefType.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? null
            : trimmedRefType.ToLowerInvariant() switch
            {
                "news" => "News",
                "booking" => "Booking",
                "request" => "Request",
                "violation" => "Violation",
                "checkin" => "Checkin",
                "utility" => "Utility",
                "invoice" => "Invoice",
                "account" => "Account",
                _ => trimmedRefType
            };

        var normalizedSearch = Truncate(search, MaxSearchLength);

        var (items, totalCount, unreadCount) = await _repo.GetUserNotificationsAsync(
            userId,
            unreadOnly,
            normalizedPage,
            normalizedPageSize,
            normalizedReadStatus,
            normalizedRefType,
            normalizedSearch);

        return new NotificationListResponseDto
        {
            Items = items.Select(MapToDto).ToList(),
            UnreadCount = unreadCount,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };
    }

    public async Task<UnreadCountResponseDto> GetUnreadCountAsync(int userId)
    {
        var count = await _repo.GetUnreadCountAsync(userId);
        return new UnreadCountResponseDto { UnreadCount = count };
    }

    public async Task<MarkNotificationReadResponseDto> MarkAsReadAsync(int userId, int notificationId)
    {
        var (entity, unreadCount) = await _repo.MarkAsReadAsync(notificationId, userId, DateTime.UtcNow);
        if (entity == null)
        {
            throw new KeyNotFoundException("Không tìm thấy thông báo.");
        }

        return new MarkNotificationReadResponseDto
        {
            Id = entity.Id,
            ReadFlag = entity.ReadFlag,
            ReadTime = entity.ReadTime,
            UnreadCount = unreadCount
        };
    }

    public async Task<MarkAllReadResponseDto> MarkAllAsReadAsync(int userId)
    {
        var marked = await _repo.MarkAllAsReadAsync(userId, DateTime.UtcNow);
        return new MarkAllReadResponseDto
        {
            MarkedCount = marked,
            UnreadCount = 0
        };
    }

    public Task NotifyUserAsync(
        int userId, string title, string? content,
        string? referenceType = null, string? referenceId = null)
    {
        if (userId <= 0)
        {
            return Task.CompletedTask;
        }

        return NotifyUsersAsync(new[] { userId }, title, content, referenceType, referenceId);
    }

    public async Task NotifyUsersAsync(
        IEnumerable<int> userIds, string title, string? content,
        string? referenceType = null, string? referenceId = null)
    {
        var safeTitle = Truncate(title, MaxTitleLength);
        if (string.IsNullOrEmpty(safeTitle))
        {
            return;
        }

        var targetIds = userIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (targetIds.Count == 0)
        {
            return;
        }

        var safeRefType = Truncate(referenceType, MaxReferenceLength);
        var safeRefId = Truncate(referenceId, MaxReferenceLength);
        var safeContent = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        var now = DateTime.UtcNow;

        var entities = targetIds.Select(uid => new Notification
        {
            UserId = uid,
            Channel = InAppChannel,
            Title = safeTitle,
            Content = safeContent,
            ReferenceType = safeRefType,
            ReferenceId = safeRefId,
            ReadFlag = false,
            SentTime = now
        }).ToList();

        try
        {
            await _repo.AddRangeAsync(entities);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không thể lưu InApp notification '{Title}' cho {Count} người dùng",
                safeTitle,
                targetIds.Count);
        }
    }

    public async Task NotifyRolesAsync(
        IEnumerable<string> roles, string title, string? content,
        string? referenceType = null, string? referenceId = null)
    {
        var roleList = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct()
            .ToList();

        if (roleList.Count == 0)
        {
            return;
        }

        try
        {
            var userIds = await _repo.GetActiveUserIdsByRolesAsync(roleList);
            await NotifyUsersAsync(userIds, title, content, referenceType, referenceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không thể truy vấn người dùng theo vai trò [{Roles}] cho thông báo '{Title}'",
                string.Join(", ", roleList),
                title);
        }
    }

    public async Task NotifyDormGuardsAsync(
        int dormId, string title, string? content,
        string? referenceType = null, string? referenceId = null)
    {
        if (dormId <= 0)
        {
            return;
        }

        try
        {
            var guardIds = await _repo.GetActiveGuardUserIdsByDormAsync(dormId);
            await NotifyUsersAsync(guardIds, title, content, referenceType, referenceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Không thể truy vấn bảo vệ tòa DormId={DormId} cho thông báo '{Title}'",
                dormId,
                title);
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static NotificationItemDto MapToDto(Notification n)
    {
        return new NotificationItemDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            ReferenceType = n.ReferenceType,
            ReferenceId = n.ReferenceId,
            Channel = n.Channel,
            ReadFlag = n.ReadFlag,
            ReadTime = n.ReadTime,
            SentTime = n.SentTime
        };
    }
}
