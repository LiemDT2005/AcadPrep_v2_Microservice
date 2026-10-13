using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Learning.Application.Common;
using Learning.Application.Services;
using Microsoft.Extensions.Logging;

namespace Learning.Infrastructure.Messaging;

public class TestSubmittedHandler(GamificationService gamificationService, ILogger<TestSubmittedHandler> logger)
    : IIntegrationEventHandler<ExamCompletedEvent>, IIntegrationEventHandler<TestSubmittedEvent>
{
    // Handle standard ExamCompletedEvent
    public async Task HandleAsync(ExamCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Learning Service processing ExamCompletedEvent: Attempt={AttemptId}, User={UserId}, Exam={ExamTitle}, Score={Score}",
            @event.AttemptId, @event.UserId, @event.ExamTitle, @event.Score);

        // Convert raw score or percentage to TOEIC scale if needed
        var totalScore = @event.Score <= 100 && @event.TotalQuestions > 0
            ? (int)Math.Round(@event.Score * 9.9) // 0-100% converted to TOEIC 0-990
            : @event.Score;

        var listeningScore = (int)Math.Round(totalScore / 2.0);
        var readingScore = totalScore - listeningScore;

        await gamificationService.OnTestSubmittedAsync(
            @event.UserId,
            @event.AttemptId,
            @event.ExamId,
            @event.ExamTitle,
            totalScore,
            listeningScore,
            readingScore,
            @event.TotalQuestions,
            @event.CorrectAnswers,
            null,
            cancellationToken
        );
    }

    // Handle full TestSubmittedEvent (TV02 event)
    public async Task HandleAsync(TestSubmittedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Learning Service processing TestSubmittedEvent: Attempt={AttemptId}, User={UserId}, Exam={ExamTitle}, Score={Score}",
            @event.AttemptId, @event.UserId, @event.ExamTitle, @event.Score);

        await gamificationService.OnTestSubmittedAsync(
            @event.UserId,
            @event.AttemptId,
            @event.ExamId,
            @event.ExamTitle,
            @event.Score,
            @event.ListeningScore,
            @event.ReadingScore,
            @event.TotalQuestions,
            @event.CorrectAnswers,
            @event.PartBreakdown,
            cancellationToken
        );
    }
}
