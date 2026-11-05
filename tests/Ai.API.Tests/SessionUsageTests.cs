using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ai.API.Entities;
using Ai.API.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Ai.API.Tests.ChatHelpers;

namespace Ai.API.Tests;

public class SessionUsageTests
{
    [Fact]
    public async Task UsageBeforeTheFirstChatIsZeroAndCreatesNothing()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var usage = await factory.Client("Learner").GetFromJsonAsync<JsonElement>("/api/ai/usage/me");
        Assert.Equal(0, usage.GetProperty("tokensUsed").GetInt64());
        Assert.Equal(15_000, usage.GetProperty("remaining").GetInt64());
        Assert.Equal(0, usage.GetProperty("percentUsed").GetInt32());
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("resetAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, usage.GetProperty("windowStart").ValueKind);
        await factory.WithDb(async db => Assert.Equal(0, await db.UsageWindows.CountAsync()));
    }

    [Fact]
    public async Task UsageShowsTheCurrentPlanImmediatelyAfterAnUpgradeOrExpiry()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var learner = factory.Client("Learner");
        await Chat(learner, "hello"); // window opened and charged under FREE
        var used = (await learner.GetFromJsonAsync<JsonElement>("/api/ai/usage/me")).GetProperty("tokensUsed").GetInt64();

        factory.Billing.PlanCode = "PRO_MONTH"; // paid or granted: no new chat yet
        var upgraded = await learner.GetFromJsonAsync<JsonElement>("/api/ai/usage/me");
        Assert.Equal("PRO_MONTH", upgraded.GetProperty("planCode").GetString());
        Assert.Equal(60_000, upgraded.GetProperty("tokenLimit").GetInt64());
        Assert.Equal(used, upgraded.GetProperty("tokensUsed").GetInt64()); // usage carries over, never reset
        Assert.Equal(60_000 - used, upgraded.GetProperty("remaining").GetInt64());
        Assert.Equal(60_000, (await learner.GetFromJsonAsync<JsonElement>("/api/ai/usage/me/daily")).GetProperty("tokenLimit").GetInt64());

        factory.Billing.PlanCode = "FREE"; // expired
        var expired = await learner.GetFromJsonAsync<JsonElement>("/api/ai/usage/me");
        Assert.Equal(("FREE", 15_000L), (expired.GetProperty("planCode").GetString(), expired.GetProperty("tokenLimit").GetInt64()));
    }

    [Fact]
    public async Task SessionsAndMessagesAreOwnOnlyOrderedAndSoftDeleted()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var me = factory.Client("Learner");
        var first = (await Chat(me, "first question")).Body.GetProperty("sessionId").GetGuid();
        factory.Clock.Now = factory.Clock.Now.AddMinutes(1);
        var second = (await Chat(me, "second question")).Body.GetProperty("sessionId").GetGuid();
        await Chat(factory.Client("Learner"), "someone else");

        var list = await me.GetFromJsonAsync<JsonElement>("/api/ai/sessions");
        Assert.Equal(new[] { second, first }, list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray());
        var messages = await me.GetFromJsonAsync<JsonElement>($"/api/ai/sessions/{first}/messages");
        Assert.Equal(new[] { "User", "Model" }, messages.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("role").GetString()).ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await factory.Client("Learner").GetAsync($"/api/ai/sessions/{first}/messages")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await factory.Client("Learner").DeleteAsync($"/api/ai/sessions/{first}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await me.DeleteAsync($"/api/ai/sessions/{first}")).StatusCode);
        Assert.Single((await me.GetFromJsonAsync<JsonElement>("/api/ai/sessions")).GetProperty("items").EnumerateArray());
        // Deleting never refunds tokens.
        Assert.Equal(1800, (await me.GetFromJsonAsync<JsonElement>("/api/ai/usage/me")).GetProperty("tokensUsed").GetInt64());
        await factory.WithDb(async db => Assert.True((await db.Sessions.SingleAsync(s => s.Id == first)).IsDeleted));
    }

    [Fact]
    public async Task DailyChartIsSevenVietnamDaysEndingTodayWithZeroFillAndIsolation()
    {
        using var factory = new AiApiFactory();
        // 2026-11-07 10:00 UTC = 17:00 Friday 07/11 in Vietnam.
        factory.Clock.Now = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        await factory.SeedLimits();
        var user = Guid.NewGuid();
        await factory.WithDb(async db =>
        {
            db.Requests.AddRange(
                Done(user, new DateTime(2026, 11, 6, 17, 30, 0, DateTimeKind.Utc), 500),  // 00:30 07/11 VN -> today
                Done(user, new DateTime(2026, 11, 6, 16, 30, 0, DateTimeKind.Utc), 300),  // 23:30 06/11 VN
                Done(user, new DateTime(2026, 10, 31, 18, 0, 0, DateTimeKind.Utc), 200),  // 01/11 VN (first day)
                Done(user, new DateTime(2026, 10, 31, 16, 0, 0, DateTimeKind.Utc), 999),  // 31/10 VN: outside
                Done(Guid.NewGuid(), new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc), 777));
            await db.SaveChangesAsync();
        });
        var daily = await factory.Client("Learner", user).GetFromJsonAsync<JsonElement>("/api/ai/usage/me/daily");
        var days = daily.GetProperty("days").EnumerateArray().ToArray();
        Assert.Equal(new[] { "2026-11-01", "2026-11-02", "2026-11-03", "2026-11-04", "2026-11-05", "2026-11-06", "2026-11-07" },
            days.Select(d => d.GetProperty("date").GetString()).ToArray());
        Assert.Equal(new long[] { 200, 0, 0, 0, 0, 300, 500 }, days.Select(d => d.GetProperty("tokens").GetInt64()).ToArray());
        Assert.Equal(1000, daily.GetProperty("totalTokens").GetInt64());
        Assert.Equal(15_000, daily.GetProperty("tokenLimit").GetInt64());

        // Next day the window rolls: 01/11 drops off on the left, 08/11 appears on the right.
        factory.Clock.Now = factory.Clock.Now.AddDays(1);
        var next = await factory.Client("Learner", user).GetFromJsonAsync<JsonElement>("/api/ai/usage/me/daily");
        Assert.Equal("2026-11-02", next.GetProperty("days")[0].GetProperty("date").GetString());
        Assert.Equal("2026-11-08", next.GetProperty("days")[6].GetProperty("date").GetString());
    }

    private static AiChatRequest Done(Guid user, DateTime completedAt, int tokens) => new()
    {
        UserId = user, RequestId = Guid.NewGuid(), Status = ChatRequestStatus.Completed, TotalTokens = tokens,
        CreatedAt = completedAt, LeaseUntil = completedAt, CompletedAt = completedAt
    };
}
