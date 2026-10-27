namespace BuildingBlocks.Common.Events;

public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public record ExamCompletedEvent(
    Guid AttemptId,
    string UserId,
    Guid ExamId,
    string ExamTitle,
    int Score,
    int TotalQuestions,
    int CorrectAnswers
) : IntegrationEvent;

public record TestSubmittedEvent(
    Guid AttemptId,
    string UserId,
    Guid ExamId,
    string ExamTitle,
    int Score,
    int ListeningScore,
    int ReadingScore,
    int TotalQuestions,
    int CorrectAnswers,
    Dictionary<int, int>? PartBreakdown = null
) : IntegrationEvent;
