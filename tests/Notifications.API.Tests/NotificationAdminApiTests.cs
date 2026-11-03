using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Notifications.Application.Interfaces;
using Notifications.Domain.Entities;
using Notifications.Domain.Enums;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;
using static Notifications.API.Tests.NotificationMarkReadApiTests;

namespace Notifications.API.Tests;

public class NotificationAdminApiTests
{
    private const string Route = "/api/admin/notifications";

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("invalid", 401)]
    [InlineData("missing-sub", 401)]
    [InlineData("invalid-sub", 401)]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    public async Task AuthenticationAuthorizationPrecedeCreation(string mode, int status)
    {
        using var factory = await CreateFactory();
        using var client = factory.CreateClient();
        if (mode != "anonymous")
        {
            var claims = new List<Claim> { new(ClaimTypes.Role, mode.EndsWith("sub") ? "Admin" : mode) };
            if (mode != "missing-sub") claims.Add(new("sub", mode == "invalid-sub" ? "bad-id" : LearnerId.ToString()));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                mode == "invalid" ? "bad-token" : factory.CreateToken(claims: claims));
        }
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(status, (int)response.StatusCode);
        await AssertUnchanged(factory);
    }

    [Theory]
    [InlineData("User", "Admin")]
    [InlineData("All", "Admin")]
    [InlineData("Learner", "Admin")]
    [InlineData("Moderator", "Admin")]
    [InlineData("All", "Admin,Learner")]
    public async Task CreatesOneSystemNotificationWithServerOwnedFields(string audience, string roles)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory, roles.Split(','));
        var target = audience == "User" ? OtherUserId : (Guid?)null;
        using var response = await client.PostAsJsonAsync(Route, Request(audience, target));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // No GET-by-id route exists; the response must not fabricate a Location header.
        Assert.Null(response.Headers.Location);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = result.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        var stored = await factory.WithDbAsync(db => db.Notifications.AsNoTracking().SingleAsync(n => n.Id == id));
        Assert.Equal("Title", stored.Title);
        Assert.Equal(" Content <b>as text</b> ", stored.Content);
        Assert.Equal(NotificationType.System, stored.Type);
        Assert.Equal(Enum.Parse<NotificationAudience>(audience), stored.Audience);
        Assert.Equal(target, stored.TargetUserId);
        Assert.Equal(LearnerId, stored.CreatedBy);
        Assert.Equal(DateTimeKind.Utc, stored.CreatedAt.Kind);
        Assert.True(stored.CreatedAt > Epoch);
        Assert.Null(stored.SourceEventId);
        Assert.Null(stored.DataJson);
        Assert.Equal(11, await factory.WithDbAsync(db => db.Notifications.CountAsync()));
        Assert.Equal(5, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync()));
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        foreach (var value in new object?[] { null, "", "   ", new string('a', 201), 123 })
            yield return Case("title", value, 400, "title");
        foreach (var value in new object?[] { null, "", " \t ", new string('b', 2001), 123 })
            yield return Case("content", value, 400, "content");
        foreach (var value in new object?[] { null, "", "Admin", "Bad", "0", "all", 0, 99 })
            yield return Case("audience", value, 400, "audience");
        yield return Case("targetUserId", "invalid-guid", 400, "targetUserId");
        yield return Case("targetUserId", 123, 400, "targetUserId");
        foreach (var audience in new[] { "All", "Learner", "Moderator" })
        {
            var request = Request(audience, OtherUserId);
            yield return [JsonSerializer.Serialize(request), 422, "targetUserId"];
        }
        yield return [JsonSerializer.Serialize(Request("User")), 422, "targetUserId"];
        yield return [JsonSerializer.Serialize(Request("User", Guid.Empty)), 422, "targetUserId"];
        foreach (var field in new[] { "title", "content", "audience" })
        {
            var request = Request();
            request.Remove(field);
            yield return [JsonSerializer.Serialize(request), 400, field];
        }
        foreach (var field in new[] { "createdBy", "type", "sourceEventId", "dataJson" })
            yield return Case(field, field == "type" ? "ExamResult" : "client-controlled", 400, "$");
        yield return ["{", 400, "$"];
        yield return ["null", 400, ""];
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestsReturnFieldErrorsWithoutWrites(string json, int status, string field)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory, "Admin");
        using var response = await client.PostAsync(Route, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fields = body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Contains(fields, name => name.Contains(field, StringComparison.OrdinalIgnoreCase));
        await AssertUnchanged(factory);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(200, 2000)]
    public async Task BoundaryLengthsValidatedAfterTitleTrim(int titleLength, int contentLength)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory, "Admin");
        var request = Request();
        request["title"] = "  " + new string('a', titleLength) + "  ";
        request["content"] = new string('b', contentLength);
        using var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var item = await factory.WithDbAsync(db => db.Notifications.AsNoTracking().SingleAsync(n => n.Id == id));
        Assert.Equal(titleLength, item.Title.Length);
        Assert.Equal(contentLength, item.Content.Length);
    }

    [Theory]
    [InlineData("User", "Learner", true)]
    [InlineData("User", "Moderator", false)]
    [InlineData("All", "Learner", true)]
    [InlineData("All", "Moderator", true)]
    [InlineData("All", "Admin", true)]
    [InlineData("Learner", "Learner", true)]
    [InlineData("Learner", "Moderator", false)]
    [InlineData("Moderator", "Moderator", true)]
    [InlineData("Moderator", "Learner", false)]
    public async Task CreatedNotificationIntegratesWithListCountReadDelete(string audience, string role, bool visible)
    {
        using var factory = await CreateFactory();
        using var admin = Client(factory, "Admin");
        var userId = audience == "User" && !visible ? OtherUserId : LearnerId;
        using var user = factory.CreateClient();
        user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(claims:
            [new Claim("sub", userId.ToString()), new Claim(ClaimTypes.Role, role)]));
        var before = await Count(user);
        using var created = await admin.PostAsJsonAsync(Route, Request(audience, audience == "User" ? LearnerId : null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(before + (visible ? 1 : 0), await Count(user));
        using var list = await user.GetAsync("/api/notifications?pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var body = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(visible, body.RootElement.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == id));
        Assert.Equal(5, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync()));
        using var read = await user.PatchAsync($"/api/notifications/{id}/read", null);
        Assert.Equal(visible ? HttpStatusCode.NoContent : HttpStatusCode.NotFound, read.StatusCode);
        using var deleted = await user.DeleteAsync($"/api/notifications/{id}");
        Assert.Equal(visible ? HttpStatusCode.NoContent : HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(before, await Count(user));
    }

    [Fact]
    public async Task UnexpectedPersistenceErrorIsSanitized()
    {
        using var factory = new NotificationMarkReadApiFactory();
        using var overridden = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationWriter>();
            services.AddScoped<INotificationWriter, FailingWriter>();
        }));
        using var client = overridden.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.CreateToken(claims: [new Claim("sub", LearnerId.ToString()), new Claim(ClaimTypes.Role, "Admin")]));
        using var response = await client.PostAsJsonAsync(Route, Request());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-diagnostic", body);
        Assert.DoesNotContain("DbUpdateException", body);
    }

    private static Dictionary<string, object?> Request(string audience = "All", Guid? target = null) => new()
    {
        ["title"] = "  Title  ", ["content"] = " Content <b>as text</b> ",
        ["audience"] = audience, ["targetUserId"] = target
    };

    private static object[] Case(string field, object? value, int status, string errorField)
    {
        var request = Request();
        request[field] = value;
        return [JsonSerializer.Serialize(request), status, errorField];
    }

    private static async Task AssertUnchanged(NotificationMarkReadApiFactory factory)
    {
        Assert.Equal(10, await factory.WithDbAsync(db => db.Notifications.CountAsync()));
        Assert.Equal(5, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync()));
    }

    private static async Task<int> Count(HttpClient client)
    {
        using var response = await client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unreadCount").GetInt32();
    }

    private sealed class FailingWriter : INotificationWriter
    {
        public Task AddAsync(Notification notification, CancellationToken ct = default) =>
            throw new DbUpdateException("private-diagnostic");
    }
}
