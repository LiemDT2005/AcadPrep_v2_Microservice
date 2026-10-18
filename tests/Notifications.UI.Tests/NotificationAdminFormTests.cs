using System.Net;
using System.Text.Json;
using Xunit;
using static Notifications.UI.Tests.NotificationRenderingTests;

namespace Notifications.UI.Tests;

public class NotificationAdminFormTests
{
    [Theory]
    [InlineData("All", "")]
    [InlineData("User", "33333333-3333-3333-3333-333333333333")]
    public async Task ValidFormUsesOnlySupportedFieldsAndRedirectsAfterCreate(string audience, string target)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin", "Learner"];
        using var client = Client(factory);
        await Login(client);
        var form = await Form(client, audience, target);
        using var response = await client.PostAsync("/Admin/Notifications/Create", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var json = JsonDocument.Parse(factory.Backend.CreatedBody!);
        Assert.Equal("Title", json.RootElement.GetProperty("title").GetString());
        if (audience == "User")
            Assert.Equal(target, json.RootElement.GetProperty("targetUserId").GetString());
        else
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("targetUserId").ValueKind);
        Assert.Equal(4, json.RootElement.EnumerateObject().Count());
        Assert.False(json.RootElement.TryGetProperty("createdBy", out _));
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location));
        Assert.Contains("Đã gửi thông báo thành công", html);
        Assert.Single(factory.Backend.Calls, call => call.Path == "/notification/api/admin/notifications");
    }

    [Theory]
    [InlineData("User", "")]
    [InlineData("User", "bad")]
    [InlineData("User", "00000000-0000-0000-0000-000000000000")]
    [InlineData("All", "33333333-3333-3333-3333-333333333333")]
    [InlineData("Moderator", "33333333-3333-3333-3333-333333333333")]
    public async Task InvalidConditionalTargetDoesNotCallCreate(string audience, string target)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin"];
        using var client = Client(factory);
        await Login(client);
        using var response = await client.PostAsync("/Admin/Notifications/Create",
            new FormUrlEncodedContent(await Form(client, audience, target)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Input.TargetUserId", await response.Content.ReadAsStringAsync());
        Assert.Null(factory.Backend.CreatedBody);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(422)]
    public async Task BackendErrorsMapToFieldAndKeepInputsWithoutLeakingDiagnostics(int upstream)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin"];
        factory.Backend.CreateStatus = upstream;
        using var client = Client(factory);
        await Login(client);
        using var response = await client.PostAsync("/Admin/Notifications/Create",
            new FormUrlEncodedContent(await Form(client, "All", "")));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-valmsg-for=\"Input.TargetUserId\"", html);
        Assert.Contains("field-validation-error", html);
        Assert.Contains("value=\"  Title  \"", html);
        Assert.DoesNotContain("internal-diagnostic", html);
    }

    [Theory]
    [InlineData(401, 302)]
    [InlineData(403, 403)]
    public async Task BackendAuthorizationFailureIsNotValidation(int upstream, int expected)
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin"];
        factory.Backend.CreateStatus = upstream;
        using var client = Client(factory);
        await Login(client);
        using var response = await client.PostAsync("/Admin/Notifications/Create",
            new FormUrlEncodedContent(await Form(client, "All", "")));
        Assert.Equal(expected, (int)response.StatusCode);
        if (upstream == 401) Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AdminPostWithoutAntiforgeryIsRejected()
    {
        using var factory = new NotificationUiFactory();
        factory.Backend.Roles = ["Admin"];
        using var client = Client(factory);
        await Login(client);
        using var response = await client.PostAsync("/Admin/Notifications/Create",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Title"] = "Title" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.Backend.CreatedBody);
    }

    private static async Task<Dictionary<string, string>> Form(HttpClient client, string audience, string target) => new()
    {
        ["__RequestVerificationToken"] = Token(await client.GetStringAsync("/Admin/Notifications/Create")),
        ["Input.Title"] = "  Title  ", ["Input.Content"] = "Content", ["Input.Audience"] = audience,
        ["Input.TargetUserId"] = target
    };
}
