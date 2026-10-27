using Exam.Data;
using Exam.Entities;
using Exam.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Exam.Repositories;

public class QuestionRepository(IExamDbContext db) : IQuestionRepository
{
    public Task<bool> AnyForExamAsync(int examId, CancellationToken ct = default) =>
        db.Questions.AnyAsync(q => q.ExamId == examId, ct);

    public Task<List<Question>> GetByExamIdAsync(int examId, CancellationToken ct = default) =>
        db.Questions.AsNoTracking().Where(q => q.ExamId == examId).ToListAsync(ct);

    public Task<List<Question>> GetPracticeCandidatesAsync(
        int examId, IReadOnlyCollection<int> parts, IReadOnlyCollection<string> tags, CancellationToken ct = default)
    {
        var query = db.Questions.AsNoTracking().Where(q => q.ExamId == examId && parts.Contains(q.Part));
        if (tags.Count > 0)
            query = query.Where(q => q.TopicTag != null && tags.Contains(q.TopicTag));
        return query.OrderBy(q => q.QuestionNumber).ThenBy(q => q.Id).ToListAsync(ct);
    }

    public Task<List<Question>> GetSessionQuestionsAsync(
        int examId, IReadOnlyCollection<int>? ids = null, CancellationToken ct = default)
    {
        var query = db.Questions.AsNoTracking().Where(q => q.ExamId == examId);
        if (ids is not null)
            query = query.Where(q => ids.Contains(q.Id));
        return query.Include(q => q.QuestionOptions).Include(q => q.Passage)
            .Include(q => q.QuestionGroup).ThenInclude(g => g!.Passages)
            .AsSplitQuery().OrderBy(q => q.Part).ThenBy(q => q.QuestionNumber).ThenBy(q => q.Id).ToListAsync(ct);
    }
}
