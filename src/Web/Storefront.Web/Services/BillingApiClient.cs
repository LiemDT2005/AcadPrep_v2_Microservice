using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public class BillingApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly string[] GrantFields = ["requestId", "userId", "planId", "durationDays", "note"];

    // Pricing is public. Private endpoints attach the authenticated session token per request.
    public async Task<BillingApiResult<IReadOnlyList<BillingPlan>>> GetPlansAsync(CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync("/billing/api/plans", ct);
            if (!response.IsSuccessStatusCode) return new((int)response.StatusCode);
            var items = await response.Content.ReadFromJsonAsync<List<BillingPlan>>(ct);
            return items is null ? new(502) : new((int)response.StatusCode, items);
        }
        catch (HttpRequestException) { return new(503); }
        catch (JsonException) { return new(502); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(504); }
    }

    public Task<BillingApiResult<BillingSubscription>> GetMySubscriptionAsync(CancellationToken ct) =>
        SendAsync<BillingSubscription>(HttpMethod.Get, "/billing/api/subscriptions/me", null, ct);

    public Task<BillingApiResult<BillingUsage>> GetMyUsageAsync(CancellationToken ct) =>
        SendAsync<BillingUsage>(HttpMethod.Get, "/billing/api/subscriptions/me/usage", null, ct);

    // UC-21.2 / UC-23.3. The browser IP is forwarded for vnp_IpAddr (the Gateway appends its own hop).
    public Task<BillingApiResult<BillingCheckoutResult>> CheckoutAsync(Guid planId, bool renew, CancellationToken ct) =>
        SendAsync<BillingCheckoutResult>(HttpMethod.Post,
            renew ? "/billing/api/subscriptions/renew" : "/billing/api/orders/checkout", new { planId }, ct,
            forwardClientIp: true);

    public Task<BillingApiResult<BillingPage<BillingOrderItem>>> GetMyOrdersAsync(string? status, int pageIndex,
        CancellationToken ct) =>
        SendAsync<BillingPage<BillingOrderItem>>(HttpMethod.Get, "/billing/api/orders/me" + Query(
            status is null ? [] : [new KeyValuePair<string, string>("status", status)],
            new KeyValuePair<string, string>("pageIndex", pageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))), null, ct);

    public Task<BillingApiResult<BillingOrderDetail>> GetMyOrderAsync(string orderCode, CancellationToken ct) =>
        SendAsync<BillingOrderDetail>(HttpMethod.Get, $"/billing/api/orders/{Uri.EscapeDataString(orderCode)}", null, ct);

    public Task<BillingApiResult<BillingPage<BillingAdminOrder>>> GetAdminOrdersAsync(BillingOrderFilter filter,
        CancellationToken ct)
    {
        var parameters = filter.ApiParameters().ToList();
        if (filter.From is { } from) parameters.Add(new("from", from.ToString("yyyy-MM-dd")));
        if (filter.To is { } to) parameters.Add(new("to", to.ToString("yyyy-MM-dd")));
        parameters.Add(new("sortBy", filter.SortBy));
        parameters.Add(new("sortDir", filter.SortDir));
        parameters.Add(new("pageIndex", filter.PageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return SendAsync<BillingPage<BillingAdminOrder>>(HttpMethod.Get, "/billing/api/admin/orders" + Query(parameters), null, ct);
    }

    // CSV is negotiated by the Billing API's custom output formatter (Accept: text/csv), one month per file.
    public Task<BillingApiResult<BillingCsvFile>> DownloadAdminOrdersCsvAsync(BillingOrderFilter filter,
        CancellationToken ct)
    {
        var parameters = filter.ApiParameters().ToList();
        parameters.Add(new("month", filter.Month ?? string.Empty));
        return DownloadCsvAsync("/billing/api/admin/orders" + Query(parameters), $"orders-{filter.Month}.csv", ct);
    }

    public Task<BillingApiResult<BillingRevenue>> GetRevenueAsync(BillingRevenueFilter filter, CancellationToken ct) =>
        SendAsync<BillingRevenue>(HttpMethod.Get, "/billing/api/admin/revenue" + Query(filter.ApiParameters()), null, ct);

    public Task<BillingApiResult<BillingCsvFile>> DownloadRevenueCsvAsync(BillingRevenueFilter filter, CancellationToken ct) =>
        DownloadCsvAsync("/billing/api/admin/revenue" + Query(filter.ApiParameters()), "revenue.csv", ct);

    private async Task<BillingApiResult<BillingCsvFile>> DownloadCsvAsync(string uri, string fallbackName, CancellationToken ct)
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("jwt");
        if (string.IsNullOrWhiteSpace(token)) return new(401);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new((int)response.StatusCode);
            if (response.Content.Headers.ContentType?.MediaType != "text/csv") return new(502);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var name = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? fallbackName;
            return new((int)response.StatusCode, new BillingCsvFile(bytes, Path.GetFileName(name)));
        }
        catch (HttpRequestException) { return new(503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(504); }
    }

    private static string Query(IEnumerable<KeyValuePair<string, string>> parameters, params KeyValuePair<string, string>[] more)
    {
        var all = parameters.Concat(more).Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}").ToArray();
        return all.Length == 0 ? string.Empty : "?" + string.Join('&', all);
    }

    public Task<BillingApiResult<BillingGrantResult>> GrantSubscriptionAsync(BillingGrantInput input, CancellationToken ct)
    {
        // Only whitelisted fields are sent; the Admin identity always comes from the session token.
        return SendAsync<BillingGrantResult>(HttpMethod.Post, "/billing/api/admin/subscriptions/grant", new
        {
            requestId = input.RequestId,
            userId = Guid.Parse(input.UserId!),
            planId = input.PlanId,
            durationDays = input.DurationDays,
            note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim()
        }, ct);
    }

    private async Task<BillingApiResult<T>> SendAsync<T>(HttpMethod method, string uri, object? body, CancellationToken ct,
        bool forwardClientIp = false)
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("jwt");
        if (string.IsNullOrWhiteSpace(token)) return new(401);
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (forwardClientIp && httpContextAccessor.HttpContext?.Connection.RemoteIpAddress is { } ip)
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", ip.ToString());
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                // Keep only known field keys. Never expose raw upstream diagnostics or messages.
                var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                if (status == 400)
                {
                    // A non-JSON error body keeps its status; field errors are optional detail.
                    try
                    {
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        if (json.RootElement.ValueKind == JsonValueKind.Object &&
                            json.RootElement.TryGetProperty("errors", out var fields) && fields.ValueKind == JsonValueKind.Object)
                            foreach (var field in fields.EnumerateObject())
                                if (GrantFields.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                                    errors[field.Name] = ["Giá trị không hợp lệ. Vui lòng kiểm tra lại."];
                    }
                    catch (JsonException) { }
                }
                string? code = null;
                if (status is 409 or 422)
                {
                    try
                    {
                        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        if (json.RootElement.ValueKind == JsonValueKind.Object &&
                            json.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                            code = title.GetString();
                    }
                    catch (JsonException) { }
                }
                return new(status, Errors: errors, Code: code);
            }
            var data = await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct);
            return data is null ? new(502) : new(status, data);
        }
        catch (HttpRequestException) { return new(503); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return new(504); }
        catch (JsonException) { return new(502); }
    }
}
