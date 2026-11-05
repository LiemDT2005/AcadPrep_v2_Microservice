using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Storefront.Web.Services;

public record AiApiResult(int Status, JsonElement? Body = null)
{
    public bool Success => Status is >= 200 and < 300;
}

// Server-side client for ai-service through the Gateway (/ai/...). The session JWT never reaches JavaScript.
// Responses are relayed as JSON to the chat widget; upstream diagnostics are filtered by the caller.
public class AiApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    public static readonly TimeSpan ChatTimeout = TimeSpan.FromSeconds(100);

    public Task<AiApiResult> ChatAsync(object body, CancellationToken ct) => SendAsync(HttpMethod.Post, "/ai/api/ai/chat", body, ct);
    public Task<AiApiResult> UsageAsync(CancellationToken ct) => SendAsync(HttpMethod.Get, "/ai/api/ai/usage/me", null, ct);
    public Task<AiApiResult> DailyAsync(CancellationToken ct) => SendAsync(HttpMethod.Get, "/ai/api/ai/usage/me/daily", null, ct);
    public Task<AiApiResult> SessionsAsync(CancellationToken ct) => SendAsync(HttpMethod.Get, "/ai/api/ai/sessions?pageSize=30", null, ct);
    public Task<AiApiResult> MessagesAsync(Guid sessionId, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"/ai/api/ai/sessions/{sessionId:D}/messages?pageSize=100", null, ct);
    public Task<AiApiResult> DeleteSessionAsync(Guid sessionId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"/ai/api/ai/sessions/{sessionId:D}", null, ct);

    // UC-22.2 (Admin page).
    public Task<AiApiResult> AdminUsageAsync(Guid? userId, int pageIndex, CancellationToken ct) =>
        SendAsync(HttpMethod.Get, $"/ai/api/admin/ai/usage?pageIndex={pageIndex}&pageSize=20" +
            (userId is { } id ? $"&userId={id:D}" : string.Empty), null, ct);
    public Task<AiApiResult> AdminLimitsAsync(CancellationToken ct) => SendAsync(HttpMethod.Get, "/ai/api/admin/ai/limits", null, ct);
    public Task<AiApiResult> UpdateLimitAsync(string planCode, long tokenLimit, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"/ai/api/admin/ai/limits/{Uri.EscapeDataString(planCode)}", new { tokenLimit }, ct);
    public Task<AiApiResult> ResetUsageAsync(Guid userId, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"/ai/api/admin/ai/usage/{userId:D}/reset", null, ct);

    private async Task<AiApiResult> SendAsync(HttpMethod method, string uri, object? body, CancellationToken ct)
    {
        var token = accessor.HttpContext?.Session.GetString("jwt");
        if (string.IsNullOrWhiteSpace(token)) return new(401);
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (response.Content.Headers.ContentLength == 0 || status == 204) return new(status);
            try { return new(status, await response.Content.ReadFromJsonAsync<JsonElement>(ct)); }
            catch (JsonException) { return new(status is >= 200 and < 300 ? 502 : status); }
        }
        catch (HttpRequestException) { return new(503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(504); }
    }
}
