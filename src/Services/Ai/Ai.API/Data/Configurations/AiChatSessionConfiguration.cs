using Ai.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ai.API.Data.Configurations;

public class AiChatSessionConfiguration : IEntityTypeConfiguration<AiChatSession>
{
    public void Configure(EntityTypeBuilder<AiChatSession> builder)
    {
        builder.ToTable("AiChatSessions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Title).HasMaxLength(100).IsRequired();
        builder.HasIndex(item => new { item.UserId, item.IsDeleted, item.UpdatedAt });
    }
}
