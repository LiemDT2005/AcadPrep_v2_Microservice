using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ai.API.Data.Configurations;

public class AiChatMessageConfiguration : IEntityTypeConfiguration<AiChatMessage>
{
    public void Configure(EntityTypeBuilder<AiChatMessage> builder)
    {
        builder.ToTable("AiChatMessages", table => table.HasCheckConstraint("CK_AiChatMessages_Role", "[Role] IN ('User', 'Model')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Role).HasMaxLength(10).IsRequired();
        builder.Property(item => item.Content).IsRequired();
        builder.HasIndex(item => new { item.SessionId, item.CreatedAt });
        builder.HasOne<AiChatSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
