using System.Net.Http.Json;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Microsoft.Extensions.Configuration;
namespace Learning.Infrastructure.Messaging;
public class HttpExamStatisticsSource(HttpClient http, IConfiguration configuration) : IExamStatisticsSource
{
    public async Task<List<ExamStatsItemDto>> GetAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "internal/exam-statistics");
        request.Headers.Add("X-Internal-Key", configuration["Exam:ApiKey"]);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ExamStatsItemDto>>(ct) ?? [];
    }
}
