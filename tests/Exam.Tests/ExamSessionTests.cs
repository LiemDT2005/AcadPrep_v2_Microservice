using System.Text.Json;
using Exam.DTOs;
using Exam.Data;
using Exam.Entities;
using Exam.Entities.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Exam.Tests;

public class ExamSessionTests
{
    [Fact]
    public async Task RuntimeSqliteSchema_CanCreateAndStartSessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ExamDbContext(
            new DbContextOptionsBuilder<ExamDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.ExamSeries.Add(new() { Id = 1, Name = "Runtime", Year = 2026 });
        db.Exams.Add(new() { Id = 1, Title = "Runtime TOEIC", Duration = 120, ExamSeriesId = 1, Status = ExamStatus.Published });
        db.Questions.Add(new() { ExamId = 1, Part = 5, QuestionNumber = 101 });
        await db.SaveChangesAsync();
        var service = TestServices.CreateSessionService(db);
        Assert.True((await service.StartFullTestAsync(1, "learner")).IsSuccess);
        var practice = await service.StartPracticeAsync(1, "learner", new() { SelectedPartNumbers = [5] });
        Assert.True(practice.IsSuccess);
        Assert.Single((await service.GetPracticeAsync(practice.Value, "learner"))!.Questions);
    }

    [Fact]
    public async Task FullTest_CreatesFreshOwnedAttemptsWithAllQuestionsAndUtcDeadline()
    {
        using var db = await CreateDb();
        var service = TestServices.CreateSessionService(db);
        var first = await service.StartFullTestAsync(1, "identity-guid");
        var second = await service.StartFullTestAsync(1, "identity-guid");
        Assert.True(first.IsSuccess);
        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(2, await db.ExamAttempts.CountAsync());
        Assert.Null(await service.GetFullTestAsync(first.Value, "someone-else"));
        var session = Assert.IsType<ExamSessionDto>(await service.GetFullTestAsync(first.Value, "identity-guid"));
        Assert.Equal(4, session.Questions.Count);
        Assert.Equal(120 * 60, (session.ExpiresAt!.Value - session.StartedAt).TotalSeconds);
        Assert.Equal(DateTimeKind.Utc, session.ExpiresAt.Value.Kind);
        var payload = JsonSerializer.Serialize(session);
        Assert.DoesNotContain("CorrectOption", payload);
        Assert.DoesNotContain("Explanation", payload);
    }

    [Fact]
    public async Task Practice_FiltersPartsAndTags_AndKeepsAllSharedPassages()
    {
        using var db = await CreateDb();
        var service = TestServices.CreateSessionService(db);
        var result = await service.StartPracticeAsync(1, "learner", new()
        {
            SelectedPartNumbers = [7, 7], SelectedTags = ["Emails"], TimeLimitMinutes = 15
        });
        Assert.True(result.IsSuccess);
        Assert.Null(await service.GetPracticeAsync(result.Value, "other-learner"));
        var session = Assert.IsType<ExamSessionDto>(await service.GetPracticeAsync(result.Value, "learner"));
        Assert.Equal(new[] { 3, 4 }, session.Questions.Select(q => q.Id));
        Assert.All(session.Questions, q =>
        {
            Assert.Equal(7, q.Part);
            Assert.Equal(new[] { "First passage", "Second passage" }, q.Passages.Select(p => p.Content));
            Assert.Equal("group.png", q.GroupImageUrl);
        });
        Assert.Equal(15 * 60, (session.ExpiresAt!.Value - session.StartedAt).TotalSeconds);
    }

    [Fact]
    public async Task Practice_NoLimitAndDirectPassageAreSupported()
    {
        using var db = await CreateDb();
        var service = TestServices.CreateSessionService(db);
        var result = await service.StartPracticeAsync(1, "learner", new() { SelectedPartNumbers = [6] });
        var session = Assert.IsType<ExamSessionDto>(await service.GetPracticeAsync(result.Value, "learner"));
        Assert.Null(session.ExpiresAt);
        Assert.Equal("Direct passage", Assert.Single(Assert.Single(session.Questions).Passages).Content);
        Assert.Equal(new[] { "A", "B" }, session.Questions[0].Options.Select(o => o.Letter));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(8, null)]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    [InlineData(5, 121)]
    public async Task Practice_RejectsInvalidSelectionWithoutCreatingSession(int part, int? minutes)
    {
        using var db = await CreateDb();
        var result = await TestServices.CreateSessionService(db).StartPracticeAsync(1, "learner", new()
        {
            SelectedPartNumbers = [part], TimeLimitMinutes = minutes
        });
        Assert.False(result.IsSuccess);
        Assert.Empty(db.PracticeSessions);
    }

