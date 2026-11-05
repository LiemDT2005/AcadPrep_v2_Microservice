using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ai.API.Data.Configurations;

public class AiUsageWindowConfiguration : IEntityTypeConfiguration<AiUsageWindow>
{
    public void Configure(EntityTypeBuilder<AiUsageWindow> builder)
    {
        builder.ToTable("AiUsageWindows", table => table.HasCheckConstraint("CK_AiUsageWindows_Range", "[WindowEnd] >= [WindowStart]"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.PlanCode).HasMaxLength(20).IsRequired();
        // Not unique: a reset window and the next one may share a timestamp; writes are serialized per user
        // by the one-request-in-flight rule.
        builder.HasIndex(item => new { item.UserId, item.WindowEnd });
        builder.HasIndex(item => new { item.WindowEnd, item.TokensUsed });
    }
}
