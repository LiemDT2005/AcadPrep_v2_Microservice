using System.Net;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Ai.API.Tests.ChatHelpers;

namespace Ai.API.Tests;

public class ChatTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Moderator")]
    public async Task OnlyLearnersChat(string role)
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        var (status, _) = await Chat(factory.Client(role), "Hello");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Empty(factory.Gemini.Calls);
    }

    [Fact]
    public async Task FirstAnswerOpensA24hWindowAndReplayNeverChargesTwice()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits();
        using var client = factory.Client("Learner");
        var requestId = Guid.NewGuid();
        var (status, body) = await Chat(client, "Khi nào dùng thì hiện tại hoàn thành?", requestId);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(900, body.GetProperty("usage").GetProperty("tokensUsed").GetInt64());
        Assert.Equal("test-only-gemini-key", factory.Gemini.Calls.First().ApiKey);
        Assert.DoesNotContain("learner@example.test", string.Concat(factory.Gemini.Calls.Select(c => c.Body)));
        var (again, replay) = await Chat(client, "Khi nào dùng thì hiện tại hoàn thành?", requestId);
        Assert.Equal(HttpStatusCode.OK, again);
        Assert.True(replay.GetProperty("replayed").GetBoolean());
        Assert.Equal(1, factory.Gemini.GenerateCalls);
        await factory.WithDb(async db => Assert.Equal(900, (await db.UsageWindows.SingleAsync()).TokensUsed));
    }

    [Fact]
    public async Task InputThatUsesUpTheBudgetLocksWithoutGenerating()
    {
        using var factory = new AiApiFactory();
        await factory.SeedLimits(free: 1000);
        using var client = factory.Client("Learner");
        Assert.Equal(HttpStatusCode.OK, (await Chat(client, "first")).Status); // uses 900, 100 left
        factory.Gemini.InputTokens = 100;
        var (status, body) = await Chat(client, "second");
        Assert.Equal((HttpStatusCode)429, status);
        Assert.Equal(1, factory.Gemini.GenerateCalls);
        Assert.True(body.GetProperty("usage").GetProperty("locked").GetBoolean());
    }
}
