using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Billing.API.Data;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanLimit> PlanLimits => Set<PlanLimit>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionGrant> SubscriptionGrants => Set<SubscriptionGrant>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<SubscriptionReminderLog> SubscriptionReminderLogs => Set<SubscriptionReminderLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
}
