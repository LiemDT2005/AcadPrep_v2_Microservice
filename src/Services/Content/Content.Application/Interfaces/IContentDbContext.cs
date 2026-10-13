using Content.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Content.Application.Interfaces;

public interface IContentDbContext
{
    DbSet<Exam> Exams { get; }
    DbSet<ExamSeries> ExamSeries { get; }
    DbSet<Question> Questions { get; }
    DbSet<Passage> Passages { get; }
    DbSet<QuestionGroup> QuestionGroups { get; }
    DbSet<QuestionOption> QuestionOptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
