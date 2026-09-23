using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<List<ExamListItem>> GetExamsAsync()
    {
        AttachToken();
        return await http.GetFromJsonAsync<List<ExamListItem>>("/exam/api/exams", JsonOpts) ?? [];
    }

    public async Task<ExamDetail?> GetExamAsync(Guid id)
    {
        AttachToken();
        return await http.GetFromJsonAsync<ExamDetail>($"/exam/api/exams/{id}", JsonOpts);
    }

    public async Task<(SubmitExamResult? Data, string? Error)> SubmitExamAsync(Guid examId, object answers)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/exam/api/exams/{examId}/submit", answers);
        if (!res.IsSuccessStatusCode)
            return (null, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<SubmitExamResult>(JsonOpts), null);
    }

    public async Task<ProgressSummary?> GetProgressAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/progress/api/progress/me");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<ProgressSummary>(JsonOpts);
    }
}
