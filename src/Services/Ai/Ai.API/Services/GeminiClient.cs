using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ai.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace Ai.API.Services;

// Gemini:ApiKey comes only from user-secrets/environment; it is sent as the x-goog-api-key header and is never
// logged, returned or written to the URL.
public class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";
    public string Model { get; set; } = "gemini-3.5-flash-lite";
    public int MaxOutputTokens { get; set; } = 1024;
    public double Temperature { get; set; } = 0.3;
    public double TopP { get; set; } = 0.9;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 1000;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}

public record AiPrompt(string SystemInstruction, IReadOnlyList<TutorTurn> Turns);

public record AiGeneration(string Text, int TotalTokens, bool Blocked);

public sealed class AiProviderException(string reason) : Exception(reason);


public class GeminiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger) : IAiClient
{
    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<int?> CountTokensAsync(AiPrompt prompt, CancellationToken ct)
    {
        var settings = options.Value;
        var body = new JsonObject
        {
            ["generateContentRequest"] = new JsonObject
            {
                ["model"] = $"models/{settings.Model}",
                ["systemInstruction"] = SystemInstruction(prompt),
                ["contents"] = Contents(prompt)
            }
        };
        try
        {
            using var response = await SendOnceAsync("countTokens", body, ct);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return json.TryGetProperty("totalTokens", out var total) && total.TryGetInt32(out var value) ? value : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException
            && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Gemini countTokens unavailable. Reason={Reason}", exception.GetType().Name);
            return null;
        }
    }

    public async Task<AiGeneration> GenerateAsync(AiPrompt prompt, int maxOutputTokens, CancellationToken ct)
    {
        var settings = options.Value;
        var body = new JsonObject
        {
            ["systemInstruction"] = SystemInstruction(prompt),
            ["contents"] = Contents(prompt),
            ["generationConfig"] = new JsonObject
            {
                ["maxOutputTokens"] = maxOutputTokens, ["temperature"] = settings.Temperature, ["topP"] = settings.TopP
            }
        };
        for (var attempt = 0; ; attempt++)
        {
            string reason;
            try
            {
                using var response = await SendOnceAsync("generateContent", body, ct);
                if (response.IsSuccessStatusCode)
                    return Parse(await response.Content.ReadFromJsonAsync<JsonElement>(ct));
                reason = $"HTTP {(int)response.StatusCode}";
                if (!IsTransient(response.StatusCode)) throw new AiProviderException(reason);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested) { reason = "Timeout"; }
            catch (HttpRequestException) { reason = "Network"; }
            catch (JsonException) { throw new AiProviderException("InvalidResponse"); }

            logger.LogWarning("Gemini generateContent attempt {Attempt} failed. Reason={Reason}", attempt + 1, reason);
            if (attempt >= settings.MaxRetries) throw new AiProviderException(reason);
            if (settings.RetryDelayMilliseconds > 0)
                await Task.Delay(settings.RetryDelayMilliseconds * (attempt + 1), ct);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(string method, JsonObject body, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) throw new AiProviderException("NotConfigured");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(settings.BaseUrl), $"models/{Uri.EscapeDataString(settings.Model)}:{method}"))
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-goog-api-key", settings.ApiKey);
        return await http.SendAsync(request, timeout.Token);
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || (int)status >= 500;

    // User text only ever goes into user turns; the system prompt is a separate systemInstruction.
    private static JsonObject SystemInstruction(AiPrompt prompt) => new()
    {
        ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt.SystemInstruction })
    };

    private static JsonArray Contents(AiPrompt prompt)
    {
        var contents = new JsonArray();
        foreach (var turn in prompt.Turns.SkipWhile(turn => turn.Role != "user"))
            contents.Add(new JsonObject
            {
                ["role"] = turn.Role == "user" ? "user" : "model",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = turn.Content })
            });
        return contents;
    }

    private static AiGeneration Parse(JsonElement json)
    {
        var blocked = json.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _);
        var text = string.Empty;
        if (json.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("finishReason", out var finish) &&
                finish.GetString() is "SAFETY" or "PROHIBITED_CONTENT" or "BLOCKLIST" or "SPII")
                blocked = true;
            if (candidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
                text = string.Concat(parts.EnumerateArray()
                    .Where(part => !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
                    .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null));
        }
        // usageMetadata.totalTokenCount is the source of truth (input + output + any other reported tokens).
        var total = 0;
        if (json.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.TryGetProperty("totalTokenCount", out var totalCount) && totalCount.TryGetInt32(out var value)) total = value;
            else total = Sum(usage, "promptTokenCount", "candidatesTokenCount", "thoughtsTokenCount");
        }
        return new AiGeneration(text.Trim(), total, blocked);
    }

    private static int Sum(JsonElement usage, params string[] names) =>
        names.Sum(name => usage.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0);
}
