using System.Net;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;
using static Notifications.API.Tests.NotificationMarkReadApiTests;

namespace Notifications.API.Tests;

// Isolated SQLite memory databases only. SQL Server locking is not verified by these tests.
public class NotificationDeleteConcurrencyTests
{
    [Fact]
    public async Task ReaderInsertsBetweenDeleteUpdateAndSelect_DeleteStillTransitionsState()
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        DateTime? readAt = null;
        factory.Race.AfterDeleteUpdate = async _ =>
        {
            factory.Race.AfterDeleteUpdate = null;
            using var read = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
            Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
            readAt = (await factory.StateAsync(1))!.ReadAt;
        };
        using var deleted = await client.DeleteAsync($"/api/notifications/{Id(1)}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var state = (await factory.StateAsync(1))!;
        Assert.True(state.IsDeleted);
        Assert.True(state.IsRead);
        Assert.Equal(readAt, state.ReadAt);
    }

    [Fact]
    public async Task TwoFirstDeletesRecoverPrimaryKeyConflictAndKeepWinnerTimestamp()
    {
        using var factory = await CreateFactory();
        var barrier = new RaceBarrier(2);
        DateTime? winner = null;
        factory.Race.BeforeInsert = (_, ct) => barrier.ArriveAsync(ct);
        factory.Race.AfterInsert = (state, _) => { winner = state.DeletedAt; return Task.CompletedTask; };
        using var client = Client(factory);
        var responses = await Task.WhenAll(client.DeleteAsync($"/api/notifications/{Id(1)}"),
            client.DeleteAsync($"/api/notifications/{Id(1)}"));
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            Assert.Equal(1, factory.Race.DuplicateConflicts);
            Assert.NotNull(winner);
            Assert.Equal(winner, (await factory.StateAsync(1))!.DeletedAt);
            Assert.Equal(1, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync(
                s => s.NotificationId == Id(1) && s.UserId == LearnerId)));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task ConcurrentExistingDeletesOnlyOneTransitionPreservesReadFields()
    {
        using var factory = await CreateFactory();
        var barrier = new RaceBarrier(4);
        factory.Race.BeforeDelete = barrier.ArriveAsync;
        using var client = Client(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.DeleteAsync($"/api/notifications/{Id(6)}")));
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            var winner = Assert.Single(factory.Race.SuccessfulDeletes);
            var state = (await factory.StateAsync(6))!;
            Assert.True(state.IsDeleted);
            Assert.Equal(winner, state.DeletedAt);
            Assert.True(state.IsRead);
            Assert.Equal(Epoch, state.ReadAt);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentFirstReadDeleteBothInsertOrdersEndDeleted(bool readWinsInsert)
    {
        using var factory = await CreateFactory();
        var barrier = new RaceBarrier(2);
        var winnerInserted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DateTime? winningReadAt = null;
        DateTime? winningDeletedAt = null;
        factory.Race.BeforeInsert = async (state, ct) =>
        {
            await barrier.ArriveAsync(ct);
            if (state.IsRead != readWinsInsert)
                await winnerInserted.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        };
        factory.Race.AfterInsert = (state, _) =>
        {
            winningReadAt = state.ReadAt;
            winningDeletedAt = state.DeletedAt;
            winnerInserted.TrySetResult();
            return Task.CompletedTask;
        };
        using var client = Client(factory);
        var readTask = client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        var deleteTask = client.DeleteAsync($"/api/notifications/{Id(1)}");
        using var read = await readTask;
        using var deleted = await deleteTask;
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        if (readWinsInsert)
            Assert.Contains(read.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound });
        else Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(1, factory.Race.DuplicateConflicts);
        var state = (await factory.StateAsync(1))!;
        Assert.True(state.IsDeleted);
        Assert.Equal(readWinsInsert, state.IsRead);
        Assert.Equal(winningReadAt, state.ReadAt);
        if (!readWinsInsert) Assert.Equal(winningDeletedAt, state.DeletedAt);
        Assert.Equal(1, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync(
            s => s.NotificationId == Id(1) && s.UserId == LearnerId)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingStateReadDeleteBothUpdateOrdersDoNotLoseDelete(bool readFirst)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        if (readFirst)
        {
            factory.Race.BeforeDelete = async _ =>
            {
                factory.Race.BeforeDelete = null;
                using var read = await client.PatchAsync($"/api/notifications/{Id(7)}/read", null);
                Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
            };
            using var deleted = await client.DeleteAsync($"/api/notifications/{Id(7)}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }
        else
        {
            factory.Race.BeforeUpdate = async _ =>
            {
                factory.Race.BeforeUpdate = null;
                using var deleted = await client.DeleteAsync($"/api/notifications/{Id(7)}");
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            };
            using var read = await client.PatchAsync($"/api/notifications/{Id(7)}/read", null);
            Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        }
        var state = (await factory.StateAsync(7))!;
        Assert.True(state.IsDeleted);
        Assert.Equal(readFirst, state.IsRead);
        if (readFirst) Assert.NotNull(state.ReadAt);
        else Assert.Null(state.ReadAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseFailureIsNotMisclassifiedAsSuccessfulDelete(bool foreignKey)
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeInsert = (state, _) =>
        {
            if (foreignKey) state.NotificationId = Id(999);
            else throw new DbUpdateException("private-diagnostic", new InvalidOperationException("storage-detail"));
            return Task.CompletedTask;
        };
        using var client = Client(factory);
        using var response = await client.DeleteAsync($"/api/notifications/{Id(1)}");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-diagnostic", body);
        Assert.DoesNotContain("storage-detail", body);
        Assert.DoesNotContain("SqliteException", body);
        Assert.Equal(0, factory.Race.DuplicateConflicts);
        Assert.Null(await factory.StateAsync(1));
    }

    [Fact]
    public async Task PhysicalRemovalBeforeInsertReturnsFailureWithoutOrphanState()
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeInsert = async (_, ct) => await factory.WithDbAsync(db => db.Notifications
            .Where(n => n.Id == Id(1)).ExecuteDeleteAsync(ct));
        using var client = Client(factory);
        using var response = await client.DeleteAsync($"/api/notifications/{Id(1)}");
        // The actual FK violation must propagate to sanitized 500, never a false 204.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, factory.Race.DuplicateConflicts);
        Assert.Null(await factory.StateAsync(1));
        Assert.False(await factory.WithDbAsync(db => db.Notifications.AnyAsync(n => n.Id == Id(1))));
    }

    private sealed class RaceBarrier(int participants)
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrived;
        public async Task ArriveAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref arrived) >= participants) release.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        }
    }
}
