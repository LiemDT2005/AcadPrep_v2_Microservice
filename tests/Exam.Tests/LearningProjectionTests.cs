using BuildingBlocks.Common.Events;
using Learning.Application.DTOs;
using Learning.Application.Interfaces;
using Learning.Application.Services;
using Learning.Infrastructure.Messaging;
using Learning.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Exam.Tests;

public class LearningProjectionTests
{
    private sealed class TestCache : ILearningCache
    {
        public TimeSpan? Ttl { get; private set; }
        private readonly Dictionary<string, object?> entries = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) =>
            Task.FromResult(entries.TryGetValue(key, out var value) ? (T?)value : default);
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
        { entries[key] = value; Ttl = expiration; return Task.CompletedTask; }
        public Task RemoveAsync(string key, CancellationToken ct = default) { entries.Remove(key); return Task.CompletedTask; }
        public Task InvalidateUserAsync(string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateAdminReportsAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StatisticsSource : IExamStatisticsSource
    {
        public int Calls { get; private set; }
        public Task<List<ExamStatsItemDto>> GetAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<List<ExamStatsItemDto>>([new(Guid.NewGuid(), "TOEIC", 2, 700, 700, 700, 50)]);
        }
    }

    [Fact]
    public async Task SubmittedEvent_ReplayDoesNotDuplicateScore_PreservesPartsAndSubmissionDate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var handler = new TestSubmittedHandler(new GamificationService(db, new TestCache()), NullLogger<TestSubmittedHandler>.Instance);
        var occurred = DateTime.UtcNow.AddDays(-3);
        var evt = new TestSubmittedEvent(Guid.NewGuid(), "owner", Guid.NewGuid(), "Paper", 725, 380, 345, 200, 150,
            new() { [1] = 90, [5] = 60 }) { OccurredOn = occurred };
        await handler.HandleAsync(evt); await handler.HandleAsync(evt);
        var row = Assert.Single(await db.ExamAttemptRecords.ToListAsync());
        Assert.Equal(occurred, row.SubmittedAt); Assert.Equal(725, row.Score);
        var performance = new PerformanceService(db);
        var analysis = await performance.GetPerformanceAnalysisAsync("owner");
        Assert.Equal(90, analysis.Value!.PartMastery[1]);
        Assert.Equal(60, analysis.Value.PartMastery[5]);
        Assert.False((await performance.GetPerformanceAnalysisAsync("other")).Value!.HasData);
        Assert.Equal(0, (await new GamificationService(db, new TestCache()).GetStudyStreakAsync("owner")).Value!.CurrentStreak);
        var second = evt with { AttemptId = Guid.NewGuid(), Score = 800, OccurredOn = DateTime.UtcNow };
        await handler.HandleAsync(second);
        var progress = await performance.GetScoreProgressAsync("owner");
        Assert.True(progress.Value!.HasSufficientData);
        Assert.Equal(new[] { 725, 800 }, progress.Value.Scores.Select(s => s.Score));
    }

    [Fact]
    public async Task Statistics_UsesAuthoritativeCompletionRate_AndCachesForOneHour()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new LearningDbContext(new DbContextOptionsBuilder<LearningDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var cache = new TestCache(); var source = new StatisticsSource();
        var service = new LearningAdminService(db, cache, source);
        var first = await service.GetExamStatisticsAsync(0, 0);
        var item = Assert.Single(first.Value!.Items);
        Assert.Equal(50, item.CompletionRate); Assert.Equal(700, item.AverageScore);
        Assert.Equal(TimeSpan.FromHours(1), cache.Ttl);
        await service.GetExamStatisticsAsync(1, 1);
        Assert.Equal(1, source.Calls);
    }
}
