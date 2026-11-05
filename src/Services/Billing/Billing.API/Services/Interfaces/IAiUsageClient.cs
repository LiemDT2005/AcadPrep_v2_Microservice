using Billing.API.DTOs;

namespace Billing.API.Services.Interfaces;

public interface IAiUsageClient
{
    // Never throws for upstream failures; returns Available = false instead.
    Task<AiUsageDto> GetMyUsageAsync(CancellationToken ct);
}
