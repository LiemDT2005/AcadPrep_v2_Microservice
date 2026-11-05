using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Billing.API.Validation;

namespace Billing.API.DTOs;

public static class SubscriptionViewStatus
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Free = "Free";
}

public record SubscriptionDto(string Status, string PlanCode, string PlanName, bool IsPro,
    DateTime? StartDate, DateTime? EndDate, int? DaysRemaining);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record GrantSubscriptionRequest
{
    private string? note;

    // Client-generated idempotency key: replaying the same request never extends twice.
    [Required, NotEmptyGuid]
    public Guid? RequestId { get; init; }

    [Required, NotEmptyGuid]
    public Guid? UserId { get; init; }

    [Required, NotEmptyGuid]
    public Guid? PlanId { get; init; }

    [Range(1, 365)]
    public int? DurationDays { get; init; }

    [StringLength(500)]
    public string? Note { get => note; init => note = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }
}

public record SubscriptionGrantedDto(Guid SubscriptionId, DateTime StartDate, DateTime EndDate);
