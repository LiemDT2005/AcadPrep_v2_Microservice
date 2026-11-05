using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Storefront.Web.Models;
using Storefront.Web.Services;

namespace Notifications.UI.Tests;

public class NotificationUiFactory : WebApplicationFactory<ToeicApiClient>
{
    public NotificationBackend Backend { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // WebApplicationFactory resolves the referenced app's content root from its build manifest.
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<ToeicApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
            services.AddHttpClient<BillingApiClient>().ConfigurePrimaryHttpMessageHandler(() => Backend);
        });
    }
}

// Test-only upstream simulator; production uses Gateway and Identity APIs.
public class NotificationBackend : HttpMessageHandler
{
    public static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid NotificationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public string[] Roles { get; set; } = ["Learner"];
    public int IdentityStatus { get; set; } = 200;
    public int ListStatus { get; set; } = 200;
    public int CountStatus { get; set; } = 200;
    public int WriteStatus { get; set; } = 204;
    public int CreateStatus { get; set; } = 201;
    public bool Empty { get; set; }
    public NotificationPage? PageOverride { get; set; }
    public ConcurrentQueue<(string Method, string Path, bool Bearer)> Calls { get; } = new();
    public string? CreatedBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery;
        Calls.Enqueue((request.Method.Method, path, request.Headers.Authorization?.Scheme == "Bearer" &&
            request.Headers.Authorization.Parameter == "test-session-token"));
        if (path == "/identity/api/auth/login") return Json(200, new AuthResponse("test-session-token", "test@example.test",
            "Test User", UserId.ToString(), DateTime.UtcNow.AddHours(1), Roles.ToList()));
        if (path == "/identity/api/auth/me") return Json(IdentityStatus, new UserProfileDto(UserId.ToString(),
            "test@example.test", "Test User", null, "Active", Roles.ToList(), DateTime.UtcNow, null));
        if (path.EndsWith("unread-count")) return Json(CountStatus, new NotificationUnreadCount(5));
        if (path == "/billing/api/subscriptions/me") return Json(200, new BillingSubscription("Free", "FREE", "Free", false, null, null, null));
        if (path == "/progress/api/progress/me") return Json(404, new { });
        // Identity's existing Admin user search, used by the recipient picker.
        if (request.Method == HttpMethod.Get && path.StartsWith("/identity/api/users?"))
            return Json(200, new UserListResponse([new UserListItemDto(UserId.ToString(), "learner@example.test",
                "<b>Learner</b>", null, "Active", "Local", DateTime.UtcNow, null, ["Learner"])], 1, 1, 8, 1));
        if (request.Method == HttpMethod.Get && path.StartsWith("/notification/api/notifications?"))
            return Json(ListStatus, PageOverride ?? new NotificationPage(Empty ? [] : [new NotificationItem(NotificationId,
                "<script>alert(1)</script>", "<img src=x onerror=alert(2)>", "System",
                new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero), false, null)], 2, 20, Empty ? 0 : 45, Empty ? 0 : 3));
        if (request.Method == HttpMethod.Post && path == "/notification/api/admin/notifications")
        {
            CreatedBody = await request.Content!.ReadAsStringAsync(ct);
            return CreateStatus == 201 ? Json(201, new NotificationCreated(NotificationId)) : Json(CreateStatus,
                new { errors = new { targetUserId = new[] { "internal-diagnostic-must-not-leak" } } });
        }
        if (request.Method == HttpMethod.Patch || request.Method == HttpMethod.Delete) return Json(WriteStatus, new { });
        throw new InvalidOperationException("Unexpected upstream route in UI test.");
    }

    private static HttpResponseMessage Json(int status, object data) => new((HttpStatusCode)status)
        { Content = JsonContent.Create(data) };
    protected override void Dispose(bool disposing) { }
}
