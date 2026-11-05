namespace Billing.API.DTOs;

public record PlanLimitDto(string Feature, int? LimitValue, string Period);
public record PlanDto(Guid Id, string Code, string Name, string? Description, decimal Price,
    int DurationDays, IReadOnlyList<PlanLimitDto> Limits);
