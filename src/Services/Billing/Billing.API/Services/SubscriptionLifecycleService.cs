using System.Text.Json;
using BuildingBlocks.Common.Events;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Services;

public record LifecycleRunResult(int ExpiringQueued, int ExpiredQueued);

// UC-23.3 reminders (approved by Huy): first reminder 3 days before EndDate, second when it has expired.
// Each due item is recorded with its outbox event in one transaction; the unique reminder log makes
// re-runs and concurrent instances safe. Runs hourly; the 3-day reminder is sent from 08:00 Vietnam time.
public class SubscriptionLifecycleService(ISubscriptionLifecycleRepository lifecycle, IOutboxRepository outbox,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<SubscriptionLifecycleService> logger) : ISubscriptionLifecycleService
{
    public const int DaysBefore = 3;
    public static readonly TimeSpan ReminderStartVietnam = TimeSpan.FromHours(8);
    private const int BatchSize = 200;

    public async Task<LifecycleRunResult> RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiring = await QueueExpiringAsync(now, ct);
        var expired = await ExpireAsync(now, ct);
        if (expiring + expired > 0)
            logger.LogInformation("Subscription lifecycle queued {Expiring} reminders and {Expired} expirations.", expiring, expired);
        return new LifecycleRunResult(expiring, expired);
    }

    private async Task<int> QueueExpiringAsync(DateTime now, CancellationToken ct)
    {
        var local = now + UsagePeriods.VietnamOffset;
        if (local.TimeOfDay < ReminderStartVietnam) return 0;
        var target = DateOnly.FromDateTime(local).AddDays(DaysBefore);
        var startUtc = DateTime.SpecifyKind(target.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) - UsagePeriods.VietnamOffset;
        var endUtc = startUtc.AddDays(1);
        var due = await lifecycle.GetDueForExpiringReminderAsync(startUtc, endUtc, now, BatchSize, ct);

        var queued = 0;
        foreach (var subscription in due)
        {
            var eventId = Guid.NewGuid();
            var @event = new SubscriptionExpiringEvent(subscription.UserId, subscription.UserEmail, subscription.Plan.Code,
                subscription.Plan.Name, subscription.EndDate, DaysBefore) { Id = eventId, OccurredOn = now };
            if (await TrySaveAsync(subscription.Id, ReminderKind.Expiring, subscription.EndDate, @event, now, null, ct)) queued++;
        }
        return queued;
    }

    private async Task<int> ExpireAsync(DateTime now, CancellationToken ct)
    {
        var ids = await lifecycle.GetEndedActiveIdsAsync(now, BatchSize, ct);

        var queued = 0;
        foreach (var id in ids)
        {
            // Re-read tracked: a concurrent grant may have extended it; the Version token rejects stale writes.
            var subscription = await lifecycle.GetEndedActiveForUpdateAsync(id, now, ct);
            if (subscription is null) continue;
            subscription.Status = SubscriptionStatus.Expired;
            subscription.UpdatedAt = now;
            subscription.Version = Guid.NewGuid();
            var eventId = Guid.NewGuid();
            var @event = new SubscriptionExpiredEvent(subscription.UserId, subscription.UserEmail, subscription.Plan.Code,
                subscription.Plan.Name, subscription.EndDate) { Id = eventId, OccurredOn = now };
            var alreadyNotified = await lifecycle.ReminderExistsAsync(id, ReminderKind.Expired, subscription.EndDate, ct);
            if (await TrySaveAsync(id, ReminderKind.Expired, subscription.EndDate, alreadyNotified ? null : @event, now,
                    subscription, ct) && !alreadyNotified)
                queued++;
        }
        return queued;
    }

    private async Task<bool> TrySaveAsync(Guid subscriptionId, string kind, DateTime endDate, IntegrationEvent? @event,
        DateTime now, Subscription? tracked, CancellationToken ct)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
            if (@event is not null)
            {
                lifecycle.Add(new SubscriptionReminderLog
                {
                    SubscriptionId = subscriptionId, Kind = kind, EndDate = endDate, EventId = @event.Id, CreatedAt = now
                });
                outbox.Add(new OutboxMessage
                {
                    Id = @event.Id, Type = @event.GetType().Name,
                    Payload = JsonSerializer.Serialize(@event, @event.GetType()), CreatedAt = now
                });
            }
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another instance or a concurrent grant got there first; the next run re-evaluates.
            unitOfWork.ResetTracking();
            logger.LogInformation("Lifecycle item skipped after a concurrent change. SubscriptionId={SubscriptionId} Kind={Kind}",
                subscriptionId, kind);
            return false;
        }
        finally
        {
            if (tracked is not null) unitOfWork.ResetTracking();
        }
    }
}
