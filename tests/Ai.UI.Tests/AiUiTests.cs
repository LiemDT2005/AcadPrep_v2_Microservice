using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Web.Models;
using Storefront.Web.Services;
using Xunit;

namespace Ai.UI.Tests;

public class AiUiTests
{
    [Theory]
    [InlineData("Learner", true)]
    [InlineData("Admin", false)]
    [InlineData("Moderator", false)]
    public async Task WidgetIsRenderedOnlyForLearners(string role, bool expected)
    {
        using var factory = new AiUiFactory();
        factory.Backend.Roles = [role];
        using var client = factory.Browser();
        await factory.Login(client);
        var html = await client.GetStringAsync("/Billing/Plans");
        Assert.Equal(expected, html.Contains("id=\"ai-chat-widget\""));
        Assert.Equal(expected, html.Contains("ai-chat-widget.js"));
    }

    [Fact]
    public async Task AnonymousVisitorsDoNotGetTheWidget()
    {
        using var factory = new AiUiFactory();
        var html = await factory.Browser().GetStringAsync("/Billing/Plans");
        Assert.DoesNotContain("id=\"ai-chat-widget\"", html);
    }

    [Fact]
    public async Task StateRelaysUsageAndSessionsWithTheSessionToken()
    {
        using var factory = new AiUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync("/Ai/Chat?handler=State");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(15000, json.GetProperty("usage").GetProperty("tokenLimit").GetInt64());
        Assert.Equal("Thì hiện tại", json.GetProperty("sessions").GetProperty("items")[0].GetProperty("title").GetString());
        Assert.Contains(factory.Backend.Calls, call => call.Path == "/ai/api/ai/usage/me" && call.Bearer);
    }

