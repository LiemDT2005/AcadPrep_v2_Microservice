using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Web.Common;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public class ToeicApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private void AttachToken()
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("jwt");
        http.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrEmpty(token))
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<(AuthResponse? Data, string? Error)> RegisterAsync(string email, string password, string fullName)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/register", new { email, password, fullName });
        if (!res.IsSuccessStatusCode)
            return (null, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts), null);
    }

    public async Task<(AuthResponse? Data, string? Error)> LoginAsync(string email, string password)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/login", new { email, password });
        if (!res.IsSuccessStatusCode)
            return (null, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts), null);
    }

    public async Task<List<CourseListItem>> GetCoursesAsync()
    {
        AttachToken();
        return await http.GetFromJsonAsync<List<CourseListItem>>("/catalog/api/courses", JsonOpts) ?? [];
    }

    public async Task<CourseDetail?> GetCourseAsync(Guid id)
    {
        AttachToken();
        return await http.GetFromJsonAsync<CourseDetail>($"/catalog/api/courses/{id}", JsonOpts);
    }

    public async Task<GetExamListResponse?> GetExamListAsync(
        string? search,
        string? seriesName,
        string? year,
        int pageIndex = PaginationConstants.DefaultPageIndex,
        int pageSize = PaginationConstants.DefaultPageSize)
    {
        AttachToken();
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(seriesName) && !seriesName.Equals("All", StringComparison.OrdinalIgnoreCase))
            queryParams.Add($"seriesName={Uri.EscapeDataString(seriesName.Trim())}");
        if (!string.IsNullOrWhiteSpace(year) && !year.Equals("All", StringComparison.OrdinalIgnoreCase) && int.TryParse(year, out var yearVal))
            queryParams.Add($"year={yearVal}");
        queryParams.Add($"pageIndex={pageIndex}");
        queryParams.Add($"pageSize={pageSize}");

        var url = "/exam/api/exams" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
        try
        {
            return await http.GetFromJsonAsync<GetExamListResponse>(url, JsonOpts);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GetExamListAsync ERROR] {ex}");
            return null;
        }
    }

    public async Task<GetExamDetailDto?> GetExamDetailAsync(int id, int? userId = null)
    {
        AttachToken();
        var url = $"/exam/api/exams/{id}" + (userId.HasValue ? $"?userId={userId.Value}" : "");
        try
        {
            return await http.GetFromJsonAsync<GetExamDetailDto>(url, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ExamDetail?> GetExamAsync(Guid id)
    {
        AttachToken();
        return await Task.FromResult<ExamDetail?>(null);
    }

    public async Task<(SubmitExamResult? Data, string? Error)> SubmitExamAsync(Guid examId, object answers)
    {
        AttachToken();
        return await Task.FromResult<(SubmitExamResult?, string?)>((null, "Feature under development."));
    }

    public async Task<ProgressSummary?> GetProgressAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/progress/api/progress/me");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<ProgressSummary>(JsonOpts);
    }
}
