using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public partial class ToeicApiClient
{
    public Task<NotificationApiResult<NotificationPage>> GetNotificationsAsync(int pageIndex, int pageSize,
        bool unreadOnly, CancellationToken ct) => SendNotificationAsync<NotificationPage>(HttpMethod.Get,
        $"/notification/api/notifications?pageIndex={pageIndex}&pageSize={pageSize}&unreadOnly={unreadOnly.ToString().ToLowerInvariant()}", null, ct);

    public Task<NotificationApiResult<NotificationUnreadCount>> GetNotificationCountAsync(CancellationToken ct) =>
        SendNotificationAsync<NotificationUnreadCount>(HttpMethod.Get, "/notification/api/notifications/unread-count", null, ct);

    public Task<NotificationApiResult<object>> ReadNotificationAsync(Guid id, CancellationToken ct) =>
        SendNotificationAsync<object>(HttpMethod.Patch, $"/notification/api/notifications/{id}/read", null, ct);

    public Task<NotificationApiResult<object>> DeleteNotificationAsync(Guid id, CancellationToken ct) =>
        SendNotificationAsync<object>(HttpMethod.Delete, $"/notification/api/notifications/{id}", null, ct);

    public Task<NotificationApiResult<NotificationCreated>> CreateNotificationAsync(NotificationInput input, CancellationToken ct)
    {
        Guid? targetUserId = null;
        if (!string.IsNullOrEmpty(input.TargetUserId))
        {
            if (!Guid.TryParse(input.TargetUserId, out var parsedId) || parsedId == Guid.Empty)
                return Task.FromResult(new NotificationApiResult<NotificationCreated>(400,
                    Errors: new Dictionary<string, string[]>
                    {
                        ["targetUserId"] = ["ID người nhận phải là GUID khác rỗng."]
                    }));
            targetUserId = parsedId;
        }
        return SendNotificationAsync<NotificationCreated>(HttpMethod.Post, "/notification/api/admin/notifications", new
        {
            title = input.Title, content = input.Content, audience = input.Audience,
            targetUserId
        }, ct);
    }

    // Verify the session token and roles at Identity, never by decoding unvalidated JWT claims.
    public Task<NotificationApiResult<UserProfileDto>> GetNotificationIdentityAsync(CancellationToken ct) =>
        SendNotificationAsync<UserProfileDto>(HttpMethod.Get, "/identity/api/auth/me", null, ct);

    private async Task<NotificationApiResult<T>> SendNotificationAsync<T>(HttpMethod method, string uri, object? body,
        CancellationToken ct)
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("jwt");
        if (string.IsNullOrWhiteSpace(token)) return new(401);
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                // Keep only known field keys. Never expose raw upstream diagnostics or field messages.
                var errors = new Dictionary<string, string[]>();
                if (status is 400 or 422)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                    if (json.RootElement.TryGetProperty("errors", out var fields) && fields.ValueKind == JsonValueKind.Object)
                        foreach (var field in fields.EnumerateObject())
                            if (new[] { "title", "content", "audience", "targetUserId" }.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                                errors[field.Name] = ["Giá trị không hợp lệ. Vui lòng kiểm tra lại."];
                }
                return new(status, Errors: errors);
            }
            if (status == 204) return new(status);
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
            return data is null ? new(502) : new(status, data);
        }
        catch (HttpRequestException) { return new(503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(504); }
        catch (JsonException) { return new(502); }
    }
}
