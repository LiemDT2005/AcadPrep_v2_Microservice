using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;
using static Notifications.API.Tests.NotificationMarkReadApiTests;

namespace Notifications.API.Tests;

// Personal notifications verify the recipient through Identity's existing Admin API (test fake here).
public class NotificationRecipientTests
{
    private const string Route = "/api/admin/notifications";

    [Fact]
    public async Task ExistingRecipientIsCheckedWithTheAdminTokenAndStoredAnyStatus()
    {
        using var factory = await CreateFactory(); // the fake reports the user as Banned: still allowed
        using var client = Client(factory, "Admin");
        using var response = await client.PostAsJsonAsync(Route, Request("User", OtherUserId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var call = Assert.Single(factory.Identity.Calls);
        Assert.Equal($"/api/users/{OtherUserId:D}", call.Path);
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.Parameter, call.Token);
        Assert.Equal(11, await factory.WithDbAsync(db => db.Notifications.CountAsync()));
    }

    [Theory]
    [InlineData("All")]
    [InlineData("Learner")]
    [InlineData("Moderator")]
    public async Task BroadcastsNeverCallIdentity(string audience)
    {
        using var factory = await CreateFactory();
        factory.Identity.Throw = true;
        using var client = Client(factory, "Admin");
        using var response = await client.PostAsJsonAsync(Route, Request(audience, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(factory.Identity.Calls);
    }

    [Fact]
    public async Task MissingRecipientIs422OnTargetUserIdWithoutWriting()
    {
        using var factory = await CreateFactory();
        var missing = Guid.NewGuid();
        factory.Identity.Missing.Add(missing);
        using var client = Client(factory, "Admin");
        using var response = await client.PostAsJsonAsync(Route, Request("User", missing));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(json.RootElement.GetProperty("errors").EnumerateObject(),
            field => field.Name.Equals("targetUserId", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(10, await factory.WithDbAsync(db => db.Notifications.CountAsync()));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(401)]
    [InlineData(-1)] // network failure
    public async Task IdentityFailureFailsClosedWith503(int failure)
    {
        using var factory = await CreateFactory();
        if (failure < 0) factory.Identity.Throw = true; else factory.Identity.ForceStatus = failure;
        using var client = Client(factory, "Admin");
        using var response = await client.PostAsJsonAsync(Route, Request("User", OtherUserId));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(10, await factory.WithDbAsync(db => db.Notifications.CountAsync()));
    }

    private static Dictionary<string, object?> Request(string audience, Guid? target) => new()
    {
        ["title"] = "Title", ["content"] = "Content", ["audience"] = audience, ["targetUserId"] = target
    };
}
