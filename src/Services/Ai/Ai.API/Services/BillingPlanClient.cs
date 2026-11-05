using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ai.API.Services.Interfaces;

namespace Ai.API.Services;

public class BillingOptions
{
    public const string SectionName = "Billing";
    public string? BaseUrl { get; set; }
}


// Approved policy (Huy, 2026-11-05): if Billing cannot answer, the learner is treated as FREE — never more.
public class BillingPlanClient(HttpClient http, IHttpContextAccessor accessor, ILogger<BillingPlanClient> logger) : IPlanResolver
{
    public const string FreePlan = "FREE";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private static readonly string[] Plans = ["FREE", "PRO_MONTH", "PRO_QUARTER"];

    public async Task<string> ResolveAsync(CancellationToken ct)
    {
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (http.BaseAddress is null || !AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(header.Parameter))
            return FreePlan;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/subscriptions/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Billing plan lookup failed; treating as FREE. Status={Status}", (int)response.StatusCode);
                return FreePlan;
            }
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var code = json.TryGetProperty("planCode", out var value) ? value.GetString() : null;
            return code is not null && Plans.Contains(code) ? code : FreePlan;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException
            && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Billing plan lookup unavailable; treating as FREE. Reason={Reason}", exception.GetType().Name);
            return FreePlan;
        }
    }
}
