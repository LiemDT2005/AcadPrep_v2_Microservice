using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ai.API.Data.Configurations;

public class AiTokenLimitConfiguration : IEntityTypeConfiguration<AiTokenLimit>
{
    public void Configure(EntityTypeBuilder<AiTokenLimit> builder)
    {
        builder.ToTable("AiTokenLimits", table =>
        {
            table.HasCheckConstraint("CK_AiTokenLimits_PlanCode", "[PlanCode] IN ('FREE', 'PRO_MONTH', 'PRO_QUARTER')");
            table.HasCheckConstraint("CK_AiTokenLimits_Range", "[TokenLimit] BETWEEN 0 AND 100000000");
        });
        builder.HasKey(item => item.PlanCode);
        builder.Property(item => item.PlanCode).HasMaxLength(20);
    }
}
