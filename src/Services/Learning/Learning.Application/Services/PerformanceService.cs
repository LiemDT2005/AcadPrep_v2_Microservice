using System.Text.Json;
using BuildingBlocks.Common.Results;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Services;

public class PerformanceService(ILearningDbContext db)
{
    public async Task<Result<PerformanceAnalysisDto>> GetPerformanceAnalysisAsync(
        string userId, CancellationToken ct = default)
    {
        var attempts = await db.ExamAttemptRecords.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);

        if (attempts.Count == 0)
        {
            return Result.Success(new PerformanceAnalysisDto(
                false,
                "Chưa có lịch sử làm bài",
                0,
                0,
                0,
                new Dictionary<int, int>()
            ));
        }

        var totalTests = attempts.Count;
        var avgListening = attempts.Average(x => x.ListeningScore);
        var avgReading = attempts.Average(x => x.ReadingScore);

        // Aggregate Part mastery across attempts
        var partMastery = new Dictionary<int, int>();
        for (int p = 1; p <= 7; p++) partMastery[p] = 0;

        var partTotals = new Dictionary<int, List<int>>();
        for (int p = 1; p <= 7; p++) partTotals[p] = [];

        foreach (var att in attempts)
        {
            if (!string.IsNullOrEmpty(att.PartMasteryJson))
            {
                try
                {
                    var map = JsonSerializer.Deserialize<Dictionary<int, int>>(att.PartMasteryJson);
                    if (map is not null)
                    {
                        foreach (var kv in map)
                        {
                            if (partTotals.ContainsKey(kv.Key))
                                partTotals[kv.Key].Add(kv.Value);
                        }
                    }
                }
                catch
                {
                    // Fallback approximation if json parsing fails
                }
            }
        }

        for (int p = 1; p <= 7; p++)
        {
            if (partTotals[p].Count > 0)
            {
                partMastery[p] = (int)Math.Round(partTotals[p].Average());
            }
            else
            {
                // Sensible default based on listening/reading score ratio if no detailed part breakdown
                var baseline = p <= 4 ? (avgListening / 495.0 * 100) : (avgReading / 495.0 * 100);
                partMastery[p] = (int)Math.Round(Math.Clamp(baseline, 0, 100));
            }
        }

        return Result.Success(new PerformanceAnalysisDto(
            true,
            string.Empty,
            Math.Round(avgListening, 1),
            Math.Round(avgReading, 1),
            totalTests,
            partMastery
        ));
    }

    public async Task<Result<ScoreProgressResultDto>> GetScoreProgressAsync(
        string userId, CancellationToken ct = default)
    {
        var attempts = await db.ExamAttemptRecords.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.SubmittedAt)
            .ToListAsync(ct);

        if (attempts.Count < 2)
        {
            return Result.Success(new ScoreProgressResultDto(
                false,
                "Cần làm tối thiểu 2 bài thi để theo dõi tiến độ cải thiện điểm số (Take more tests to track progress)",
                attempts.Select(a => new ScoreProgressItemDto(a.SubmittedAt, a.Score, a.ExamTitle)).ToList()
            ));
        }

        var scores = attempts.Select(a => new ScoreProgressItemDto(
            a.SubmittedAt, a.Score, a.ExamTitle
        )).ToList();

        return Result.Success(new ScoreProgressResultDto(
            true,
            string.Empty,
            scores
        ));
    }
}
