using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

// SQL Server provider metadata and SQL generation only. No connection is opened or database created.
public class NotificationSchemaTests
{
    [Fact]
    public void Model_ContainsOnlyNotificationAndPerUserState()
    {
        using var db = CreateContext();
        Assert.Equal(new[] { typeof(Notification), typeof(UserNotificationState) },
            db.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.Equal("Notifications", NotificationModel(db).GetTableName());
        Assert.Equal("UserNotificationStates", StateModel(db).GetTableName());
    }

    [Fact]
    public void Columns_HaveRequiredLengthsNullabilityAndSqlServerTypes()
    {
        using var db = CreateContext();
        var notification = NotificationModel(db);
        Assert.Equal(200, notification.FindProperty(nameof(Notification.Title))!.GetMaxLength());
        Assert.Equal(2000, notification.FindProperty(nameof(Notification.Content))!.GetMaxLength());
        foreach (var name in new[] { "Id", "Title", "Content", "Type", "Audience", "CreatedAt" })
            Assert.False(notification.FindProperty(name)!.IsNullable);
        foreach (var name in new[] { "TargetUserId", "DataJson", "SourceEventId", "CreatedBy" })
            Assert.True(notification.FindProperty(name)!.IsNullable);
        Assert.Equal("nvarchar(200)", notification.FindProperty("Title")!.GetColumnType());
        Assert.Equal("nvarchar(2000)", notification.FindProperty("Content")!.GetColumnType());
        Assert.Equal("nvarchar(max)", notification.FindProperty("DataJson")!.GetColumnType());
        Assert.Equal("uniqueidentifier", notification.FindProperty("Id")!.GetColumnType());
        Assert.True(StateModel(db).FindProperty("ReadAt")!.IsNullable);
        Assert.True(StateModel(db).FindProperty("DeletedAt")!.IsNullable);
    }

    [Fact]
    public void State_HasCompositeKeyAndRequiredNotificationForeignKey()
    {
        using var db = CreateContext();
        var state = StateModel(db);
        Assert.Equal(new[] { "NotificationId", "UserId" }, state.FindPrimaryKey()!.Properties.Select(property => property.Name));
        var foreignKey = Assert.Single(state.GetForeignKeys());
        Assert.Equal(typeof(Notification), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal("NotificationId", Assert.Single(foreignKey.Properties).Name);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void DuplicateEvent_HasUniqueFilteredIndexAllowingNullSourceIds()
    {
        using var db = CreateContext();
        var index = Assert.Single(NotificationModel(db).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "SourceEventId" }));
        Assert.True(index.IsUnique);
        Assert.Equal("[SourceEventId] IS NOT NULL", index.GetFilter());
        Assert.True(Assert.Single(index.Properties).IsNullable);
    }

    [Theory]
    [InlineData("Audience", "CreatedAt")]
    [InlineData("TargetUserId", "CreatedAt")]
    public void RecipientIndexes_SupportNewestFirstQueries(string recipient, string timestamp)
    {
        using var db = CreateContext();
        var index = Assert.Single(NotificationModel(db, designTime: true).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { recipient, timestamp }));
        Assert.Equal(new[] { false, true }, index.IsDescending);
    }

