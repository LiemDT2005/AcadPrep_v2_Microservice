using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ai.API.Data.Configurations;

public class AiChatRequestConfiguration : IEntityTypeConfiguration<AiChatRequest>
{
    public void Configure(EntityTypeBuilder<AiChatRequest> builder)
    {
        builder.ToTable("AiChatRequests", table => table.HasCheckConstraint("CK_AiChatRequests_Status",
            "[Status] IN ('Processing', 'Completed', 'Rejected', 'Failed')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Status).HasMaxLength(20).IsRequired();
        builder.Property(item => item.Error).HasMaxLength(200);
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => new { item.UserId, item.RequestId }).IsUnique();
        // One request in flight per user (across instances).
        builder.HasIndex(item => item.UserId).IsUnique().HasFilter("[Status] = 'Processing'")
            .HasDatabaseName("UX_AiChatRequests_UserId_Processing");
        builder.HasIndex(item => new { item.UserId, item.Status, item.CompletedAt });
    }
}
