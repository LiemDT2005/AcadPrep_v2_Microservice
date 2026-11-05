using Billing.API.Data.Converters;
using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class SubscriptionGrantConfiguration : IEntityTypeConfiguration<SubscriptionGrant>
{
    public void Configure(EntityTypeBuilder<SubscriptionGrant> builder)
    {
        builder.ToTable("SubscriptionGrants", table =>
            table.HasCheckConstraint("CK_SubscriptionGrants_Duration", "[DurationDays] BETWEEN 1 AND 365"));
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Id).ValueGeneratedNever();
        builder.HasIndex(grant => grant.RequestId).IsUnique();
        builder.HasIndex(grant => new { grant.TargetUserId, grant.CreatedAt });
        builder.Property(grant => grant.Note).HasMaxLength(500);
        builder.Property(grant => grant.PreviousEndDate).HasConversion<NullableUtcDateTimeConverter>();
        foreach (var property in new[] { nameof(SubscriptionGrant.NewStartDate), nameof(SubscriptionGrant.NewEndDate),
                     nameof(SubscriptionGrant.CreatedAt) })
            builder.Property<DateTime>(property).HasConversion<UtcDateTimeConverter>();
        builder.HasOne<Subscription>().WithMany().HasForeignKey(grant => grant.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Plan>().WithMany().HasForeignKey(grant => grant.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
