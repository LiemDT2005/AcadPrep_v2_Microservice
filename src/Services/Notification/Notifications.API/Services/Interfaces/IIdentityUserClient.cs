using Notifications.API.Services;

namespace Notifications.API.Services.Interfaces;

public interface IIdentityUserClient
{
    // Never throws for upstream failures; returns Unavailable instead.
    Task<IdentityLookupStatus> FindUserAsync(Guid userId, CancellationToken ct);
}
