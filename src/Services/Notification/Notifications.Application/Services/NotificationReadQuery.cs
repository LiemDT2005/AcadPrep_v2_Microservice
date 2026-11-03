using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain.Enums;
using Notifications.Domain.Entities;

namespace Notifications.Application.Services;

// Shared by list/count; internal visibility lets tests verify SQL Server translation offline.
internal static class NotificationReadQuery
{
    internal static IQueryable<Notification> AudienceFor(INotificationsDbContext db, Guid userId,
        IReadOnlyCollection<string> roles)
    {
        var isLearner = roles.Contains("Learner");
        var isModerator = roles.Contains("Moderator");
        return db.Notifications.AsNoTracking().Where(notification =>
            notification.Audience == NotificationAudience.User && notification.TargetUserId == userId ||
            notification.Audience == NotificationAudience.All ||
            isLearner && notification.Audience == NotificationAudience.Learner ||
            isModerator && notification.Audience == NotificationAudience.Moderator);
    }

    internal static IQueryable<NotificationListItemDto> VisibleTo(INotificationsDbContext db, Guid userId,
        IReadOnlyCollection<string> roles)
    {
        return from notification in AudienceFor(db, userId, roles)
               join state in db.UserNotificationStates.AsNoTracking().Where(state => state.UserId == userId)
                   on notification.Id equals state.NotificationId into states
               from state in states.DefaultIfEmpty()
               where state == null || !state.IsDeleted
               select new NotificationListItemDto
               {
                   Id = notification.Id, Title = notification.Title, Content = notification.Content,
                   Type = notification.Type, CreatedAt = notification.CreatedAt,
                   IsRead = state != null && state.IsRead, ReadAt = state == null ? null : state.ReadAt
               };
    }

    internal static IQueryable<NotificationListItemDto> Page(IQueryable<NotificationListItemDto> query,
        int skip, int pageSize) => query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Skip(skip).Take(pageSize);
}
