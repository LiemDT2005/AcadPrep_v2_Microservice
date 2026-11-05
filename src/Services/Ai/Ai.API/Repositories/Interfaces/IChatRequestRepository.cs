using Ai.API.Entities;

namespace Ai.API.Repositories.Interfaces;

// Chat request ledger: idempotency per RequestId, one request in flight per user, completed-token history.
public interface IChatRequestRepository
{
    // Tracked, so the claim/complete steps update the same row.
    Task<AiChatRequest?> FindAsync(Guid userId, Guid requestId, CancellationToken ct);
    // Marks this user's Processing requests whose lease ended as Failed ("LeaseExpired").
    Task ExpireLeasesAsync(Guid userId, DateTime now, CancellationToken ct);
    Task ReloadAsync(AiChatRequest request, CancellationToken ct);
    // False when another request of the user is in flight (filtered unique index) or the id was claimed concurrently.
    Task<bool> TryClaimAsync(AiChatRequest request, bool isNew, CancellationToken ct);
    // Saves the request and every other tracked change of this unit of work (e.g. a locked usage window).
    Task UpdateAsync(AiChatRequest request, CancellationToken ct);
    // Discards pending changes and stores the failure on a fresh copy of the row.
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct);
    // One transaction: new session (if any), both messages, new usage window (if any) and the completed request.
    Task CompleteAsync(AiChatRequest request, AiChatSession? newSession, IReadOnlyList<AiChatMessage> messages,
        AiUsageWindow? newWindow, CancellationToken ct);
    Task<IReadOnlyList<(DateTime CompletedAt, int Tokens)>> ListCompletedTokensSinceAsync(Guid userId, DateTime fromUtc,
        CancellationToken ct);
}
