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
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                return err.GetString() ?? rawJson;
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString() ?? rawJson;

            if (doc.RootElement.TryGetProperty("errors", out var errorsObj) && errorsObj.ValueKind == JsonValueKind.Object)
            {
                var errorList = new List<string>();
                foreach (var prop in errorsObj.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in prop.Value.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) errorList.Add(s);
                        }
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var s = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) errorList.Add(s);
                    }
                }
                if (errorList.Count > 0)
                    return string.Join(" ", errorList);
            }

            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString() ?? rawJson;
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

    // ==========================================
    // UC-7.2: Edit Profile
    // ==========================================
    public async Task<(UserProfileDto? Data, string? Error)> UpdateProfileAsync(string fullName, string? avatar)
    {
        AttachToken();
        var cleanAvatar = string.IsNullOrWhiteSpace(avatar) ? null : avatar.Trim();
        var res = await http.PutAsJsonAsync("/identity/api/auth/profile", new { fullName = fullName.Trim(), avatar = cleanAvatar });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (null, ExtractError(raw));

        var data = JsonSerializer.Deserialize<UserProfileDto>(raw, JsonOpts);
        if (data is not null)
        {
            httpContextAccessor.HttpContext?.Session.SetString("name", data.FullName);
            if (!string.IsNullOrEmpty(data.Avatar))
                httpContextAccessor.HttpContext?.Session.SetString("avatar", data.Avatar);
            else
                httpContextAccessor.HttpContext?.Session.Remove("avatar");
        }
        return (data, null);
    }

    // ==========================================
    // UC-7.3: Change Password
    // ==========================================
    public async Task<(bool Success, string? Error)> ChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync("/identity/api/auth/change-password", new { currentPassword, newPassword, confirmPassword });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (false, ExtractError(raw));

        return (true, null);
    }

    // ==========================================
    // UC-8: Forgot Password
    // ==========================================
    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/forgot-password", new { email });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (false, ExtractError(raw));

        return (true, ExtractError(raw));
    }

    // ==========================================
    // UC-8: Reset Password
    // ==========================================
    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string otp, string newPassword, string confirmPassword)
    {
        var res = await http.PostAsJsonAsync("/identity/api/auth/reset-password", new { email, otp, newPassword, confirmPassword });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (false, ExtractError(raw));

        return (true, null);
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

    // ==========================================
    // UC-19.1: Admin View Accounts List
    // ==========================================
    public async Task<UserListResponse?> GetUsersAsync(string? search = null, string? role = null, string? status = null, int pageIndex = 1, int pageSize = 10)
    {
        AttachToken();
        var queryParams = new List<string>
        {
            $"pageIndex={pageIndex}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(role)) queryParams.Add($"role={Uri.EscapeDataString(role.Trim())}");
        if (!string.IsNullOrWhiteSpace(status)) queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");

        var url = $"/identity/api/users?{string.Join("&", queryParams)}";
        var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;

        return await res.Content.ReadFromJsonAsync<UserListResponse>(JsonOpts);
    }

    // ==========================================
    // UC-19.1: Admin Export Users CSV
    // ==========================================
    public async Task<(byte[]? FileBytes, string? FileName)> DownloadUsersCsvAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/identity/api/users/export-csv");
        if (!res.IsSuccessStatusCode) return (null, null);

        var bytes = await res.Content.ReadAsByteArrayAsync();
        var fileName = res.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "users.csv";
        return (bytes, fileName);
    }

    // ==========================================
    // UC-19.2: Admin View Account Detail
    // ==========================================
    public async Task<UserDetailAdminDto?> GetUserDetailAsync(string id)
    {
        AttachToken();
        var res = await http.GetAsync($"/identity/api/users/{id}");
        if (!res.IsSuccessStatusCode) return null;

        return await res.Content.ReadFromJsonAsync<UserDetailAdminDto>(JsonOpts);
    }

    // ==========================================
    // UC-19.4: Admin Assign Roles
    // ==========================================
    public async Task<(bool Success, string? Error)> AssignRolesAsync(string id, List<string> roles)
    {
        AttachToken();
        var res = await http.PutAsJsonAsync($"/identity/api/users/{id}/roles", new { roles });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (false, ExtractError(raw));

        return (true, null);
    }

    // ==========================================
    // UC-19.3: Admin Update User Status (Ban / Unban)
    // ==========================================
    public async Task<(bool Success, string? Error)> UpdateUserStatusAsync(string id, int status, string? reason = null)
    {
        AttachToken();
        var res = await http.PutAsJsonAsync($"/identity/api/users/{id}/status", new { status, reason });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return (false, ExtractError(raw));

        return (true, null);
    }
}


