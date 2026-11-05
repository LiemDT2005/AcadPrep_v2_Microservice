namespace Ai.API.Services.Interfaces;

public interface IPlanResolver
{
    // The caller's effective plan code from Billing (GET /api/subscriptions/me with the caller's JWT).
    Task<string> ResolveAsync(CancellationToken ct);
}
