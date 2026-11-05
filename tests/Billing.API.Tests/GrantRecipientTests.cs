using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static Billing.API.Tests.BillingTestData;

namespace Billing.API.Tests;

public class GrantRecipientTests
{
    private const string Url = "/api/admin/subscriptions/grant";

    [Fact]
    public async Task RecipientIsVerifiedAtIdentityWithTheAdminTokenAndEmailIsSnapshotted()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var user = Guid.NewGuid();
        factory.Identity.Users[user] = (200, ["Learner"], "Active", "learner@example.test");
        using var client = factory.Client("Admin");
        using var response = await client.PostAsJsonAsync(Url, GrantTests.Body(user));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var call = Assert.Single(factory.Identity.Calls);
        Assert.Equal($"/api/users/{user:D}", call.Path);
        Assert.Equal(client.DefaultRequestHeaders.Authorization!.Parameter, call.Token);
        await factory.WithDb(async db =>
            Assert.Equal("learner@example.test", (await db.Subscriptions.SingleAsync()).UserEmail));
    }

    [Theory]
    [InlineData(404, "Learner", "Active", 404)]
    [InlineData(200, "Admin", "Active", 422)]
    [InlineData(200, "Moderator", "Active", 422)]
    [InlineData(200, "Learner,Moderator", "Active", 422)]
    [InlineData(200, "Learner", "Banned", 422)]
    public async Task OnlyAnExistingActiveLearnerCanReceiveAPlan(int identityStatus, string roles, string status, int expected)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var user = Guid.NewGuid();
        factory.Identity.Users[user] = (identityStatus, roles.Split(','), status, null);
        using var client = factory.Client("Admin");
        using var response = await client.PostAsJsonAsync(Url, GrantTests.Body(user));
        Assert.Equal(expected, (int)response.StatusCode);
        await factory.WithDb(async db =>
        {
            Assert.Equal(0, await db.Subscriptions.CountAsync());
            Assert.Equal(0, await db.SubscriptionGrants.CountAsync());
        });
    }

    [Theory]
    [InlineData(500)]
    [InlineData(401)]
    [InlineData(-1)] // network failure
    public async Task IdentityFailureFailsClosedWith503(int failure)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        if (failure < 0) factory.Identity.Throw = true; else factory.Identity.ForceStatus = failure;
        using var client = factory.Client("Admin");
        using var response = await client.PostAsJsonAsync(Url, GrantTests.Body(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await factory.WithDb(async db => Assert.Equal(0, await db.Subscriptions.CountAsync()));
    }

    [Fact]
    public async Task ReplayDoesNotCallIdentityAgain()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(db => SeedPlans(db));
        var body = GrantTests.Body(Guid.NewGuid());
        using var client = factory.Client("Admin");
        using var first = await client.PostAsJsonAsync(Url, body);
        factory.Identity.Throw = true; // a replay must still succeed from the stored grant
        using var second = await client.PostAsJsonAsync(Url, body);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Single(factory.Identity.Calls, call => call.Path.StartsWith("/api/users/"));
    }
}
