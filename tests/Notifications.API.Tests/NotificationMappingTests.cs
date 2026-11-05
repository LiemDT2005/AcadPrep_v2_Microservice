using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Notifications.API;
using Notifications.API.DTOs;
using Notifications.API.Entities;
using Notifications.API.Enums;
using Xunit;

namespace Notifications.API.Tests;

public class NotificationMappingTests
{
    [Fact]
    public void RegisteredProfileIsValid()
    {
        using var provider = CreateProvider();
        provider.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
    }

    [Theory]
    [InlineData("User")]
    [InlineData("All")]
    [InlineData("Learner")]
    [InlineData("Moderator")]
    public void RequestMapsEditableFieldsWithoutOverwritingServerOwnedFields(string audience)
    {
        using var provider = CreateProvider();
        var target = audience == "User" ? Guid.NewGuid() : (Guid?)null;
        var request = new CreateAdminNotificationRequest
        {
            Title = "  Title  ", Content = " Content ", Audience = audience, TargetUserId = target
        };
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
        var state = new UserNotificationState { UserId = Guid.NewGuid(), NotificationId = id };
        var entity = new Notification
        {
            Id = id, Type = NotificationType.Reminder, CreatedBy = adminId, CreatedAt = createdAt,
            SourceEventId = eventId, DataJson = "{}", UserStates = [state]
        };

        provider.GetRequiredService<IMapper>().Map(request, entity);

        Assert.Equal("Title", entity.Title);
        Assert.Equal(" Content ", entity.Content);
        Assert.Equal(Enum.Parse<NotificationAudience>(audience), entity.Audience);
        Assert.Equal(target, entity.TargetUserId);
        Assert.Equal(id, entity.Id);
        Assert.Equal(NotificationType.Reminder, entity.Type);
        Assert.Equal(adminId, entity.CreatedBy);
        Assert.Equal(createdAt, entity.CreatedAt);
        Assert.Equal(eventId, entity.SourceEventId);
        Assert.Equal("{}", entity.DataJson);
        Assert.Same(state, Assert.Single(entity.UserStates));
    }

    [Fact]
    public void CreatedResponseMapsNotificationIdentifier()
    {
        using var provider = CreateProvider();
        var entity = new Notification();
        var response = provider.GetRequiredService<IMapper>().Map<NotificationCreatedDto>(entity);
        Assert.Equal(entity.Id, response.Id);
    }

    private static ServiceProvider CreateProvider() => new ServiceCollection()
        .AddLogging().AddApplication().BuildServiceProvider();
}
