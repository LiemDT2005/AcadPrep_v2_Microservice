using Billing.API.Data.Converters;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Status", "[Status] IN ('Pending', 'Paid', 'Failed', 'Expired')");
            table.HasCheckConstraint("CK_Orders_Kind", "[Kind] IN ('Purchase', 'Renewal')");
            table.HasCheckConstraint("CK_Orders_Currency", "[Currency] = 'VND'");
            table.HasCheckConstraint("CK_Orders_Amount_Duration", "[Amount] > 0 AND [DurationDays] > 0");
            table.HasCheckConstraint("CK_Orders_PaidAt", "([Status] = 'Paid' AND [PaidAt] IS NOT NULL) OR ([Status] <> 'Paid' AND [PaidAt] IS NULL)");
        });
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();
        builder.Property(order => order.OrderCode).HasMaxLength(20).IsRequired();
        builder.HasIndex(order => order.OrderCode).IsUnique();
        builder.Property(order => order.UserEmail).HasMaxLength(256);
        builder.Property(order => order.PlanCode).HasMaxLength(20).IsRequired();
        builder.Property(order => order.PlanName).HasMaxLength(200).IsRequired();
        builder.Property(order => order.Amount).HasPrecision(18, 0);
        builder.Property(order => order.Currency).HasMaxLength(3).IsUnicode(false).IsRequired();
        builder.Property(order => order.Kind).HasMaxLength(20).IsRequired();
        builder.Property(order => order.Status).HasMaxLength(20).IsRequired();
        builder.Property(order => order.PaymentUrl).HasMaxLength(2048);
        builder.Property(order => order.VnpTransactionNo).HasMaxLength(50);
        // At most one Pending order per user and plan (Huy, 2026-11-05): a repeated click reuses it.
        builder.HasIndex(order => new { order.UserId, order.PlanId }).IsUnique()
            .HasFilter("[Status] = 'Pending'").HasDatabaseName("UX_Orders_UserId_PlanId_Pending");
        builder.Property(order => order.VnpResponseCode).HasMaxLength(10);
        builder.Property(order => order.Version).IsConcurrencyToken();
        foreach (var property in new[] { nameof(Order.CreatedAt), nameof(Order.ExpiresAt) })
            builder.Property<DateTime>(property).HasConversion<UtcDateTimeConverter>();
        builder.Property(order => order.PaidAt).HasConversion<NullableUtcDateTimeConverter>();
        builder.HasIndex(order => new { order.UserId, order.CreatedAt });
        builder.HasIndex(order => order.CreatedAt);
        builder.HasIndex(order => new { order.Status, order.ExpiresAt });
        builder.HasIndex(order => order.PaidAt);
        builder.HasOne<Plan>().WithMany().HasForeignKey(order => order.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
