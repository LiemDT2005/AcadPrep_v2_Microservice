namespace Billing.API.Services.Interfaces;

public interface IIdentityUserClient
{
    // Never throws for upstream failures; returns Unavailable instead.
    Task<IdentityLookup> GetUserAsync(Guid userId, CancellationToken ct);
}
