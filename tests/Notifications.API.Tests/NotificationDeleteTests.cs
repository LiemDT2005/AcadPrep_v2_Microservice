using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Entities;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;
using static Notifications.API.Tests.NotificationMarkReadApiTests;

namespace Notifications.API.Tests;

public class NotificationDeleteTests
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("invalid")]
    [InlineData("missing-sub")]
    [InlineData("invalid-sub")]
    public async Task AuthenticationRejectsInvalidIdentity(string mode)
    {
        using var factory = await CreateFactory();
        using var client = factory.CreateClient();
        if (mode != "anonymous")
        {
            var claims = mode == "invalid-sub" ? new[] { new Claim("sub", "invalid") } : Array.Empty<Claim>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                mode == "invalid" ? "bad-token" : factory.CreateToken(claims: claims));
        }
        using var response = await client.DeleteAsync($"/api/notifications/{Id(1)}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await factory.StateAsync(1));
    }

    [Theory]
    [InlineData("bad-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidIdIsFieldValidationProblem(string id)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var response = await client.DeleteAsync($"/api/notifications/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("id", out _));
    }

    [Theory]
    [InlineData(2, "Learner")]
    [InlineData(5, "Learner")]
    [InlineData(2, "Admin")]
    [InlineData(999, "Learner")]
    public async Task OutsideAudienceOrMissingIs404(int number, string role)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory, role);
        using var response = await client.DeleteAsync($"/api/notifications/{Id(number)}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await factory.StateAsync(number));
    }

    [Theory]
    [InlineData(1, "Learner")]
    [InlineData(3, "Admin")]
    [InlineData(4, "Learner")]
    [InlineData(5, "Moderator")]
    [InlineData(5, "Learner,Moderator")]
    public async Task FirstDeleteCreatesOnlyOwnStateAndKeepsNotification(int number, string roles)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory, roles.Split(','));
        using var response = await client.DeleteAsync($"/api/notifications/{Id(number)}?userId={OtherUserId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
        var state = (await factory.StateAsync(number))!;
        Assert.True(state.IsDeleted);
        Assert.False(state.IsRead);
        Assert.Null(state.ReadAt);
        Assert.Equal(DateTimeKind.Utc, state.DeletedAt!.Value.Kind);
        Assert.Null(await factory.StateAsync(number, OtherUserId));
        Assert.Equal(1, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.Id == Id(number))));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    public async Task ExistingStatePreservesReadFieldsAndRepeatedDeleteTimestamp(int number)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        var before = (await factory.StateAsync(number))!;
        using var response = await client.DeleteAsync($"/api/notifications/{Id(number)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var first = (await factory.StateAsync(number))!;
        Assert.True(first.IsDeleted);
        Assert.Equal(before.IsRead, first.IsRead);
        Assert.Equal(before.ReadAt, first.ReadAt);
        if (before.IsDeleted) Assert.Equal(before.DeletedAt, first.DeletedAt);
        using var repeated = await client.DeleteAsync($"/api/notifications/{Id(number)}");
        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(first.DeletedAt, (await factory.StateAsync(number))!.DeletedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingDeletedAtIsNeverOverwrittenIncludingNullAlreadyDeleted(bool alreadyDeleted)
    {
        using var factory = await CreateFactory();
        await factory.WithDbAsync(db => db.UserNotificationStates.Where(s => s.NotificationId == Id(7))
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.IsDeleted, alreadyDeleted)
                .SetProperty(s => s.DeletedAt, alreadyDeleted ? (DateTime?)null : Epoch)));
        using var client = Client(factory);
        using var response = await client.DeleteAsync($"/api/notifications/{Id(7)}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(alreadyDeleted ? (DateTime?)null : Epoch, (await factory.StateAsync(7))!.DeletedAt);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(6, 0)]
    public async Task DeleteUpdatesGetAndCountAndBlocksPatch(int number, int decrement)
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        var stateCount = await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync());
        var before = await Count(client);
        Assert.Equal(stateCount, await factory.WithDbAsync(db => db.UserNotificationStates.CountAsync()));
        using var deleted = await client.DeleteAsync($"/api/notifications/{Id(number)}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(before - decrement, await Count(client));
        foreach (var suffix in new[] { "", "&unreadOnly=true" })
        {
            using var response = await client.GetAsync($"/api/notifications?pageSize=100{suffix}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.DoesNotContain(json.RootElement.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("id").GetGuid() == Id(number));
        }
        using var patch = await client.PatchAsync($"/api/notifications/{Id(number)}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        using var repeated = await client.DeleteAsync($"/api/notifications/{Id(number)}");
        Assert.Equal(before - decrement, await Count(client));
    }

    [Fact]
    public async Task BroadcastAndOtherUsersReadStateRemainAvailable()
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        var other = (await factory.StateAsync(9, OtherUserId))!;
        using var deleted = await client.DeleteAsync($"/api/notifications/{Id(9)}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var after = (await factory.StateAsync(9, OtherUserId))!;
        Assert.Equal(other.ReadAt, after.ReadAt);
        Assert.True(after.IsRead);
        Assert.False(after.IsDeleted);
        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.CreateToken(claims: [new Claim("sub", OtherUserId.ToString()), new Claim(ClaimTypes.Role, "Learner")]));
        using var list = await otherClient.GetAsync("/api/notifications?pageSize=100");
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Contains(json.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == Id(9));
    }

    [Fact]
    public async Task DeleteAfterReadKeepsCommittedReadAt()
    {
        using var factory = await CreateFactory();
        using var client = Client(factory);
        using var read = await client.PatchAsync($"/api/notifications/{Id(1)}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        var readAt = (await factory.StateAsync(1))!.ReadAt;
        using var deleted = await client.DeleteAsync($"/api/notifications/{Id(1)}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var state = (await factory.StateAsync(1))!;
        Assert.True(state.IsRead);
        Assert.True(state.IsDeleted);
        Assert.Equal(readAt, state.ReadAt);
    }

    private static async Task<int> Count(HttpClient client)
    {
        using var response = await client.GetAsync("/api/notifications/unread-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("unreadCount").GetInt32();
    }
}
