using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Web.Models;
using Xunit;
using static Notifications.UI.Tests.NotificationRenderingTests;

namespace Notifications.UI.Tests;

public class NotificationAjaxTests
{
    [Theory]
    [InlineData("Read", "PATCH")]
    [InlineData("Delete", "DELETE")]
    public async Task AjaxUsesRealHandlerWithAntiforgeryAndReturnsRefreshContract(string handler, string method)
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        var token = Token(await client.GetStringAsync("/Notifications"));
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await Post(client, handler, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("refresh").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("message").GetString()));
        Assert.Contains(factory.Backend.Calls, call => call.Method == method && call.Bearer);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AjaxWithoutAntiforgeryNeverWrites()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await Post(client, "Read", "");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(factory.Backend.Calls, call => call.Method == "PATCH");
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task AjaxGuardReturnsStatusInsteadOfLoginHtml(int status)
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        var token = Token(await client.GetStringAsync("/Notifications"));
        factory.Backend.IdentityStatus = status;
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await Post(client, "Read", token);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain(factory.Backend.Calls, call => call.Method == "PATCH");
    }

    [Fact]
    public async Task AnonymousAjaxGetReturns401WithoutCallingUpstream()
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await client.GetAsync("/Notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.Backend.Calls);
    }

    [Theory]
    [InlineData(401, 401, false)]
    [InlineData(403, 403, false)]
    [InlineData(404, 404, true)]
    [InlineData(500, 502, false)]
    public async Task AjaxWriteFailureIsNotReportedAsSuccess(int upstream, int expected, bool refresh)
    {
        using var factory = new NotificationUiFactory();
        using var client = Client(factory);
        await Login(client);
        var token = Token(await client.GetStringAsync("/Notifications"));
        factory.Backend.WriteStatus = upstream;
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        using var response = await Post(client, "Delete", token);
        Assert.Equal(expected, (int)response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(refresh, json.TryGetProperty("refresh", out var value) && value.GetBoolean());
    }

    [Fact]
    public async Task EmptyLastPageIsClampedAndFetchedAgain()
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.PageOverride = new NotificationPage([], 2, 20, 20, 1);
        using var client = Client(factory);
        await Login(client);
        await client.GetStringAsync("/Notifications?pageIndex=2&pageSize=20&unreadOnly=true");
        Assert.Contains(factory.Backend.Calls, call => call.Path.Contains("pageIndex=1&pageSize=20&unreadOnly=true"));
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string handler, string token) =>
        client.PostAsync($"/Notifications?handler={handler}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["id"] = NotificationBackend.NotificationId.ToString(),
            ["PageIndex"] = "2", ["PageSize"] = "20", ["UnreadOnly"] = "true"
        }));
}
