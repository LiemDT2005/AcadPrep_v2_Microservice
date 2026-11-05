using Billing.API.Data;
using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services;
using Billing.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Billing.API.Tests.BillingTestData;

namespace Billing.API.Tests;

public class GrantConcurrencyTests
{
    [Fact]
    public async Task DatabaseRejectsASecondActiveSubscriptionForTheSameUser()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(user, now, now.AddDays(1)));
            await db.SaveChangesAsync();
            db.Subscriptions.Add(Subscription(user, now, now.AddDays(2)));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        await factory.WithDb(async db =>
        {
            db.Subscriptions.Add(Subscription(user, now.AddDays(-9), now.AddDays(-8), SubscriptionStatus.Expired));
            await db.SaveChangesAsync(); // history rows are not limited
        });
    }

    [Fact]
    public async Task StaleVersionCannotOverwriteAConcurrentExtension()
    {
        using var factory = new BillingApiFactory();
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await factory.WithDb(async db =>
        {
            await SeedPlans(db);
            db.Subscriptions.Add(Subscription(user, now, now.AddDays(10)));
            await db.SaveChangesAsync();
        });
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var a = scopeA.ServiceProvider.GetRequiredService<BillingDbContext>();
        var b = scopeB.ServiceProvider.GetRequiredService<BillingDbContext>();
        var rowA = await a.Subscriptions.SingleAsync();
        var rowB = await b.Subscriptions.SingleAsync();
        rowA.EndDate = rowA.EndDate.AddDays(5); rowA.Version = Guid.NewGuid();
        await a.SaveChangesAsync();
        rowB.EndDate = rowB.EndDate.AddDays(30); rowB.Version = Guid.NewGuid();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task ConflictIsRetriedOnFreshDataSoBothGrantsApplyExactlyOnce()
    {
        var now = DateTime.UtcNow;
        var user = Guid.NewGuid();
        var repo = new FakeRepository(now, user, endDays: 10) { FailuresBeforeSuccess = 1 };
        var service = new SubscriptionGrantService(repo, new LearnerIdentity(), new FixedClock(now), NullLogger<SubscriptionGrantService>.Instance);
        var result = await service.GrantAsync(Request(user), Guid.NewGuid(), default);
        Assert.True(result.IsSuccess);
        // 10 days + concurrent competitor 5 days + this grant 30 days, never a lost update.
        Assert.Equal(now.AddDays(45), repo.Committed.Single().EndDate);
        Assert.Single(repo.Grants);
        Assert.Equal(1, repo.Resets);
    }

    [Fact]
    public async Task RepeatedConflictReturnsConflictWithoutPartialGrant()
    {
        var now = DateTime.UtcNow;
        var user = Guid.NewGuid();
        var repo = new FakeRepository(now, user, endDays: 10) { FailuresBeforeSuccess = 5 };
        var service = new SubscriptionGrantService(repo, new LearnerIdentity(), new FixedClock(now), NullLogger<SubscriptionGrantService>.Instance);
        var result = await service.GrantAsync(Request(user), Guid.NewGuid(), default);
        Assert.Equal(BillingError.Conflict, result.Error);
        Assert.Empty(repo.Grants);
    }

    private sealed class LearnerIdentity : IIdentityUserClient
    {
        public Task<IdentityLookup> GetUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(
            new IdentityLookup(IdentityLookupStatus.Found, new IdentityUserInfo(["Learner"], "Active", null)));
    }

    private static GrantSubscriptionRequest Request(Guid user) => new()
        { RequestId = Guid.NewGuid(), UserId = user, PlanId = MonthId, DurationDays = 30 };

    // Test-only repository: each injected failure simulates another Admin committing a 5-day extension first.
    private sealed class FakeRepository : ISubscriptionRepository
    {
        private readonly Plan plan = new() { Id = MonthId, Code = "PRO_MONTH", Name = "Test", Price = 1, DurationDays = 30, IsActive = true };
        private readonly List<object> pending = [];
        private Subscription? tracked;
        public List<Subscription> Committed { get; } = [];
        public List<SubscriptionGrant> Grants { get; } = [];
        public int FailuresBeforeSuccess { get; set; }
        public int Resets { get; private set; }

        public FakeRepository(DateTime now, Guid user, int endDays) =>
            Committed.Add(BillingTestData.Subscription(user, now.AddDays(-1), now.AddDays(endDays)));

        public Task<Subscription?> GetLatestAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Subscription?> GetActiveForUpdateAsync(Guid userId, CancellationToken ct)
        {
            var row = Committed.SingleOrDefault(s => s.UserId == userId && s.Status == SubscriptionStatus.Active);
            tracked = row is null ? null : Clone(row);
            return Task.FromResult(tracked);
        }
        public Task<Plan?> GetPlanAsync(Guid planId, CancellationToken ct) => Task.FromResult<Plan?>(planId == plan.Id ? plan : null);
        public Task<Plan?> GetPlanByCodeAsync(string code, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasAnyAsync(Guid userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Plan?> GetPlanWithLimitsAsync(Guid planId, CancellationToken ct) => throw new NotSupportedException();
        public Task<SubscriptionGrant?> GetGrantByRequestIdAsync(Guid requestId, CancellationToken ct) =>
            Task.FromResult(Grants.SingleOrDefault(g => g.RequestId == requestId));
        public void Add(Subscription subscription) => pending.Add(subscription);
        public void Add(SubscriptionGrant grant) => pending.Add(grant);

        public Task SaveChangesAsync(CancellationToken ct)
        {
            if (FailuresBeforeSuccess-- > 0)
            {
                var row = Committed.Single(s => s.Status == SubscriptionStatus.Active);
                row.EndDate = row.EndDate.AddDays(5);
                row.Version = Guid.NewGuid();
                throw new DbUpdateConcurrencyException("Simulated concurrent update.");
            }
            if (tracked is not null)
            {
                var index = Committed.FindIndex(s => s.Id == tracked.Id);
                Committed[index] = tracked;
            }
            Committed.AddRange(pending.OfType<Subscription>());
            Grants.AddRange(pending.OfType<SubscriptionGrant>());
            pending.Clear();
            return Task.CompletedTask;
        }

        public Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IAsyncDisposable>(new NoopTransaction());
        public Task CommitAsync(IAsyncDisposable transaction, CancellationToken ct) => Task.CompletedTask;
        public void ResetTracking() { Resets++; pending.Clear(); tracked = null; }

        private static Subscription Clone(Subscription s) => new()
        {
            Id = s.Id, UserId = s.UserId, PlanId = s.PlanId, Status = s.Status, StartDate = s.StartDate,
            EndDate = s.EndDate, CreatedAt = s.CreatedAt, UpdatedAt = s.UpdatedAt, Version = s.Version
        };

        private sealed class NoopTransaction : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
