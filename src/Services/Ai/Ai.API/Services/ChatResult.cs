using Ai.API.DTOs;

namespace Ai.API.Services;

public enum ChatOutcome { Ok, NotFound, InProgress, QuotaExceeded, Unavailable, ProviderFailed }

public record ChatResult(ChatOutcome Outcome, ChatResponseDto? Response = null, UsageDto? Usage = null, string? Message = null);
