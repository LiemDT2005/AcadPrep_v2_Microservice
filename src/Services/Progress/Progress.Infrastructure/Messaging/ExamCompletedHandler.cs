using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Microsoft.Extensions.Logging;
using Progress.Application.Services;

namespace Progress.Infrastructure.Messaging;

public class ExamCompletedHandler(ProgressService progressService, ILogger<ExamCompletedHandler> logger)
    : IIntegrationEventHandler<ExamCompletedEvent>
{
    public async Task HandleAsync(ExamCompletedEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling ExamCompletedEvent Attempt={Attempt} User={User} Score={Score}",
            @event.AttemptId, @event.UserId, @event.Score);

        await progressService.RecordExamCompletedAsync(
            @event.AttemptId, @event.UserId, @event.ExamId, @event.ExamTitle,
            @event.Score, @event.TotalQuestions, @event.CorrectAnswers, cancellationToken);
    }
}
