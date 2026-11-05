using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Billing.API.Services.Interfaces;

namespace Billing.API.Services;

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

public record IdentityUserInfo(IReadOnlyList<string> Roles, string Status, string? Email);

public record IdentityLookup(IdentityLookupStatus Status, IdentityUserInfo? User = null);

// Reads a user through Identity's existing Admin API (GET /api/users/{id}) with the calling Admin's JWT.
// Billing never reads the Identity database and never uses its own credentials for this call.
public class IdentityUserClient(HttpClient http, IHttpContextAccessor accessor, ILogger<IdentityUserClient> logger)
    : IIdentityUserClient
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IdentityLookup> GetUserAsync(Guid userId, CancellationToken ct)
    {
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (http.BaseAddress is null || string.IsNullOrWhiteSpace(authorization) ||
            !AuthenticationHeaderValue.TryParse(authorization, out var header) ||
            !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter))
            return new(IdentityLookupStatus.Unavailable);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/users/{userId:D}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", header.Parameter);
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return new(IdentityLookupStatus.NotFound);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Identity user lookup failed. Status={Status}", (int)response.StatusCode);
                return new(IdentityLookupStatus.Unavailable);
            }
            var body = await response.Content.ReadFromJsonAsync<IdentityUserResponse>(JsonOpts, ct);
            if (body?.Roles is null || string.IsNullOrWhiteSpace(body.Status) ||
                !Guid.TryParse(body.Id, out var id) || id != userId)
                return new(IdentityLookupStatus.Unavailable);
            return new(IdentityLookupStatus.Found, new IdentityUserInfo(body.Roles, body.Status,
                string.IsNullOrWhiteSpace(body.Email) ? null : body.Email));
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException
            or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Never log the token or the upstream body.
            logger.LogWarning("Identity user lookup unavailable. Reason={Reason}", exception.GetType().Name);
            return new(IdentityLookupStatus.Unavailable);
        }
    }

    private sealed record IdentityUserResponse(string? Id, string? Email, string? Status, List<string>? Roles);
}
