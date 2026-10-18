using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Notifications.UI.Tests;

public class NotificationRenderingTests
{
    [Fact]
    public async Task AnonymousPageRedirectsWithoutCallingProtectedApi()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        using var response = await client.GetAsync("/Notifications");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
        Assert.Empty(factory.Backend.Calls);
    }

    [Fact]
    public async Task ListRendersEncodedContentFormsPaginationAndBadgeWithBearerForwarding()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        var html = await client.GetStringAsync("/Notifications?pageIndex=2&pageSize=20&unreadOnly=true");
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;img", html);
        Assert.Contains("07:00", html);
        Assert.Contains("handler=Read", html);
        Assert.Contains("handler=Delete", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("pageIndex=3", html);
        Assert.Contains("unreadOnly=True", html);
        Assert.Contains("notifications</span>", html);
        Assert.Contains(factory.Backend.Calls, call => call.Path.Contains("pageIndex=2&pageSize=20&unreadOnly=true") && call.Bearer);
        Assert.All(factory.Backend.Calls.Where(call => call.Path != "/identity/api/auth/login"), call => Assert.True(call.Bearer));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyAndErrorAreDistinct(bool empty)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Empty = empty;
        if (!empty) factory.Backend.ListStatus = 503;
        using var client = Client(factory);
        await Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Notifications"));
        Assert.Contains(empty ? "Chưa có thông báo" : "Không thể tải thông báo", html);
        if (!empty) Assert.DoesNotContain("Chưa có thông báo trong trang này", html);
    }

    [Theory]
    [InlineData("Read", "PATCH")]
    [InlineData("Delete", "DELETE")]
    public async Task PostHandlersRequireAntiforgeryAndPreserveFilter(string handler, string method)
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        var form = new Dictionary<string, string> { ["id"] = NotificationBackend.NotificationId.ToString(),
            ["PageIndex"] = "2", ["PageSize"] = "20", ["UnreadOnly"] = "true" };
        using var rejected = await client.PostAsync($"/Notifications?handler={handler}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.DoesNotContain(factory.Backend.Calls, call => call.Method == method);
        form["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Notifications"));
        using var response = await client.PostAsync($"/Notifications?handler={handler}", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("pageIndex=2", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unreadOnly=True", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(factory.Backend.Calls, call => call.Method == method && call.Bearer);
    }

    [Theory]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    [InlineData("Admin", 200)]
    public async Task AdminPageUsesVerifiedIdentityRoles(string role, int status)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = [role];
        using var client = Client(factory);
        await Login(client);
        using var response = await client.GetAsync("/Admin/Notifications/Create");
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 200)
        {
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("data-val-notificationaudience", html);
            Assert.Contains("notification-validation.js", html);
            Assert.Contains("jquery.validate.unobtrusive.min.js", html);
        }
    }

    [Theory]
    [InlineData(401, 302)]
    [InlineData(503, 503)]
    public async Task IdentityFailureFailsClosed(int upstream, int expected)
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        factory.Backend.IdentityStatus = upstream;
        using var response = await client.GetAsync("/Admin/Notifications/Create");
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.DoesNotContain(factory.Backend.Calls, call => call.Path == "/notification/api/admin/notifications");
    }

    [Fact]
    public async Task BadgeFailureDoesNotCrashOrInventZero()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        factory.Backend.CountStatus = 503;
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Notifications"));
        Assert.Contains("Chưa thể tải số thông báo chưa đọc", html);
        Assert.DoesNotContain("0 thông báo chưa đọc", html);
    }

    internal static HttpClient Client(NotificationUiFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    internal static async Task Login(HttpClient client)
    {
        var token = Token(await client.GetStringAsync("/Account/Login"));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Email"] = "test@example.test", ["Password"] = "test-only-password"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    internal static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
}
