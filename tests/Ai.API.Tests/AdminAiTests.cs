using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ai.API.Data;
using Ai.API.Entities;
using Ai.API.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using static Ai.API.Tests.ChatHelpers;

namespace Ai.API.Tests;

public class AdminAiTests
{
    [Theory]
    [InlineData(null, 401)]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    public async Task OnlyAdminManagesAi(string? role, int expected)
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var client = factory.Client(role);
        Assert.Equal(expected, (int)(await client.GetAsync("/api/admin/ai/usage")).StatusCode);
        Assert.Equal(expected, (int)(await client.PutAsJsonAsync("/api/admin/ai/limits/FREE", new { tokenLimit = 1 })).StatusCode);
        Assert.Equal(expected, (int)(await client.PostAsync($"/api/admin/ai/usage/{Guid.NewGuid()}/reset", null)).StatusCode);
    }

    [Fact]
    public async Task UsageListsActiveWindowsSortedAndFiltered()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var heavy = Guid.NewGuid();
        var light = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            db.UsageWindows.AddRange(
                Window(light, now.AddHours(-1), 100), Window(heavy, now.AddHours(-2), 9000, "PRO_MONTH"),
                Window(Guid.NewGuid(), now.AddHours(-30), 50_000)); // expired window: not listed
            await db.SaveChangesAsync();
        });
        var admin = factory.Client("Admin");
        var page = await admin.GetFromJsonAsync<JsonElement>("/api/admin/ai/usage");
        var rows = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new[] { heavy, light }, rows.Select(r => r.GetProperty("userId").GetGuid()).ToArray());
        Assert.Equal(60_000, rows[0].GetProperty("tokenLimit").GetInt64());
        Assert.Equal(51_000, rows[0].GetProperty("remaining").GetInt64());
        var filtered = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/ai/usage?userId={light}");
        Assert.Single(filtered.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/ai/usage?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task LimitChangeIsAuditedAndEffectiveForTheNextChat()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var admin = factory.Client("Admin");
        using var response = await admin.PutAsJsonAsync("/api/admin/ai/limits/FREE", new { tokenLimit = 500 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(500, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tokenLimit").GetInt64());
        var (_, body) = await Chat(factory.Client("Learner"), "hi"); // 150 input < 500, answer 900 overshoots
        Assert.Equal(500, body.GetProperty("usage").GetProperty("tokenLimit").GetInt64());
        Assert.True(body.GetProperty("usage").GetProperty("locked").GetBoolean());
        await factory.WithDb(async db =>
        {
            var audit = await db.Audits.SingleAsync();
            Assert.Equal((AuditAction.ChangeLimit, "FREE", 15_000L, 500L), (audit.Action, audit.PlanCode, audit.OldValue!.Value, audit.NewValue!.Value));
        });
    }

    [Theory]
    [InlineData("FREE", "{\"tokenLimit\":-1}", 400)]
    [InlineData("FREE", "{\"tokenLimit\":100000001}", 400)]
    [InlineData("FREE", "{}", 400)]
    [InlineData("GOLD", "{\"tokenLimit\":1}", 404)]
    public async Task InvalidLimitUpdatesAreRejected(string plan, string json, int expected)
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        using var response = await factory.Client("Admin").PutAsync($"/api/admin/ai/limits/{plan}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(expected, (int)response.StatusCode);
        await factory.WithDb(async db => Assert.Equal(0, await db.Audits.CountAsync()));
    }

    [Fact]
    public async Task ResetClosesTheWindowUnlocksAndIsAudited()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits(free: 1000);
        var user = Guid.NewGuid();
        var learner = factory.Client("Learner", user);
        await Chat(learner, "first");
        factory.Gemini.InputTokens = 100;
        Assert.Equal((HttpStatusCode)429, (await Chat(learner, "second")).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Client("Admin").PostAsync($"/api/admin/ai/usage/{user}/reset", null)).StatusCode);
        var usage = await learner.GetFromJsonAsync<JsonElement>("/api/ai/usage/me");
        Assert.Equal(0, usage.GetProperty("tokensUsed").GetInt64());
        Assert.False(usage.GetProperty("locked").GetBoolean());
        factory.Gemini.InputTokens = 150;
        Assert.Equal(HttpStatusCode.OK, (await Chat(learner, "after reset")).Status);
        await factory.WithDb(async db =>
        {
            var audit = await db.Audits.SingleAsync();
            Assert.Equal((AuditAction.ResetUsage, user, 900L, 0L), (audit.Action, audit.UserId!.Value, audit.OldValue!.Value, audit.NewValue!.Value));
        });
        // Unknown user: still 204.
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Client("Admin").PostAsync($"/api/admin/ai/usage/{Guid.NewGuid()}/reset", null)).StatusCode);
    }

    [Fact]
    public void SqlServerModelAndSeedMigrationAreConsistentOffline()
    {
        using var db = new AiDbContext(new DbContextOptionsBuilder<AiDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=AiModelTests;Integrated Security=True").Options);
        Assert.Equal(new[] { "AiChatMessages", "AiChatRequests", "AiChatSessions", "AiTokenLimits", "AiUsageAudits", "AiUsageWindows" },
            db.Model.GetEntityTypes().Select(e => e.GetTableName()).Order().ToArray());
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("(N'FREE', CAST(15000 AS bigint)), (N'PRO_MONTH', CAST(60000 AS bigint)), (N'PRO_QUARTER', CAST(60000 AS bigint))", script);
        Assert.Contains("UX_AiChatRequests_UserId_Processing", script);
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    private static AiUsageWindow Window(Guid user, DateTime start, long used, string plan = "FREE") => new()
    {
        UserId = user, WindowStart = start, WindowEnd = start.AddHours(24), TokensUsed = used, PlanCode = plan, UpdatedAt = start
    };
}
