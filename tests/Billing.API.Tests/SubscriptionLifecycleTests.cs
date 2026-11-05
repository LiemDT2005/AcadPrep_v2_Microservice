using System.Text.Json;
using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Billing.API.Data;
using Billing.API.Entities;
using Billing.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Billing.API.Services.Interfaces;
using Xunit;
using static Billing.API.Tests.BillingTestData;

namespace Billing.API.Tests;

public class SubscriptionLifecycleTests
{
    // 2026-11-05 01:30 UTC = 08:30 Vietnam; three VN days later is 2026-11-08 (VN).
    private static readonly DateTimeOffset AfterEight = new(2026, 11, 5, 1, 30, 0, TimeSpan.Zero);
    private static readonly DateTime InThreeVnDays = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc); // 17:00 08/11 VN

    [Fact]
    public async Task ThreeDayReminderIsQueuedOnceWithEmailPlanAndDays()
    {
        using var factory = new BillingApiFactory();
        factory.Clock.Now = AfterEight;
        var user = Guid.NewGuid();
        await Seed(factory, Sub(user, InThreeVnDays, "learner@example.test"));
        Assert.Equal(new LifecycleRunResult(1, 0), await Run(factory));
        Assert.Equal(new LifecycleRunResult(0, 0), await Run(factory)); // re-run: nothing new
        await factory.WithDb(async db =>
        {
            var message = await db.OutboxMessages.SingleAsync();
            Assert.Equal(nameof(SubscriptionExpiringEvent), message.Type);
            Assert.Null(message.PublishedAt);
            var @event = JsonSerializer.Deserialize<SubscriptionExpiringEvent>(message.Payload)!;
            Assert.Equal(message.Id, @event.Id);
            Assert.Equal(user, @event.UserId);
            Assert.Equal("learner@example.test", @event.UserEmail);
            Assert.Equal("Test month", @event.PlanName);
            Assert.Equal(3, @event.DaysRemaining);
            var log = await db.SubscriptionReminderLogs.SingleAsync();
            Assert.Equal(ReminderKind.Expiring, log.Kind);
            Assert.Equal(message.Id, log.EventId);
        });
    }

    [Fact]
    public async Task ReminderWaitsForEightOClockVietnamTime()
    {
        using var factory = new BillingApiFactory();
        factory.Clock.Now = new DateTimeOffset(2026, 11, 4, 23, 30, 0, TimeSpan.Zero); // 06:30 05/11 VN
        await Seed(factory, Sub(Guid.NewGuid(), InThreeVnDays));
        Assert.Equal(new LifecycleRunResult(0, 0), await Run(factory));
        factory.Clock.Now = AfterEight;
        Assert.Equal(new LifecycleRunResult(1, 0), await Run(factory));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task OnlyTheThirdVietnamDayIsReminded(int days)
    {
        using var factory = new BillingApiFactory();
        factory.Clock.Now = AfterEight;
        await Seed(factory, Sub(Guid.NewGuid(), InThreeVnDays.AddDays(days - 3)));
        Assert.Equal(new LifecycleRunResult(0, 0), await Run(factory));
    }

    [Fact]
    public async Task ExpiredSubscriptionIsMarkedOnceAndAnnouncedWithoutEmailWhenUnknown()
    {
        using var factory = new BillingApiFactory();
        factory.Clock.Now = AfterEight;
        var user = Guid.NewGuid();
        var sub = Sub(user, AfterEight.UtcDateTime); // exactly at EndDate: expired
        await Seed(factory, sub, Sub(Guid.NewGuid(), AfterEight.UtcDateTime.AddDays(-9), status: SubscriptionStatus.Expired));
        Assert.Equal(new LifecycleRunResult(0, 1), await Run(factory));
        Assert.Equal(new LifecycleRunResult(0, 0), await Run(factory));
        await factory.WithDb(async db =>
        {
            Assert.Equal(SubscriptionStatus.Expired, (await db.Subscriptions.SingleAsync(s => s.Id == sub.Id)).Status);
            var message = await db.OutboxMessages.SingleAsync();
            Assert.Equal(nameof(SubscriptionExpiredEvent), message.Type);
            var @event = JsonSerializer.Deserialize<SubscriptionExpiredEvent>(message.Payload)!;
            Assert.Equal(user, @event.UserId);
            Assert.Null(@event.UserEmail);
        });
    }

    [Fact]
    public async Task RenewedPeriodGetsItsOwnReminderAndTheOldOneIsNotRepeated()
    {
        using var factory = new BillingApiFactory();
        factory.Clock.Now = AfterEight;
        var sub = Sub(Guid.NewGuid(), InThreeVnDays);
        await Seed(factory, sub);
        Assert.Equal(new LifecycleRunResult(1, 0), await Run(factory));
        // Extended (e.g. grant/renewal) by 30 days: nothing due now, and the old period is not reminded again.
        await factory.WithDb(async db =>
        {
            var row = await db.Subscriptions.SingleAsync();
            row.EndDate = row.EndDate.AddDays(30);
            row.Version = Guid.NewGuid();
            await db.SaveChangesAsync();
        });
        Assert.Equal(new LifecycleRunResult(0, 0), await Run(factory));
        factory.Clock.Now = AfterEight.AddDays(30);
        Assert.Equal(new LifecycleRunResult(1, 0), await Run(factory));
        await factory.WithDb(async db => Assert.Equal(2, await db.SubscriptionReminderLogs.CountAsync()));
    }

    [Fact]
    public async Task DatabaseRejectsADuplicateReminderForTheSamePeriod()
    {
        using var factory = new BillingApiFactory();
        var sub = Sub(Guid.NewGuid(), InThreeVnDays);
        await Seed(factory, sub);
        await factory.WithDb(async db =>
        {
            db.SubscriptionReminderLogs.Add(Log(sub));
            await db.SaveChangesAsync();
            db.SubscriptionReminderLogs.Add(Log(sub));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }

    [Fact]
    public async Task OutboxPublishesInOrderAndRetriesAfterBrokerFailure()
    {
        var bus = new FakeBus { FailuresLeft = 1 };
        using var factory = new BillingApiFactory
        {
            ExtraServices = services => { services.RemoveAll<IEventBus>(); services.AddSingleton<IEventBus>(bus); }
        };
        factory.Clock.Now = AfterEight;
        await Seed(factory, Sub(Guid.NewGuid(), InThreeVnDays, "a@example.test"), Sub(Guid.NewGuid(), AfterEight.UtcDateTime.AddHours(-1)));
        await Run(factory);
        Assert.Equal(0, await Publish(factory)); // broker down: nothing lost
        await factory.WithDb(async db =>
        {
            Assert.All(await db.OutboxMessages.ToListAsync(), message => Assert.Null(message.PublishedAt));
            Assert.Equal(1, (await db.OutboxMessages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).FirstAsync()).Attempts);
        });
        Assert.Equal(2, await Publish(factory));
        Assert.Equal(0, await Publish(factory));
        Assert.Equal(2, bus.Published.Count);
        Assert.Contains(bus.Published, e => e is SubscriptionExpiringEvent);
        Assert.Contains(bus.Published, e => e is SubscriptionExpiredEvent);
        await factory.WithDb(async db =>
            Assert.All(await db.OutboxMessages.ToListAsync(), message => Assert.NotNull(message.PublishedAt)));
    }

    private static Subscription Sub(Guid user, DateTime end, string? email = null, string status = SubscriptionStatus.Active)
    {
        var sub = Subscription(user, end.AddDays(-30), end, status);
        sub.UserEmail = email;
        return sub;
    }

    private static SubscriptionReminderLog Log(Subscription sub) => new()
    {
        SubscriptionId = sub.Id, Kind = ReminderKind.Expiring, EndDate = sub.EndDate, EventId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow
    };

    private static async Task Seed(BillingApiFactory factory, params Subscription[] subs) => await factory.WithDb(async db =>
    {
        await SeedPlans(db);
        db.Subscriptions.AddRange(subs);
        await db.SaveChangesAsync();
    });

    private static async Task<LifecycleRunResult> Run(BillingApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycleService>().RunOnceAsync(default);
    }

    private static async Task<int> Publish(BillingApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IOutboxPublisher>().RunOnceAsync(default);
    }

    // Test-only bus: no broker is contacted.
    private sealed class FakeBus : IEventBus
    {
        public int FailuresLeft { get; set; }
        public List<IntegrationEvent> Published { get; } = [];

        public Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IntegrationEvent
        {
            if (FailuresLeft-- > 0) throw new InvalidOperationException("RabbitMQ is unavailable.");
            Published.Add(@event);
            return Task.CompletedTask;
        }

        public void Subscribe<T, TH>() where T : IntegrationEvent where TH : class, IIntegrationEventHandler<T> { }
    }
}
