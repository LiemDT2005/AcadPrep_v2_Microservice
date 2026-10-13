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
        if (!res.IsSuccessStatusCode) return new PaginatedListDto<ExamStatsItemDto>([], 1, 0, 0, false, false);
        return await res.Content.ReadFromJsonAsync<PaginatedListDto<ExamStatsItemDto>>(JsonOpts);
    }

    // ==================== CONTENT SERVICE APIs (CMS EXAM) ====================
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
            var err = await res.Content.ReadAsStringAsync();
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
            return (false, await res.Content.ReadAsStringAsync());
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangeExamStatusAsync(int id, string status)
    {
        AttachToken();
        var res = await http.PostAsJsonAsync($"/content/api/cms/exams/{id}/status", new AdminChangeExamStatusRequest(status));
        if (!res.IsSuccessStatusCode)
            return (false, await res.Content.ReadAsStringAsync());
        return (true, null);
    }

    public async Task<(DeleteExamApiResponse? Data, string? Error)> DeleteAdminExamAsync(int id)
    {
        AttachToken();
        var res = await http.DeleteAsync($"/content/api/cms/exams/{id}");
        if (!res.IsSuccessStatusCode)
            return (null, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<DeleteExamApiResponse>(JsonOpts), null);
    }

    public async Task<(bool Success, string? Error)> RestoreAdminExamAsync(int id)
    {
        AttachToken();
        var res = await http.PostAsync($"/content/api/cms/exams/{id}/restore", null);
        if (!res.IsSuccessStatusCode)
            return (false, await res.Content.ReadAsStringAsync());
        return (true, null);
    }

    public async Task<List<AdminExamSeries>> GetExamSeriesAsync()
    {
        AttachToken();
        var res = await http.GetAsync("/content/api/cms/exams/series");
        if (!res.IsSuccessStatusCode) return [];
        return await res.Content.ReadFromJsonAsync<List<AdminExamSeries>>(JsonOpts) ?? [];
    }
}
