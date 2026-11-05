using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Ai.API.Validation;

namespace Ai.API.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record ChatRequestDto
{
    private string? message;

    // Client-generated per message; resending the same id never calls Gemini or charges twice.
    [Required, NotEmptyGuid]
    public Guid? RequestId { get; init; }

    [NotEmptyGuid]
    public Guid? SessionId { get; init; }

    [Required, StringLength(2000, MinimumLength = 1)]
    public string? Message { get => message; init => message = value?.Trim(); }
}

// PercentUsed is capped at 100; TokensUsed may exceed TokenLimit after the last allowed answer.
public record UsageDto(string PlanCode, long TokenLimit, long TokensUsed, long Remaining, int PercentUsed,
    DateTime? WindowStart, DateTime? ResetAt, bool Locked, string? PeriodKey);

public record ChatResponseDto(Guid RequestId, Guid SessionId, string SessionTitle, string Reply, bool IsOutOfScope,
    int TokensUsed, UsageDto Usage, bool Replayed);

public record DailyUsageDto(string Date, long Tokens);

public record DailyUsageResponseDto(IReadOnlyList<DailyUsageDto> Days, long TotalTokens, long TokenLimit);

public record SessionDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt);

public record MessageDto(Guid Id, string Role, string Content, bool IsOutOfScope, DateTime CreatedAt);

public record PagedDto<T>(IReadOnlyList<T> Items, int PageIndex, int PageSize, int TotalCount, int TotalPages);

public class PageQuery
{
    [Range(1, int.MaxValue)] public int PageIndex { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public class AdminUsageQuery : PageQuery
{
    [NotEmptyGuid] public Guid? UserId { get; init; }
}

public record AdminUsageRowDto(Guid UserId, string PlanCode, long TokensUsed, long TokenLimit, long Remaining,
    DateTime WindowStart, DateTime ResetAt, bool Locked);

public record TokenLimitDto(string PlanCode, long TokenLimit, Guid? UpdatedBy, DateTime UpdatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateTokenLimitRequest
{
    [Required, Range(0, 100_000_000)]
    public long? TokenLimit { get; init; }
}
