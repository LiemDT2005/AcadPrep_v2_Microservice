using Billing.API.Data.Converters;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class SubscriptionReminderLogConfiguration : IEntityTypeConfiguration<SubscriptionReminderLog>
{
    public void Configure(EntityTypeBuilder<SubscriptionReminderLog> builder)
    {
        builder.ToTable("SubscriptionReminderLogs", table =>
            table.HasCheckConstraint("CK_SubscriptionReminderLogs_Kind", "[Kind] IN ('Expiring', 'Expired')"));
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.Kind).HasMaxLength(20).IsRequired();
        builder.Property(log => log.EndDate).HasConversion<UtcDateTimeConverter>();
        builder.Property(log => log.CreatedAt).HasConversion<UtcDateTimeConverter>();
        builder.HasIndex(log => new { log.SubscriptionId, log.Kind, log.EndDate }).IsUnique();
        builder.HasOne<Subscription>().WithMany().HasForeignKey(log => log.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Type).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Payload).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(500);
        builder.Property(message => message.CreatedAt).HasConversion<UtcDateTimeConverter>();
        builder.Property(message => message.PublishedAt).HasConversion<NullableUtcDateTimeConverter>();
        builder.HasIndex(message => new { message.PublishedAt, message.CreatedAt });
    }
}