    [Fact]
    public async Task Start_RejectsEmptyPartsUnmatchedTagsAndUnpublishedOrEmptyExams()
    {
        using var db = await CreateDb();
        var service = TestServices.CreateSessionService(db);
        Assert.False((await service.StartPracticeAsync(1, "u", new())).IsSuccess);
        Assert.False((await service.StartPracticeAsync(1, "u", new() { SelectedPartNumbers = [5], SelectedTags = ["missing"] })).IsSuccess);
        Assert.False((await service.StartFullTestAsync(99, "u")).IsSuccess);
        db.Exams.Add(new() { Id = 2, Title = "Empty", Duration = 120, Status = ExamStatus.Published, ExamSeriesId = 1 });
        await db.SaveChangesAsync();
        Assert.False((await service.StartFullTestAsync(2, "u")).IsSuccess);
        var exam = await db.Exams.FindAsync(1);
        exam!.Status = ExamStatus.Draft;
        await db.SaveChangesAsync();
        Assert.False((await service.StartFullTestAsync(1, "u")).IsSuccess);
        Assert.False((await service.StartPracticeAsync(1, "u", new() { SelectedPartNumbers = [5] })).IsSuccess);
        Assert.Empty(db.ExamAttempts);
        Assert.Empty(db.PracticeSessions);
    }

    internal static async Task<TestDb> CreateDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new TestDb(new DbContextOptionsBuilder<TestDb>().UseSqlite(connection).Options, connection);
        await db.Database.EnsureCreatedAsync();
        db.ExamSeries.Add(new() { Id = 1, Name = "Test", Year = 2025 });
        db.Exams.Add(new() { Id = 1, Title = "TOEIC", Duration = 120, Status = ExamStatus.Published, ExamSeriesId = 1 });
        db.QuestionGroups.Add(new() { Id = 1, ExamId = 1, Name = "Email set", ImageUrl = "group.png" });
        db.Passages.AddRange(
            new() { Id = 1, ExamId = 1, Content = "Direct passage" },
            new() { Id = 2, ExamId = 1, QuestionGroupId = 1, DisplayOrder = 2, Content = "Second passage" },
            new() { Id = 3, ExamId = 1, QuestionGroupId = 1, DisplayOrder = 1, Content = "First passage" });
        db.Questions.AddRange(
            new() { Id = 1, ExamId = 1, Part = 5, QuestionNumber = 101, TopicTag = "Grammar", CorrectOption = OptionLetter.B },
            new() { Id = 2, ExamId = 1, Part = 6, QuestionNumber = 131, PassageId = 1, QuestionOptions = [
                new() { OptionLetter = OptionLetter.B, OptionText = "Second" },
                new() { OptionLetter = OptionLetter.A, OptionText = "First" }] },
            new() { Id = 4, ExamId = 1, Part = 7, QuestionNumber = 148, QuestionGroupId = 1, TopicTag = "Emails" },
            new() { Id = 3, ExamId = 1, Part = 7, QuestionNumber = 147, QuestionGroupId = 1, TopicTag = "Emails" });
        await db.SaveChangesAsync();
        return db;
    }

    internal sealed class TestDb(DbContextOptions<TestDb> options, SqliteConnection connection) : DbContext(options), IExamDbContext
    {
        public DbSet<global::Exam.Entities.Exam> Exams => Set<global::Exam.Entities.Exam>();
        public DbSet<ExamSeries> ExamSeries => Set<ExamSeries>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<QuestionGroup> QuestionGroups => Set<QuestionGroup>();
        public DbSet<Passage> Passages => Set<Passage>();
        public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
        public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();
        public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
        public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<AttemptAnswer>().HasKey(a => new { a.AttemptId, a.QuestionId });
            model.Entity<AttemptAnswer>().HasOne(a => a.ExamAttempt).WithMany(a => a.AttemptAnswers).HasForeignKey(a => a.AttemptId);
        }
        public override void Dispose() { base.Dispose(); connection.Dispose(); }
    }
}
