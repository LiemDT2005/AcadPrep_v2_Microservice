using Ai.API.DTOs;
using Ai.API.Entities;
using Ai.API.Entities.Enums;
using Ai.API.Repositories.Interfaces;
using Ai.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Ai.API.Services;

// UC-22.1 chat with the approved quota rule (Huy, 2026-11-05):
// - already at/over the limit or locked: rejected before calling Gemini (nothing charged);
// - the exact input (countTokens) leaves at least 1 token: answer in full, charge totalTokenCount (may overshoot),
//   the window then locks naturally;
// - the input alone uses up the remaining budget: no generation, window locked until reset, nothing charged.
// RequestId makes every send idempotent; at most one request per user is processed at a time.
public class ChatService(IChatRequestRepository requests, IChatSessionRepository sessions, IAiClient ai,
    IPlanResolver plans, IUsageService usage, IOptions<TutorOptions> tutorOptions, IOptions<GeminiOptions> geminiOptions,
    ILogger<ChatService> logger) : IChatService
{
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private TutorOptions Tutor => tutorOptions.Value;

    public async Task<ChatResult> SendAsync(Guid userId, ChatRequestDto dto, CancellationToken ct)
    {
        var now = usage.Now;
        var requestId = dto.RequestId!.Value;
        var message = dto.Message!;

        var existing = await requests.FindAsync(userId, requestId, ct);
        if (existing is { Status: ChatRequestStatus.Completed }) return await ReplayAsync(userId, existing, ct);
        if (existing is { Status: ChatRequestStatus.Processing } && existing.LeaseUntil > now)
            return new ChatResult(ChatOutcome.InProgress, Message: "This message is still being answered.");

        AiChatSession? session = null;
        var sessionId = dto.SessionId ?? existing?.SessionId;
        if (sessionId is { } id)
        {
            session = await sessions.GetOwnedForUpdateAsync(userId, id, ct);
            if (session is null) return new ChatResult(ChatOutcome.NotFound, Message: "Session not found.");
        }

        var request = await ClaimAsync(userId, requestId, existing, session?.Id, now, ct);
        if (request is null) return new ChatResult(ChatOutcome.InProgress, Message: "Another message is still being answered.");

        try
        {
            var plan = await plans.ResolveAsync(ct);
            var limit = await usage.GetLimitAsync(plan, ct);
            if (!ai.IsConfigured) throw new AiConfigurationException("Gemini is not configured.");

            var window = await usage.GetCurrentWindowAsync(userId, now, track: true, ct);
            if (window is not null && (window.Locked || window.TokensUsed >= limit))
                return await RejectAsync(request, plan, limit, window, ct);

            var history = session is null ? [] : await HistoryAsync(session.Id, ct);
            var prompt = new AiPrompt(TutorPrompt.System, [.. history, new TutorTurn("user", message)]);

            string reply;
            var outOfScope = false;
            var tokens = 0;
            int? inputTokens = null;
            if (TutorGuardrails.IsOutOfScopeSubject(message))
            {
                // Ported FTOEIC behaviour: other school subjects are redirected without calling Gemini.
                reply = HasVietnamese(message)
                    ? TutorGuardrails.OutOfScopeRedirectVietnamese : TutorGuardrails.OutOfScopeRedirectEnglish;
                outOfScope = true;
            }
            else
            {
                inputTokens = await ai.CountTokensAsync(prompt, ct) ?? Estimate(prompt);
                var remaining = limit - (window?.TokensUsed ?? 0);
                if (inputTokens >= remaining)
                {
                    if (window is not null) window.Locked = true;
                    return await RejectAsync(request, plan, limit, window, ct);
                }

                var generation = await ai.GenerateAsync(prompt, geminiOptions.Value.MaxOutputTokens, ct);
                tokens = generation.TotalTokens > 0 ? generation.TotalTokens : inputTokens.Value;
                (reply, outOfScope) = Finalize(generation, message, history);
            }

            return await CompleteAsync(request, userId, session, message, reply, outOfScope, tokens, inputTokens, plan, limit, window, ct);
        }
        catch (AiConfigurationException exception)
        {
            await FailAsync(request, "Configuration", ct);
            logger.LogError("AI chat unavailable: {Reason}", exception.Message);
            return new ChatResult(ChatOutcome.Unavailable, Message: "The AI tutor is not configured.");
        }
        catch (AiProviderException exception)
        {
            // Nothing stored, nothing charged; the same RequestId can be retried.
            await FailAsync(request, exception.Message, ct);
            return new ChatResult(ChatOutcome.ProviderFailed, Message: "The AI tutor is busy. Please try again.");
        }
    }

    private async Task<AiChatRequest?> ClaimAsync(Guid userId, Guid requestId, AiChatRequest? existing, Guid? sessionId,
        DateTime now, CancellationToken ct)
    {
        // A crashed request must not block the user forever.
        await requests.ExpireLeasesAsync(userId, now, ct);
        if (existing is not null) await requests.ReloadAsync(existing, ct);

        var request = existing ?? new AiChatRequest { UserId = userId, RequestId = requestId, CreatedAt = now };
        request.Status = ChatRequestStatus.Processing;
        request.SessionId = sessionId;
        request.LeaseUntil = now + Lease;
        request.Error = null;
        request.Version = Guid.NewGuid();
        // Another request of this user in flight, or this id claimed concurrently: not claimed.
        return await requests.TryClaimAsync(request, isNew: existing is null, ct) ? request : null;
    }

    private async Task<IReadOnlyList<TutorTurn>> HistoryAsync(Guid sessionId, CancellationToken ct)
    {
        var recent = await sessions.ListRecentMessagesAsync(sessionId, Tutor.ContextMessages, ct);
        return recent.AsEnumerable().Reverse()
            .Select(item => new TutorTurn(item.Role == ChatRole.User ? "user" : "model", item.Content)).ToArray();
    }

    private (string Reply, bool OutOfScope) Finalize(AiGeneration generation, string message, IReadOnlyList<TutorTurn> history)
    {
        if (generation.Blocked || string.IsNullOrWhiteSpace(generation.Text)) return (Tutor.BlockedMessage, true);
        if (generation.Text.Trim().Trim('.', '*', '`', '"').Equals(TutorPrompt.OutOfScopeMarker, StringComparison.OrdinalIgnoreCase))
            return (Tutor.OutOfScopeMessage, true);
        // Ported answer-leak guard: never hand over a multiple-choice answer directly.
        return TutorGuardrails.IsAnswerLeak(message, history, generation.Text)
            ? (TutorGuardrails.GeneratePedagogicalHint(message), false)
            : (generation.Text, false);
    }

    private async Task<ChatResult> CompleteAsync(AiChatRequest request, Guid userId, AiChatSession? session, string message,
        string reply, bool outOfScope, int tokens, int? inputTokens, string plan, long limit, AiUsageWindow? window,
        CancellationToken ct)
    {
        var now = usage.Now;
        AiChatSession? newSession = null;
        if (session is null)
        {
            session = newSession = new AiChatSession
            {
                UserId = userId, Title = message.Length <= 50 ? message : message[..50], CreatedAt = now, UpdatedAt = now
            };
        }
        session.UpdatedAt = now;
        var question = new AiChatMessage { SessionId = session.Id, Role = ChatRole.User, Content = message, CreatedAt = now };
        var answer = new AiChatMessage
        {
            SessionId = session.Id, Role = ChatRole.Model, Content = reply, TotalTokens = tokens, IsOutOfScope = outOfScope,
            CreatedAt = now.AddTicks(1)
        };

        AiUsageWindow? newWindow = null;
        if (tokens > 0)
        {
            if (window is null)
            {
                // The first successfully answered chat opens the rolling window.
                window = newWindow = new AiUsageWindow
                {
                    UserId = userId, WindowStart = now, WindowEnd = now.AddHours(Tutor.WindowHours), TokensUsed = tokens,
                    PlanCode = plan, UpdatedAt = now
                };
            }
            else
            {
                window.TokensUsed += tokens;
                window.PlanCode = plan;
                window.UpdatedAt = now;
            }
        }

        request.Status = ChatRequestStatus.Completed;
        request.SessionId = session.Id;
        request.ReplyMessageId = answer.Id;
        request.TotalTokens = tokens;
        request.InputTokens = inputTokens;
        request.CompletedAt = now;
        request.Version = Guid.NewGuid();
        await requests.CompleteAsync(request, newSession, [question, answer], newWindow, ct);
        logger.LogInformation("AI chat answered. Length={Length} Tokens={Tokens}", message.Length, tokens);
        return new ChatResult(ChatOutcome.Ok, new ChatResponseDto(request.RequestId, session.Id, session.Title, reply,
            outOfScope, tokens, UsageService.View(plan, limit, window), false));
    }

    private async Task<ChatResult> RejectAsync(AiChatRequest request, string plan, long limit, AiUsageWindow? window,
        CancellationToken ct)
    {
        request.Status = ChatRequestStatus.Rejected;
        request.Error = "QuotaExceeded";
        request.Version = Guid.NewGuid();
        // Also saves the tracked window when it was just locked.
        await requests.UpdateAsync(request, ct);
        var view = UsageService.View(plan, limit, window);
        // Without a window the input alone exceeds a full budget: nothing to lock, ask for a shorter message.
        return new ChatResult(ChatOutcome.QuotaExceeded, Usage: view with { Locked = window is not null || view.Locked },
            Message: window is null ? "MessageTooLarge" : "QuotaExceeded");
    }

    private Task FailAsync(AiChatRequest request, string reason, CancellationToken ct) =>
        requests.MarkFailedAsync(request.Id, reason.Length > 200 ? reason[..200] : reason, ct);

    private async Task<ChatResult> ReplayAsync(Guid userId, AiChatRequest request, CancellationToken ct)
    {
        var answer = await sessions.GetMessageAsync(request.ReplyMessageId!.Value, ct);
        var session = await sessions.GetAsync(request.SessionId!.Value, ct);
        var window = await usage.GetCurrentWindowAsync(userId, usage.Now, track: false, ct);
        var plan = window?.PlanCode ?? BillingPlanClient.FreePlan;
        var limit = await usage.GetLimitAsync(plan, ct);
        return new ChatResult(ChatOutcome.Ok, new ChatResponseDto(request.RequestId, session.Id, session.Title, answer.Content,
            answer.IsOutOfScope, request.TotalTokens ?? 0, UsageService.View(plan, limit, window), true));
    }

    // Conservative fallback when countTokens is unavailable (over-estimates rather than under-estimates).
    public static int Estimate(AiPrompt prompt) =>
        (int)Math.Ceiling((prompt.SystemInstruction.Length + prompt.Turns.Sum(turn => turn.Content.Length)) / 2.5) + 16;

    private static bool HasVietnamese(string text) => text.Any(c => "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ"
        .Contains(char.ToLowerInvariant(c)));
}
