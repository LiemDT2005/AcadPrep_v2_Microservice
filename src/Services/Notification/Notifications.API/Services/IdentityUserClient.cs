using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Notifications.API.Services.Interfaces;

namespace Notifications.API.Services;

public class IdentityOptions
{
    public const string SectionName = "Identity";

    // Identity base URL (local http://localhost:5001/, Docker http://identity.api:5001/). Required.
    public string? BaseUrl { get; set; }
}

public enum IdentityLookupStatus
{
    Found,
    NotFound,
    Unavailable
}

// Checks a recipient through Identity's existing Admin API (GET /api/users/{id}) with the calling Admin's JWT.
// Notification never reads the Identity database and Identity is not modified.
public class IdentityUserClient(HttpClient http, IHttpContextAccessor accessor, ILogger<IdentityUserClient> logger)
    : IIdentityUserClient
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<IdentityLookupStatus> FindUserAsync(Guid userId, CancellationToken ct)
    {
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (http.BaseAddress is null || string.IsNullOrWhiteSpace(authorization) ||
            !AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
            return IdentityLookupStatus.Unavailable;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/users/{userId:D}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return IdentityLookupStatus.NotFound;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Identity recipient lookup failed. Status={Status}", (int)response.StatusCode);
                return IdentityLookupStatus.Unavailable;
            }
            // Only the identifier is checked; no profile data is stored or logged.
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var parsed) &&
                parsed == userId
                ? IdentityLookupStatus.Found
                : IdentityLookupStatus.Unavailable;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
            or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Never log the token or the upstream body.
            logger.LogWarning("Identity recipient lookup unavailable. Reason={Reason}", exception.GetType().Name);
            return IdentityLookupStatus.Unavailable;
        }
    }
}
