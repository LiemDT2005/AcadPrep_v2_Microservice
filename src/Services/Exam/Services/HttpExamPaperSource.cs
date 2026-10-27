using Exam.Services.Interfaces;
namespace Exam.Services;
public class HttpExamPaperSource(HttpClient http, IConfiguration configuration) : IExamPaperSource
{
    public async Task<ExamPaperSnapshot?> GetAsync(int examId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/exam-papers/{examId}");
        request.Headers.Add("X-Internal-Key", configuration["Content:ApiKey"]);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExamPaperSnapshot>(ct);
    }
}
