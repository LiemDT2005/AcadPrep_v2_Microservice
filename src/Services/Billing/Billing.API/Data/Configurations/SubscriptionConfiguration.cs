using Billing.API.Data.Converters;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions", table =>
        {
            table.HasCheckConstraint("CK_Subscriptions_Status", "[Status] IN ('Active', 'Expired')");
            table.HasCheckConstraint("CK_Subscriptions_Dates", "[EndDate] > [StartDate]");
        });
        builder.HasKey(subscription => subscription.Id);
        builder.Property(subscription => subscription.Id).ValueGeneratedNever();
        builder.Property(subscription => subscription.Status).HasMaxLength(20).IsRequired();
        builder.Property(subscription => subscription.Note).HasMaxLength(500);
        builder.Property(subscription => subscription.UserEmail).HasMaxLength(256);
        builder.Property(subscription => subscription.Version).IsConcurrencyToken();
        foreach (var property in new[] { nameof(Subscription.StartDate), nameof(Subscription.EndDate),
                     nameof(Subscription.CreatedAt), nameof(Subscription.UpdatedAt) })
            builder.Property<DateTime>(property).HasConversion<UtcDateTimeConverter>();
        // At most one persisted Active subscription per user.
        builder.HasIndex(subscription => subscription.UserId).IsUnique()
            .HasFilter("[Status] = 'Active'").HasDatabaseName("UX_Subscriptions_UserId_Active");
        builder.HasIndex(subscription => new { subscription.UserId, subscription.EndDate });
        builder.HasOne(subscription => subscription.Plan).WithMany()
            .HasForeignKey(subscription => subscription.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
