using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Billing.UI.Tests;

public class GrantUiTests
{
    private const string Page = "/Admin/Billing/Orders";
    private const string Target = "33333333-3333-3333-3333-333333333333";

    [Fact]
    public async Task LearnerIsForbiddenAndNeverCallsGrant()
    {
        using var factory = new BillingUiFactory();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.GetAsync(Page);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Backend.GrantBodies);
    }

    [Fact]
    public async Task AdminFormListsOnlyPaidPlansWithCustomClientValidation()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var html = await client.GetStringAsync(Page);
        Assert.Contains($"value=\"{BillingBackend.MonthId}\"", html);
        Assert.DoesNotContain($"value=\"{BillingBackend.FreeId}\"", html);
        Assert.Contains("data-val-billingguid=", html);
        Assert.Contains("data-val-range-max=\"365\"", html);
        Assert.Contains("data-val-length-max=\"500\"", html);
        Assert.Contains("billing-validation.js", html);
        Assert.True(Guid.TryParse(RequestId(html), out _));
    }

    [Fact]
    public async Task ValidGrantSendsOnlyWhitelistedFieldsAndRedirects()
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var form = await Form(client, Target);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var json = JsonDocument.Parse(Assert.Single(factory.Backend.GrantBodies));
        var names = json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "durationDays", "note", "planId", "requestId", "userId" }, names);
        Assert.Equal(form["Input.RequestId"], json.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(Target, json.RootElement.GetProperty("userId").GetString());
        Assert.Equal("note", json.RootElement.GetProperty("note").GetString());
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location!.OriginalString));
        Assert.Contains("Đã cấp gói", html);
        Assert.Contains("05/12/2026 07:00", html);
        Assert.NotEqual(form["Input.RequestId"], RequestId(html)); // a fresh form gets a new key
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidUserIdIsRejectedBeforeCallingBilling(string userId)
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client, userId)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        Assert.Empty(factory.Backend.GrantBodies);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    public async Task DurationOutOfRangeIsRejectedBeforeCallingBilling(string days)
    {
        using var factory = Admin();
        using var client = factory.Browser();
        await factory.Login(client);
        var form = await Form(client, Target);
        form["Input.DurationDays"] = days;
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(form));
        Assert.Contains("Số ngày phải từ 1 đến 365", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Empty(factory.Backend.GrantBodies);
    }

    [Theory]
    [InlineData(400, "Dữ liệu chưa được chấp nhận")]
    [InlineData(404, "Gói hoặc người dùng không tồn tại")]
    [InlineData(503, "Chưa thể xác minh người dùng")]
    [InlineData(409, "vừa thay đổi")]
    [InlineData(422, "Chỉ có thể cấp gói trả phí")]
    [InlineData(500, "Không thể cấp gói lúc này")]
    public async Task BackendFailuresKeepInputAndRequestIdWithoutLeakingDiagnostics(int upstream, string expected)
    {
        using var factory = Admin();
        factory.Backend.GrantStatus = upstream;
        using var client = factory.Browser();
        await factory.Login(client);
        var form = await Form(client, Target);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(form));
        var raw = await response.Content.ReadAsStringAsync();
        var html = WebUtility.HtmlDecode(raw);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, html);
        Assert.Contains($"value=\"{Target}\"", raw);
        Assert.Equal(form["Input.RequestId"], RequestId(raw));
        Assert.DoesNotContain("internal-diagnostic", html);
    }

    [Fact]
    public async Task PlansUnavailableHidesForm()
    {
        using var factory = Admin();
        factory.Backend.PlansStatus = 503;
        using var client = factory.Browser();
        await factory.Login(client);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync(Page));
        Assert.Contains("Chưa thể tải danh sách gói", html);
        Assert.DoesNotContain("id=\"billing-grant\"", html);
    }

    [Fact]
    public async Task BillingUnauthorizedRedirectsToLogin()
    {
        using var factory = Admin();
        factory.Backend.GrantStatus = 401;
        using var client = factory.Browser();
        await factory.Login(client);
        using var response = await client.PostAsync(Page, new FormUrlEncodedContent(await Form(client, Target)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    private static BillingUiFactory Admin()
    {
        var factory = new BillingUiFactory();
        factory.Backend.Roles = ["Admin"];
        return factory;
    }

    private static async Task<Dictionary<string, string>> Form(HttpClient client, string userId)
    {
        var html = await client.GetStringAsync(Page);
        return new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = BillingUiFactory.Antiforgery(html),
            ["Input.RequestId"] = RequestId(html), ["Input.UserId"] = userId,
            ["Input.PlanId"] = BillingBackend.MonthId.ToString(), ["Input.DurationDays"] = "", ["Input.Note"] = "note"
        };
    }

    private static string RequestId(string html) =>
        Regex.Match(html, "name=\"Input.RequestId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value is { Length: > 0 } value
            ? value
            : Regex.Match(html, "value=\"([^\"]+)\"[^>]*name=\"Input.RequestId\"").Groups[1].Value;
}
