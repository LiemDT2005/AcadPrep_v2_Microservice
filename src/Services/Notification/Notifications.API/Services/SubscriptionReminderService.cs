using System.Globalization;
using BuildingBlocks.Common.Events;
using Microsoft.Extensions.Options;
using Notifications.API.Entities;
using Notifications.API.Enums;
using Notifications.API.Repositories.Interfaces;
using Notifications.API.Services.Interfaces;

namespace Notifications.API.Services;

public record ReminderMessage(Guid EventId, Guid UserId, string? Email, string Title, string Content,
    NotificationType Type = NotificationType.Reminder);

// UC-23.3 consumer (approved by Huy): every reminder creates an in-app notification for the user and, when an
// address is known and SMTP is configured, sends the same text by email. Idempotent per event Id: the
// notification is unique by SourceEventId and the mail state by EmailDelivery.SourceEventId.
public class SubscriptionReminderService(INotificationRepository notifications, IEmailDeliveryRepository deliveries,
    IEmailSender email, IOptions<SmtpOptions> smtp, TimeProvider clock, ILogger<SubscriptionReminderService> logger)
    : ISubscriptionReminderService
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public static readonly TimeSpan StalePendingAfter = TimeSpan.FromMinutes(2);

    public static ReminderMessage Expiring(SubscriptionExpiringEvent @event) => new(@event.Id, @event.UserId, @event.UserEmail,
        "Gói của bạn sắp hết hạn",
        $"Gói {@event.PlanName} của bạn sẽ hết hạn lúc {VietnamTime(@event.EndDate)}. Gia hạn để không gián đoạn.");

    public static ReminderMessage Expired(SubscriptionExpiredEvent @event) => new(@event.Id, @event.UserId, @event.UserEmail,
        "Gói của bạn đã hết hạn",
        $"Gói {@event.PlanName} của bạn đã hết hạn. Bạn đang dùng gói miễn phí.");

    // UC-21.2 (approved by Huy): confirmed payment -> "plan activated" notification + email.
    public static ReminderMessage Activated(SubscriptionActivatedEvent @event) => new(@event.Id, @event.UserId,
        @event.UserEmail, @event.IsRenewal ? "Gia hạn gói thành công" : "Gói của bạn đã được kích hoạt",
        (@event.OrderCode is null ? "" : $"Thanh toán đơn {@event.OrderCode} thành công. ") +
        $"Gói {@event.PlanName} có hiệu lực đến {VietnamTime(@event.EndDate)}.", NotificationType.Subscription);

    public async Task HandleAsync(ReminderMessage message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty || message.UserId == Guid.Empty)
        {
            logger.LogWarning("Reminder event ignored: missing identifiers.");
            return;
        }
        var notificationId = await EnsureNotificationAsync(message, ct);
        await DeliverEmailAsync(message, notificationId, ct);
    }

    private async Task<Guid> EnsureNotificationAsync(ReminderMessage message, CancellationToken ct)
    {
        var existing = await notifications.FindIdBySourceEventAsync(message.EventId, ct);
        if (existing is not null) return existing.Value;
        var notification = new Notification
        {
            Id = Guid.NewGuid(), Title = message.Title, Content = message.Content, Type = message.Type,
            Audience = NotificationAudience.User, TargetUserId = message.UserId, SourceEventId = message.EventId,
            CreatedBy = null, CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        if (await notifications.TryAddAsync(notification, ct)) return notification.Id;
        // A concurrent redelivery inserted it first (unique SourceEventId): reuse that row.
        return await notifications.FindIdBySourceEventAsync(message.EventId, ct) ?? throw new InvalidOperationException(
            "Reminder notification could not be stored.");
    }

    private async Task DeliverEmailAsync(ReminderMessage message, Guid notificationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var delivery = await deliveries.GetBySourceEventForUpdateAsync(message.EventId, ct);
        if (delivery is null)
        {
            delivery = new EmailDelivery
            {
                SourceEventId = message.EventId, NotificationId = notificationId, Recipient = message.Email,
                Subject = message.Title, CreatedAt = now, UpdatedAt = now,
                Status = string.IsNullOrWhiteSpace(message.Email) || !email.IsConfigured
                    ? EmailDeliveryStatus.Skipped : EmailDeliveryStatus.Pending,
                LastError = string.IsNullOrWhiteSpace(message.Email) ? "No recipient address."
                    : !email.IsConfigured ? "SMTP is not configured." : null
            };
            // Another delivery of the same event owns the mail; never send twice.
            if (!await deliveries.TryAddAsync(delivery, ct)) return;
            if (delivery.Status == EmailDeliveryStatus.Skipped)
            {
                logger.LogInformation("Reminder email skipped. EventId={EventId} Reason={Reason}", message.EventId, delivery.LastError);
                return;
            }
        }
        else if (delivery.Status != EmailDeliveryStatus.Pending || now - delivery.UpdatedAt < StalePendingAfter)
            return; // Sent/Failed/Skipped are final; a fresh Pending is being handled by another delivery.

        var settings = smtp.Value;
        while (delivery.Attempts < settings.MaxAttempts)
        {
            delivery.Attempts++;
            delivery.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            try
            {
                await email.SendAsync(delivery.Recipient!, message.Title, message.Content, ct);
                delivery.Status = EmailDeliveryStatus.Sent;
                delivery.SentAt = delivery.UpdatedAt;
                delivery.LastError = null;
                await deliveries.UpdateAsync(delivery, ct);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Never log the address, credentials or the message body.
                delivery.LastError = Truncate(exception.GetType().Name, 500);
                logger.LogWarning("Reminder email attempt {Attempt} failed. EventId={EventId} Reason={Reason}",
                    delivery.Attempts, message.EventId, exception.GetType().Name);
                if (delivery.Attempts >= settings.MaxAttempts) delivery.Status = EmailDeliveryStatus.Failed;
                await deliveries.UpdateAsync(delivery, ct);
                if (delivery.Status == EmailDeliveryStatus.Failed) return;
                if (settings.RetryDelaySeconds > 0)
                    await Task.Delay(TimeSpan.FromSeconds(settings.RetryDelaySeconds * delivery.Attempts), clock, ct);
            }
        }
    }

    public static string VietnamTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7).ToString("dd/MM/yyyy HH:mm", Vi);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
