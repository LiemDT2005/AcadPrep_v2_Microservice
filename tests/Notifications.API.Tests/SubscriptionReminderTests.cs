using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Common.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Notifications.API.Entities;
using Notifications.API.Enums;
using Notifications.API.Services;
using Notifications.API.Services.Interfaces;
using Notifications.API.Repositories;
using Xunit;
using static Notifications.API.Tests.NotificationMarkReadApiFactory;
using static Notifications.API.Tests.NotificationMarkReadApiTests;

namespace Notifications.API.Tests;

public class SubscriptionReminderTests
{
    private static readonly DateTime EndDate = new(2026, 11, 8, 10, 0, 0, DateTimeKind.Utc); // 17:00 08/11 VN

    [Fact]
    public async Task ExpiringEventCreatesOneReminderForTheUserAndSendsTheSameTextByEmail()
    {
        using var factory = await CreateFactory();
        var email = new FakeEmail();
        var @event = Expiring("learner@example.test");
        await Handle(factory, email, SubscriptionReminderService.Expiring(@event));

        var notification = await factory.WithDbAsync(db => db.Notifications.SingleAsync(n => n.SourceEventId == @event.Id));
        Assert.Equal(NotificationType.Reminder, notification.Type);
        Assert.Equal(NotificationAudience.User, notification.Audience);
        Assert.Equal(LearnerId, notification.TargetUserId);
        Assert.Null(notification.CreatedBy);
        Assert.Equal("Gói Pro 1 tháng của bạn sẽ hết hạn lúc 08/11/2026 17:00. Gia hạn để không gián đoạn.", notification.Content);
        var sent = Assert.Single(email.Sent);
        Assert.Equal(("learner@example.test", notification.Title, notification.Content), sent);
        var delivery = await factory.WithDbAsync(db => db.EmailDeliveries.SingleAsync());
        Assert.Equal(EmailDeliveryStatus.Sent, delivery.Status);
        Assert.Equal(1, delivery.Attempts);

        // Visible to that learner through the existing list API (UC-15.1), not to another user.
        using var learner = Client(factory, "Learner");
        var page = await learner.GetFromJsonAsync<JsonElement>("/api/notifications?pageSize=100");
        Assert.Contains(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == notification.Id);
    }

