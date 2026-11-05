using Ai.API.Data.Converters;
using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ai.API.Data;

public class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public DbSet<AiChatSession> Sessions => Set<AiChatSession>();
    public DbSet<AiChatMessage> Messages => Set<AiChatMessage>();
    public DbSet<AiChatRequest> Requests => Set<AiChatRequest>();
    public DbSet<AiUsageWindow> UsageWindows => Set<AiUsageWindow>();
    public DbSet<AiTokenLimit> TokenLimits => Set<AiTokenLimit>();
    public DbSet<AiUsageAudit> Audits => Set<AiUsageAudit>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        builder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    // Same as Exam: one IEntityTypeConfiguration per entity in Data/Configurations.
    protected override void OnModelCreating(ModelBuilder model) =>
        model.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);
}
