using Billing.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Billing.API.Tests;

// Offline: the SQL Server migration script carries the approved catalog and is idempotent by Code/Feature.
public class SeedPlansMigrationTests
{
    [Fact]
    public void SeedScriptContainsApprovedCatalogAndNeverDuplicates()
    {
        using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=BillingSeedScript;Integrated Security=True").Options);
        var script = db.GetService<IMigrator>().GenerateScript("20261105094611_AddOrders", "20261105101705_SeedPlans");
        foreach (var expected in new[]
                 {
                     "N'FREE', N'Miễn phí', 0, 0, 0", "N'PRO_MONTH', N'Pro 1 tháng', 79000, 30, 1",
                     "N'PRO_QUARTER', N'Pro 3 tháng', 199000, 90, 2", "(N'FREE', N'FullTest', 3, N'Monthly')",
                     "(N'FREE', N'Practice', 5, N'Daily')", "(N'FREE', N'Vocab', 100, N'Lifetime')",
                     "(N'PRO_MONTH', N'FullTest', NULL, N'Monthly')", "(N'PRO_QUARTER', N'Vocab', NULL, N'Lifetime')",
                     "WHERE NOT EXISTS (SELECT 1 FROM [Plans] p WHERE p.[Code] = v.[Code])",
                     "WHERE NOT EXISTS (SELECT 1 FROM [PlanLimits] l WHERE l.[PlanId] = p.[Id] AND l.[Feature] = v.[Feature])"
                 })
            Assert.Contains(expected, script);
        Assert.DoesNotContain("UPDATE", script, StringComparison.OrdinalIgnoreCase); // never overwrites edited rows
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }
}