    [Fact]
    public async Task RedeliveredEventNeverDuplicatesTheNotificationOrTheEmail()
    {
        using var factory = await CreateFactory();
        var email = new FakeEmail();
        var message = SubscriptionReminderService.Expired(Expired("learner@example.test"));
        await Handle(factory, email, message);
        await Handle(factory, email, message);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.SourceEventId == message.EventId)));
        Assert.Single(email.Sent);
        Assert.Equal("Gói Pro 1 tháng của bạn đã hết hạn. Bạn đang dùng gói miễn phí.", email.Sent[0].Body);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("learner@example.test", false)]
    public async Task MissingAddressOrSmtpKeepsTheInAppNotificationOnly(string? address, bool smtpConfigured)
    {
        using var factory = await CreateFactory();
        var email = new FakeEmail { Configured = smtpConfigured };
        var message = SubscriptionReminderService.Expiring(Expiring(address));
        await Handle(factory, email, message);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.SourceEventId == message.EventId)));
        Assert.Empty(email.Sent);
        Assert.Equal(EmailDeliveryStatus.Skipped, (await factory.WithDbAsync(db => db.EmailDeliveries.SingleAsync())).Status);
    }

    [Fact]
    public async Task EmailRetriesAtMostThreeTimesThenFailsWithoutTouchingTheNotification()
    {
        using var factory = await CreateFactory();
        var email = new FakeEmail { FailuresLeft = 10 };
        var message = SubscriptionReminderService.Expiring(Expiring("learner@example.test"));
        await Handle(factory, email, message);
        var delivery = await factory.WithDbAsync(db => db.EmailDeliveries.SingleAsync());
        Assert.Equal(EmailDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(3, delivery.Attempts);
        Assert.Equal(3, email.Calls);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Notifications.CountAsync(n => n.SourceEventId == message.EventId)));
        await Handle(factory, email, message); // redelivery: Failed is final, no fourth attempt
        Assert.Equal(3, email.Calls);
    }

    [Fact]
    public async Task TransientEmailFailureIsRetriedAndSucceeds()
    {
        using var factory = await CreateFactory();
        var email = new FakeEmail { FailuresLeft = 1 };
        await Handle(factory, email, SubscriptionReminderService.Expiring(Expiring("learner@example.test")));
        var delivery = await factory.WithDbAsync(db => db.EmailDeliveries.SingleAsync());
        Assert.Equal(EmailDeliveryStatus.Sent, delivery.Status);
        Assert.Equal(2, delivery.Attempts);
        Assert.Single(email.Sent);
    }

    [Theory]
    [InlineData(false, "Gói của bạn đã được kích hoạt")]
    [InlineData(true, "Gia hạn gói thành công")]
    public async Task ActivatedEventCreatesASubscriptionNotificationAndEmail(bool renewal, string title)
    {
        using var factory = await CreateFactory();
        var email = new FakeEmail();
        var @event = new SubscriptionActivatedEvent(LearnerId, "learner@example.test", "PRO_MONTH", "Pro 1 tháng",
            EndDate.AddDays(-30), EndDate, "12345678901234", renewal) { Id = Guid.NewGuid() };
        await Handle(factory, email, SubscriptionReminderService.Activated(@event));
        var notification = await factory.WithDbAsync(db => db.Notifications.SingleAsync(n => n.SourceEventId == @event.Id));
        Assert.Equal(NotificationType.Subscription, notification.Type);
        Assert.Equal(title, notification.Title);
        Assert.Equal("Thanh toán đơn 12345678901234 thành công. Gói Pro 1 tháng có hiệu lực đến 08/11/2026 17:00.",
            notification.Content);
        Assert.Single(email.Sent);
    }

    [Fact]
    public void SmtpIsNotConfiguredWithoutHostOrSender()
    {
        Assert.False(new SmtpOptions().IsConfigured);
        Assert.False(new SmtpOptions { Host = "smtp.test" }.IsConfigured);
        Assert.True(new SmtpOptions { Host = "smtp.test", From = "noreply@example.test" }.IsConfigured);
    }

    private static SubscriptionExpiringEvent Expiring(string? email) =>
        new(LearnerId, email, "PRO_MONTH", "Pro 1 tháng", EndDate, 3) { Id = Guid.NewGuid() };

    private static SubscriptionExpiredEvent Expired(string? email) =>
        new(LearnerId, email, "PRO_MONTH", "Pro 1 tháng", EndDate) { Id = Guid.NewGuid() };

    private static Task Handle(NotificationMarkReadApiFactory factory, FakeEmail email, ReminderMessage message) =>
        factory.WithDbAsync(async db =>
        {
            var service = new SubscriptionReminderService(new NotificationRepository(db), new EmailDeliveryRepository(db), email,
                Options.Create(new SmtpOptions { Host = "smtp.test", From = "noreply@example.test", RetryDelaySeconds = 0 }),
                TimeProvider.System, NullLogger<SubscriptionReminderService>.Instance);
            await service.HandleAsync(message, default);
            return true;
        });

    // Test-only email sender; production uses SmtpEmailSender with real SMTP settings.
    private sealed class FakeEmail : IEmailSender
    {
        public bool Configured { get; init; } = true;
        public int FailuresLeft { get; set; }
        public int Calls { get; private set; }
        public List<(string To, string Subject, string Body)> Sent { get; } = [];
        public bool IsConfigured => Configured;

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            Calls++;
            if (FailuresLeft-- > 0) throw new InvalidOperationException("Simulated SMTP failure.");
            Sent.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
