using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;

namespace Notifications.API.Tests;

public class NotificationMarkReadApiTests
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("invalid")]
    [InlineData("missing-sub")]
    [InlineData("invalid-sub")]
    public async Task InvalidAuthentication_IsUnauthorized(string mode)
    {
        using var factory = await CreateFactory();
        using var client = factory.CreateClient();
        if (mode != "anonymous") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            mode == "invalid" ? "invalid-jwt" : factory.CreateToken(claims: mode == "missing-sub"
                ? [new Claim(ClaimTypes.Role, "Learner")] : [new Claim("sub", "invalid")]));
        using var response = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await factory.StateAsync(1));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("123")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidId_Returns400FieldProblemDetails(string id)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{id}/read", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(8)]
    public async Task MissingOtherUsersWrongAudienceOrDeleted_ReturnsUniform404(int number)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(number)}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Notification not found.", json.RootElement.GetProperty("title").GetString());
        if (number == 8)
        {
            var state = Assert.IsType<UserNotificationState>(await factory.StateAsync(8));
            Assert.True(state.IsDeleted);
            Assert.False(state.IsRead);
            Assert.Null(state.ReadAt);
            Assert.Equal(Epoch, state.DeletedAt);
        }
        else Assert.Null(await factory.StateAsync(number));
    }

    [Theory]
    [InlineData(1, "Learner")]
    [InlineData(3, "Learner")]
    [InlineData(4, "Learner")]
    [InlineData(5, "Moderator")]
    [InlineData(5, "Learner,Moderator")]
    [InlineData(3, "Admin")]
    public async Task FirstRead_CreatesExactlyOneReadStateWithUtcTimestamp(int number, string roles)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory, roles.Split(','));
        using var response = await client.PatchAsync($"/api/notifications/{Id(number)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var state = Assert.IsType<UserNotificationState>(await factory.StateAsync(number));
        Assert.True(state.IsRead);
        Assert.False(state.IsDeleted);
        Assert.Null(state.DeletedAt);
        Assert.NotNull(state.ReadAt);
        Assert.Equal(DateTimeKind.Utc, state.ReadAt.Value.Kind);
        Assert.Equal(1, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync(
            row => row.NotificationId == Id(number) && row.UserId == LearnerId)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public async Task RepeatedRead_PreservesReadAtAndCreatesNoDuplicate(int number)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var first = await client.PatchAsync($"/api/notifications/{Id(number)}/read", null);
        var before = Assert.IsType<UserNotificationState>(await factory.StateAsync(number));
        using var second = await client.PatchAsync($"/api/notifications/{Id(number)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(before.ReadAt, (await factory.StateAsync(number))!.ReadAt);
        Assert.Equal(1, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync(
            row => row.NotificationId == Id(number) && row.UserId == LearnerId)));
    }

    [Theory]
    [InlineData(7, false)]
    [InlineData(10, true)]
    public async Task ExistingUnread_TransitionsWithoutOverwritingExistingReadAt(int number, bool hasTimestamp)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(number)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var state = (await factory.StateAsync(number))!;
        Assert.True(state.IsRead);
        Assert.NotNull(state.ReadAt);
        if (hasTimestamp) Assert.Equal(Epoch, state.ReadAt);
        else Assert.True(state.ReadAt > Epoch);
        Assert.False(state.IsDeleted);
        Assert.Null(state.DeletedAt);
    }

    [Fact]
    public async Task AlreadyReadWithNullTimestamp_IsNotRepairedOrOtherwiseChanged()
    {
        using var factory = await CreateFactory();
        await factory.WithDbAsync(db => db.UserNotificationStates.Where(row => row.NotificationId == Id(6))
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ReadAt, (DateTime?)null)));
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(6)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await factory.StateAsync(6))!.ReadAt);
    }

    [Fact]
    public async Task OtherUsersStateAndRequestSuppliedUserId_CannotChangeIdentity()
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(9)}/read?userId={OtherUserId}", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True((await factory.StateAsync(9))!.IsRead);
        Assert.Equal(Epoch, (await factory.StateAsync(9, OtherUserId))!.ReadAt);
        using var forbidden = await client.PatchAsync($"/api/notifications/{Id(2)}/read?userId={OtherUserId}", null);
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task Read_UpdatesGetStateAndDecrementsUnreadCountExactlyOnce()
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        var before = await UnreadCount(client);
        using var first = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(before - 1, await UnreadCount(client));
        using var repeat = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(before - 1, await UnreadCount(client));
        using var unread = await GetJson(client, "/api/notifications?unreadOnly=true&pageSize=100");
        Assert.DoesNotContain(unread.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == Id(1));
        using var all = await GetJson(client, "/api/notifications?pageSize=100");
        Assert.True(all.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == Id(1)).GetProperty("isRead").GetBoolean());
    }

    [Fact]
    public async Task UnexpectedDatabaseError_Returns500WithoutInternalDetails()
    {
        using var factory = await CreateFactory();
        factory.Race.BeforeInsert = (_, _) => throw new DbUpdateException("Internal database diagnostic.",
            new InvalidOperationException("Internal storage detail."));
        using var client = Client(factory);
        using var response = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("diagnostic", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.Equal(0, factory.Race.DuplicateConflicts);
    }

    internal static async Task<NotificationMarkReadApiFactory> CreateFactory()
    {
        var factory = new NotificationMarkReadApiFactory();
        await factory.InitializeAsync();
        return factory;
    }

    internal static HttpClient Client(NotificationMarkReadApiFactory factory, params string[] roles)
    {
        var client = factory.CreateClient();
        var claims = new List<Claim> { new("sub", LearnerId.ToString()) };
        claims.AddRange((roles.Length == 0 ? ["Learner"] : roles).Select(role => new Claim(ClaimTypes.Role, role)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(claims: claims));
        return client;
    }

    private static async Task<JsonDocument> GetJson(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<int> UnreadCount(HttpClient client)
    {
        using var json = await GetJson(client, "/api/notifications/unread-count");
        return json.RootElement.GetProperty("unreadCount").GetInt32();
    }
}
