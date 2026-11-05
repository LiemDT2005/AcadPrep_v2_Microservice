namespace Billing.API.DTOs;

// LimitValue null = unlimited. Used/Remaining null with UsageAvailable=false = no usage source yet;
// never reported as a fabricated zero.
public record FeatureUsageDto(string Feature, int? LimitValue, int? Used, int? Remaining, bool UsageAvailable,
    string Period, DateTime? ResetAt);

public record AiUsageDto(bool Available, long? TokenLimit, long? TokensUsed, long? Remaining, DateTime? ResetAt)
{
    public static AiUsageDto Unavailable { get; } = new(false, null, null, null, null);
}

public record UsageDto(string PlanCode, IReadOnlyList<FeatureUsageDto> Features, AiUsageDto Ai);
