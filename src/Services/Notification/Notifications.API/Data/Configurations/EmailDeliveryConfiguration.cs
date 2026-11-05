using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.API.Data.Converters;
using Notifications.API.Entities;

namespace Notifications.API.Data.Configurations;

public class EmailDeliveryConfiguration : IEntityTypeConfiguration<EmailDelivery>
{
    public void Configure(EntityTypeBuilder<EmailDelivery> builder)
    {
        builder.ToTable("EmailDeliveries", table =>
            table.HasCheckConstraint("CK_EmailDeliveries_Status", "[Status] IN ('Pending', 'Sent', 'Failed', 'Skipped')"));
        builder.HasKey(delivery => delivery.Id);
        builder.Property(delivery => delivery.Id).ValueGeneratedNever();
        builder.HasIndex(delivery => delivery.SourceEventId).IsUnique();
        builder.Property(delivery => delivery.Recipient).HasMaxLength(256);
        builder.Property(delivery => delivery.Subject).HasMaxLength(200).IsRequired();
        builder.Property(delivery => delivery.Status).HasMaxLength(20).IsRequired();
        builder.Property(delivery => delivery.LastError).HasMaxLength(500);
        builder.Property(delivery => delivery.CreatedAt).HasConversion<UtcDateTimeConverter>();
        builder.Property(delivery => delivery.UpdatedAt).HasConversion<UtcDateTimeConverter>();
        builder.Property(delivery => delivery.SentAt).HasConversion(
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
        builder.HasOne<Notification>().WithMany().HasForeignKey(delivery => delivery.NotificationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
