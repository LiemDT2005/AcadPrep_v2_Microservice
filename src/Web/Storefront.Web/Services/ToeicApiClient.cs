using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Storefront.Web.Common;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public partial class ToeicApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
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
        var session = httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            var guestKey = session.GetString("examGuestKey");
            if (guestKey is null)
            {
                guestKey = Guid.NewGuid().ToString("N");
                session.SetString("examGuestKey", guestKey);
            }
            http.DefaultRequestHeaders.Remove("X-Exam-Guest-Key");
            http.DefaultRequestHeaders.Add("X-Exam-Guest-Key", guestKey);
        }
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
    // UC-5.3: Refresh Token
    // ==========================================
    public async Task<(bool Success, string? Error)> RefreshTokenAsync()
    {
        var session = httpContextAccessor.HttpContext?.Session;
        var refreshToken = session?.GetString("refreshToken");
        if (string.IsNullOrWhiteSpace(refreshToken))
            return (false, "No refresh token available in session.");

        try
        {
            var res = await http.PostAsJsonAsync("/identity/api/auth/refresh-token", new { refreshToken });
            var raw = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return (false, ExtractError(raw));

            var data = JsonSerializer.Deserialize<AuthResponse>(raw, JsonOpts);
            if (data is not null && session is not null)
            {
                session.SaveAuthentication(data);
                AttachToken();
                return (true, null);
            }
            return (false, "Failed to parse refresh response.");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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

        var url = "/content/api/exams" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
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
        var url = $"/content/api/exams/{id}";
        try
        {
            var detail = await http.GetFromJsonAsync<GetExamDetailDto>(url, JsonOpts);
            if (detail is not null && !string.IsNullOrEmpty(httpContextAccessor.HttpContext?.Session.GetString("jwt")))
                detail.AttemptHistory = await GetMyExamAttemptHistoryAsync(id);
            return detail;
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<UserAttemptHistoryDto>> GetMyExamAttemptHistoryAsync(int examId)
    {
        try
        {
            using var response = await http.GetAsync($"/exam/api/exams/{examId}/attempts/me");
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<UserAttemptHistoryDto>>(JsonOpts) ?? [];
        }
        catch (HttpRequestException) { return []; }
        catch (TaskCanceledException) { return []; }
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
    // ==================== LEARNING SERVICE APIs (TV03) ====================
    public async Task<LeaderboardResultDto?> GetLeaderboardAsync(string sortBy = "Score")
    {
        AttachToken();
        var res = await http.GetAsync($"/learning/api/leaderboard?sortBy={sortBy}");
        if (!res.IsSuccessStatusCode) return new LeaderboardResultDto([], null);
        return await res.Content.ReadFromJsonAsync<LeaderboardResultDto>(JsonOpts);
    }

    public async Task<PerformanceAnalysisDto?> GetPerformanceAnalysisAsync(string? userId = null)
    {
        AttachToken();
        var url = string.IsNullOrEmpty(userId) ? "/learning/api/performance/analysis" : $"/learning/api/performance/analysis?userId={userId}";
        var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<PerformanceAnalysisDto>(JsonOpts);
    }

    public async Task<ScoreProgressResultDto?> GetScoreProgressAsync(string? userId = null)
    {
        AttachToken();
        var url = string.IsNullOrEmpty(userId) ? "/learning/api/performance/progress" : $"/learning/api/performance/progress?userId={userId}";
        var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<ScoreProgressResultDto>(JsonOpts);
    }

    public async Task<PaginatedListDto<SavedVocabularyDto>?> GetReviewFlashcardsAsync(int pageNumber = 1, int pageSize = 10)
    {
        AttachToken();
        var res = await http.GetAsync($"/learning/api/vocabulary/flashcards?pageNumber={pageNumber}&pageSize={pageSize}");
        if (!res.IsSuccessStatusCode) return new PaginatedListDto<SavedVocabularyDto>([], 1, 0, 0, false, false);
        return await res.Content.ReadFromJsonAsync<PaginatedListDto<SavedVocabularyDto>>(JsonOpts);
    }

    public async Task<(bool Success, string? Error)> RateVocabularyAsync(int vocabularyId, bool isRemembered)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync("/learning/api/vocabulary/rate", new { vocabularyId, isRemembered });
        if (!res.IsSuccessStatusCode)
            return (false, await res.Content.ReadAsStringAsync());
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SaveVocabularyAsync(int vocabularyId)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync("/learning/api/vocabulary/save", new { vocabularyId });
        if (!res.IsSuccessStatusCode)
            return (false, await res.Content.ReadAsStringAsync());
        return (true, null);
    }

    public async Task<PaginatedListDto<SavedVocabularyDto>?> GetSavedVocabulariesAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 10)
    {
        AttachToken();
        var url = $"/learning/api/vocabulary/notebook?pageNumber={pageNumber}&pageSize={pageSize}" + 
                  (string.IsNullOrWhiteSpace(searchTerm) ? "" : $"&searchTerm={Uri.EscapeDataString(searchTerm)}");
        var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return new PaginatedListDto<SavedVocabularyDto>([], 1, 0, 0, false, false);
        return await res.Content.ReadFromJsonAsync<PaginatedListDto<SavedVocabularyDto>>(JsonOpts);
    }

    public async Task<(bool Success, string? Error)> RemoveSavedVocabularyAsync(int vocabularyId)
    {
        AttachToken();
        var res = await http.DeleteAsync($"/learning/api/vocabulary/{vocabularyId}");
        if (!res.IsSuccessStatusCode)
            return (false, await res.Content.ReadAsStringAsync());
        return (true, null);
    }

    public async Task<VocabContextDto?> GetVocabularyPassagesAsync(int vocabularyId)
    {
        AttachToken();
        var res = await http.GetAsync($"/learning/api/vocabulary/{vocabularyId}/passages");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<VocabContextDto>(JsonOpts);
    }

    public async Task<VocabularyDetailDto?> LookupVocabularyAsync(string keyword)
    {
        AttachToken();
        var res = await http.GetAsync($"/learning/api/vocabulary/lookup?keyword={Uri.EscapeDataString(keyword)}");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<VocabularyDetailDto>(JsonOpts);
    }

    public async Task<List<AchievementDto>> GetAchievementsAsync(string? userId = null)
    {
        AttachToken();
        var url = string.IsNullOrEmpty(userId) ? "/learning/api/gamification/achievements" : $"/learning/api/gamification/achievements?userId={userId}";
        var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return [];
        return await res.Content.ReadFromJsonAsync<List<AchievementDto>>(JsonOpts) ?? [];
    }

    public async Task<StudyStreakDto?> GetStudyStreakAsync(string? userId = null)
    {
        AttachToken();
        var url = string.IsNullOrEmpty(userId) ? "/learning/api/gamification/streak" : $"/learning/api/gamification/streak?userId={userId}";
        var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<StudyStreakDto>(JsonOpts);
    }

    public async Task<ProgressReportDto?> GetProgressReportAsync(DateTime startDate, DateTime endDate)
    {
        AttachToken();
        var res = await http.GetAsync($"/learning/api/admin/learning/reports?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");
        if (!res.IsSuccessStatusCode) return new ProgressReportDto([], 0, 0);
        return await res.Content.ReadFromJsonAsync<ProgressReportDto>(JsonOpts);
    }

    public async Task<UserStatsDto?> GetUserStatsAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/learning/api/admin/learning/user-stats");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<UserStatsDto>(JsonOpts);
    }

    public async Task<PaginatedListDto<ExamStatsItemDto>?> GetExamStatsAsync(int pageNumber = 1, int pageSize = 10)
    {
        AttachToken();
        var res = await http.GetAsync($"/learning/api/admin/learning/exam-stats?pageNumber={pageNumber}&pageSize={pageSize}");
        if (res.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException();
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<PaginatedListDto<ExamStatsItemDto>>(JsonOpts);
    }

    // ==================== CONTENT SERVICE APIs (CMS EXAM - UC-16) ====================
    public async Task<PagedResult<AdminExamListItem>?> GetAdminExamsPagedAsync(
        string? search = null,
        string? status = null,
        string? examType = null,
        int pageNumber = 1,
        int pageSize = 10)
    {
        AttachToken();
        var q = new List<string>
        {
            $"page={pageNumber}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"keyword={Uri.EscapeDataString(search.Trim())}");
        if (!string.IsNullOrWhiteSpace(status) && status != "All") q.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(examType) && examType != "All") q.Add($"examType={Uri.EscapeDataString(examType)}");

        var queryString = string.Join("&", q);
        var res = await http.GetAsync($"/content/api/cms/exams?{queryString}");
        if (!res.IsSuccessStatusCode) return new PagedResult<AdminExamListItem>([], 0, pageNumber, pageSize, 0);
        return await res.Content.ReadFromJsonAsync<PagedResult<AdminExamListItem>>(JsonOpts);
    }

    public async Task<AdminExamDetail?> GetAdminExamAsync(int id)
    {
        AttachToken();
        var res = await http.GetAsync($"/content/api/cms/exams/{id}");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<AdminExamDetail>(JsonOpts);
    }

    public async Task<(int? Id, string? Error)> CreateAdminExamAsync(AdminCreateExamRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync("/content/api/cms/exams", request);
        if (!res.IsSuccessStatusCode)
        {
            var err = await ExtractErrorMessageAsync(res);
            return (null, err);
        }
        var created = await res.Content.ReadFromJsonAsync<AdminExamDetail>(JsonOpts);
        return (created?.Id, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAdminExamAsync(int id, AdminUpdateExamRequest request)
    {
        AttachToken();
        var res = await http.PutAsJsonAsync($"/content/api/cms/exams/{id}", request);
        if (!res.IsSuccessStatusCode)
            return (false, await ExtractErrorMessageAsync(res));
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangeExamStatusAsync(int id, string status)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{id}/status", new AdminChangeExamStatusRequest(status));
        if (!res.IsSuccessStatusCode)
            return (false, await ExtractErrorMessageAsync(res));
        return (true, null);
    }

    public async Task<(DeleteExamApiResponse? Data, string? Error)> DeleteAdminExamAsync(int id)
    {
        AttachToken();
        var res = await http.DeleteAsync($"/content/api/cms/exams/{id}");
        if (!res.IsSuccessStatusCode)
            return (null, await ExtractErrorMessageAsync(res));
        return (await res.Content.ReadFromJsonAsync<DeleteExamApiResponse>(JsonOpts), null);
    }

    public async Task<(bool Success, string? Error)> RestoreAdminExamAsync(int id)
    {
        AttachToken();
        var res = await http.PostAsync($"/content/api/cms/exams/{id}/restore", null);
        if (!res.IsSuccessStatusCode)
            return (false, await ExtractErrorMessageAsync(res));
        return (true, null);
    }

    public async Task<List<AdminExamSeries>> GetExamSeriesAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/content/api/cms/exams/series");
        if (!res.IsSuccessStatusCode) return [];
        return await res.Content.ReadFromJsonAsync<List<AdminExamSeries>>(JsonOpts) ?? [];
    }


    public async Task<ExamStatsSummary?> GetCmsExamStatsAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/content/api/cms/exams/stats");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<ExamStatsSummary>(JsonOpts);
    }

    public async Task<QuestionStatsSummary?> GetQuestionStatsAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/content/api/cms/questions/stats");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<QuestionStatsSummary>(JsonOpts);
    }

    // ==================== CONTENT SERVICE APIs (CMS QUESTIONS & EXAM CONTENT - UC-17) ====================
    public async Task<PagedResult<AdminQuestionListItem>?> GetAdminQuestionsPagedAsync(
        string? keyword = null,
        int? part = null,
        string? difficulty = null,
        int? examId = null,
        int page = 1,
        int pageSize = 15)
    {
        AttachToken();
        var q = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(keyword)) q.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (part.HasValue && part.Value >= 1 && part.Value <= 7) q.Add($"part={part.Value}");
        if (!string.IsNullOrWhiteSpace(difficulty) && difficulty != "All") q.Add($"difficulty={Uri.EscapeDataString(difficulty)}");
        if (examId.HasValue && examId.Value > 0) q.Add($"examId={examId.Value}");

        var queryString = string.Join("&", q);
        var res = await http.GetAsync($"/content/api/cms/questions?{queryString}");
        if (!res.IsSuccessStatusCode) return new PagedResult<AdminQuestionListItem>([], 0, page, pageSize, 0);
        return await res.Content.ReadFromJsonAsync<PagedResult<AdminQuestionListItem>>(JsonOpts);
    }

    public async Task<AdminQuestionBankDetail?> GetAdminQuestionDetailAsync(int id)
    {
        AttachToken();
        var res = await http.GetAsync($"/content/api/cms/questions/{id}");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<AdminQuestionBankDetail>(JsonOpts);
    }

    public async Task<(int? Id, string? Error)> CreateAdminQuestionAsync(AdminUpsertQuestionRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync("/content/api/cms/questions", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAdminQuestionAsync(int id, AdminUpsertQuestionRequest request)
    {
        AttachToken();
        var res = await http.PutAsJsonAsync($"/content/api/cms/questions/{id}", request);
        if (!res.IsSuccessStatusCode)
        {
            return (false, await ExtractErrorMessageAsync(res));
        }
        return (true, null);
    }

    public async Task<(DeleteQuestionApiResponse? Data, string? Error)> DeleteAdminQuestionAsync(int id)
    {
        AttachToken();
        var res = await http.DeleteAsync($"/content/api/cms/questions/{id}");
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        return (await res.Content.ReadFromJsonAsync<DeleteQuestionApiResponse>(JsonOpts), null);
    }

    public async Task<(int? Id, string? Error)> SaveListeningQuestionAsync(int examId, AdminSaveListeningQuestionRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{examId}/content/listening-question", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(int? Id, string? Error)> SaveListeningGroupAsync(int examId, AdminSaveListeningGroupRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{examId}/content/listening-group", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(int? Id, string? Error)> SavePart5QuestionAsync(int examId, AdminSavePart5QuestionRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{examId}/content/part5", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(int? Id, string? Error)> SavePart6SetAsync(int examId, AdminSavePart6SetRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{examId}/content/part6", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(int? Id, string? Error)> SavePart7SetAsync(int examId, AdminSavePart7SetRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{examId}/content/part7", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(DeleteQuestionApiResponse? Data, string? Error)> DeleteExamContentAsync(int examId, string contentType, int targetId)
    {
        AttachToken();
        var res = await http.DeleteAsync($"/content/api/cms/exams/{examId}/content/{Uri.EscapeDataString(contentType)}/{targetId}");
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        return (await res.Content.ReadFromJsonAsync<DeleteQuestionApiResponse>(JsonOpts), null);
    }

    // ==================== CONTENT SERVICE APIs (CMS PASSAGES - UC-18) ====================
    public async Task<PagedResult<AdminPassageListItem>?> GetAdminPassagesPagedAsync(
        string? keyword = null,
        int? examId = null,
        int? part = null,
        int page = 1,
        int pageSize = 12)
    {
        AttachToken();
        var q = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        if (!string.IsNullOrWhiteSpace(keyword)) q.Add($"keyword={Uri.EscapeDataString(keyword.Trim())}");
        if (examId.HasValue && examId.Value > 0) q.Add($"examId={examId.Value}");
        if (part.HasValue && (part.Value == 6 || part.Value == 7)) q.Add($"part={part.Value}");

        var queryString = string.Join("&", q);
        var res = await http.GetAsync($"/content/api/cms/passages?{queryString}");
        if (!res.IsSuccessStatusCode) return new PagedResult<AdminPassageListItem>([], 0, page, pageSize, 0);
        return await res.Content.ReadFromJsonAsync<PagedResult<AdminPassageListItem>>(JsonOpts);
    }

    public async Task<PassageStatsSummary?> GetPassageStatsAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/content/api/cms/passages/stats");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<PassageStatsSummary>(JsonOpts);
    }

    public async Task<AdminPassageCmsDetail?> GetAdminPassageDetailAsync(int id)
    {
        AttachToken();
        var res = await http.GetAsync($"/content/api/cms/passages/{id}");
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<AdminPassageCmsDetail>(JsonOpts);
    }

    public async Task<(int? Id, string? Error)> CreateAdminPassageAsync(AdminUpsertPassageRequest request)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync("/content/api/cms/passages", request);
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        var data = await res.Content.ReadFromJsonAsync<IdApiResponse>(JsonOpts);
        return (data?.Id, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAdminPassageAsync(int id, AdminUpsertPassageRequest request)
    {
        AttachToken();
        var res = await http.PutAsJsonAsync($"/content/api/cms/passages/{id}", request);
        if (!res.IsSuccessStatusCode)
        {
            return (false, await ExtractErrorMessageAsync(res));
        }
        return (true, null);
    }

    public async Task<(DeletePassageApiResponse? Data, string? Error)> DeleteAdminPassageAsync(int id, bool deleteWithQuestions = false)
    {
        AttachToken();
        var res = await http.DeleteAsync($"/content/api/cms/passages/{id}?deleteWithQuestions={deleteWithQuestions.ToString().ToLowerInvariant()}");
        if (!res.IsSuccessStatusCode)
        {
            return (null, await ExtractErrorMessageAsync(res));
        }
        return (await res.Content.ReadFromJsonAsync<DeletePassageApiResponse>(JsonOpts), null);
    }

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage res)
    {
        var raw = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(raw)) return $"Request failed with status {(int)res.StatusCode}.";
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String)
            {
                return errProp.GetString() ?? raw;
            }
        }
        catch
        {
            // ignore JSON parse errors
        }
        return raw;
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
