using System.Net;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;
using static Notifications.API.Tests.NotificationMarkReadApiTests;

namespace Notifications.API.Tests;

// All database writes here target isolated SQLite memory databases, not SQL Server.
public class NotificationMarkReadConcurrencyTests
{
    [Fact]
    public async Task TwoFirstReads_RaceOnRealPrimaryKey_Both204AndKeepWinningReadAt()
    {
        using var factory = await CreateFactory();
        var barrier = new AsyncRaceBarrier(2);
        DateTime? winningReadAt = null;
        factory.Race.BeforeInsert = (_, ct) => barrier.ArriveAsync(ct);
        factory.Race.AfterInsert = (state, _) =>
        {
            winningReadAt = state.ReadAt;
            return Task.CompletedTask;
        };
        using var client = Client(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => client.PatchAsync($"/api/notifications/{Id(1)}/read", null)));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            Assert.Equal(1, factory.Race.DuplicateConflicts);
            Assert.NotNull(winningReadAt);
            Assert.Equal(winningReadAt, (await factory.StateAsync(1))!.ReadAt);
            Assert.Equal(1, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync(
                state => state.NotificationId == Id(1) && state.UserId == LearnerId)));
            using var repeated = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
            Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
            Assert.Equal(winningReadAt, (await factory.StateAsync(1))!.ReadAt);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentUnreadUpdates_OnlyOneTransitionWinsWithoutOverwritingTimestamp()
    {
        using var factory = await CreateFactory();
        var barrier = new AsyncRaceBarrier(4);
        factory.Race.BeforeUpdate = barrier.ArriveAsync;
        using var client = Client(factory);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => client.PatchAsync($"/api/notifications/{Id(7)}/read", null)));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.NoContent, response.StatusCode));
            var winner = Assert.Single(factory.Race.SuccessfulUpdates);
            var state = (await factory.StateAsync(7))!;
            Assert.True(state.IsRead);
            Assert.Equal(winner, state.ReadAt);
            Assert.Equal(0, factory.Race.DuplicateConflicts);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task DuplicateInsert_VerifiesAlreadyReadWinnerInsteadOfOverwritingIt()
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeInsert = async (state, ct) => await factory.WithDbAsync(async db =>
        {
            db.UserNotificationStates.Add(new UserNotificationState
            {
                NotificationId = state.NotificationId, UserId = state.UserId, IsRead = true, ReadAt = Epoch
            });
            return await db.SaveChangesAsync(ct);
        });
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, factory.Race.DuplicateConflicts);
        Assert.Equal(Epoch, (await factory.StateAsync(1))!.ReadAt);
    }

    [Fact]
    public async Task DuplicateInsert_WithDeletedWinner_Returns404WithoutResurrection()
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeInsert = async (state, ct) => await factory.WithDbAsync(async db =>
        {
            db.UserNotificationStates.Add(new UserNotificationState
            {
                NotificationId = state.NotificationId, UserId = state.UserId, IsDeleted = true, DeletedAt = Epoch
            });
            return await db.SaveChangesAsync(ct);
        });
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, factory.Race.DuplicateConflicts);
        var state = (await factory.StateAsync(1))!;
        Assert.True(state.IsDeleted);
        Assert.False(state.IsRead);
        Assert.Null(state.ReadAt);
        Assert.Equal(Epoch, state.DeletedAt);
    }

    [Fact]
    public async Task DeleteCommittedBeforeConditionalUpdate_ReadCannotResurrectState()
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeUpdate = async ct =>
        {
            factory.Race.BeforeUpdate = null;
            await factory.WithDbAsync(db => db.UserNotificationStates
                .Where(state => state.NotificationId == Id(7) && state.UserId == LearnerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.IsDeleted, true)
                    .SetProperty(state => state.DeletedAt, Epoch), ct));
        };
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(7)}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var state = (await factory.StateAsync(7))!;
        Assert.True(state.IsDeleted);
        Assert.False(state.IsRead);
        Assert.Null(state.ReadAt);
        Assert.Equal(Epoch, state.DeletedAt);
        Assert.Empty(factory.Race.SuccessfulUpdates);
    }

    [Fact]
    public async Task DeleteAfterInsertBeforeVerification_Returns404AndKeepsDeletedState()
    {
        using var factory = await CreateFactory();
        factory.Race.AfterInsert = async (inserted, ct) => await factory.WithDbAsync(db => db.UserNotificationStates
            .Where(state => state.NotificationId == inserted.NotificationId && state.UserId == inserted.UserId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(state => state.IsDeleted, true)
                .SetProperty(state => state.DeletedAt, Epoch), ct));
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var state = (await factory.StateAsync(1))!;
        Assert.True(state.IsDeleted);
        Assert.Equal(Epoch, state.DeletedAt);
        Assert.NotNull(state.ReadAt); // Read committed first; delete wins before final verification.
    }

    [Fact]
    public async Task ForeignKeyFailure_IsNotMisclassifiedAsDuplicateState()
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeInsert = (state, _) =>
        {
            state.NotificationId = Id(999);
            return Task.CompletedTask;
        };
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, factory.Race.DuplicateConflicts);
        Assert.Null(await factory.StateAsync(1));
    }

    private sealed class AsyncRaceBarrier(int participants)
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
