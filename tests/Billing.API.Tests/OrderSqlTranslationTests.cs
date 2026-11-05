using Billing.API.Data;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Billing.API.Tests;

// Offline only: verifies SQL Server translation of the read-time status logic without connecting.
public class OrderSqlTranslationTests
{
    [Fact]
    public void EffectiveStatusAndExpiredFilterTranslateToServerSideSql()
    {
        using var db = new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=BillingTranslation;Integrated Security=True").Options);
        var now = DateTime.UtcNow;
        var sql = db.Orders.Where(order => order.Status == OrderStatus.Expired ||
                order.Status == OrderStatus.Pending && order.ExpiresAt <= now)
            .OrderByDescending(order => order.Amount).ThenByDescending(order => order.Id).Skip(20).Take(20)
            .Select(order => order.Status == OrderStatus.Pending && order.ExpiresAt <= now ? OrderStatus.Expired : order.Status)
            .ToQueryString();
        Assert.Contains("CASE", sql);
        Assert.Contains("OFFSET", sql);
        Assert.Contains("[o].[ExpiresAt] <= @", sql);
        Assert.Equal(System.Data.ConnectionState.Closed, db.Database.GetDbConnection().State);
    }
}
