using System.Net.Http.Headers;
using Billing.API.Data;
using Billing.API.Entities;

namespace Billing.API.Tests;

// Test-only fixture data. Names/prices are placeholders, not approved business values.
internal static class BillingTestData
{
    public static readonly Guid FreeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid MonthId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid QuarterId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    public static async Task SeedPlans(BillingDbContext db, bool includeFree = true, bool quarterActive = true)
    {
        if (includeFree) db.Plans.Add(new Plan { Id = FreeId, Code = "FREE", Name = "Test free", IsActive = true });
        db.Plans.Add(new Plan { Id = MonthId, Code = "PRO_MONTH", Name = "Test month", Price = 11, DurationDays = 30, IsActive = true });
        db.Plans.Add(new Plan { Id = QuarterId, Code = "PRO_QUARTER", Name = "Test quarter", Price = 22, DurationDays = 90, IsActive = quarterActive });
        await db.SaveChangesAsync();
    }

    public static Subscription Subscription(Guid userId, DateTime start, DateTime end, string status = SubscriptionStatus.Active,
        Guid? planId = null) => new()
    {
        UserId = userId, PlanId = planId ?? MonthId, Status = status, StartDate = start, EndDate = end,
        CreatedAt = start, UpdatedAt = start
    };

    public static HttpClient Client(this BillingApiFactory factory, string? role, Guid? subject = null)
    {
        var client = factory.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                factory.Token(role, (subject ?? Guid.NewGuid()).ToString()));
        return client;
    }
}
