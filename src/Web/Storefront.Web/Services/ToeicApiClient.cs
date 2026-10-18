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

    private void AttachExamIdentity()
    {
        AttachToken();
        var session = httpContextAccessor.HttpContext!.Session;
        var guestKey = session.GetString("examGuestKey");
        if (guestKey is null)
        {
            guestKey = Guid.NewGuid().ToString("N");
            session.SetString("examGuestKey", guestKey);
        }
        http.DefaultRequestHeaders.Remove("X-Exam-Guest-Key");
        http.DefaultRequestHeaders.Add("X-Exam-Guest-Key", guestKey);
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

    public async Task<GetExamDetailDto?> GetExamDetailAsync(int id)
    {
        AttachToken();
        var url = $"/exam/api/exams/{id}";
        try
        {
            return await http.GetFromJsonAsync<GetExamDetailDto>(url, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(int? SessionId, string? Error, bool Unauthorized)> StartSessionAsync(int examId, StartPracticeRequest? practice)
    {
        AttachExamIdentity();
        try
        {
            using var response = await http.PostAsJsonAsync(
                $"/exam/api/exams/{examId}/{(practice is null ? "full-test" : "practice")}", (object?)practice ?? new { });
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (null, "Guest sessions are unavailable. Check the Exam API guest setting or sign in.", true);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                try
                {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("error", out var error))
                        return (null, error.GetString(), false);
                }
                catch (JsonException) { }
                return (null, "Unable to start the session. Please check your selection and try again.", false);
            }
            var result = await response.Content.ReadFromJsonAsync<StartSessionResponse>(JsonOpts);
            return (result?.SessionId, null, false);
        }
        catch (HttpRequestException) { return (null, "Exam service is unavailable. Please try again later.", false); }
        catch (TaskCanceledException) { return (null, "Exam service timed out. Please try again.", false); }
    }

    public async Task<(ExamSessionDto? Session, bool Unauthorized)> GetSessionAsync(int id, string mode)
    {
        AttachExamIdentity();
        try
        {
            var route = mode == "practice" ? "practice-sessions" : "full-tests";
            using var response = await http.GetAsync($"/exam/api/{route}/{id}");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) return (null, true);
            if (!response.IsSuccessStatusCode) return (null, false);
            return (await response.Content.ReadFromJsonAsync<ExamSessionDto>(JsonOpts), false);
        }
        catch (HttpRequestException) { return (null, false); }
        catch (TaskCanceledException) { return (null, false); }
    }

    private record StartSessionResponse(int SessionId);

    public async Task<ProgressSummary?> GetProgressAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/progress/api/progress/me");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<ProgressSummary>(JsonOpts);
    }
}
