namespace BuildingBlocks.Common.Events;

// Published by Billing (UC-23.3), consumed by Notification. New file only; existing contracts unchanged.
// UserEmail is a snapshot and may be null (e.g. no email known): consumers then skip the email, never the
// in-app notification. Dates are UTC.
public record SubscriptionExpiringEvent(
    Guid UserId,
    string? UserEmail,
    string PlanCode,
    string PlanName,
    DateTime EndDate,
    int DaysRemaining
) : IntegrationEvent;

// Published after a confirmed VNPay payment activates or extends a subscription. OrderCode is null for
// manual Admin grants.
public record SubscriptionActivatedEvent(
    Guid UserId,
    string? UserEmail,
    string PlanCode,
    string PlanName,
    DateTime StartDate,
    DateTime EndDate,
    string? OrderCode,
    bool IsRenewal
) : IntegrationEvent;

public record SubscriptionExpiredEvent(
    Guid UserId,
    string? UserEmail,
    string PlanCode,
    string PlanName,
    DateTime EndDate
) : IntegrationEvent;
