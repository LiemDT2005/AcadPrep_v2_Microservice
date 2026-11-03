using System.Collections.Concurrent;
using System.Net.Http.Json;
using Content.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Content.Services;

/// <summary>
/// Microservice Adapter for querying Exam Attempts from Exam Engine Service (port 5003).
/// Enforces decoupled boundary without cross-database queries.
/// </summary>
public class HttpExamAttemptChecker(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<HttpExamAttemptChecker> logger) : IExamAttemptChecker
{
    // Seed attempt tracking dictionary for standalone demo mode or when Exam attempt endpoint is not yet connected
    private static readonly ConcurrentDictionary<int, int> DemoAttemptCounts = new(new Dictionary<int, int>
    {
        { 1, 15 }, // Exam 1 has 15 attempts -> will be soft-deleted if deleted
        { 2, 0  }, // Exam 2 has 0 attempts -> will be hard-deleted if deleted
        { 3, 8  }, // Exam 3 has 8 attempts -> already soft-deleted/hidden
        { 4, 0  }, // Exam 4 has 0 attempts -> will be hard-deleted if deleted
        { 5, 3  }  // Exam 5 has 3 attempts -> will be soft-deleted if deleted
    });

    public async Task<int> GetAttemptCountAsync(int examId, CancellationToken ct = default)
    {
        var examApiUrl = configuration["Services:ExamApi"] ?? "http://localhost:5003";
        try
        {
            var res = await httpClient.GetAsync($"{examApiUrl}/api/exams/{examId}/attempts/count", ct);
            if (res.IsSuccessStatusCode)
            {
                var count = await res.Content.ReadFromJsonAsync<int>(ct);
                return count;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Could not query Exam at {Url}: {Message}. Using local attempts store adapter.", examApiUrl, ex.Message);
        }

        // Return tracked attempt count (or 0 for newly created exams)
        return DemoAttemptCounts.TryGetValue(examId, out var countVal) ? countVal : 0;
    }

    public static void SetAttemptCount(int examId, int count)
    {
        DemoAttemptCounts[examId] = count;
    }
}
