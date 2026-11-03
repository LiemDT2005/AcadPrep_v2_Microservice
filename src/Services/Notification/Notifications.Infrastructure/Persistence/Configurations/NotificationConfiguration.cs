using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Entities;
using Notifications.Infrastructure.Persistence.Converters;

namespace Notifications.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Type",
                "[Type] IN (N'System', N'ExamResult', N'Subscription', N'Reminder')");
            table.HasCheckConstraint("CK_Notifications_Audience_TargetUserId",
                "([Audience] = N'User' AND [TargetUserId] IS NOT NULL) OR " +
                "([Audience] IN (N'All', N'Learner', N'Moderator') AND [TargetUserId] IS NULL)");
        });
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.Title).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Content).HasMaxLength(2000).IsRequired();
        builder.Property(notification => notification.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notification => notification.Audience).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notification => notification.DataJson).HasColumnType("nvarchar(max)");
        builder.Property(notification => notification.CreatedAt)
            .HasConversion(new UtcDateTimeConverter())
            .HasColumnType("datetime2")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(notification => notification.SourceEventId)
            .IsUnique().HasFilter("[SourceEventId] IS NOT NULL");
        builder.HasIndex(notification => new { notification.Audience, notification.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(notification => new { notification.TargetUserId, notification.CreatedAt })
            .IsDescending(false, true);
        builder.HasIndex(notification => notification.CreatedAt).IsDescending();
    }
}
