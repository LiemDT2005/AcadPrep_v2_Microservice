using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Entities;
using Notifications.Infrastructure.Persistence.Converters;

namespace Notifications.Infrastructure.Persistence.Configurations;

public class UserNotificationStateConfiguration : IEntityTypeConfiguration<UserNotificationState>
{
    public void Configure(EntityTypeBuilder<UserNotificationState> builder)
    {
        builder.ToTable("UserNotificationStates");
        builder.HasKey(state => new { state.NotificationId, state.UserId });
        builder.HasOne(state => state.Notification).WithMany(notification => notification.UserStates)
            .HasForeignKey(state => state.NotificationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(state => state.ReadAt)
            .HasConversion(new UtcDateTimeConverter()).HasColumnType("datetime2");
        builder.Property(state => state.DeletedAt)
            .HasConversion(new UtcDateTimeConverter()).HasColumnType("datetime2");
        builder.HasIndex(state => new { state.UserId, state.IsDeleted, state.IsRead });
    }
}
