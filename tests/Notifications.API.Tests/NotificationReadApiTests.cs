using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Services;
using Xunit;
using static Notifications.API.Tests.NotificationReadApiFactory;

namespace Notifications.API.Tests;

public class NotificationReadApiTests(NotificationReadApiFactory factory) : IClassFixture<NotificationReadApiFactory>
{
    [Theory]
    [InlineData("/api/notifications")]
    [InlineData("/api/notifications/unread-count")]
    public async Task Anonymous_IsUnauthorizedBeforeDatabaseAccess(string path)
    {
        factory.Commands.Reset();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(factory.Commands.Snapshot);
    }

    [Theory]
    [InlineData("/api/notifications", null)]
    [InlineData("/api/notifications", "invalid")]
    [InlineData("/api/notifications/unread-count", null)]
    [InlineData("/api/notifications/unread-count", "invalid")]
    public async Task MissingOrInvalidSubject_IsUnauthorized(string path, string? subject)
    {
        factory.Commands.Reset();
        using var client = factory.CreateClient();
        var claims = new List<Claim> { new(ClaimTypes.Role, "Learner") };
        if (subject is not null) claims.Add(new Claim("sub", subject));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(claims: claims));
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.Commands.Snapshot);
    }

    [Theory]
    [InlineData("Learner", 4, 5)]
    [InlineData("Moderator", 5, 4)]
    public async Task Audience_UsesOwnTargetAllAndMatchingRole(string role, int included, int excluded)
    {
        using var client = Client(LearnerId, role);
        using var json = await GetJson(client, "/api/notifications?pageSize=100");
        var ids = ItemIds(json);
        Assert.Contains(Id(1), ids);
        Assert.Contains(Id(3), ids);
        Assert.Contains(Id(included), ids);
        Assert.DoesNotContain(Id(excluded), ids);
        Assert.DoesNotContain(Id(2), ids);
        Assert.DoesNotContain(Id(12), ids);
        Assert.Equal(32, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public async Task MultipleRoles_AreAllConsidered()
    {
        using var client = Client(LearnerId, "Learner", "Moderator");
        using var json = await GetJson(client, "/api/notifications?pageSize=100");
        Assert.Contains(Id(4), ItemIds(json));
        Assert.Contains(Id(5), ItemIds(json));
        Assert.Equal(33, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Admin_SeesAllAndOwnTargetWithoutAccessToOthersPersonalMessages()
    {
        using var client = Client(AdminId, "Admin");
        using var json = await GetJson(client, "/api/notifications?pageSize=100");
        Assert.Equal(new[] { 3, 6, 7, 8, 9, 11, 12 }.Select(Id), ItemIds(json));
    }

    [Fact]
    public async Task State_IsScopedToCurrentUserAndMissingStateIsUnread()
    {
        using var client = Client(LearnerId, "Learner");
        using var json = await GetJson(client, "/api/notifications?pageSize=100");
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
        Assert.False(items[Id(1)].GetProperty("isRead").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[Id(1)].GetProperty("readAt").ValueKind);
        Assert.True(items[Id(6)].GetProperty("isRead").GetBoolean());
        Assert.Equal(Epoch, items[Id(6)].GetProperty("readAt").GetDateTime());
        Assert.False(items[Id(7)].GetProperty("isRead").GetBoolean());
        Assert.False(items[Id(9)].GetProperty("isRead").GetBoolean());
        Assert.Equal(JsonValueKind.Null, items[Id(9)].GetProperty("readAt").ValueKind);
        Assert.False(items.ContainsKey(Id(8)));
        Assert.False(items.ContainsKey(Id(11)));
    }

    [Fact]
    public async Task OtherUserState_DoesNotHideMessagesForCurrentUser()
    {
        using var client = Client(OtherUserId, "Learner");
        using var json = await GetJson(client, "/api/notifications?pageSize=100");
        var ids = ItemIds(json);
        Assert.Contains(Id(2), ids);
        Assert.Contains(Id(8), ids);
        Assert.Contains(Id(11), ids);
        Assert.DoesNotContain(Id(9), ids);
        Assert.DoesNotContain(Id(1), ids);
    }

    [Fact]
    public async Task UnreadOnly_ExcludesOwnReadAndDeletedStates()
    {
        using var client = Client(LearnerId, "Learner");
        using var json = await GetJson(client, "/api/notifications?unreadOnly=true&pageSize=100");
        var ids = ItemIds(json);
        Assert.Contains(Id(1), ids);
        Assert.Contains(Id(7), ids);
        Assert.Contains(Id(9), ids);
        Assert.DoesNotContain(Id(6), ids);
        Assert.DoesNotContain(Id(10), ids);
        Assert.DoesNotContain(Id(8), ids);
        Assert.DoesNotContain(Id(11), ids);
        Assert.All(json.RootElement.GetProperty("items").EnumerateArray(), item => Assert.False(item.GetProperty("isRead").GetBoolean()));
        Assert.Equal(30, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Defaults_AndStablePaginationUseCreatedAtDescendingThenIdAscending()
    {
        using var client = Client(LearnerId, "Learner");
        using var first = await GetJson(client, "/api/notifications");
        Assert.Equal(1, first.RootElement.GetProperty("pageIndex").GetInt32());
        Assert.Equal(20, first.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(32, first.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.RootElement.GetProperty("totalPages").GetInt32());
        Assert.Equal(Enumerable.Range(20, 20).Select(Id), ItemIds(first));
        using var second = await GetJson(client, "/api/notifications?pageIndex=2");
        Assert.Equal(Enumerable.Range(40, 5).Concat(new[] { 1, 3, 4, 6, 7, 9, 10 }).Select(Id), ItemIds(second));
        Assert.Empty(ItemIds(first).Intersect(ItemIds(second)));
    }

    [Theory]
    [InlineData("pageIndex=0", "pageIndex")]
    [InlineData("pageIndex=-1", "pageIndex")]
    [InlineData("pageIndex=no", "pageIndex")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=no", "pageSize")]
    [InlineData("unreadOnly=no", "unreadOnly")]
    public async Task InvalidQuery_ReturnsFieldValidationBeforeDatabaseAccess(string query, string field)
    {
        factory.Commands.Reset();
        using var client = Client(LearnerId, "Learner");
        using var response = await client.GetAsync($"/api/notifications?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Empty(factory.Commands.Snapshot);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public async Task OutOfRangePage_IsEmptyWithoutOverflow(int page)
    {
        using var client = Client(LearnerId, "Learner");
        using var json = await GetJson(client, $"/api/notifications?pageIndex={page}&pageSize=100");
        Assert.Empty(ItemIds(json));
        Assert.Equal(32, json.RootElement.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task NoVisibleMessages_ReturnsEmptyPageAndZeroUnreadCount()
    {
        using var client = Client(EmptyUserId);
        using var json = await GetJson(client, "/api/notifications");
        Assert.Empty(ItemIds(json));
        Assert.Equal(0, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("totalPages").GetInt32());
        using var count = await GetJson(client, "/api/notifications/unread-count");
        Assert.Equal(0, count.RootElement.GetProperty("unreadCount").GetInt32());
    }

    [Theory]
    [InlineData("Learner", 30)]
    [InlineData("Moderator", 30)]
    [InlineData("Admin", 29)]
    public async Task UnreadCount_MatchesFilteredListForSameUser(string role, int expected)
    {
        using var client = Client(LearnerId, role);
        using var count = await GetJson(client, "/api/notifications/unread-count");
        using var page = await GetJson(client, "/api/notifications?unreadOnly=true&pageSize=100");
        Assert.Equal(expected, count.RootElement.GetProperty("unreadCount").GetInt32());
        Assert.Equal(expected, page.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(expected, ItemIds(page).Length);
    }

    [Fact]
    public async Task QueryUserId_CannotOverrideAuthenticatedIdentity()
    {
        using var client = Client(LearnerId, "Learner");
        using var normal = await GetJson(client, "/api/notifications?pageSize=100");
        using var forged = await GetJson(client, $"/api/notifications?pageSize=100&userId={OtherUserId}");
        Assert.Equal(ItemIds(normal), ItemIds(forged));
        using var count = await GetJson(client, $"/api/notifications/unread-count?userId={OtherUserId}");
        Assert.Equal(30, count.RootElement.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task Response_ContainsOnlyContractFieldsAndUtcTimestamps()
    {
        using var client = Client(LearnerId, "Learner");
        using var json = await GetJson(client, "/api/notifications");
        var item = json.RootElement.GetProperty("items")[0];
        Assert.Equal(new[] { "id", "title", "content", "type", "createdAt", "isRead", "readAt" }, item.EnumerateObject().Select(property => property.Name));
        Assert.Equal("System", item.GetProperty("type").GetString());
        Assert.EndsWith("Z", item.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task RelationalQueries_UseDatabasePaginationAndConstantQueryCountWithoutWrites()
    {
        using var client = Client(LearnerId, "Learner");
        factory.Commands.Reset();
        using var page = await GetJson(client, "/api/notifications?pageIndex=2&pageSize=10");
        var sql = factory.Commands.Snapshot;
        Assert.Equal(2, sql.Length);
        Assert.Contains("COUNT(*)", sql[0]);
        Assert.Contains("LEFT JOIN", sql[1]);
        Assert.Contains("WHERE", sql[1]);
        Assert.Contains("ORDER BY", sql[1]);
        Assert.Contains("LIMIT", sql[1]);
        Assert.Contains("OFFSET", sql[1]);
        Assert.DoesNotContain("DataJson", sql[1]);
        Assert.Equal(Enumerable.Range(30, 10).Select(Id), ItemIds(page));
        Assert.All(sql, statement => Assert.StartsWith("SELECT", statement.TrimStart()));
        factory.Commands.Reset();
        using var count = await GetJson(client, "/api/notifications/unread-count");
        Assert.Contains("COUNT(*)", Assert.Single(factory.Commands.Snapshot));
    }

    [Fact]
    public async Task ReadQueries_DoNotTrackEntitiesOrCreateStates()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqliteReadDbContext>();
        var before = await db.UserNotificationStates.CountAsync();
        using var client = Client(LearnerId, "Learner");
        using var page = await GetJson(client, "/api/notifications");
        using var count = await GetJson(client, "/api/notifications/unread-count");
        Assert.Equal(before, await db.UserNotificationStates.CountAsync());
        await new NotificationQueryService(db).GetAsync(LearnerId, new[] { "Learner" }, 1, 100, false);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private HttpClient Client(Guid userId, params string[] roles)
    {
        var client = factory.CreateClient();
        var claims = new List<Claim> { new("sub", userId.ToString()) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.CreateToken(claims: claims));
        return client;
    }

    private static async Task<JsonDocument> GetJson(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static Guid[] ItemIds(JsonDocument json) => json.RootElement.GetProperty("items")
        .EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
}
