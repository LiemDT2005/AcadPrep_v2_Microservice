using BuildingBlocks.Common.Results;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Learning.Application.Services;

public class LearningAdminService(ILearningDbContext db, ILearningCache cache, IExamStatisticsSource examStatistics)
{
    private static readonly TimeSpan AdminCacheDuration = TimeSpan.FromHours(1);

    // UC-20.1 View Learning Progress Reports (Cache Redis >= 1 hour)
    public async Task<Result<ProgressReportDto>> GetProgressReportAsync(
        DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        var cacheKey = $"admin_report_{start:yyyyMMdd}_{end:yyyyMMdd}";
        var cached = await cache.GetAsync<ProgressReportDto>(cacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        var attempts = await db.ExamAttemptRecords.AsNoTracking()
            .Where(ea => ea.SubmittedAt >= start && ea.SubmittedAt <= end)
            .ToListAsync(ct);

        var grouped = attempts
            .GroupBy(ea => DateOnly.FromDateTime(ea.SubmittedAt))
            .Select(g => new ProgressDataPointDto(
                g.Key,
                g.Count(),
                Math.Round(g.Average(x => x.Score), 1)
            ))
            .OrderBy(p => p.Date)
            .ToList();

        var totalCompleted = attempts.Count;
        var overallAvg = totalCompleted == 0 ? 0 : Math.Round(attempts.Average(x => x.Score), 1);

        var report = new ProgressReportDto(grouped, totalCompleted, overallAvg);
        await cache.SetAsync(cacheKey, report, AdminCacheDuration, ct);

        return Result.Success(report);
    }

    // UC-20.2 View User Statistics (Cache Redis >= 1 hour)
    public async Task<Result<UserStatsDto>> GetUserStatisticsAsync(CancellationToken ct = default)
    {
        const string cacheKey = "admin_user_stats";
        var cached = await cache.GetAsync<UserStatsDto>(cacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sevenDaysAgo = GamificationService.GetCurrentDateVietnam().AddDays(-7);

        var totalUsers = await db.UserProfiles.CountAsync(ct);
        var newRegistrations = await db.UserProfiles.CountAsync(u => u.CreatedAt >= thirtyDaysAgo, ct);

        var activeUsers = await db.StudyStreaks.CountAsync(s => s.LastActiveDate >= sevenDaysAgo, ct);

        var totalExamsTaken = await db.ExamAttemptRecords.CountAsync(ct);
        var avgScore = 0.0;
        if (totalExamsTaken > 0)
        {
            avgScore = await db.ExamAttemptRecords.AverageAsync(ea => (double)ea.Score, ct);
        }

        var retentionRate = totalUsers > 0 ? Math.Round((double)activeUsers / totalUsers * 100, 1) : 0;

        var stats = new UserStatsDto(
            totalUsers,
            newRegistrations,
            activeUsers,
            totalExamsTaken,
            Math.Round(avgScore, 1),
            retentionRate
        );

        await cache.SetAsync(cacheKey, stats, AdminCacheDuration, ct);
        return Result.Success(stats);
    }

    // UC-20.3 View Exam Statistics (Cache Redis >= 1 hour)
    public async Task<Result<PaginatedListDto<ExamStatsItemDto>>> GetExamStatisticsAsync(
        int pageNumber = 1, int pageSize = 10, CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var cacheKey = $"admin_exam_stats_v2_p{pageNumber}_s{pageSize}";
        var cached = await cache.GetAsync<PaginatedListDto<ExamStatsItemDto>>(cacheKey, ct);
        if (cached is not null) return Result.Success(cached);

        List<ExamStatsItemDto> grouped;
        try { grouped = await examStatistics.GetAsync(ct); }
        catch (HttpRequestException) { return Result<PaginatedListDto<ExamStatsItemDto>>.Failure("Exam statistics are temporarily unavailable."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return Result<PaginatedListDto<ExamStatsItemDto>>.Failure("Exam statistics request timed out."); }

        var totalCount = grouped.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedItems = grouped
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PaginatedListDto<ExamStatsItemDto>(
            pagedItems, pageNumber, totalPages, totalCount, pageNumber > 1, pageNumber < totalPages
        );

        await cache.SetAsync(cacheKey, result, AdminCacheDuration, ct);
        return Result.Success(result);
    }
}
