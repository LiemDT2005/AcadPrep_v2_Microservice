using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.DTOs;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Notifications.Infrastructure.Persistence;
using Xunit;

namespace Notifications.API.Tests;

[Collection("NotificationSqlServer")]
[Trait("Category", "SqlServer")]
public class NotificationSqlServerIntegrationTests
{
    [SqlServerFact]
    public async Task ValidInsertAndDatabaseUtcDefaultWork()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var item = factory.NewNotification();
            item.CreatedAt = default;
            await factory.SaveAsync(item);
            await using var db = factory.OpenContext();
            var saved = await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == item.Id);
            Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
            Assert.InRange(saved.CreatedAt, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(2));
            Assert.Equal(item.Title, saved.Title);
            Assert.Equal(item.Content, saved.Content);
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public async Task AudienceTypeAndLengthConstraintsRejectInvalidRows()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            foreach (var kind in new[] { "UserNoTarget", "AllWithTarget", "BadType", "BadAudience", "LongTitle", "LongContent" })
            {
                var item = factory.NewNotification();
                if (kind == "UserNoTarget") item.Audience = NotificationAudience.User;
                if (kind == "AllWithTarget") item.TargetUserId = Guid.NewGuid();
                if (kind == "BadType") item.Type = (NotificationType)999;
                if (kind == "BadAudience") item.Audience = (NotificationAudience)999;
                if (kind == "LongTitle") item.Title = new string('a', 201);
                if (kind == "LongContent") item.Content = new string('a', 2001);
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => factory.SaveAsync(item));
                var sql = Assert.IsType<SqlException>(error.InnerException);
                Assert.Contains(sql.Number, kind.StartsWith("Long") ? new[] { 2628, 8152 } : new[] { 547 });
            }
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public async Task SourceEventUniqueFilterAcceptsNullsAndRejectsDuplicateNonNull()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var first = factory.NewNotification();
            first.SourceEventId = Guid.NewGuid();
            var second = factory.NewNotification();
            var third = factory.NewNotification();
            await factory.SaveAsync(first, second, third);
            var duplicate = factory.NewNotification();
            duplicate.SourceEventId = first.SourceEventId;
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => factory.SaveAsync(duplicate));
            var sql = Assert.IsType<SqlException>(error.InnerException);
            Assert.Equal(2601, sql.Number);
            await using var db = factory.OpenContext();
            Assert.Equal(2, await db.Notifications.CountAsync(n => (n.Id == second.Id || n.Id == third.Id) && n.SourceEventId == null));
            Assert.False(new ConflictProbe(db).Expected(error, new UserNotificationState()));
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public async Task CompositePkAndForeignKeyErrorsUseRealSqlServerClassifier()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var item = factory.NewNotification();
            await factory.SaveAsync(item);
            await using (var first = factory.OpenContext())
            {
                first.UserNotificationStates.Add(new() { NotificationId = item.Id, UserId = factory.LearnerId });
                await first.SaveChangesAsync();
            }
            await using (var duplicateDb = factory.OpenContext())
            {
                var duplicate = new UserNotificationState { NotificationId = item.Id, UserId = factory.LearnerId };
                duplicateDb.UserNotificationStates.Add(duplicate);
                var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
                Assert.Equal(2627, Assert.IsType<SqlException>(error.InnerException).Number);
                Assert.True(new ConflictProbe(duplicateDb).Expected(error, duplicate));
            }
            await using var fkDb = factory.OpenContext();
            var orphan = new UserNotificationState { NotificationId = Guid.NewGuid(), UserId = factory.LearnerId };
            fkDb.UserNotificationStates.Add(orphan);
            var fk = await Assert.ThrowsAsync<DbUpdateException>(() => fkDb.SaveChangesAsync());
            Assert.Equal(547, Assert.IsType<SqlException>(fk.InnerException).Number);
            Assert.False(new ConflictProbe(fkDb).Expected(fk, orphan));
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public async Task ApiQueriesReadDeleteAndUserIsolationWorkOnSqlServer()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var all = factory.NewNotification();
            var own = factory.NewNotification(NotificationAudience.User, factory.LearnerId);
            var other = factory.NewNotification(NotificationAudience.User, factory.OtherUserId);
            var learner = factory.NewNotification(NotificationAudience.Learner);
            var moderator = factory.NewNotification(NotificationAudience.Moderator);
            await factory.SaveAsync(all, own, other, learner, moderator);
            var otherReadAt = DateTime.UtcNow.AddHours(-1);
            await using (var db = factory.OpenContext())
            {
                db.UserNotificationStates.Add(new()
                {
                    NotificationId = all.Id, UserId = factory.OtherUserId, IsRead = true, ReadAt = otherReadAt
                });
                await db.SaveChangesAsync();
            }
            using var client = factory.AuthenticatedClient();
            var page = await GetPageAsync(client, "/api/notifications?pageSize=100");
            Assert.NotNull(page);
            Assert.Contains(page.Items, n => n.Id == own.Id);
            Assert.Contains(page.Items, n => n.Id == learner.Id);
            Assert.DoesNotContain(page.Items, n => n.Id == other.Id || n.Id == moderator.Id);
            using var multi = factory.AuthenticatedClient(null, "Learner", "Moderator");
            var multiple = await GetPageAsync(multi, "/api/notifications?pageSize=100");
            Assert.Contains(multiple!.Items, n => n.Id == moderator.Id);
            var before = (await client.GetFromJsonAsync<UnreadNotificationCountDto>("/api/notifications/unread-count"))!.UnreadCount;
            Assert.Null(await factory.StateAsync(all.Id));
            using var read = await client.PatchAsync($"/api/notifications/{all.Id}/read", null);
            Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
            var readAt = (await factory.StateAsync(all.Id))!.ReadAt;
            using var repeatedRead = await client.PatchAsync($"/api/notifications/{all.Id}/read", null);
            Assert.Equal(HttpStatusCode.NoContent, repeatedRead.StatusCode);
            Assert.Equal(readAt, (await factory.StateAsync(all.Id))!.ReadAt);
            Assert.Equal(before - 1, (await client.GetFromJsonAsync<UnreadNotificationCountDto>("/api/notifications/unread-count"))!.UnreadCount);
            using var deleted = await client.DeleteAsync($"/api/notifications/{all.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            var deletedAt = (await factory.StateAsync(all.Id))!.DeletedAt;
            using var repeatedDelete = await client.DeleteAsync($"/api/notifications/{all.Id}");
            Assert.Equal(HttpStatusCode.NoContent, repeatedDelete.StatusCode);
            var final = (await factory.StateAsync(all.Id))!;
            Assert.Equal(deletedAt, final.DeletedAt);
            Assert.Equal(readAt, final.ReadAt);
            using var afterDeleteRead = await client.PatchAsync($"/api/notifications/{all.Id}/read", null);
            Assert.Equal(HttpStatusCode.NotFound, afterDeleteRead.StatusCode);
            var unread = await GetPageAsync(client, "/api/notifications?unreadOnly=true&pageSize=100");
            Assert.DoesNotContain(unread!.Items, n => n.Id == all.Id);
            using var otherClient = factory.AuthenticatedClient(factory.OtherUserId);
            var otherPage = await GetPageAsync(otherClient, "/api/notifications?pageSize=100");
            Assert.Contains(otherPage!.Items, n => n.Id == all.Id);
            var otherState = (await factory.StateAsync(all.Id, factory.OtherUserId))!;
            Assert.True(otherState.IsRead);
            Assert.False(otherState.IsDeleted);
            Assert.Equal(otherReadAt, otherState.ReadAt);
            Assert.Null(otherState.DeletedAt);
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public async Task ForeignKeyFailureInPersistenceIsNotConvertedTo204()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var item = factory.NewNotification();
            await factory.SaveAsync(item);
            factory.Race.BeforeInsert = (state, _) => { state.NotificationId = Guid.NewGuid(); return Task.CompletedTask; };
            using var client = factory.AuthenticatedClient();
            using var response = await client.PatchAsync($"/api/notifications/{item.Id}/read", null);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(0, factory.Race.DuplicateConflicts);
            Assert.Null(await factory.StateAsync(item.Id));
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    private static Task<NotificationPageDto?> GetPageAsync(HttpClient client, string path) =>
        client.GetFromJsonAsync<NotificationPageDto>(path, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        });

    private sealed class ConflictProbe(NotificationsDbContext db) : NotificationReadStateStore(db)
    {
        public bool Expected(DbUpdateException exception, UserNotificationState state) => IsDuplicateStateConflict(exception, state);
    }
}
