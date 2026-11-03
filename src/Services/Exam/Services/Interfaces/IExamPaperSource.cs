using Exam.DTOs;
namespace Exam.Services.Interfaces;
public record ExamPaperSnapshot(int Id, string Title, int Duration, List<ReviewQuestionDto> Questions);
public interface IExamPaperSource
{
    Task<ExamPaperSnapshot?> GetAsync(int examId, CancellationToken ct);
}