    [Fact]
    public void Indexes_SupportCreatedAtAndPerUserStateLookups()
    {
        using var db = CreateContext();
        Assert.Contains(NotificationModel(db).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "CreatedAt" }));
        Assert.Contains(StateModel(db).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "UserId", "IsDeleted", "IsRead" }));
    }

    [Theory]
    [InlineData(NotificationType.System, "System")]
    [InlineData(NotificationType.ExamResult, "ExamResult")]
    [InlineData(NotificationType.Subscription, "Subscription")]
    [InlineData(NotificationType.Reminder, "Reminder")]
    public void Type_IsConvertedToAndFromString(NotificationType value, string stored)
    {
        using var db = CreateContext();
        var converter = NotificationModel(db).FindProperty("Type")!.GetTypeMapping().Converter!;
        Assert.Equal(stored, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(stored));
    }

    [Theory]
    [InlineData(NotificationAudience.User, "User")]
    [InlineData(NotificationAudience.All, "All")]
    [InlineData(NotificationAudience.Learner, "Learner")]
    [InlineData(NotificationAudience.Moderator, "Moderator")]
    public void Audience_IsConvertedToAndFromString(NotificationAudience value, string stored)
    {
        using var db = CreateContext();
        var converter = NotificationModel(db).FindProperty("Audience")!.GetTypeMapping().Converter!;
        Assert.Equal(stored, converter.ConvertToProvider(value));
        Assert.Equal(value, converter.ConvertFromProvider(stored));
    }

    [Theory]
    [InlineData("CreatedAt")]
    [InlineData("ReadAt")]
    [InlineData("DeletedAt")]
    public void TimestampMapping_PreservesUtcAndRestoresKindAfterDatabaseRead(string name)
    {
        using var db = CreateContext();
        var entity = name == "CreatedAt" ? NotificationModel(db) : StateModel(db);
        var property = entity.FindProperty(name)!;
        var converter = property.GetTypeMapping().Converter!;
        var utc = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal("datetime2", property.GetColumnType());
        Assert.Equal(utc, converter.ConvertToProvider(utc));
        var read = Assert.IsType<DateTime>(converter.ConvertFromProvider(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(utc.Ticks, read.Ticks);
        if (property.IsNullable)
        {
            Assert.Null(converter.ConvertToProvider(null));
            Assert.Null(converter.ConvertFromProvider(null));
        }
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void NonUtcTimestamp_IsRejectedInsteadOfSilentlyAssumingTimezone(DateTimeKind kind)
    {
        using var db = CreateContext();
        var converter = NotificationModel(db).FindProperty("CreatedAt")!.GetTypeMapping().Converter!;
        Assert.Throws<ArgumentException>(() => converter.ConvertToProvider(new DateTime(2026, 10, 18, 12, 0, 0, kind)));
    }

    [Fact]
    public void NewNotification_HasGuidUtcCreationAndDoesNotFanOutStates()
    {
        var notification = new Notification { Audience = NotificationAudience.All };
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.Equal(DateTimeKind.Utc, notification.CreatedAt.Kind);
        Assert.Equal(NotificationType.System, notification.Type);
        Assert.Empty(notification.UserStates);
    }

    [Fact]
    public void TrackingDuplicateStateKey_IsRejectedBeforeDatabaseAccess()
    {
        using var db = CreateContext();
        var notificationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.UserNotificationStates.Add(new UserNotificationState { NotificationId = notificationId, UserId = userId });
        Assert.Throws<InvalidOperationException>(() => db.UserNotificationStates.Add(
            new UserNotificationState { NotificationId = notificationId, UserId = userId }));
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void MarkingOneUsersStateDeleted_DoesNotDeleteNotificationOrOtherUsersState()
    {
        using var db = CreateContext();
        var notification = new Notification { Title = "Broadcast", Content = "Message", Audience = NotificationAudience.All };
        var first = new UserNotificationState { Notification = notification, NotificationId = notification.Id, UserId = Guid.NewGuid() };
        var second = new UserNotificationState { Notification = notification, NotificationId = notification.Id, UserId = Guid.NewGuid() };
        db.Notifications.Attach(notification);
        db.UserNotificationStates.AttachRange(first, second);
        first.IsDeleted = true;
        first.DeletedAt = DateTime.UtcNow;
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Unchanged, db.Entry(notification).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(second).State);
        Assert.False(second.IsDeleted);
        Assert.Equal(EntityState.Modified, db.Entry(first).State);
    }

    [Fact]
    public void Migration_ContainsOnlyTwoTablesWithDuplicateAndForeignKeyConstraints()
    {
        using var db = CreateContext();
        var migration = GetInitialMigration(db);
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(new[] { "Notifications", "UserNotificationStates" }, tables.Select(table => table.Name));
        var stateTable = tables.Single(table => table.Name == "UserNotificationStates");
        Assert.Equal(new[] { "NotificationId", "UserId" }, stateTable.PrimaryKey!.Columns);
        var foreignKey = Assert.Single(stateTable.ForeignKeys);
        Assert.Equal("Notifications", foreignKey.PrincipalTable);
        Assert.Equal(ReferentialAction.Restrict, foreignKey.OnDelete);
        var eventIndex = migration.UpOperations.OfType<CreateIndexOperation>().Single(index => index.Name == "IX_Notifications_SourceEventId");
        Assert.True(eventIndex.IsUnique);
        Assert.Equal("[SourceEventId] IS NOT NULL", eventIndex.Filter);
        Assert.Equal(new[] { "UserNotificationStates", "Notifications" },
            migration.DownOperations.OfType<DropTableOperation>().Select(table => table.Name));
    }

    [Fact]
    public void SqlServerMigrationScript_ContainsRequiredConstraintsWithoutExecutingSql()
    {
        using var db = CreateContext();
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [Notifications]", sql);
        Assert.Contains("[Title] nvarchar(200) NOT NULL", sql);
        Assert.Contains("[Content] nvarchar(2000) NOT NULL", sql);
        Assert.Contains("[Type] nvarchar(20) NOT NULL", sql);
        Assert.Contains("SYSUTCDATETIME()", sql);
        Assert.Contains("PRIMARY KEY ([NotificationId], [UserId])", sql);
        Assert.Contains("FOREIGN KEY ([NotificationId]) REFERENCES [Notifications] ([Id])", sql);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Notifications_SourceEventId] ON [Notifications] ([SourceEventId]) WHERE [SourceEventId] IS NOT NULL", sql);
        Assert.Contains("CHECK (([Audience] = N'User' AND [TargetUserId] IS NOT NULL)", sql);
        Assert.Contains("CHECK ([Type] IN (N'System', N'ExamResult', N'Subscription', N'Reminder'))", sql);
        Assert.DoesNotContain("ON DELETE CASCADE", sql);
        Assert.DoesNotContain("AspNetUsers", sql);
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    [Fact]
    public void Snapshot_MatchesCurrentSqlServerModel()
    {
        using var db = CreateContext();
        Assert.NotNull(db.GetService<IMigrationsAssembly>().ModelSnapshot);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    private static NotificationsDbContext CreateContext() => new NotificationsDbContextFactory().CreateDbContext([]);

    private static IEntityType NotificationModel(NotificationsDbContext db, bool designTime = false) =>
        (designTime ? db.GetService<IDesignTimeModel>().Model : db.Model).FindEntityType(typeof(Notification))!;

    private static IEntityType StateModel(NotificationsDbContext db) => db.Model.FindEntityType(typeof(UserNotificationState))!;

    private static Migration GetInitialMigration(NotificationsDbContext db)
    {
        var assembly = db.GetService<IMigrationsAssembly>();
        return assembly.CreateMigration(Assert.Single(assembly.Migrations).Value, db.Database.ProviderName!);
    }
}
