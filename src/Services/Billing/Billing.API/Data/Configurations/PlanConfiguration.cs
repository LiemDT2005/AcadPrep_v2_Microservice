using Billing.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Billing.API.Data.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Plans", table =>
        {
            table.HasCheckConstraint("CK_Plans_Code", "[Code] IN ('FREE', 'PRO_MONTH', 'PRO_QUARTER')");
            table.HasCheckConstraint("CK_Plans_Price_Duration", "([Code] = 'FREE' AND [Price] = 0 AND [DurationDays] = 0) OR ([Code] <> 'FREE' AND [Price] > 0 AND [DurationDays] > 0)");
        });
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(plan => plan.Code).IsUnique();
        builder.Property(plan => plan.Name).HasMaxLength(200).IsRequired();
        builder.Property(plan => plan.Description).HasMaxLength(2000);
        builder.Property(plan => plan.Price).HasPrecision(18, 0);
    }
}
