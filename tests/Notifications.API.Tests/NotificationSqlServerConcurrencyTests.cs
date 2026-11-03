using System.Net;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Notifications.API.Tests;

[Collection("NotificationSqlServer")]
[Trait("Category", "SqlServer")]
public class NotificationSqlServerConcurrencyTests
{
    [SqlServerFact]
    public Task ConcurrentPatchInsertUsesProductionDuplicateRecovery() => FirstInsertRace(false);

    [SqlServerFact]
    public Task ConcurrentDeleteInsertUsesProductionDuplicateRecovery() => FirstInsertRace(true);

    private static async Task FirstInsertRace(bool delete)
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var item = factory.NewNotification();
            await factory.SaveAsync(item);
            var barrier = new RaceBarrier(2);
            DateTime? winner = null;
            factory.Race.BeforeInsert = (_, ct) => barrier.ArriveAsync(ct);
            factory.Race.AfterInsert = state => winner = delete ? state.DeletedAt : state.ReadAt;
            using var client = factory.AuthenticatedClient();
            Task<HttpResponseMessage> Send() => delete ? client.DeleteAsync($"/api/notifications/{item.Id}")
                : client.PatchAsync($"/api/notifications/{item.Id}/read", null);
            var responses = await Task.WhenAll(Send(), Send());
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
                Assert.Equal(1, factory.Race.DuplicateConflicts);
                Assert.Equal(2627, factory.Race.LastDuplicateNumber);
                var state = (await factory.StateAsync(item.Id))!;
                Assert.NotNull(winner);
                Assert.Equal(winner, delete ? state.DeletedAt : state.ReadAt);
                Assert.Equal(delete, state.IsDeleted);
                await using var db = factory.OpenContext();
                Assert.Equal(1, await db.UserNotificationStates.CountAsync(s => s.NotificationId == item.Id));
                using var repeated = await Send();
                Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
                var after = (await factory.StateAsync(item.Id))!;
                Assert.Equal(state.ReadAt, after.ReadAt);
                Assert.Equal(state.DeletedAt, after.DeletedAt);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public Task ConcurrentPatchDeleteWhenPatchInsertsFirst() => MixedInsertRace(true);

    [SqlServerFact]
    public Task ConcurrentPatchDeleteWhenDeleteInsertsFirst() => MixedInsertRace(false);

    private static async Task MixedInsertRace(bool readWins)
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var item = factory.NewNotification();
            await factory.SaveAsync(item);
            var barrier = new RaceBarrier(2);
            var inserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            DateTime? winnerReadAt = null;
            DateTime? winnerDeletedAt = null;
            factory.Race.BeforeInsert = async (state, ct) =>
            {
                await barrier.ArriveAsync(ct);
                if (state.IsRead != readWins) await inserted.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            };
            factory.Race.AfterInsert = state =>
            {
                winnerReadAt = state.ReadAt;
                winnerDeletedAt = state.DeletedAt;
                inserted.TrySetResult();
            };
            using var client = factory.AuthenticatedClient();
            var readTask = client.PatchAsync($"/api/notifications/{item.Id}/read", null);
            var deleteTask = client.DeleteAsync($"/api/notifications/{item.Id}");
            using var read = await readTask;
            using var deleted = await deleteTask;
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            if (readWins) Assert.Contains(read.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
            else Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
            Assert.Equal(1, factory.Race.DuplicateConflicts);
            var state = (await factory.StateAsync(item.Id))!;
            Assert.True(state.IsDeleted);
            Assert.Equal(readWins, state.IsRead);
            Assert.Equal(winnerReadAt, state.ReadAt);
            if (!readWins) Assert.Equal(winnerDeletedAt, state.DeletedAt);
            using var laterRead = await client.PatchAsync($"/api/notifications/{item.Id}/read", null);
            Assert.Equal(HttpStatusCode.NotFound, laterRead.StatusCode);
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    [SqlServerFact]
    public async Task ConcurrentExistingReadDeletePreservesCommittedReadAndDeleteTimestamps()
    {
        using var factory = new SqlServerNotificationFactory();
        await factory.VerifyTargetAsync();
        try
        {
            var item = factory.NewNotification();
            await factory.SaveAsync(item);
            await using (var db = factory.OpenContext())
            {
                db.UserNotificationStates.Add(new() { NotificationId = item.Id, UserId = factory.LearnerId });
                await db.SaveChangesAsync();
            }
            using var client = factory.AuthenticatedClient();
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => i % 2 == 0
                ? client.PatchAsync($"/api/notifications/{item.Id}/read", null)
                : client.DeleteAsync($"/api/notifications/{item.Id}")));
            try
            {
                for (var i = 0; i < responses.Length; i++)
                    if (i % 2 == 0) Assert.Contains(responses[i].StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
                    else Assert.Equal(HttpStatusCode.NoContent, responses[i].StatusCode);
                var saved = (await factory.StateAsync(item.Id))!;
                Assert.True(saved.IsDeleted);
                using var repeated = await client.DeleteAsync($"/api/notifications/{item.Id}");
                Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
                var after = (await factory.StateAsync(item.Id))!;
                Assert.Equal(saved.ReadAt, after.ReadAt);
                Assert.Equal(saved.DeletedAt, after.DeletedAt);
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { await factory.CleanupOwnedAsync(); }
    }

    private sealed class RaceBarrier(int participants)
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;
        public async Task ArriveAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrived) >= participants) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
    }
}
