using Ai.API.DTOs;
using Ai.API.Entities;
using Ai.API.Repositories.Interfaces;
using Ai.API.Services.Interfaces;

namespace Ai.API.Services;

public sealed class AiConfigurationException(string reason) : Exception(reason);

public class TutorOptions
{
    public const string SectionName = "Tutor";
    public int ContextMessages { get; set; } = 10;
    public int WindowHours { get; set; } = 24;
    public string OutOfScopeMessage { get; set; } =
        "Mình chỉ hỗ trợ các câu hỏi về TOEIC và học tiếng Anh. Bạn thử hỏi về ngữ pháp, từ vựng hoặc cách làm một Part nhé!";
    public string BlockedMessage { get; set; } =
        "Mình không thể trả lời nội dung này. Bạn thử hỏi một câu khác về tiếng Anh hoặc TOEIC nhé!";
}

// Rolling 24h windows (Huy, 2026-11-05). Reads never create rows; a window is opened only by a successful answer.
public class UsageService(IUsageRepository usage, IChatRequestRepository requests, IPlanResolver plans, TimeProvider clock)
    : IUsageService
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private const int ChartDays = 7;

    public DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<AiUsageWindow?> GetCurrentWindowAsync(Guid userId, DateTime now, bool track, CancellationToken ct) =>
        usage.GetCurrentWindowAsync(userId, now, track, ct);

    // A missing limit is a configuration error, never implicit unlimited access.
    public async Task<long> GetLimitAsync(string planCode, CancellationToken ct) =>
        (await usage.GetLimitAsync(planCode, ct))?.TokenLimit
        ?? throw new AiConfigurationException($"No token limit configured for {planCode}.");

    // Also consumed by Billing UC-23.2. Shows the caller's current plan (the same plan the next chat is checked
    // against), so an upgrade or expiry is visible immediately, not only after the next charged chat.
    public async Task<UsageDto> GetMyUsageAsync(Guid userId, CancellationToken ct)
    {
        var window = await usage.GetCurrentWindowAsync(userId, Now, track: false, ct);
        var plan = await plans.ResolveAsync(ct);
        return View(plan, await GetLimitAsync(plan, ct), window);
    }

    public async Task<DailyUsageResponseDto> GetMyDailyUsageAsync(Guid userId, CancellationToken ct)
    {
        var days = await DailyAsync(userId, ChartDays, ct);
        var limit = await GetLimitAsync(await plans.ResolveAsync(ct), ct);
        return new DailyUsageResponseDto(days, days.Sum(day => day.Tokens), limit);
    }

    // planCode is the plan whose limit is shown; the window only contributes what was already used.
    public static UsageDto View(string planCode, long limit, AiUsageWindow? window)
    {
        var used = window?.TokensUsed ?? 0;
        var percent = limit <= 0 ? (used > 0 || window is not null ? 100 : 0) : (int)Math.Min(100, Math.Ceiling(used * 100.0 / limit));
        return new UsageDto(planCode, limit, used, Math.Max(0, limit - used), percent,
            window?.WindowStart, window?.WindowEnd, window is not null && (window.Locked || used >= limit),
            window?.WindowStart.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    // 7-day rolling chart: today (Vietnam) and the six days before; empty days are 0. History is not affected by
    // Admin resets or deleted sessions (it reads the request ledger).
    private async Task<IReadOnlyList<DailyUsageDto>> DailyAsync(Guid userId, int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(Now + VietnamOffset);
        var first = today.AddDays(-(days - 1));
        var fromUtc = DateTime.SpecifyKind(first.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) - VietnamOffset;
        var rows = await requests.ListCompletedTokensSinceAsync(userId, fromUtc, ct);
        var totals = rows.GroupBy(row => DateOnly.FromDateTime(row.CompletedAt + VietnamOffset))
            .ToDictionary(group => group.Key, group => group.Sum(row => (long)row.Tokens));
        return Enumerable.Range(0, days).Select(offset => first.AddDays(offset))
            .Select(day => new DailyUsageDto(day.ToString("yyyy-MM-dd"), totals.GetValueOrDefault(day))).ToArray();
    }
}
