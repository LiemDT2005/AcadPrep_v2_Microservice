using System.Net.Http.Json;
using Storefront.Web.Models;

namespace Storefront.Web.Services;

public partial class ToeicApiClient
{
    private static string SessionRoute(int id, string mode) =>
        $"/exam/api/{(mode == "practice" ? "practice-sessions" : "full-tests")}/{id}";

    private async Task<T?> ReadExamAsync<T>(string route)
    {
        AttachExamIdentity();
        try
        {
            using var response = await http.GetAsync(route);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(JsonOpts) : default;
        }
        catch (HttpRequestException) { return default; }
        catch (TaskCanceledException) { return default; }
    }

    public Task<AttemptReviewDto?> GetSessionReviewAsync(int id, string mode) =>
        ReadExamAsync<AttemptReviewDto>($"{SessionRoute(id, mode)}/review");
    public async Task<StudyHistoryResultDto?> GetStudyHistoryAsync()
    {
        var history = await ReadExamAsync<StudyHistoryResultDto>("/exam/api/study-history/me");
        if (history is null || string.IsNullOrEmpty(httpContextAccessor.HttpContext?.Session.GetString("jwt"))) return history;
        var vocabulary = await ReadExamAsync<StudyHistoryResultDto>("/learning/api/performance/study-history");
        if (vocabulary is not null) history.Items.AddRange(vocabulary.Items);
        history.Items = history.Items.OrderByDescending(i => i.OccurredAt).ToList();
        return history;
    }
    public Task<ExamAttemptsResultDto?> GetAllExamAttemptsAsync() => ReadExamAsync<ExamAttemptsResultDto>("/exam/api/attempts/me");
    public Task<IncorrectAnswersResultDto?> GetIncorrectAnswersAsync() => ReadExamAsync<IncorrectAnswersResultDto>("/exam/api/incorrect-answers/me");

    public Task<(bool Success, string? Error)> SaveSessionAsync(int id, string mode, SaveSessionRequest request) =>
        WriteExamAsync(id, mode, request, false);
    public Task<(bool Success, string? Error)> SubmitSessionAsync(int id, string mode, SaveSessionRequest request) =>
        WriteExamAsync(id, mode, request, true);

    private async Task<(bool Success, string? Error)> WriteExamAsync(int id, string mode, SaveSessionRequest request, bool submit)
    {
        AttachExamIdentity();
        try
        {
            using var response = submit
                ? await http.PostAsJsonAsync($"{SessionRoute(id, mode)}/submit", request)
                : await http.PutAsJsonAsync($"{SessionRoute(id, mode)}/checkpoint", request);
            if (response.IsSuccessStatusCode) return (true, null);
            return (false, response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Your session has expired. Sign in again to continue." : "Unable to save or submit. Check your connection and retry.");
        }
        catch (HttpRequestException) { return (false, "Exam service unavailable. Your answers are still on this page; retry when connected."); }
        catch (TaskCanceledException) { return (false, "Request timed out. Please retry."); }
    }
}
