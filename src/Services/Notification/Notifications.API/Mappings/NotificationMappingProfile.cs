using AutoMapper;
using Notifications.API.DTOs;
using Notifications.API.Entities;
using Notifications.API.Enums;

namespace Notifications.API.Mappings;

public class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        CreateMap<CreateAdminNotificationRequest, Notification>()
            .ForMember(destination => destination.Audience,
                options => options.MapFrom(source => Enum.Parse<NotificationAudience>(source.Audience!)))
            // The application service owns identity, timestamps and notification metadata.
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.Type, options => options.Ignore())
            .ForMember(destination => destination.CreatedBy, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.SourceEventId, options => options.Ignore())
            .ForMember(destination => destination.DataJson, options => options.Ignore())
            .ForMember(destination => destination.UserStates, options => options.Ignore());

        CreateMap<Notification, NotificationCreatedDto>();
    }
}
