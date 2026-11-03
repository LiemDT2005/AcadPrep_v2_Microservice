using System.Text.Json;
using Exam.Data;
using Exam.Entities;
using Exam.Entities.Enums;
using Exam.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Repositories;

public class SessionStateRepository(ExamDbContext db) : ISessionStateRepository
{
    public async Task<int> CreateAsync(ExamSessionState state, bool practice, int? timeLimit, string parts, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // This local row satisfies the legacy FK. The immutable Content paper in SessionState is authoritative.
        var localExam = await db.Exams.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Id == state.ExamId, ct);
        localExam ??= await db.Exams.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.Title == state.ExamTitle, ct);
        if (localExam is null)
        {
            var series = await db.ExamSeries.FirstAsync(ct);
            localExam = new Entities.Exam { Title = state.ExamTitle, Duration = 120, ExamSeriesId = series.Id, Status = ExamStatus.Published };
            db.Exams.Add(localExam);
            await db.SaveChangesAsync(ct);
        }
        int id;
        if (practice)
        {
            var session = new PracticeSession
            {
                ExamId = localExam.Id, UserId = state.UserId, SelectedParts = parts, TimeLimit = timeLimit,
                CombinedQuestionsList = JsonSerializer.Serialize(JsonSerializer.Deserialize<List<DTOs.ReviewQuestionDto>>(state.QuestionsJson)!.Select(q => q.QuestionId)),
                CreatedAt = state.StartedAt
            };
            db.PracticeSessions.Add(session);
            await db.SaveChangesAsync(ct);
            id = session.Id;
        }
        else
        {
            var attempt = new ExamAttempt
            {
                ExamId = localExam.Id, UserId = state.UserId, StartedAt = state.StartedAt,
                RemainingTime = (int)(state.ExpiresAt!.Value - state.StartedAt).TotalSeconds
            };
            db.ExamAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
            id = attempt.Id;
        }
        state.Id = $"{(practice ? "practice" : "full-test")}:{id}";
        db.SessionStates.Add(state);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return id;
    }

    public Task<ExamSessionState?> GetAsync(string key, string userId, CancellationToken ct) =>
        db.SessionStates.SingleOrDefaultAsync(s => s.Id == key && s.UserId == userId, ct);

    public async Task SaveAsync(ExamSessionState state, CancellationToken ct)
    {
        state.Revision = Guid.NewGuid().ToString("N");
        if (db.Entry(state).State == EntityState.Detached) db.SessionStates.Add(state);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<ExamSessionState>> GetHistoryAsync(string userId, CancellationToken ct) =>
        db.SessionStates.AsNoTracking().Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt).ToListAsync(ct);

    public async Task CompleteAsync(ExamSessionState state, int listeningCorrect, int readingCorrect,
        int listeningTotal, int readingTotal, CancellationToken ct)
    {
        var id = int.Parse(state.Id.Split(':')[1]);
        if (state.Id.StartsWith("full-test:"))
        {
            var attempt = await db.ExamAttempts.Include(a => a.AttemptAnswers)
                .SingleAsync(a => a.Id == id && a.UserId == state.UserId, ct);
            attempt.IsSubmitted = true;
            attempt.CompletedAt = state.CompletedAt;
            attempt.ListeningScore = state.ListeningScore;
            attempt.ReadingScore = state.ReadingScore;
            attempt.TotalScore = state.ListeningScore + state.ReadingScore;
            attempt.RemainingTime = state.ExpiresAt.HasValue
                ? Math.Max(0, (int)(state.ExpiresAt.Value - state.CompletedAt!.Value).TotalSeconds) : 0;
            // SessionState owns answers for newly migrated sessions. Historical AttemptAnswers
            // are retained for backwards-compatible review of pre-migration attempts.
        }
        else
        {
            var practice = await db.PracticeSessions.SingleAsync(s => s.Id == id && s.UserId == state.UserId, ct);
            practice.IsSubmitted = true;
            practice.CompletedAt = state.CompletedAt;
            practice.AnswersJson = state.AnswersJson;
            practice.CorrectCount = listeningCorrect + readingCorrect;
            practice.TotalQuestions = listeningTotal + readingTotal;
            practice.ListeningCorrect = listeningCorrect;
            practice.ReadingCorrect = readingCorrect;
            practice.ListeningTotal = listeningTotal;
            practice.ReadingTotal = readingTotal;
        }
        await SaveAsync(state, ct); // Result and pending event commit together.
    }
}
