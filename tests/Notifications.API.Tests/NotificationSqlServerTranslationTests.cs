using Microsoft.EntityFrameworkCore;
using Notifications.Application.Services;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

public class NotificationSqlServerTranslationTests
{
    [Fact]
    public void ListQuery_TranslatesAudienceOwnershipStateAndPagingToSqlServerOffline()
    {
        using var db = new NotificationsDbContextFactory().CreateDbContext([]);
        var visible = NotificationReadQuery.VisibleTo(db, Guid.NewGuid(), new[] { "Learner", "Moderator" });
        var sql = NotificationReadQuery.Page(visible.Where(item => !item.IsRead), 20, 20).ToQueryString();
        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("[UserId]", sql);
        Assert.Contains("[TargetUserId]", sql);
        Assert.Contains("N'User'", sql);
        Assert.Contains("N'All'", sql);
        Assert.Contains("N'Learner'", sql);
        Assert.Contains("N'Moderator'", sql);
        Assert.Contains("[IsDeleted]", sql);
        Assert.Contains("[IsRead]", sql);
        Assert.Contains("ORDER BY [n].[CreatedAt] DESC, [n].[Id]", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("FETCH NEXT", sql);
        Assert.DoesNotContain("[DataJson]", sql);
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }
}
