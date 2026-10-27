using AutoMapper;
using Content.Data;
using Content.Entities.Enums;
using Content.Mappings;
using Content.Repositories;
using Content.Services;
using Content.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Content.Tests;

internal sealed class ContentTestContext : IAsyncDisposable
{
    internal static readonly MapperConfiguration MappingConfiguration =
        new(cfg => cfg.AddProfile<ContentMappingProfile>(), NullLoggerFactory.Instance);

    private readonly SqliteConnection connection;
    internal ContentDbContext Db { get; }
    internal AttemptChecker Attempts { get; } = new();
    internal IContentService Service { get; }

    private ContentTestContext(SqliteConnection connection)
    {
        this.connection = connection;
        Db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(connection).Options);
        var mapper = MappingConfiguration.CreateMapper();
        Service = new ContentService(new ExamRepository(Db, mapper), new ExamSeriesRepository(Db, mapper), Attempts, mapper);
    }

    internal static async Task<ContentTestContext> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var context = new ContentTestContext(connection);
        var db = context.Db;
        await db.Database.EnsureCreatedAsync();
        db.ExamSeries.AddRange(
            new() { Id = 1, Name = "Zeta", Year = 2024 },
            new() { Id = 2, Name = "Alpha", Year = 2025 },
            new() { Id = 3, Name = "Alpha", Year = 2023 },
            new() { Id = 4, Name = "Deleted", Year = 2022, IsDeleted = true });
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Exams.AddRange(
            new() { Id = 1, Title = "Published TOEIC", Description = "Grammar practice", Duration = 120,
                AudioUrl = "exam.mp3", ExamSeriesId = 1, Status = ExamStatus.Published, CreatedAt = createdAt },
            new() { Id = 2, Title = "Draft TOEIC", ExamType = "Practice", ExamSeriesId = 2,
                Status = ExamStatus.Draft, CreatedAt = createdAt.AddDays(1) },
            new() { Id = 3, Title = "Deleted TOEIC", ExamSeriesId = 1,
                Status = ExamStatus.Draft, IsDeleted = true, CreatedAt = createdAt.AddDays(2) },
            new() { Id = 4, Title = "Hidden TOEIC", ExamSeriesId = 1,
                Status = ExamStatus.Hidden, CreatedAt = createdAt.AddDays(3) });
        db.QuestionGroups.Add(new() { Id = 1, ExamId = 1, Name = "Emails", ImageUrl = "group.png" });
        db.Passages.AddRange(
            new() { Id = 1, ExamId = 1, QuestionGroupId = 1, DisplayOrder = 2, Content = "Grouped passage" },
            new() { Id = 2, ExamId = 1, DisplayOrder = 1, Content = "Direct passage" });
        db.Questions.AddRange(
            new() { Id = 1, ExamId = 1, Part = 7, QuestionNumber = 148, QuestionText = "Email question",
                QuestionGroupId = 1, PassageId = 1, CorrectOption = "B", Explanation = "Because B",
                AudioUrl = "question.mp3", AudioStartSecond = 5, AudioEndSecond = 10,
                QuestionOptions = [new() { Id = 1, OptionLetter = "A", OptionText = "First" },
                    new() { Id = 2, OptionLetter = "B", OptionText = "Second" }] },
            new() { Id = 2, ExamId = 1, Part = 6, QuestionNumber = 131, PassageId = 2 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return context;
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }

    internal sealed class AttemptChecker : IExamAttemptChecker
    {
        internal Dictionary<int, int> Counts { get; } = new() { [1] = 15, [3] = 8 };
        public Task<int> GetAttemptCountAsync(int examId, CancellationToken ct = default) =>
            Task.FromResult(Counts.GetValueOrDefault(examId));
    }
}
