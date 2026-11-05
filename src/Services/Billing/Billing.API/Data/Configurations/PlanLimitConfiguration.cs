using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class PlanLimitConfiguration : IEntityTypeConfiguration<PlanLimit>
{
    public void Configure(EntityTypeBuilder<PlanLimit> builder)
    {
        builder.ToTable("PlanLimits", table =>
        {
            table.HasCheckConstraint("CK_PlanLimits_Feature", "[Feature] IN ('FullTest', 'Practice', 'Vocab')");
            table.HasCheckConstraint("CK_PlanLimits_Period", "[Period] IN ('Daily', 'Monthly', 'Lifetime')");
            table.HasCheckConstraint("CK_PlanLimits_Value", "[LimitValue] IS NULL OR [LimitValue] >= 0");
        });
        builder.HasKey(limit => new { limit.PlanId, limit.Feature });
        builder.Property(limit => limit.Feature).HasMaxLength(20).IsRequired();
        builder.Property(limit => limit.Period).HasMaxLength(20).IsRequired();
        builder.HasOne(limit => limit.Plan).WithMany(plan => plan.Limits)
            .HasForeignKey(limit => limit.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
