using System.Text.Json;
using BuildingBlocks.Common.Events;
using BuildingBlocks.EventBus.Abstractions;
using Exam.Data;
using Microsoft.EntityFrameworkCore;

namespace Exam.Messaging;

public class SubmissionOutboxWorker(IServiceScopeFactory scopes, ILogger<SubmissionOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
                var pending = await db.SessionStates.Where(s => s.SubmissionEventJson != null && !s.EventPublished)
                    .Take(50).ToListAsync(stoppingToken);
                if (pending.Count > 0)
                {
                    var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                    foreach (var state in pending)
                    {
                        await bus.PublishAsync(JsonSerializer.Deserialize<TestSubmittedEvent>(state.SubmissionEventJson!)!, stoppingToken);
                        state.EventPublished = true;
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Submission events remain pending; will retry."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
