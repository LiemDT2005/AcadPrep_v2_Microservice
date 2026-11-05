using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ai.API.Entities;
using Ai.API.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Ai.API.Tests.ChatHelpers;

namespace Ai.API.Tests;

public class ChatRulesTests
{
    [Fact]
    public async Task AnonymousIsUnauthorized()
    {
        using var factory = new AiApiFactory();
        var (status, _) = await Chat(factory.Client(null), "Hello");
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task FirstAnswerCreatesSessionMessagesAndWindowWithSeparateSystemPrompt()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var user = Guid.NewGuid();
        var message = new string('a', 60) + " present perfect?";
        var (status, body) = await Chat(factory.Client("Learner", user), "  " + message + "  ");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new string('a', 50), body.GetProperty("sessionTitle").GetString());
        var usage = body.GetProperty("usage");
        Assert.Equal(15_000, usage.GetProperty("tokenLimit").GetInt64());
        Assert.Equal(14_100, usage.GetProperty("remaining").GetInt64());
        Assert.Equal(6, usage.GetProperty("percentUsed").GetInt32());
        Assert.False(usage.GetProperty("locked").GetBoolean());
        Assert.Equal(factory.Clock.Now.UtcDateTime.AddHours(24), usage.GetProperty("resetAt").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
        // countTokens first, then generate; learner text only in user contents, never in systemInstruction.
        Assert.Equal(new[] { "countTokens", "generateContent" }, factory.Gemini.Calls.Select(c => c.Method).ToArray());
        using var generate = JsonDocument.Parse(factory.Gemini.Calls.Last().Body);
        Assert.DoesNotContain(message, generate.RootElement.GetProperty("systemInstruction").GetRawText());
        Assert.Equal(message, generate.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(1024, generate.RootElement.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32());
        Assert.DoesNotContain(user.ToString(), generate.RootElement.GetRawText());
        await factory.WithDb(async db =>
        {
            Assert.Equal(2, await db.Messages.CountAsync());
            Assert.Equal(ChatRequestStatus.Completed, (await db.Requests.SingleAsync()).Status);
            Assert.Equal("FREE", (await db.UsageWindows.SingleAsync()).PlanCode);
        });
    }

    [Fact]
    public async Task ContextKeepsTheLastTenMessagesOfTheSession()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits(free: 1_000_000);
        using var client = factory.Client("Learner");
        var session = (await Chat(client, "m0")).Body.GetProperty("sessionId").GetGuid();
        for (var i = 1; i < 7; i++) await Chat(client, $"m{i}", sessionId: session);
        using var last = JsonDocument.Parse(factory.Gemini.Calls.Last(c => c.Method == "generateContent").Body);
        var contents = last.RootElement.GetProperty("contents");
        Assert.Equal(11, contents.GetArrayLength()); // 10 history turns + the new message
        Assert.Equal("m6", contents[10].GetProperty("parts")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ProPlanFromBillingUsesTheProLimitAndForwardsTheJwt()
    {
        using var factory = new AiApiFactory();
        factory.Billing.PlanCode = "PRO_QUARTER";
        await factory.SeedLimits();
        using var client = factory.Client("Learner");
        var (_, body) = await Chat(client, "hi");
        Assert.Equal(60_000, body.GetProperty("usage").GetProperty("tokenLimit").GetInt64());
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.Parameter, Assert.Single(factory.Billing.Tokens));
    }

    [Fact]
    public async Task BillingFailureFallsBackToFree()
    {
        using var factory = new AiApiFactory();
        factory.Billing.Status = 500;
        await factory.SeedLimits();
        var (status, body) = await Chat(factory.Client("Learner"), "hi");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("FREE", body.GetProperty("usage").GetProperty("planCode").GetString());
    }

    [Fact]
    public async Task AnswerWithAtLeastOneTokenLeftOvershootsThenTheNextIsBlockedBeforeGemini()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits(free: 1000);
        using var client = factory.Client("Learner");
        await Chat(client, "first");                      // 900 used, 100 left
        factory.Gemini.InputTokens = 99;                  // 1 token left after input: still answered in full
        var (status, body) = await Chat(client, "second");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1800, body.GetProperty("usage").GetProperty("tokensUsed").GetInt64());
        Assert.True(body.GetProperty("usage").GetProperty("locked").GetBoolean());
        var calls = factory.Gemini.Calls.Count;
        var (blocked, problem) = await Chat(client, "third");
        Assert.Equal((HttpStatusCode)429, blocked);
        Assert.Equal("QuotaExceeded", problem.GetProperty("code").GetString());
        Assert.Equal(calls, factory.Gemini.Calls.Count); // neither countTokens nor generate
    }

    [Fact]
    public async Task LockedRequestCanBeRetriedWithTheSameIdAfterReset()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits(free: 1000);
        using var client = factory.Client("Learner");
        await Chat(client, "first");
        factory.Gemini.InputTokens = 100;
        var requestId = Guid.NewGuid();
        Assert.Equal((HttpStatusCode)429, (await Chat(client, "second", requestId)).Status);
        await factory.WithDb(async db =>
        {
            Assert.True((await db.UsageWindows.SingleAsync()).Locked);
            Assert.Equal(900, (await db.UsageWindows.SingleAsync()).TokensUsed); // nothing charged
            Assert.Equal(ChatRequestStatus.Rejected, (await db.Requests.SingleAsync(r => r.RequestId == requestId)).Status);
        });
        Assert.Equal((HttpStatusCode)429, (await Chat(client, "second", requestId)).Status); // still locked
        factory.Clock.Now = factory.Clock.Now.AddHours(24).AddMinutes(1);
        var (status, body) = await Chat(client, "second", requestId);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(900, body.GetProperty("usage").GetProperty("tokensUsed").GetInt64()); // a new window
        await factory.WithDb(async db => Assert.Equal(2, await db.UsageWindows.CountAsync()));
    }

    [Fact]
    public async Task CountTokensUnavailableFallsBackToAConservativeEstimate()
    {
        using var factory = new AiApiFactory();
        factory.Gemini.CountStatus = 500;
        await factory.SeedLimits();
        Assert.Equal(HttpStatusCode.OK, (await Chat(factory.Client("Learner"), "hello")).Status);
        await factory.WithDb(async db => Assert.True((await db.Requests.SingleAsync()).InputTokens > 0));
    }

    [Fact]
    public async Task TransientGeminiErrorsAreRetriedTwiceThen502WithoutChargingAndTheIdCanBeRetried()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        foreach (var code in new[] { 503, 429, 500 }) factory.Gemini.GenerateStatuses.Enqueue(code);
        using var client = factory.Client("Learner");
        var requestId = Guid.NewGuid();
        var (status, body) = await Chat(client, "hello", requestId);
        Assert.Equal(HttpStatusCode.BadGateway, status);
        Assert.Equal(3, factory.Gemini.GenerateCalls);
        Assert.DoesNotContain("sensitive", body.GetRawText());
        await factory.WithDb(async db =>
        {
            Assert.Equal(0, await db.Messages.CountAsync());
            Assert.Equal(0, await db.UsageWindows.CountAsync());
        });
        Assert.Equal(HttpStatusCode.OK, (await Chat(client, "hello", requestId)).Status);
    }

    [Fact]
    public async Task NonTransientGeminiErrorIsNotRetried()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        factory.Gemini.GenerateStatuses.Enqueue(400);
        Assert.Equal(HttpStatusCode.BadGateway, (await Chat(factory.Client("Learner"), "hello")).Status);
        Assert.Equal(1, factory.Gemini.GenerateCalls);
    }

    [Fact]
    public async Task OutOfScopeMarkerAndSafetyBlockAreReplacedAndTokensStillCounted()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        using var client = factory.Client("Learner");
        factory.Gemini.Reply = "OUT_OF_SCOPE";
        var (_, outOfScope) = await Chat(client, "Thời tiết hôm nay thế nào?");
        Assert.True(outOfScope.GetProperty("isOutOfScope").GetBoolean());
        Assert.StartsWith("Mình chỉ hỗ trợ các câu hỏi về TOEIC", outOfScope.GetProperty("reply").GetString());
        factory.Gemini.Blocked = true;
        var (_, blocked) = await Chat(client, "something unsafe");
        Assert.StartsWith("Mình không thể trả lời", blocked.GetProperty("reply").GetString());
        Assert.Equal(1800, blocked.GetProperty("usage").GetProperty("tokensUsed").GetInt64());
    }

    [Fact]
    public async Task OtherSubjectsAreRedirectedWithoutCallingGemini()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var (status, body) = await Chat(factory.Client("Learner"), "Tính chu kỳ con lắc lò xo có k = 100 N/m");
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("AcadPrep AI Tutor", body.GetProperty("reply").GetString());
        Assert.Empty(factory.Gemini.Calls);
    }

    [Fact]
    public async Task MultipleChoiceAnswerLeakIsReplacedWithAPedagogicalHint()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        factory.Gemini.Reply = "Đáp án đúng là B vì đây là quá khứ đơn.";
        var (_, body) = await Chat(factory.Client("Learner"), "She ___ to Paris yesterday. A. go B. went C. goes D. going");
        var reply = body.GetProperty("reply").GetString()!;
        Assert.DoesNotContain("Đáp án đúng là B", reply);
        Assert.Contains("yesterday", reply);
    }

    [Fact]
    public async Task AnotherLearnersOrDeletedSessionIs404()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var owner = factory.Client("Learner");
        var session = (await Chat(owner, "hi")).Body.GetProperty("sessionId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Chat(factory.Client("Learner"), "x", sessionId: session)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/ai/sessions/{session}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Chat(owner, "x", sessionId: session)).Status);
    }

    [Fact]
    public async Task OnlyOneRequestInFlightPerUserAndStaleLeasesExpire()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var user = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            db.Requests.Add(new AiChatRequest { UserId = user, RequestId = Guid.NewGuid(), CreatedAt = now, LeaseUntil = now.AddMinutes(1) });
            await db.SaveChangesAsync();
        });
        using var client = factory.Client("Learner", user);
        Assert.Equal(HttpStatusCode.Conflict, (await Chat(client, "hi")).Status);
        Assert.Empty(factory.Gemini.Calls);
        factory.Clock.Now = factory.Clock.Now.AddMinutes(3); // lease expired (crashed request)
        Assert.Equal(HttpStatusCode.OK, (await Chat(client, "hi")).Status);
    }

    [Fact]
    public async Task SameIdStillProcessingIsConflict()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var user = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var now = factory.Clock.Now.UtcDateTime;
        await factory.WithDb(async db =>
        {
            db.Requests.Add(new AiChatRequest { UserId = user, RequestId = requestId, CreatedAt = now, LeaseUntil = now.AddMinutes(2) });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await Chat(factory.Client("Learner", user), "hi", requestId)).Status);
    }

    [Fact]
    public async Task MissingLimitIs503AndMissingGeminiKeyIs503()
    {
        using var noLimits = new AiApiFactory();
        await noLimits.WithDb(_ => Task.CompletedTask);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Chat(noLimits.Client("Learner"), "hi")).Status);
        using var noKey = new AiApiFactory { GeminiKey = "" };
        await noKey.SeedLimits();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Chat(noKey.Client("Learner"), "hi")).Status);
        Assert.Empty(noKey.Gemini.Calls);
    }

    [Theory]
    [InlineData("{\"requestId\":\"R\",\"message\":\"   \"}")]
    [InlineData("{\"requestId\":\"R\",\"message\":\"LONG\"}")]
    [InlineData("{\"message\":\"hi\"}")]
    [InlineData("{\"requestId\":\"00000000-0000-0000-0000-000000000000\",\"message\":\"hi\"}")]
    [InlineData("{\"requestId\":\"R\",\"message\":\"hi\",\"history\":[]}")]
    public async Task InvalidBodyIs400(string template)
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var json = template.Replace("\"R\"", $"\"{Guid.NewGuid()}\"").Replace("LONG", new string('x', 2001));
        using var response = await factory.Client("Learner").PostAsync("/api/ai/chat", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Gemini.Calls);
    }
}
