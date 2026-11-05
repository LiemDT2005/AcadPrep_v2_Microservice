using System.Net;
using System.Text.Json;
using Xunit;
using static Notifications.UI.Tests.NotificationRenderingTests;

namespace Notifications.UI.Tests;

// Facebook-style bell dropdown, Vietnamese page-size validation and the admin recipient picker.
public class NotificationBellTests
{
    [Theory]
    [InlineData("Learner", "/Notifications", "AcadPrep")]
    [InlineData("Admin", "/Admin/Notifications/Create", "AcadPrep Admin")]
    [InlineData("Moderator", "/Account/Profile", "AcadPrep CMS")]
    public async Task BellLinksToTheFullListInTheLayoutForTheRole(string role, string page, string layoutTitle)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = [role];
        using var client = Client(factory);
        await Login(client);
        var html = await client.GetStringAsync(page);
        Assert.Contains("data-notification-menu", html);
        Assert.Contains("aria-controls=\"notification-menu-panel\"", html);
        Assert.Contains("aria-expanded=\"false\"", html);
        Assert.Contains("id=\"notification-menu-panel\" hidden", html);
        Assert.Contains("Xem tất cả", WebUtility.HtmlDecode(html));
        Assert.Contains("href=\"/Notifications\"", html); // without JavaScript the bell still opens the full list
        Assert.Contains("/js/notification-menu.js", html);

        var inbox = await client.GetStringAsync("/Notifications");
        Assert.Contains($" - {layoutTitle}</title>", inbox);
        Assert.Contains("id=\"notification-panel\"", inbox);
    }

    [Theory]
    [InlineData("Learner", "Moderator", "AcadPrep CMS")]
    [InlineData("Moderator", "Learner", "AcadPrep")]
    public async Task InboxUsesVerifiedRoleWhenSessionRoleIsStale(string loginRole, string currentRole, string layoutTitle)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = [loginRole];
        using var client = Client(factory);
        await Login(client);
        factory.Backend.Roles = [currentRole];

        var inbox = await client.GetStringAsync("/Notifications");

        Assert.Contains($" - {layoutTitle}</title>", inbox);
    }

    [Fact]
    public async Task LatestReturnsTheNewestItemsAndUnreadCountAsJson()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Notifications?handler=Latest");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, json.RootElement.GetProperty("unreadCount").GetInt32());
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(NotificationBackend.NotificationId, item.GetProperty("id").GetGuid());
        Assert.False(item.GetProperty("isRead").GetBoolean());
        Assert.Contains(factory.Backend.Calls, call => call.Path.Contains("pageIndex=1&pageSize=6&unreadOnly=false") && call.Bearer);
    }

    [Fact]
    public async Task LatestWithoutSessionIs401JsonAndUpstreamFailureIsNotAnEmptyList()
    {
        using var factory = new NotificationUiFactory();
        using var anonymous = Client(factory);
        using var guest = new HttpRequestMessage(HttpMethod.Get, "/Notifications?handler=Latest");
        guest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(guest)).StatusCode);

        factory.Backend.ListStatus = 500;
        using var client = Client(factory);
        await Login(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Notifications?handler=Latest");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.DoesNotContain("items", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PageSizeUsesTheVietnameseRangeMessageInsteadOfBrowserDefaults()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Notifications"));
        Assert.Contains("data-val-range=\"Số thông báo mỗi trang phải từ 1 đến 100.\"", html);
        Assert.DoesNotContain(" max=\"100\"", html); // native/jQuery defaults would show an English message
        Assert.Contains("data-confirm=\"Xóa thông báo này khỏi danh sách của bạn?\"", html);
        Assert.DoesNotContain("return confirm(", html);
    }

    [Theory]
    [InlineData("Admin", HttpStatusCode.OK)]
    [InlineData("Learner", HttpStatusCode.Forbidden)]
    public async Task RecipientSearchIsAdminOnlyAndUsesIdentity(string role, HttpStatusCode expected)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = [role];
        using var client = Client(factory);
        await Login(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Notifications/Create?handler=Users&q=learner");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected != HttpStatusCode.OK)
        {
            Assert.DoesNotContain(factory.Backend.Calls, call => call.Path.StartsWith("/identity/api/users?"));
            return;
        }
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("learner@example.test", user.GetProperty("email").GetString());
        Assert.Contains(factory.Backend.Calls, call => call.Path.StartsWith("/identity/api/users?") &&
            call.Path.Contains("search=learner") && call.Bearer);
    }
}
