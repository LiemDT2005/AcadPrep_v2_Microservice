using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Billing.API.DTOs;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

public class AiUsageOptions
{
    public const string SectionName = "AiService";

    // Internal ai-service base URL (e.g. http://ai.api:8080/). Empty means AI is not deployed:
    // usage is reported unavailable without any call, never as zero.
    public string? BaseUrl { get; set; }
}

// Calls ai-service GET /api/ai/usage/me with the caller's own JWT. Billing never reads the AI database.
public class AiUsageClient(HttpClient http, IHttpContextAccessor accessor, ILogger<AiUsageClient> logger) : IAiUsageClient
{
    public const string UsagePath = "api/ai/usage/me";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<AiUsageDto> GetMyUsageAsync(CancellationToken ct)
    {
        if (http.BaseAddress is null) return AiUsageDto.Unavailable;
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
            return AiUsageDto.Unavailable;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UsagePath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI usage unavailable. Status={Status}", (int)response.StatusCode);
                return AiUsageDto.Unavailable;
            }
            var body = await response.Content.ReadFromJsonAsync<AiUsageResponse>(JsonOpts, ct);
            if (body is null || body.TokensUsed < 0 || body.TokenLimit < 0 || body.Remaining < 0)
                return AiUsageDto.Unavailable;
            return new AiUsageDto(true, body.TokenLimit, body.TokensUsed, body.Remaining,
                body.ResetAt?.ToUniversalTime());
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Never log the token or the upstream body.
            logger.LogWarning("AI usage unavailable. Reason={Reason}", exception.GetType().Name);
            return AiUsageDto.Unavailable;
        }
    }

    private sealed record AiUsageResponse(long? TokenLimit, [property: JsonRequired] long TokensUsed, long? Remaining,
        string? PeriodKey, DateTime? ResetAt);
}
