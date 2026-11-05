using System.Net;
using Xunit;
using static Notifications.UI.Tests.NotificationRenderingTests;

namespace Notifications.UI.Tests;

public class NotificationRecipientUiTests
{
    private const string Page = "/Admin/Notifications/Create";
    private const string Target = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public async Task MissingRecipientIsShownOnTheTargetFieldAndInputIsKept()
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin"];
        factory.Backend.CreateStatus = 422;
        using var client = Client(factory);
        await Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client)));
        var raw = await response.Content.ReadAsStringAsync();
        var html = WebUtility.HtmlDecode(raw);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Người dùng không tồn tại.", html);
        Assert.Contains("Không tìm thấy người nhận", html);
        Assert.Contains($"value=\"{Target}\"", raw);
        Assert.DoesNotContain("internal-diagnostic", html);
    }

    [Fact]
    public async Task IdentityUnavailableIsAFriendlyRetryMessage()
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin"];
        factory.Backend.CreateStatus = 503;
        using var client = Client(factory);
        await Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client)));
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Chưa thể xác minh người nhận", html);
        Assert.DoesNotContain("internal-diagnostic", html);
    }

    private static async Task<Dictionary<string, string>> Form(HttpClient client) => new()
    {
        ["__RequestVerificationToken"] = Token(await client.GetStringAsync(Page)),
        ["Input.Title"] = "Title", ["Input.Content"] = "Content", ["Input.Audience"] = "User",
        ["Input.TargetUserId"] = Target
    };
}
