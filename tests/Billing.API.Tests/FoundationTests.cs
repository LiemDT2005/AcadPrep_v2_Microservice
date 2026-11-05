using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.API;
using Billing.API.Data;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Billing.API.Tests;

public class FoundationTests
{
    [Fact]
    public async Task HealthDoesNotRequireDatabaseSchema()
    {
        using var factory = new BillingApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var swagger = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);
        Assert.Equal("application/problem+json", swagger.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void MissingDatabaseTargetFailsWithoutConnecting()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var provider = new ServiceCollection().AddBilling(configuration).BuildServiceProvider();
        var error = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<BillingDbContext>());
        Assert.Equal("ConnectionStrings:Default is required.", error.Message);
    }

    [Fact]
    public void SqlServerModelIsOwnedByBillingWithoutSeedOrIdentityTables()
    {
        using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=BillingModelTests;Integrated Security=True").Options);
        Assert.Equal(new[] { "Orders", "OutboxMessages", "PaymentLogs", "PlanLimits", "Plans", "SubscriptionGrants", "SubscriptionReminderLogs", "Subscriptions" }, db.Model.GetEntityTypes().Select(e => e.GetTableName()).Order().ToArray());
        Assert.Equal(18, db.Model.FindEntityType(typeof(Plan))!.FindProperty(nameof(Plan.Price))!.GetPrecision());
        Assert.Empty(db.GetService<IDesignTimeModel>().Model.GetEntityTypes().SelectMany(e => e.GetSeedData()));
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(new[] { "20261105010917_InitialBillingPlans", "20261105054128_AddSubscriptions", "20261105094611_AddOrders", "20261105101705_SeedPlans", "20261105105155_AddSubscriptionLifecycle", "20261105115016_AddPayments" }, db.Database.GetMigrations().ToArray());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("key")]
    [InlineData("subject")]
    public async Task InvalidAuthenticationIsUnauthorized(string mode)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.CreateClient();
        if (mode != "missing") client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            factory.Token(subject: mode == "subject" ? "invalid" : null, issuer: mode == "issuer" ? "other" : null,
                audience: mode == "audience" ? "other" : null, expired: mode == "expired", wrongKey: mode == "key"));
        using var response = await client.GetAsync("/test/auth");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("Learner", 403)]
    [InlineData("Moderator", 403)]
    [InlineData("Admin", 200)]
    public async Task RoleClaimMatchesIdentity(string role, int expected)
    {
        using var factory = new BillingApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token(role));
        using var response = await client.GetAsync("/test/auth/admin");
        Assert.Equal(expected, (int)response.StatusCode);
    }

    [Fact]
    public async Task EmptyPricingDoesNotInventPlansOrWriteRows()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(_ => Task.CompletedTask);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        await factory.WithDb(async db => Assert.Equal(0, await db.Plans.CountAsync()));
    }

    [Fact]
    public async Task IncompleteLimitsReturn503InsteadOfImplicitUnlimited()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(async db =>
        {
            db.Plans.Add(new Plan { Code = "FREE", Name = "Test-only plan", IsActive = true });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ActivePricingUsesPersistedValuesAndExcludesInactivePlans()
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(async db =>
        {
            var plan = new Plan { Code = "FREE", Name = "Test-only free", IsActive = true };
            plan.Limits = new[] { "FullTest", "Practice", "Vocab" }.Select(feature => new PlanLimit
            { PlanId = plan.Id, Feature = feature, LimitValue = 7, Period = "Daily" }).ToList();
            db.Plans.AddRange(plan, new Plan { Code = "PRO_MONTH", Name = "Test-only inactive", Price = 123, DurationDays = 30 });
            await db.SaveChangesAsync();
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal("FREE", item.GetProperty("code").GetString());
        Assert.All(item.GetProperty("limits").EnumerateArray(), limit => Assert.Equal(7, limit.GetProperty("limitValue").GetInt32()));
    }

    [Theory]
    [InlineData("FullTest", "Daily", -1)]
    [InlineData("AiToken", "Daily", 1)]
    [InlineData("Practice", "Unknown", 1)]
    public async Task InvalidLimitRowsAreRejected(string feature, string period, int value)
    {
        using var factory = new BillingApiFactory();
        await factory.WithDb(async db =>
        {
            var plan = new Plan { Code = "FREE", Name = "Test-only plan" };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
            db.PlanLimits.Add(new PlanLimit { PlanId = plan.Id, Feature = feature, LimitValue = value, Period = period });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
    }
}
