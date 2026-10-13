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

    private static string ExtractError(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? rawJson;
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? rawJson;
        }
        catch { }
        return rawJson;
    }

    // ==========================================
    // UC-1: Register
    // ==========================================
    public async Task<(RegisterResponse? Data, string? Error)> RegisterAsync(string email, string password, string fullName, string? confirmPassword = null)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/register", new { email, password, fullName, confirmPassword });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (null, ExtractError(raw));

        return (JsonSerializer.Deserialize<RegisterResponse>(raw, JsonOpts), null);
    }

    // ==========================================
    // UC-2: Verify OTP
    // ==========================================
    public async Task<(AuthResponse? Data, string? Error)> VerifyOtpAsync(string email, string otp)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/verify-otp", new { email, otp });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (null, ExtractError(raw));

        return (JsonSerializer.Deserialize<AuthResponse>(raw, JsonOpts), null);
    }

    // ==========================================
    // UC-2: Resend OTP
    // ==========================================
    public async Task<(bool Success, string Message)> ResendOtpAsync(string email)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/resend-otp", new { email });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (false, ExtractError(raw));

        return (true, ExtractError(raw));
    }

    // ==========================================
    // UC-5.1: Login with Email
    // ==========================================
    public async Task<(AuthResponse? Data, string? Error, bool RequiresOtp, string? Email)> LoginAsync(string email, string password)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/login", new { email, password });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            bool requiresOtp = false;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("requiresOtpVerification", out var req) && req.GetBoolean())
                {
                    requiresOtp = true;
                }
            }
            catch { }

            return (null, ExtractError(raw), requiresOtp, email);
        }

        return (JsonSerializer.Deserialize<AuthResponse>(raw, JsonOpts), null, false, null);
    }

    // ==========================================
    // UC-5.2: Login with Google
    // ==========================================
    public async Task<(AuthResponse? Data, string? Error)> GoogleLoginAsync(string idToken)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/google", new { idToken });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (null, ExtractError(raw));

        return (JsonSerializer.Deserialize<AuthResponse>(raw, JsonOpts), null);
    }

    // ==========================================
    // UC-6: Logout
    // ==========================================
    public async Task LogoutAsync()
    {
        AttachToken();
        var refreshToken = httpContextAccessor.HttpContext?.Session.GetString("refreshToken");
        try
        {
            await http.PostAsJsonAsync("/identity/api/auth/logout", new { refreshToken });
        }
        catch { }

        httpContextAccessor.HttpContext?.Session.Clear();
    }

    // ==========================================
    // UC-7.1: View Profile
    // ==========================================
    public async Task<UserProfileDto?> GetProfileAsync()
    {
        var token = httpContextAccessor.HttpContext?.Session.GetString("jwt");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/identity/api/auth/me");
        if (!string.IsNullOrEmpty(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        var res = await http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<UserProfileDto>(JsonOpts);
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
