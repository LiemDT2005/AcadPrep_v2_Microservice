using System.ComponentModel.DataAnnotations;
using Billing.API.DTOs;
using Billing.API.Entities;
using Billing.API.Repositories.Interfaces;
using Billing.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Services;

public class SubscriptionGrantService(ISubscriptionRepository subscriptions, IIdentityUserClient identity,
    TimeProvider clock, ILogger<SubscriptionGrantService> logger) : ISubscriptionGrantService
{
    private const int MaxAttempts = 2;
    private readonly SubscriptionActivator activator = new(subscriptions);

    public async Task<BillingResult<SubscriptionGrantedDto>> GrantAsync(GrantSubscriptionRequest request,
        Guid adminId, CancellationToken ct)
    {
        if (adminId == Guid.Empty || !Validator.TryValidateObject(request, new ValidationContext(request), [], true))
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Unprocessable, "Invalid grant request.");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TryGrantAsync(request, adminId, ct);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // A concurrent grant/activation changed the Active row or used the same request ID.
                // Re-read everything; the replay check makes the retry idempotent.
                subscriptions.ResetTracking();
            }
            catch (DbUpdateException)
            {
                subscriptions.ResetTracking();
                logger.LogWarning("Subscription grant conflicted repeatedly. RequestId={RequestId}", request.RequestId);
                return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Conflict,
                    "The subscription changed concurrently. Please retry.");
            }
        }
    }

    private async Task<BillingResult<SubscriptionGrantedDto>> TryGrantAsync(GrantSubscriptionRequest request,
        Guid adminId, CancellationToken ct)
    {
        var requestId = request.RequestId!.Value;
        var userId = request.UserId!.Value;
        var replay = await subscriptions.GetGrantByRequestIdAsync(requestId, ct);
        if (replay is not null) return Replay(replay, request, adminId);

        var plan = await subscriptions.GetPlanAsync(request.PlanId!.Value, ct);
        if (plan is null || !plan.IsActive)
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.NotFound, "Plan not found.");
        if (plan.Code == SubscriptionService.FreePlanCode || plan.Price <= 0 || plan.DurationDays <= 0)
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Unprocessable,
                "Only a paid plan can be granted.");

        // The recipient must be an existing, active Learner (each role has one job). Fail closed when
        // Identity cannot answer: a grant involves no payment, so the Admin can simply retry.
        var lookup = await identity.GetUserAsync(userId, ct);
        if (lookup.Status == IdentityLookupStatus.NotFound)
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.NotFound, "User not found.");
        if (lookup.Status != IdentityLookupStatus.Found || lookup.User is null)
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Unavailable,
                "User verification is temporarily unavailable.");
        var roles = lookup.User.Roles;
        if (!roles.Contains("Learner") || roles.Contains("Admin") || roles.Contains("Moderator"))
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Unprocessable,
                "Only Learner accounts can receive a plan.");
        if (!string.Equals(lookup.User.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Unprocessable,
                "The user account is not active.");
        var email = lookup.User.Email;

        var duration = request.DurationDays ?? plan.DurationDays;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await subscriptions.BeginTransactionAsync(ct);
        // Shared spec 5.4 rule (also used by confirmed payments): extend if still valid, else start now.
        var activation = await activator.ActivateAsync(userId, plan, duration, email,
            new ActivationSource(GrantedBy: adminId, Note: request.Note), now, ct);
        var target = activation.Subscription;
        var previousEnd = activation.PreviousEndDate;

        subscriptions.Add(new SubscriptionGrant
        {
            RequestId = requestId, AdminId = adminId, TargetUserId = userId, PlanId = plan.Id,
            SubscriptionId = target.Id, DurationDays = duration, Note = request.Note,
            PreviousEndDate = previousEnd, NewStartDate = target.StartDate, NewEndDate = target.EndDate,
            CreatedAt = now
        });
        await subscriptions.SaveChangesAsync(ct);
        await subscriptions.CommitAsync(transaction, ct);
        logger.LogInformation("Subscription granted. RequestId={RequestId} SubscriptionId={SubscriptionId}",
            requestId, target.Id);
        return BillingResult<SubscriptionGrantedDto>.Success(
            new SubscriptionGrantedDto(target.Id, target.StartDate, target.EndDate));
    }

    private static BillingResult<SubscriptionGrantedDto> Replay(SubscriptionGrant grant,
        GrantSubscriptionRequest request, Guid adminId)
    {
        // The same key with a different payload is a client error, never a second grant.
        var same = grant.AdminId == adminId && grant.TargetUserId == request.UserId &&
            grant.PlanId == request.PlanId &&
            (request.DurationDays is null || grant.DurationDays == request.DurationDays) &&
            grant.Note == request.Note;
        return same
            ? BillingResult<SubscriptionGrantedDto>.Success(
                new SubscriptionGrantedDto(grant.SubscriptionId, grant.NewStartDate, grant.NewEndDate))
            : BillingResult<SubscriptionGrantedDto>.Failure(BillingError.Conflict,
                "The request ID was already used for a different grant.");
    }
}
