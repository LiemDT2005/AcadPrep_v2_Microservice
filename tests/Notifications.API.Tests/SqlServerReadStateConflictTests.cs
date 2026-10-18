using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

public class SqlServerReadStateConflictTests
{
    [Theory]
    [InlineData(2601, "Duplicate key with unique index 'PK_UserNotificationStates'.", true)]
    [InlineData(2627, "Violation of PRIMARY KEY constraint 'PK_UserNotificationStates'.", true)]
    [InlineData(2627, "Violation of PRIMARY KEY constraint 'PK_Notifications'.", false)]
    [InlineData(2627, "Violation of constraint 'PK_UserNotificationStates_Extra'.", false)]
    [InlineData(547, "Foreign key constraint 'PK_UserNotificationStates'.", false)]
    [InlineData(1205, "Deadlock 'PK_UserNotificationStates'.", false)]
    [InlineData(2601, "Duplicate key with index 'IX_Notifications_SourceEventId'.", false)]
    public void ErrorClassification_RequiresExpectedNumberAndExactStateKey(int number, string message, bool expected)
    {
        Assert.Equal(expected, NotificationReadStateStore.IsExpectedSqlServerDuplicate(number, message));
    }

    [Fact]
    public void NonSqlServerDbUpdateException_IsNotSuppressed()
    {
        using var db = new NotificationsDbContextFactory().CreateDbContext([]);
        var probe = new ConflictProbe(db);
        Assert.False(probe.IsExpected(new DbUpdateException("Other persistence failure.", new InvalidOperationException()),
            new UserNotificationState()));
    }

    private sealed class ConflictProbe(NotificationsDbContext db) : NotificationReadStateStore(db)
    {
        public bool IsExpected(DbUpdateException exception, UserNotificationState state) => IsDuplicateStateConflict(exception, state);
    }
}
