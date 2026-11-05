using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;

namespace Billing.API.Services;

public record ActivationResult(Subscription Subscription, DateTime? PreviousEndDate, bool Extended);

public record ActivationSource(Guid? GrantedBy = null, string? Note = null, Guid? SourceOrderId = null);

// Spec 5.4 rule shared by Admin grants and confirmed payments: a still-valid subscription is extended from
// its current EndDate (remaining days kept); otherwise it is closed and a new period starts now. Must run
// inside the caller's transaction; the caller saves and handles concurrency (Version token, unique Active).
public class SubscriptionActivator(ISubscriptionRepository subscriptions)
{
    public async Task<ActivationResult> ActivateAsync(Guid userId, Plan plan, int durationDays, string? email,
        ActivationSource source, DateTime now, CancellationToken ct)
    {
        var active = await subscriptions.GetActiveForUpdateAsync(userId, ct);
        var previousEnd = active?.EndDate;
        if (active is not null && active.EndDate > now)
        {
            active.EndDate = active.EndDate.AddDays(durationDays);
            active.PlanId = plan.Id;
            if (source.GrantedBy is not null) { active.GrantedBy = source.GrantedBy; active.Note = source.Note; }
            if (source.SourceOrderId is not null) active.SourceOrderId = source.SourceOrderId;
            active.UserEmail = email ?? active.UserEmail;
            active.UpdatedAt = now;
            active.Version = Guid.NewGuid();
            return new ActivationResult(active, previousEnd, true);
        }

        if (active is not null)
        {
            // Persist the read-time expiry first so the filtered unique index admits the new Active row.
            active.Status = SubscriptionStatus.Expired;
            active.UpdatedAt = now;
            active.Version = Guid.NewGuid();
            await subscriptions.SaveChangesAsync(ct);
        }
        var created = new Subscription
        {
            UserId = userId, UserEmail = email, PlanId = plan.Id, Status = SubscriptionStatus.Active,
            StartDate = now, EndDate = now.AddDays(durationDays), GrantedBy = source.GrantedBy, Note = source.Note,
            SourceOrderId = source.SourceOrderId, CreatedAt = now, UpdatedAt = now
        };
        subscriptions.Add(created);
        return new ActivationResult(created, previousEnd, false);
    }
}
