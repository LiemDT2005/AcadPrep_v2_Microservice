using System.Text.Json;
using Exam.Data;
using Exam.DTOs;
using Exam.Entities;
using Exam.Repositories;
using Exam.Services;
using Exam.Services.Interfaces;
using Exam.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Exam.Tests;

public class ExamLifecycleTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public ExamDbContext Db { get; private set; } = null!;
        public FakePaper Paper { get; } = new();
        public ExamLifecycleService Service { get; private set; } = null!;
        public async Task Initialize()
        {
            await connection.OpenAsync();
            Db = new(new DbContextOptionsBuilder<ExamDbContext>().UseSqlite(connection).Options);
            await Db.Database.EnsureCreatedAsync();
            await SessionStateSchema.UpgradeAsync(Db);
            Db.ExamSeries.Add(new() { Name = "Test", Year = 2026 });
            await Db.SaveChangesAsync();
            var legacy = new ExamSessionService(new ExamRepository(Db), new QuestionRepository(Db),
                new ExamAttemptRepository(Db), new PracticeSessionRepository(Db), TestServices.CreateMapper());
            Service = new(new SessionStateRepository(Db), legacy, new QuestionRepository(Db), Paper, new PendingBillingQuotaPolicy());
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class FakePaper : IExamPaperSource
    {
        public ExamPaperSnapshot? Value { get; set; } = new(42, "Published Content exam", 120,
        [
            new() { QuestionId = 700, Part = 1, QuestionNumber = 1, CorrectOption = "B", AudioUrl = "/audio/test.wav",
                Explanation = "Correct explanation", Options = [new() { Letter = "A", Text = "Wrong" }, new() { Letter = "B", Text = "Right" }] },
            new() { QuestionId = 701, Part = 5, QuestionNumber = 101, CorrectOption = "A",
                Options = [new() { Letter = "A", Text = "Right" }, new() { Letter = "B", Text = "Wrong" }] }
        ]);
        public Task<ExamPaperSnapshot?> GetAsync(int examId, CancellationToken ct) => Task.FromResult(Value);
    }

    [Fact]
    public async Task Statistics_CountsUnsubmittedAttemptsInDenominator_AndProtectsInternalEndpoint()
    {
        await using var f = new Fixture(); await f.Initialize();
        var first = (await f.Service.StartAsync(42, "user", null, default)).Value;
        await f.Service.StartAsync(42, "user", null, default);
        await f.Service.SubmitAsync(first, "full-test", "user", new([]), default);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["InternalApiKey"] = "test-key" }).Build();
        var controller = new ExamStatisticsController(f.Db, config)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<UnauthorizedResult>(await controller.Get(default));
        controller.Request.Headers["X-Internal-Key"] = "test-key";
        var result = Assert.IsType<OkObjectResult>(await controller.Get(default));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var row = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(2, row.GetProperty("TotalAttempts").GetInt32());
        Assert.Equal(50, row.GetProperty("CompletionRate").GetDouble());
        Assert.Equal(10, row.GetProperty("AverageScore").GetDouble());
    }

    [Fact]
    public async Task Start_UnpublishedPaperOrUnmatchedPractice_DoesNotCreateAttempt()
    {
        await using var f = new Fixture(); await f.Initialize();
        Assert.False((await f.Service.StartAsync(42, "user", new() { SelectedPartNumbers = [7] }, default)).IsSuccess);
        f.Paper.Value = null;
        Assert.False((await f.Service.StartAsync(42, "user", null, default)).IsSuccess);
        Assert.Empty(f.Db.SessionStates); Assert.Empty(f.Db.ExamAttempts);
    }

    [Fact]
    public async Task Start_UsesContentSnapshot_ResumeRestoresAnswers_AndNeverLeaksAnswerKey()
    {
        await using var f = new Fixture(); await f.Initialize();
        var id = (await f.Service.StartAsync(42, "user", null, default)).Value;
        Assert.True((await f.Service.SaveAsync(id, "full-test", "user", new(new() { [700] = "B" }), default)).IsSuccess);
        f.Paper.Value!.Questions[0].CorrectOption = "A"; // CMS edits cannot affect the snapshot.
        var session = await f.Service.GetAsync(id, "full-test", "user", default);
        Assert.Equal("B", session!.Answers[700]);
        Assert.Equal(42, session.ExamId);
        Assert.Equal(7200, (session.ExpiresAt!.Value - session.StartedAt).TotalSeconds);
        var json = JsonSerializer.Serialize(session);
        Assert.DoesNotContain("CorrectOption", json); Assert.DoesNotContain("Explanation", json);
        Assert.Null(await f.Service.GetAsync(id, "full-test", "other", default));
        Assert.Null(await f.Service.ReviewAsync(id, "full-test", "user", default));
        var submitted = await f.Service.SubmitAsync(id, "full-test", "user", new(new() { [701] = "A" }), default);
        Assert.Equal(2, submitted.Value!.CorrectCount);
    }

    [Fact]
    public async Task Submit_IsIdempotent_PersistsResultAndOutbox_AndRejectsFurtherChanges()
    {
        await using var f = new Fixture(); await f.Initialize();
        var id = (await f.Service.StartAsync(42, "user", null, default)).Value;
        var result = await f.Service.SubmitAsync(id, "full-test", "user", new(new() { [700] = "B" }), default);
        Assert.True(result.IsSuccess); Assert.Equal(1, result.Value!.CorrectCount); Assert.Equal(1, result.Value.UnansweredCount);
        var state = await f.Db.SessionStates.SingleAsync(); var pending = state.SubmissionEventJson;
        Assert.NotNull(pending); Assert.False(state.EventPublished);
        Assert.True((await f.Db.ExamAttempts.SingleAsync()).IsSubmitted);
        var duplicate = await f.Service.SubmitAsync(id, "full-test", "user", new(new() { [701] = "A" }), default);
        Assert.Equal(result.Value.TotalScore, duplicate.Value!.TotalScore); Assert.Equal(pending, state.SubmissionEventJson);
        Assert.False((await f.Service.SaveAsync(id, "full-test", "user", new(new() { [701] = "A" }), default)).IsSuccess);
        Assert.False((await f.Service.SubmitAsync(id, "full-test", "other", new([]), default)).IsSuccess);
        var wrong = await f.Service.IncorrectAsync("user", default);
        Assert.Equal(701, Assert.Single(Assert.Single(wrong.Groups).Items).QuestionId);
        Assert.Empty((await f.Service.IncorrectAsync("other", default)).Groups);
    }

    [Fact]
    public async Task Deadline_RejectsLateCheckpoints_GradesOnlyPersistedAnswers()
    {
        await using var f = new Fixture(); await f.Initialize();
        var id = (await f.Service.StartAsync(42, "user", null, default)).Value;
        await f.Service.SaveAsync(id, "full-test", "user", new(new() { [700] = "B" }), default);
        var state = await f.Db.SessionStates.SingleAsync(); state.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        await f.Db.SaveChangesAsync();
        Assert.False((await f.Service.SaveAsync(id, "full-test", "user", new(new() { [701] = "A" }), default)).IsSuccess);
        var result = await f.Service.SubmitAsync(id, "full-test", "user", new(new() { [701] = "A" }), default);
        Assert.Equal(1, result.Value!.CorrectCount);
        Assert.Equal(1, result.Value.UnansweredCount);
    }

    [Fact]
    public async Task Practice_CorrectCountScoring_NoToeicEvent_AndResumeWithoutTimeLimit()
    {
        await using var f = new Fixture(); await f.Initialize();
        var id = (await f.Service.StartAsync(42, "user", new() { SelectedPartNumbers = [5] }, default)).Value;
        Assert.Null((await f.Service.GetAsync(id, "practice", "user", default))!.ExpiresAt);
        var result = await f.Service.SubmitAsync(id, "practice", "user", new(new() { [701] = "A" }), default);
        Assert.True(result.Value!.IsPractice); Assert.Equal(1, result.Value.MaxScore); Assert.Equal(1, result.Value.TotalScore);
        Assert.Null((await f.Db.SessionStates.SingleAsync()).SubmissionEventJson);
        Assert.True((await f.Db.PracticeSessions.SingleAsync()).IsSubmitted);
        Assert.Single((await f.Service.HistoryAsync("user", default)).Items);
        Assert.Empty((await f.Service.AttemptsAsync("user", default)).Attempts);
    }

    [Theory]
    [InlineData(999, "A")]
    [InlineData(700, "D")]
    [InlineData(700, "0")]
    public async Task Save_RejectsForeignQuestionsAndUnavailableOptions(int questionId, string option)
    {
        await using var f = new Fixture(); await f.Initialize();
        var id = (await f.Service.StartAsync(42, "user", null, default)).Value;
        Assert.False((await f.Service.SaveAsync(id, "full-test", "user", new(new() { [questionId] = option }), default)).IsSuccess);
        Assert.Empty((await f.Service.GetAsync(id, "full-test", "user", default))!.Answers);
    }

    [Fact]
    public async Task GuestSubmit_DoesNotPublishAnonymousScores_AndAudioCannotRewind()
    {
        await using var f = new Fixture(); await f.Initialize();
        var id = (await f.Service.StartAsync(42, "guest:key", null, default)).Value;
        await f.Service.SaveAsync(id, "full-test", "guest:key", new([], new() { ["q-700"] = new(20, true) }), default);
        await f.Service.SaveAsync(id, "full-test", "guest:key", new([], new() { ["q-700"] = new(1, false) }), default);
        var session = await f.Service.GetAsync(id, "full-test", "guest:key", default);
        Assert.Equal(new AudioCheckpointDto(20, true), session!.AudioProgress["q-700"]);
        await f.Service.SubmitAsync(id, "full-test", "guest:key", new([]), default);
        Assert.Null((await f.Db.SessionStates.SingleAsync()).SubmissionEventJson);
    }
}
