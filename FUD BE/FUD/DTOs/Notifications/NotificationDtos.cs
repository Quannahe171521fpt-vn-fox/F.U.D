using System;
using System.Collections.Generic;

namespace FUD.DTOs.Notifications;

public class NotificationItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string Channel { get; set; } = "InApp";
    public bool ReadFlag { get; set; }
    public DateTime? ReadTime { get; set; }
    public DateTime SentTime { get; set; }
}

public class NotificationListResponseDto
{
    public List<NotificationItemDto> Items { get; set; } = new();
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UnreadCountResponseDto
{
    public int UnreadCount { get; set; }
}

public class MarkNotificationReadResponseDto
{
    public int Id { get; set; }
    public bool ReadFlag { get; set; }
    public DateTime? ReadTime { get; set; }
    public int UnreadCount { get; set; }
}

public class MarkAllReadResponseDto
{
    public int MarkedCount { get; set; }
    public int UnreadCount { get; set; }
}
