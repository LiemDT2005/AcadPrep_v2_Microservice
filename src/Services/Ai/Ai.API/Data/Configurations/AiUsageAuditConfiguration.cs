using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ai.API.Data.Configurations;

public class AiUsageAuditConfiguration : IEntityTypeConfiguration<AiUsageAudit>
{
    public void Configure(EntityTypeBuilder<AiUsageAudit> builder)
    {
        builder.ToTable("AiUsageAudits", table => table.HasCheckConstraint("CK_AiUsageAudits_Action", "[Action] IN ('ResetUsage', 'ChangeLimit')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Action).HasMaxLength(20).IsRequired();
        builder.Property(item => item.PlanCode).HasMaxLength(20);
        builder.HasIndex(item => new { item.UserId, item.CreatedAt });
    }
}
