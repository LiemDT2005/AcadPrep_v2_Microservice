using Ai.API.Services;

namespace Ai.API.Services.Interfaces;

public interface IAiClient
{
    bool IsConfigured { get; }
    // Exact input size from Gemini countTokens; null when it could not be determined.
    Task<int?> CountTokensAsync(AiPrompt prompt, CancellationToken ct);
    // Throws AiProviderException after the configured retries for transient errors (429, 5xx, timeout).
    Task<AiGeneration> GenerateAsync(AiPrompt prompt, int maxOutputTokens, CancellationToken ct);
}