    [Fact]
    public async Task SendRequiresAntiforgeryAndRelaysOnlyWhitelistedFields()
    {
        using var factory = new AiUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        var requestId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { requestId, message = "  hello  ", history = "ignored" });
        using var noToken = await client.PostAsync("/Ai/Chat?handler=Send", new StringContent(payload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Empty(factory.Backend.ChatBodies);

        using var response = await client.SendAsync(Post(payload, await Token(client)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var sent = JsonDocument.Parse(Assert.Single(factory.Backend.ChatBodies));
        Assert.Equal(new[] { "message", "requestId", "sessionId" }, sent.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("hello", sent.RootElement.GetProperty("message").GetString());
        Assert.Equal("Xin chào!", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reply").GetString());
    }

    [Theory]
    [InlineData(429, "Bạn đã dùng hết hạn mức hôm nay")]
    [InlineData(502, "AI đang bận")]
    [InlineData(409, "Tin nhắn trước vẫn đang được trả lời")]
    [InlineData(401, "Phiên đăng nhập đã hết hạn")]
    public async Task ErrorsAreFriendlyJsonWithoutDiagnostics(int upstream, string expected)
    {
        using var factory = new AiUiFactory();
        factory.Backend.ChatStatus = upstream;
        using var client = factory.Browser();
        await factory.Login(client);
        var token = await Token(client);
        using var response = await client.SendAsync(Post(JsonSerializer.Serialize(new { requestId = Guid.NewGuid(), message = "hi" }), token));
        Assert.Equal(upstream, (int)response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains(expected, Regex.Unescape(text));
        Assert.DoesNotContain("sensitive", text);
        if (upstream == 429)
        {
            var json = JsonDocument.Parse(text).RootElement;
            Assert.True(json.GetProperty("usage").GetProperty("locked").GetBoolean());
            Assert.Equal("QuotaExceeded", json.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task InvalidMessageNeverReachesTheAiService()
    {
        using var factory = new AiUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.SendAsync(Post(JsonSerializer.Serialize(new { requestId = Guid.NewGuid(), message = new string('x', 2001) }), await Token(client)));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Backend.ChatBodies);
    }

    [Fact]
    public async Task AdminUsagePageIsAdminOnlyAndShowsLimitsAndWindows()
    {
        using var factory = new AiUiFactory();
        using var learner = factory.Browser();
        await factory.Login(learner);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/Admin/Ai/Usage")).StatusCode);

        factory.Backend.Roles = ["Admin"];
        using var admin = factory.Browser();
        await factory.Login(admin);
        var raw = await admin.GetStringAsync("/Admin/Ai/Usage");
        var html = WebUtility.HtmlDecode(raw);
        Assert.Contains("PRO_QUARTER", html);
        Assert.Contains("data-val-aitokenlimit", raw);
        Assert.Contains(AiBackend.UserId.ToString(), html);
        Assert.Contains("Đã khóa", html);
        Assert.Contains("ai-admin-validation.js", raw);
    }

    [Theory]
    [InlineData("5000", true)]
    [InlineData("-1", false)]
    [InlineData("100000001", false)]
    public async Task LimitFormValidatesBeforeCallingTheAiService(string value, bool called)
    {
        using var factory = new AiUiFactory();
        factory.Backend.Roles = ["Admin"];
        using var admin = factory.Browser();
        await factory.Login(admin);
        var page = await admin.GetStringAsync("/Admin/Ai/Usage");
        using var response = await admin.PostAsync("/Admin/Ai/Usage?handler=Limit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AiUiFactory.Antiforgery(page), ["Limit.PlanCode"] = "FREE", ["Limit.TokenLimit"] = value
        }));
        Assert.Equal(called, factory.Backend.LimitBodies.Count == 1);
        if (called)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("{\"tokenLimit\":5000}", factory.Backend.LimitBodies.Single());
        }
    }

    [Fact]
    public async Task ResetPostsToTheAiService()
    {
        using var factory = new AiUiFactory();
        factory.Backend.Roles = ["Admin"];
        using var admin = factory.Browser();
        await factory.Login(admin);
        var page = await admin.GetStringAsync("/Admin/Ai/Usage");
        using var response = await admin.PostAsync("/Admin/Ai/Usage?handler=Reset", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = AiUiFactory.Antiforgery(page), ["resetUserId"] = AiBackend.UserId.ToString()
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(factory.Backend.Calls, call => call.Method == "POST" && call.Path == $"/ai/api/admin/ai/usage/{AiBackend.UserId}/reset");
    }

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/Billing/Plans"); // the widget renders an antiforgery token
        return AiUiFactory.Antiforgery(html);
    }

    private static HttpRequestMessage Post(string json, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/Ai/Chat?handler=Send") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add("RequestVerificationToken", token);
        return request;
    }
}

internal class AiUiFactory : WebApplicationFactory<AiApiClient>
{
    public AiBackend Backend { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<ToeicApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
            services.AddHttpClient<BillingApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
            services.AddHttpClient<AiApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    public async Task Login(HttpClient client)
    {
        var token = Antiforgery(await client.GetStringAsync("/Account/Login"));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Email"] = "test@example.test", ["Password"] = "test-only-password"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    public static string Antiforgery(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
}

// Test-only upstream simulator (Identity, Billing plans, ai-service); production goes through the Gateway.
internal class AiBackend : HttpMessageHandler
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string[] Roles { get; set; } = ["Learner"];
    public int ChatStatus { get; set; } = 200;
    public ConcurrentQueue<(string Method, string Path, bool Bearer)> Calls { get; } = new();
    public ConcurrentBag<string> ChatBodies { get; } = new();
    public ConcurrentBag<string> LimitBodies { get; } = new();

    private static object Usage(bool locked) => new
    {
        planCode = "FREE", tokenLimit = 15000, tokensUsed = locked ? 15200 : 900, remaining = locked ? 0 : 14100,
        percentUsed = locked ? 100 : 6, windowStart = DateTime.UtcNow, resetAt = DateTime.UtcNow.AddHours(23), locked, periodKey = "x"
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        Calls.Enqueue((request.Method.Method, path, request.Headers.Authorization?.Parameter == "test-session-token"));
        if (path == "/identity/api/auth/login") return Json(200, new AuthResponse("test-session-token", "test@example.test",
            "Test User", UserId.ToString(), DateTime.UtcNow.AddHours(1), Roles.ToList()));
        if (path == "/identity/api/auth/me") return Json(200, new UserProfileDto(UserId.ToString(), "test@example.test", "Test User",
            null, "Active", Roles.ToList(), DateTime.UtcNow, null));
        if (path == "/notification/api/notifications/unread-count") return Json(200, new NotificationUnreadCount(0));
        if (path == "/billing/api/plans") return Json(200, Array.Empty<BillingPlan>());
        // The price list marks the signed-in Learner's current plan.
        if (path == "/billing/api/subscriptions/me/usage")
            return Json(200, new BillingUsage("FREE", [], new BillingAiUsage(false, null, null, null, null)));
        if (path == "/billing/api/subscriptions/me")
            return Json(200, new BillingSubscription("Free", "FREE", "Miễn phí", false, null, null, null));
        if (path == "/ai/api/ai/usage/me") return Json(200, Usage(false));
        if (path.StartsWith("/ai/api/ai/sessions?")) return Json(200, new { items = new[] { new { id = Guid.NewGuid(), title = "Thì hiện tại" } } });
        if (path == "/ai/api/ai/chat")
        {
            ChatBodies.Add(await request.Content!.ReadAsStringAsync(ct));
            return ChatStatus == 200
                ? Json(200, new { requestId = Guid.NewGuid(), sessionId = Guid.NewGuid(), sessionTitle = "hello", reply = "Xin chào!", isOutOfScope = false, tokensUsed = 900, usage = Usage(false), replayed = false })
                : Json(ChatStatus, new { title = "sensitive upstream detail", code = "QuotaExceeded", usage = Usage(true) });
        }
        if (path == "/ai/api/admin/ai/limits") return Json(200, new[] { new { planCode = "FREE", tokenLimit = 15000 }, new { planCode = "PRO_MONTH", tokenLimit = 60000 }, new { planCode = "PRO_QUARTER", tokenLimit = 60000 } });
        if (path.StartsWith("/ai/api/admin/ai/limits/"))
        {
            LimitBodies.Add(await request.Content!.ReadAsStringAsync(ct));
            return Json(200, new { planCode = "FREE", tokenLimit = 5000 });
        }
        if (path.StartsWith("/ai/api/admin/ai/usage?")) return Json(200, new
        {
            items = new[] { new { userId = UserId, planCode = "FREE", tokensUsed = 15200, tokenLimit = 15000, remaining = 0, windowStart = DateTime.UtcNow, resetAt = DateTime.UtcNow.AddHours(3), locked = true } },
            pageIndex = 1, pageSize = 20, totalCount = 1, totalPages = 1
        });
        if (path.EndsWith("/reset")) return new HttpResponseMessage(HttpStatusCode.NoContent);
        throw new InvalidOperationException($"Unexpected upstream route in UI test: {path}");
    }

    private static HttpResponseMessage Json(int status, object data) => new((HttpStatusCode)status) { Content = JsonContent.Create(data) };
    protected override void Dispose(bool disposing) { }
}
