using Notifications.Domain.Enums;

namespace Notifications.Application.DTOs;

public record NotificationListItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public NotificationType Type { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
}

public record NotificationPageDto(IReadOnlyList<NotificationListItemDto> Items, int PageIndex, int PageSize,
    int TotalCount, int TotalPages);

public record UnreadNotificationCountDto(int UnreadCount);
